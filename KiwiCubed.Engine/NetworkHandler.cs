namespace KiwiCubed.Engine;

using K4os.Compression.LZ4;
using KiwiCubed.Api;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Buffers;
using System.Collections.Concurrent;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.IPlayer;
using static KiwiCubed.Api.Utils;
using ArchEntity = Arch.Core.Entity;

public class NetworkHandler {
	private static string connectionSecretKey = "KiwiCubed_Engine_Server_Connection_Secret_Key";
	private KLogger logger;
	private EventManager eventManager;
	private List<Action<int, NetDataReader>> packetHandlers;
	private EventBasedNetListener listener;
	private NetManager netManager;
	private ConcurrentQueue<QueuedPacket> queuedPackets;
	private Dictionary<int, NetPeer> connectedPeers;
    private ConcurrentBag<NetDataWriter> writerPool = new ConcurrentBag<NetDataWriter>();
    private NetDataReader reusableReader = new NetDataReader();
    private bool packetReceiveCallbackSet;
	private bool clientIsConnected = false;

    public NetworkHandler() {
		logger = new KLogger("NetworkHandler");
		eventManager = (EventManager)MetaHandler.Get<IEventManager>();
		packetHandlers = [];
		listener = new EventBasedNetListener();
		netManager = new NetManager(listener);
		queuedPackets = [];
		connectedPeers = [];

		MetaHandler.Register<NetworkHandler>(this);

        RegisterClientboundPacketType<ConnectionInfoPacket>();
		RegisterClientboundPacketType<PlayerPositionCorrectionPacket>();
        RegisterClientboundPacketType<ChunkDataPacket>();
		RegisterClientboundPacketType<ChunkEditPacket>();
		RegisterClientboundPacketType<NewEntityPacket>();
		RegisterClientboundPacketType<UnloadEntityPacket>();
        RegisterClientboundPacketType<EntityUpdatePacket>();
        RegisterClientboundPacketType<AlertPacket>();

        RegisterServerboundPacketType<ConnectionRequestPacket>();
		RegisterServerboundPacketType<DataReadyPacket>();
		RegisterServerboundPacketType<BlockInteractPacket>();
        RegisterServerboundPacketType<EntityInteractPacket>();
        RegisterServerboundPacketType<ChatSendPacket>();
		RegisterServerboundPacketType<PlayerTransformPacket>();
    }

	public bool StartServer(string address, int port) {
        eventManager.RegisterEvent(typeof(PeerDisconnectedEvent));

        if (!netManager.Start(address, "", port)) {
			logger.ERR("Failed to start server on port {" + port + "}");
			return false;
		}

		eventManager.SubscribeToEvent<ConnectionRequestPacket>((ConnectionRequestPacket packet) => {
			logger.INFO("Got connection request from client with ID {" + packet.clientPeerID + "} and username \"" + packet.playerName + "\"");

			ConnectionInfoPacket connectionInfoPacket = new ConnectionInfoPacket(0, MakeAUID(packet.playerName));
			QueuePacketTo(connectionInfoPacket, (int)PacketType.CONNECTION_INFO, packet.clientPeerID);
        });

        listener.ConnectionRequestEvent += (ConnectionRequest request) => {
			request.AcceptIfKey(connectionSecretKey);
		};
        listener.PeerConnectedEvent += (NetPeer peer) => {
            logger.INFO("New client connected from " + peer.Address + " with client ID {" + peer.Id + "}");
            connectedPeers[peer.Id] = peer;
        };
        listener.PeerDisconnectedEvent += (NetPeer peer, DisconnectInfo info) => {
            logger.INFO("Client from " + peer.Address + " disconnected with reason: " + info.Reason);

			if (connectedPeers.TryGetValue(peer.Id, out NetPeer foundPeer)) {
				connectedPeers.Remove(peer.Id);
			} else {
				logger.ERR("Tried to remove a client from client list that wasn't found");
				logger.BREAK();
			}

			eventManager.TriggerEvent<PeerDisconnectedEvent>(new PeerDisconnectedEvent(peer.Id, info));
        };
        listener.NetworkReceiveEvent += (NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod) => {
            byte[] decompressedData = DecapsulatePacket(reader, out int packetID, out int originalSize);
            //logger.INFO("Got packet with ID {" + packetID + "}");
            reusableReader.SetSource(decompressedData, 0, originalSize);
            packetHandlers[packetID](peer.Id, reusableReader);
            ArrayPool<byte>.Shared.Return(decompressedData);
        };

        logger.INFO("Started server on port {" + port + "}, listening for secret key \"" + connectionSecretKey + "\"");

		return true;
	}

	public bool StartClient(string address, int port) {
		if (clientIsConnected) {
			logger.WARN("Tried to connect to a server while one was already connected");
			return false;
		}

		netManager.Start();

		netManager.Connect(address, port, connectionSecretKey);

		logger.INFO("Attempting to connect to server at \"" + address + "\" on port {" + port + "} using secret key \"" + connectionSecretKey + "\"");

        listener.PeerConnectedEvent += (NetPeer peer) => {
            logger.INFO("Successfully connected to server at " + peer.Address + ", attempting to join...");

			logger.INFO("Requesting to join server...");
			ConnectionRequestPacket connectionRequestPacket = new ConnectionRequestPacket(playerUsername);
			SendPacket(connectionRequestPacket, PacketType.CONNECTION_REQUEST);

			eventManager.SubscribeToEvent<ConnectionInfoPacket>((ConnectionInfoPacket packet) => {
				logger.INFO("Got connection response from server with status code {" + packet.statusCode + "}");
				if (packet.statusCode == 0) {
					logger.INFO("Joining server...");

                    WorldClientHandler worldHandler = (WorldClientHandler)MetaHandler.Get<IWorldClientHandler>();
                    WorldClient world = (WorldClient)(World)worldHandler.CreateClientWorld();
					DataReadyPacket dataReadyPacket = new DataReadyPacket();
					SendPacket(dataReadyPacket, PacketType.DATA_READY);
                    eventManager.SubscribeForNEvents<NewEntityPacket>(1, (NewEntityPacket packet) => {
                        ClientPlayer.Setup(world, packet.newEntity);
                        worldHandler.StartClientWorld();
                    });
                } else {
					logger.INFO("Server rejected join request with status code {" + packet.statusCode + "}");
					logger.BREAK();
				}
            });

            clientIsConnected = true;
        };
        listener.PeerDisconnectedEvent += (NetPeer peer, DisconnectInfo info) => {
            logger.INFO("Disconnected from server with reason: " + info.Reason);
        };
		listener.NetworkReceiveEvent += (NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod) => {
            byte[] decompressedData = DecapsulatePacket(reader, out int packetID, out int originalSize);
            //logger.INFO("Got packet with ID {" + packetID + "}");
            reusableReader.SetSource(decompressedData, 0, originalSize);
            packetHandlers[packetID](peer.Id, reusableReader);
            ArrayPool<byte>.Shared.Return(decompressedData);
		};

        return true;
	}

	public void PollEvents() {
		netManager.PollEvents();
	}

	public void SetPacketReceiveCallback(Action<NetPeer, NetPacketReader, DeliveryMethod> callback) {
		if (packetReceiveCallbackSet) {
			logger.CRITICAL("Tried to set packet recieve callaback twice");
			logger.BREAK();
		}
		packetReceiveCallbackSet = true;
		listener.NetworkReceiveEvent += (NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod) => {
			callback(peer, reader, deliveryMethod);
		};
	}

	private static byte[] EncapsulatePacket(NetDataWriter packet, int packetID, out int packetSize) {
        byte[] packetData = packet.Data;
        int packetDataLength = packet.Length;
        int maxCompressedSize = LZ4Codec.MaximumOutputSize(packetDataLength);
        byte[] compressionBuffer = ArrayPool<byte>.Shared.Rent(maxCompressedSize);

        try {
            int actualCompressedSize = LZ4Codec.Encode(packetData, 0, packetDataLength, compressionBuffer, 0, maxCompressedSize, LZ4Level.L00_FAST);

            packetSize = 12 + actualCompressedSize;
            byte[] finalPacket = ArrayPool<byte>.Shared.Rent(packetSize);

            BitConverter.TryWriteBytes(finalPacket.AsSpan(0, 4), packetID);
            BitConverter.TryWriteBytes(finalPacket.AsSpan(4, 4), actualCompressedSize);
            BitConverter.TryWriteBytes(finalPacket.AsSpan(8, 4), packetDataLength);

            Buffer.BlockCopy(compressionBuffer, 0, finalPacket, 12, actualCompressedSize);

            return finalPacket;
        } finally {
            ArrayPool<byte>.Shared.Return(compressionBuffer);
        }
    }

    private static byte[] DecapsulatePacket(NetPacketReader reader, out int packetID, out int originalSize) {
        int compressedSize = BitConverter.ToInt32(reader.RawData, reader.UserDataOffset + 4);
        originalSize = BitConverter.ToInt32(reader.RawData, reader.UserDataOffset + 8);
        packetID = BitConverter.ToInt32(reader.RawData, reader.UserDataOffset);

        byte[] decompressedData = ArrayPool<byte>.Shared.Rent(originalSize);
        byte[] compressedBuffer = ArrayPool<byte>.Shared.Rent(compressedSize);

        try {
            Buffer.BlockCopy(reader.RawData, reader.UserDataOffset + 12, compressedBuffer, 0, compressedSize);
            LZ4Codec.Decode(compressedBuffer, 0, compressedSize, decompressedData, 0, originalSize);
        } finally {
            ArrayPool<byte>.Shared.Return(compressedBuffer);
        }

        reader.SkipBytes(12 + compressedSize);
        return decompressedData;
    }

    private void SendPacket<T>(T packet, PacketType packetID, List<int> clientIDs = null) where T : struct, INetSerializable {
		QueuePacketToAll(packet, packetID, clientIDs);
		FlushPackets();
    }

    public void QueuePacketToAll<T>(T packet, PacketType packetID, List<int> clientIDs = null, int excludedClientID = -1) where T: struct, INetSerializable {
		NetDataWriter writer = GetWriter();
		packet.Serialize(writer);

        byte[] finalPacket = EncapsulatePacket(writer, (int)packetID, out int packetSize);
        queuedPackets.Enqueue(new QueuedPacket(finalPacket, packetSize, clientIDs, excludedClientID));

        writerPool.Add(writer);
    }

    public void QueuePacketTo<T>(T packet, PacketType packetID, int clientID) where T: struct, INetSerializable {
        QueuePacketToAll(packet, packetID, [clientID]);
    }

    public void FlushPackets() {
        GameType gameType = Meta.GetGameType();

        while (queuedPackets.TryDequeue(out QueuedPacket packet)) {
            if (packet.clientIDs == null || gameType == GameType.CLIENT) {
				if (packet.excludedClientID == -1) {
					netManager.SendToAll(packet.data, DeliveryMethod.ReliableOrdered);
				} else {
                    foreach (KeyValuePair<int, NetPeer> peerPair in connectedPeers) {
                        if (peerPair.Key != packet.excludedClientID) {
                            peerPair.Value.Send(packet.data, DeliveryMethod.ReliableOrdered);
                        }
                    }
                }
            } else {
                for (int iterator = 0; iterator < packet.clientIDs.Count; iterator++) {
                    connectedPeers[packet.clientIDs[iterator]].Send(packet.data, DeliveryMethod.ReliableOrdered);
                }
            }

			ArrayPool<byte>.Shared.Return(packet.data);
        }
    }

    private void RegisterClientboundPacketType<T>() where T : struct, INetSerializable {
        eventManager.RegisterEvent(typeof(T));
        packetHandlers.Add((int peerID, NetDataReader reader) => {
            T packetData = new T();
            packetData.Deserialize(reader);
            eventManager.TriggerEvent<T>(packetData);
        });
    }

    private void RegisterServerboundPacketType<T>() where T: struct, IClientPacket, INetSerializable {
		eventManager.RegisterEvent(typeof(T));
		packetHandlers.Add((int peerID, NetDataReader reader) => {
			T packetData = new T();
			packetData.Deserialize(reader);
			packetData.clientPeerID = peerID;
			eventManager.TriggerEvent<T>(packetData);
		});
	}

    private NetDataWriter GetWriter() {
        if (writerPool.TryTake(out NetDataWriter writer)) {
            writer.Reset();
            return writer;
        }
        return new NetDataWriter();
    }

    private struct QueuedPacket {
        public byte[] data;
		public int dataLength;
        public List<int> clientIDs;
		public int excludedClientID;

		public QueuedPacket(byte[] data, int dataLength, List<int> clientIDs, int excludedClientID = -1) {
			this.data = data;
			this.dataLength = dataLength;
			this.clientIDs = clientIDs;
			this.excludedClientID = excludedClientID;
		}
    }
}

public struct PeerDisconnectedEvent {
    public int clientPeerID;
    public DisconnectInfo disconnectInfo;

    public PeerDisconnectedEvent(int clientPeerID, DisconnectInfo disconnectInfo) {
        this.clientPeerID = clientPeerID;
        this.disconnectInfo = disconnectInfo;
    }
}

public interface IClientPacket {
	public int clientPeerID { get; set; }
}

public enum PacketType : int {
	                            // Server->Client
	CONNECTION_INFO,            // Sends different status codes to players describing the state of the client in the world
	PLAYER_POSITION_CORRECTION, // Sends info about the server's authoritative position of the current player
	CHUNK_DATA,                 // Holds block data for an entire chunk
	CHUNK_EDIT,                 // Holds a diff for a single block within a chunk
	NEW_ENTITY,                 // Holds data about a entity newly in radius of the player
	UNLOAD_ENTITY,              // Tells the client to unload an entity
	ENTITY_UPDATE,              // Holds update data about an entity in radius of the player
	ALERT_BROADCAST,            // Alerts and chat messages from the server
						        
	                            // Client->Server
	CONNECTION_REQUEST,         // Request to join the server
	DATA_READY,                 // Tells the server the client is ready for game data
	BLOCK_INTERACT,             // Info about player interactions with interactable blocks
	ENTITY_INTERACT,            // Info about player interactions with entities
	CHAT_SEND,                  // Chat messages sent by the player
	PLAYER_TRANSFORM,           // Info about the player's position, orientation, and ground status
}

public struct ConnectionInfoPacket : INetSerializable {
	// 0 - Connection reqeust accepted
	// 1 - Connection reqeust denied
	// 2 - Server is ready to start sending world data
	// 3 - Player has been forcefully disconnected
	public int statusCode;
	public ulong playerAUID;

	public ConnectionInfoPacket(int statusCode, ulong playerAUID) {
		this.statusCode = statusCode;
		this.playerAUID = playerAUID;
	}

	public readonly void Serialize(NetDataWriter writer) {
		writer.Put(statusCode);
		writer.Put(playerAUID);
	}

	public void Deserialize(NetDataReader reader) {
		statusCode = reader.GetInt();
		playerAUID = reader.GetULong();
	}
}

public struct PlayerPositionCorrectionPacket : INetSerializable {
	public Vector3 truePosition;
	public Vector3 trueVelocity;

	public ulong clientSessionTickNumber;

	public PlayerPositionCorrectionPacket() {
		truePosition = Vector3.Zero;
		trueVelocity = Vector3.Zero;
	}

	public PlayerPositionCorrectionPacket(Vector3 truePosition, Vector3 trueVelocity) {
		this.truePosition = truePosition;
		this.trueVelocity = trueVelocity;
    }

	public readonly void Serialize(NetDataWriter writer) {
		writer.Put(truePosition.X); 
		writer.Put(truePosition.Y); 
		writer.Put(truePosition.Z);
		writer.Put(trueVelocity.X);
		writer.Put(trueVelocity.Y);
		writer.Put(trueVelocity.Z);
		writer.Put(clientSessionTickNumber);
	}

	public void Deserialize(NetDataReader reader) {
		float truePositionX = reader.GetFloat();
        float truePositionY = reader.GetFloat();
        float truePositionZ = reader.GetFloat();
		truePosition = new Vector3(truePositionX, truePositionY, truePositionZ);
        float trueVelocityX = reader.GetFloat();
        float trueVelocityY = reader.GetFloat();
        float trueVelocityZ = reader.GetFloat();
		trueVelocity = new Vector3(trueVelocityX, trueVelocityY, trueVelocityZ);
		clientSessionTickNumber = reader.GetULong();
    }
}

public struct ChunkDataPacket : INetSerializable {
	public int X;
	public int Y;
	public int Z;

	public ushort[] blockPalette;
	public ushort[] blockIndices;

	public ChunkDataPacket(int x, int y, int z, ushort[] blockPalette, ushort[] blockIndices) {
		X = x;
		Y = y;
		Z = z;
		this.blockPalette = blockPalette;
		this.blockIndices = blockIndices;
	}

    public readonly void Serialize(NetDataWriter writer) {
		writer.Put(X);
		writer.Put(Y);
		writer.Put(Z);

		writer.Put(blockPalette.Length);
		for (int iterator = 0; iterator < blockPalette.Length; iterator++) {
			writer.Put(blockPalette[iterator]);
		}

        writer.Put(blockIndices.Length);
		for (int iterator = 0; iterator < blockIndices.Length; iterator++) {
			writer.Put(blockIndices[iterator]);
		}
	}

	public void Deserialize(NetDataReader reader) {
		X = reader.GetInt();
		Y = reader.GetInt();
		Z = reader.GetInt();

		int blockPaletteLength = reader.GetInt();
		blockPalette = new ushort[blockPaletteLength];
		for (int iterator = 0; iterator < blockPaletteLength; iterator++) {
			blockPalette[iterator] = reader.GetUShort();
		}

		int blockIndicesLength = reader.GetInt();
		blockIndices = new ushort[blockIndicesLength];
		for (int iterator = 0; iterator < blockIndicesLength; iterator++) {
			blockIndices[iterator] = reader.GetUShort();
        }
    }
}

public struct ChunkEditPacket : INetSerializable {
	public FullBlockPosition editedBlockPosition;
	public AssetStringID newBlockStringID;

	public ChunkEditPacket(FullBlockPosition editedBlockPosition, AssetStringID newBlockStringID) {
		this.editedBlockPosition = editedBlockPosition;
		this.newBlockStringID = newBlockStringID;
	}

	public void Serialize(NetDataWriter writer) {
		editedBlockPosition.Serialize(writer);
		writer.Put(newBlockStringID.CanonicalName());
	}

	public void Deserialize(NetDataReader reader) {
		editedBlockPosition = FullBlockPosition.Deserialize(reader);
		newBlockStringID = AssetStringID.FromString(reader.GetString());
	}
}

public struct NewEntityPacket : INetSerializable {
    public ArchEntity newEntity;
    public EntityType newEntityType;
    public SimpleTransform newEntityTransform;
    public ulong newEntityAUID;

    public NewEntityPacket(ArchEntity newEntity, EntityType newEntityType, SimpleTransform newEntityTransform, ulong newEntityAUID) {
        this.newEntity = newEntity;
        this.newEntityType = newEntityType;
        this.newEntityTransform = newEntityTransform;
        this.newEntityAUID = newEntityAUID;
    }

    public readonly void Serialize(NetDataWriter writer) {
        writer.Put(newEntityType.stringID.CanonicalName());
        writer.Put(newEntityTransform.position.X);
        writer.Put(newEntityTransform.position.Y);
        writer.Put(newEntityTransform.position.Z);
        writer.Put(newEntityTransform.orientation.X);
        writer.Put(newEntityTransform.orientation.Y);
        writer.Put(newEntityTransform.orientation.Z);
        writer.Put(newEntityTransform.orientation.W);
        writer.Put(newEntityAUID);

        ArchEntitySerializer serializer = newEntityType.networkFunctions.serializer;
        serializer(writer, newEntity);
    }

    public void Deserialize(NetDataReader reader) {
        newEntityType = Meta.Get<IAssetManager>().GetEntityType(AssetStringID.FromString(reader.GetString()));
        Vector3 entityPosition = Vector3.Zero;
        Quaternion entityOrientation = Quaternion.Identity;
        entityPosition.X = reader.GetFloat();
        entityPosition.Y = reader.GetFloat();
        entityPosition.Z = reader.GetFloat();
        entityOrientation.X = reader.GetFloat();
        entityOrientation.Y = reader.GetFloat();
        entityOrientation.Z = reader.GetFloat();
        entityOrientation.W = reader.GetFloat();
        newEntityTransform = new SimpleTransform(entityPosition, entityOrientation);
        newEntityAUID = reader.GetULong();
		
        ArchEntity newEntity = MetaHandler.Get<IWorldClientHandler>().GetWorld().GetEntityManager().SpawnEntity(newEntityAUID, newEntityType, newEntityTransform.position, newEntityTransform.orientation);
        ArchEntityDeserializer deserializer = newEntityType.networkFunctions.deserializer;
        deserializer(reader, newEntity);
    }
}

public struct UnloadEntityPacket : INetSerializable {
	public ulong entityAUID;

	public UnloadEntityPacket(ulong entityAUID) {
		this.entityAUID = entityAUID;
	}

    public readonly void Serialize(NetDataWriter writer) {
		writer.Put(entityAUID);
	}

	public void Deserialize(NetDataReader reader) {
		entityAUID = reader.GetULong();
	}
}

public struct EntityUpdatePacket : INetSerializable {
	public ulong entityAUID;
	public SimpleTransform entityTransform;

	public EntityUpdatePacket() {
		entityAUID = 0;
		entityTransform = new SimpleTransform();
	}

	public EntityUpdatePacket(ulong entityAUID, SimpleTransform entityTransform) {
		this.entityAUID = entityAUID;
		this.entityTransform = entityTransform;
	}

	public void Serialize(NetDataWriter writer) {
		writer.Put(entityAUID);
		entityTransform.Serialize(writer);
	}

	public void Deserialize(NetDataReader reader) {
		entityAUID = reader.GetULong();
		entityTransform.Deserialize(reader);
	}
}

public struct BlockUpdatePacket : INetSerializable {
	public FullBlockPosition updatedBlockPosition;
	public AssetStringID newBlock;

    public readonly void Serialize(NetDataWriter writer) {
	}

	public void Deserialize(NetDataReader reader) {

	}
}

public struct AlertPacket : INetSerializable {
	public string message;
	public bool isChatMessage;

	public AlertPacket(string message, bool isChatMessage) {
		this.message = message;
		this.isChatMessage = isChatMessage;
	}

    public readonly void Serialize(NetDataWriter writer) {
		writer.Put(message);
		writer.Put(isChatMessage);
	}

	public void Deserialize(NetDataReader reader) {
		message = reader.GetString();
		isChatMessage = reader.GetBool();
	}
}

public struct ConnectionRequestPacket : IClientPacket, INetSerializable {
	public string playerName;

    public int clientPeerID { get; set; }

    public ConnectionRequestPacket(string playerName) {
		this.playerName = playerName;
	}

    public readonly void Serialize(NetDataWriter writer) {
		writer.Put(playerName);
	}

	public void Deserialize(NetDataReader reader) {
		playerName = reader.GetString();
	}
}

public struct DataReadyPacket : IClientPacket, INetSerializable {
	public int clientPeerID { get; set; }

    public readonly void Serialize(NetDataWriter writer) { }

    public void Deserialize(NetDataReader reader) { }
}

public struct BlockInteractPacket : IClientPacket, INetSerializable {
	public FullBlockPosition interactedBlockPosition;
	public BlockInteractionType interactionType;
	public AssetStringID heldItem;

	public int clientPeerID { get; set; }
	
	public BlockInteractPacket(FullBlockPosition interactedBlockPosition, BlockInteractionType interactionType, AssetStringID heldItemStringID) {
		this.interactedBlockPosition = interactedBlockPosition;
		this.interactionType = interactionType;
		heldItem = heldItemStringID;
	}

	public void Serialize(NetDataWriter writer) {
		interactedBlockPosition.Serialize(writer);
		writer.Put((byte)interactionType);
		writer.Put(heldItem.CanonicalName());
    }

	public void Deserialize(NetDataReader reader) {
		interactedBlockPosition = FullBlockPosition.Deserialize(reader);
		interactionType = (BlockInteractionType)reader.GetByte();
		heldItem = AssetStringID.FromString(reader.GetString());
    }
}

public struct EntityInteractPacket : IClientPacket, INetSerializable {
	public ulong entityAUID;
	public bool isAttackOrInteract;
	public AssetStringID heldItem;

	public int clientPeerID { get; set; }

	public EntityInteractPacket(ulong entityAUID, bool isAttackOrInteract, AssetStringID heldItemStringID) {
		this.entityAUID = entityAUID;
		this.isAttackOrInteract = isAttackOrInteract;
		heldItem = heldItemStringID;
	}

    public readonly void Serialize(NetDataWriter writer) {
		writer.Put(entityAUID);
		writer.Put(isAttackOrInteract);
		writer.Put(heldItem.CanonicalName());
	}

	public void Deserialize(NetDataReader reader) {
		entityAUID = reader.GetULong();
		isAttackOrInteract = reader.GetBool();
		heldItem = AssetStringID.FromString(reader.GetString());
	}
}

public struct ChatSendPacket : IClientPacket, INetSerializable {
	public string chatMessage;

    public int clientPeerID { get; set; }

    public ChatSendPacket(string chatMessage) {
		this.chatMessage = chatMessage;
	}

    public readonly void Serialize(NetDataWriter writer) {
		writer.Put(chatMessage);
	}

	public void Deserialize(NetDataReader reader) {
		chatMessage = reader.GetString();
	}
}

public struct PlayerTransformPacket : IClientPacket, INetSerializable {
	public ulong AUID;
	public ulong sessionTickNumber;
	public Vector3 position;
	public Quaternion orientation;
	public bool isGrounded;

	public int clientPeerID { get; set; }

	public PlayerTransformPacket(ulong playerAUID, ulong clientSessionTickNumber, Vector3 playerPosition, Quaternion playerOrinetation, bool isGrounded) {
		AUID = playerAUID;
		sessionTickNumber = clientSessionTickNumber;
		position = playerPosition;
		orientation = playerOrinetation;
		this.isGrounded = isGrounded;
	}

    public readonly void Serialize(NetDataWriter writer) {
		writer.Put(AUID);
		writer.Put(sessionTickNumber);
		writer.Put(position.X);
		writer.Put(position.Y);
		writer.Put(position.Z);
		writer.Put(orientation.X);
		writer.Put(orientation.Y);
		writer.Put(orientation.Z);
		writer.Put(orientation.W);
		writer.Put(isGrounded);
	}

	public void Deserialize(NetDataReader reader) {
		AUID = reader.GetULong();
		sessionTickNumber = reader.GetULong();
		position.X = reader.GetFloat();
		position.Y = reader.GetFloat();
		position.Z = reader.GetFloat();
		orientation.X = reader.GetFloat();
		orientation.Y = reader.GetFloat();
		orientation.Z = reader.GetFloat();
        orientation.W = reader.GetFloat();
        isGrounded = reader.GetBool();
    }
}