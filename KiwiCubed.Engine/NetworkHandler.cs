namespace KiwiCubed.Engine;

using ArchEntity = Arch.Core.Entity;
using System.Collections.Concurrent;
using System.Numerics;
using K4os.Compression.LZ4;
using KiwiCubed.Api;
using LiteNetLib;
using LiteNetLib.Utils;
using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.Util;

public class NetworkHandler {
	private static string connectionSecretKey = "KiwiCubed_Engine_Server_Connection_Secret_Key";
	private EventManager eventManager;
	private List<Action<int, NetDataReader>> packetHandlers;
	private EventBasedNetListener listener;
	private NetManager netManager;
	private ConcurrentDictionary<byte[], List<int>> queuedPackets;
	private Dictionary<int, NetPeer> connectedPeers;
	private bool packetReceiveCallbackSet;
	private bool clientIsConnected = false;

    public NetworkHandler() {
		eventManager = (EventManager)MetaHandler.Get<IEventManager>();
		packetHandlers = [];
		listener = new EventBasedNetListener();
		netManager = new NetManager(listener);
		queuedPackets = new();
		connectedPeers = [];

		MetaHandler.Register<NetworkHandler>(this);

        RegisterClientPacketType<ConnectionInfoPacket>();
		RegisterClientPacketType<PlayerPositionCorrectionPacket>();
        RegisterClientPacketType<ChunkDataPacket>();
		RegisterClientPacketType<NewEntityPacket>();
        RegisterClientPacketType<EntityUpdatesPacket>();
        RegisterClientPacketType<AlertPacket>();

        RegisterServerPacketType<ConnectionRequestPacket>();
		RegisterServerPacketType<PlayerInteractPacket>();
		RegisterServerPacketType<ChatSendPacket>();
		RegisterServerPacketType<PlayerTransformPacket>();
    }

	public bool StartServer(string address, int port) {
		OVERRIDE_LOG_NAME("NetworkHandler");

		if (!netManager.Start(address, "", port)) {
			KERR("Failed to start server on port {" + port + "}");
			return false;
		}

		eventManager.SubscribeToEvent<ConnectionRequestPacket>((ConnectionRequestPacket packet) => {
			KINFO("Got connection request from client with ID {" + packet.clientPeerID + "} and username \"" + packet.playerName + "\"");

			ConnectionInfoPacket connectionInfoPacket = new ConnectionInfoPacket(0, MakeAUID(packet.playerName));
			QueuePacket(connectionInfoPacket, (int)PacketType.CONNECTION_INFO, packet.clientPeerID);
        });

        listener.ConnectionRequestEvent += (ConnectionRequest request) => {
			request.AcceptIfKey(connectionSecretKey);
		};
        listener.PeerConnectedEvent += (NetPeer peer) => {
            KINFO("New client connected from " + peer.Address + " with client ID {" + peer.Id + "}");
            connectedPeers[peer.Id] = peer;
        };
        listener.PeerDisconnectedEvent += (NetPeer peer, DisconnectInfo info) => {
            KINFO("Client from " + peer.Address + " disconnected with reason: " + info.Reason);

			if (connectedPeers.TryGetValue(peer.Id, out NetPeer foundPeer)) {
				connectedPeers.Remove(peer.Id);
			} else {
				KERR("Tried to remove a client from client list that wasn't found");
				KBREAK();
			}
        };
        listener.NetworkReceiveEvent += (NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod) => {
			byte[] packetData = DecapsulatePacket(reader, out int packetID);
            //KINFO("Got packet with ID {" + packetID + "}");
            NetDataReader packetReader = new NetDataReader(packetData);
			packetHandlers[packetID](peer.Id, packetReader);
		};

        KINFO("Started server on port {" + port + "}, listening for secret key \"" + connectionSecretKey + "\"");

		return true;
	}

	public bool StartClient(string address, int port) {
		OVERRIDE_LOG_NAME("NetworkHandler");

		if (clientIsConnected) {
			KWARN("Tried to connect to a server while one was already connected");
			return false;
		}

		netManager.Start();

		netManager.Connect(address, port, connectionSecretKey);

		KINFO("Attempting to connect to server at \"" + address + "\" on port {" + port + "} using secret key \"" + connectionSecretKey + "\"");

        listener.PeerConnectedEvent += (NetPeer peer) => {
            KINFO("Successfully connected to server at " + peer.Address + ", attempting to join...");

			ConnectionRequestPacket connectionRequestPacket = new ConnectionRequestPacket(playerUsername);
			QueuePacket(connectionRequestPacket, PacketType.CONNECTION_REQUEST, peer.Id);
			FlushPackets();

			eventManager.SubscribeToEvent<ConnectionInfoPacket>((ConnectionInfoPacket packet) => {
				KINFO("Got connection response from server with status code {" + packet.statusCode + "}");
				if (packet.statusCode == 0) {
					KINFO("Joining server...");
					IWorldClientHandler singleplayerHandler = MetaHandler.Get<IWorldClientHandler>();
					singleplayerHandler.CreateClientWorld();
				}
            });

            clientIsConnected = true;
        };
        listener.PeerDisconnectedEvent += (NetPeer peer, DisconnectInfo info) => {
            KINFO("Disconnected from server with reason: " + info.Reason);
        };
		listener.NetworkReceiveEvent += (NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod) => {
			byte[] packetData = DecapsulatePacket(reader, out int packetID);
			//KINFO("Got packet with ID {" + packetID + "}");
			NetDataReader packetReader = new NetDataReader(packetData);
			packetHandlers[packetID](peer.Id, packetReader);
		};

        return true;
	}

	public void PollEvents() {
		netManager.PollEvents();
	}

	public void SetPacketReceiveCallback(Action<NetPeer, NetPacketReader, DeliveryMethod> callback) {
		OVERRIDE_LOG_NAME("NetworkHandler");

		if (packetReceiveCallbackSet) {
			KCRITICAL("Tried to set packet recieve callaback twice");
			KBREAK();
		}
		packetReceiveCallbackSet = true;
		listener.NetworkReceiveEvent += (NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod) => {
			callback(peer, reader, deliveryMethod);
		};
	}

	private static byte[] EncapsulatePacket(NetDataWriter packet, int packetID) {
		byte[] packetData = packet.Data;
		int maxCompressedSize = LZ4Codec.MaximumOutputSize(packetData.Length);
		byte[] compressionBuffer = new byte[maxCompressedSize];

		int actualCompressedSize = LZ4Codec.Encode(
			packetData, 0, packetData.Length,
			compressionBuffer, 0, compressionBuffer.Length,
			LZ4Level.L00_FAST
		);

		byte[] finalPacket = new byte[12 + actualCompressedSize];

		BitConverter.TryWriteBytes(finalPacket.AsSpan(0, 4), packetID);
		BitConverter.TryWriteBytes(finalPacket.AsSpan(4, 4), actualCompressedSize);
		BitConverter.TryWriteBytes(finalPacket.AsSpan(8, 4), packetData.Length);

		Buffer.BlockCopy(compressionBuffer, 0, finalPacket, 12, actualCompressedSize);

		return finalPacket;
	}

	private static byte[] DecapsulatePacket(NetPacketReader reader, out int packetID) {
		byte[] packet = reader.GetRemainingBytes();
		int readPacketID = BitConverter.ToInt32(packet, 0);
		int compressedSize = BitConverter.ToInt32(packet, 4);
		int originalSize = BitConverter.ToInt32(packet, 8);

		byte[] compressedData = new byte[compressedSize];
		Buffer.BlockCopy(packet, 12, compressedData, 0, compressedSize);
		byte[] decompressedData = new byte[originalSize];
		LZ4Codec.Decode(compressedData, 0, compressedSize, decompressedData, 0, originalSize);
		packetID = readPacketID;

		return decompressedData;
	}

	private void QueuePacket(NetDataWriter packet, PacketType packetID, List<int> clientIDs) {
		byte[] finalPacket = EncapsulatePacket(packet, (int)packetID);
		queuedPackets.TryAdd(finalPacket, clientIDs);
	}

	public void QueuePacket<T>(T packet, PacketType packetID, List<int> clientIDs = null) where T: struct, INetSerializable {
		NetDataWriter writer = new NetDataWriter();
		packet.Serialize(writer);
		QueuePacket(writer, packetID, clientIDs);
	}

    public void QueuePacket<T>(T packet, PacketType packetID, int clientID) where T: struct, INetSerializable {
        QueuePacket(packet, packetID, [clientID]);
    }

    public void FlushPackets() {
		GameType gameType = Meta.GetGameType();
		foreach (KeyValuePair<byte[], List<int>> packetPair in queuedPackets) {
			if (packetPair.Value == null || gameType == GameType.CLIENT) {
				netManager.SendToAll(packetPair.Key, DeliveryMethod.ReliableOrdered);
			} else {
				for (int iterator = 0; iterator < packetPair.Value.Count; iterator++) {
					connectedPeers[packetPair.Value[iterator]].Send(packetPair.Key, DeliveryMethod.ReliableOrdered);
				}
			}
		}
		queuedPackets.Clear();
	}

    private void RegisterServerPacketType<T>() where T: struct, IClientPacket, INetSerializable {
		eventManager.RegisterEvent(typeof(T));
		packetHandlers.Add((int peerID, NetDataReader reader) => {
			T packetData = new T();
			packetData.Deserialize(reader);
			packetData.clientPeerID = peerID;
			eventManager.TriggerEvent<T>(packetData);
		});
	}

    private void RegisterClientPacketType<T>() where T : struct, INetSerializable {
        eventManager.RegisterEvent(typeof(T));
        packetHandlers.Add((int peerID, NetDataReader reader) => {
            T packetData = new T();
            packetData.Deserialize(reader);
            eventManager.TriggerEvent<T>(packetData);
        });
    }
}

public interface IClientPacket {
	public int clientPeerID { get; set; }
}

public enum PacketType : int {
	                            // Server->Client
	CONNECTION_INFO,            // Sends different status codes to players describing the state of the client in the world
	PLAYER_POSITION_CORRECTION, // Sends info about the server's authoritative position of the current player
	CHUNK_DATA,                 // Holds chunk data
	NEW_ENTITIES,               // Holds data about newly spawned entities in radius of the player
	ENTITY_UPDATES,             // Holds updated data about all entities in radius of the player
	ALERT_BROADCAST,            // Alerts and chat messages from the server
						        
	                            // Client->Server
	CONNECTION_REQUEST,         // Request to join the server
	PLAYER_INTERACT,            // Info about player interactions with interactable blocks
	CHAT_SEND,                  // Chat messages sent by the player
	PLAYER_TRANSFORM,           // Info about the player's position, orientation, and ground status
}

public struct ConnectionInfoPacket : INetSerializable {
	// 0 - Connection reqeust accepted
	// 1 - Connection reqeust denied
	// 2 - Server is ready to start sending world data
	// 3 - Player has been forcefully disconnected
	public int statusCode;

	public ConnectionInfoPacket(int statusCode, ulong playerAUID) {
		this.statusCode = statusCode;
	}

	public void Serialize(NetDataWriter writer) {
		writer.Put(statusCode);
	}

	public void Deserialize(NetDataReader reader) {
		statusCode = reader.GetInt();
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

	public void Serialize(NetDataWriter writer) {
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

	public void Serialize(NetDataWriter writer) {
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

public struct NewEntityPacket : INetSerializable {
    public ArchEntity newEntity;
    public EntityType newEntityType;
    public SimpleTransform newEntityTransform;
    public ulong newEntityAUID;
    public NetDataReader reader;

    public NewEntityPacket(ArchEntity newEntity, EntityType newEntityType, SimpleTransform newEntityTransform, ulong newEntityAUID) {
        this.newEntity = newEntity;
        this.newEntityType = newEntityType;
        this.newEntityTransform = newEntityTransform;
        this.newEntityAUID = newEntityAUID;
    }

    public void Serialize(NetDataWriter writer) {
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
        this.reader = reader;
    }
}

public struct EntityUpdatesPacket : INetSerializable {
	public ulong entityAUID;
	public SimpleTransform entityTransform;

	public EntityUpdatesPacket() {
		entityAUID = 0;
		entityTransform = new SimpleTransform();
	}

	public EntityUpdatesPacket(ulong entityAUID, SimpleTransform entityTransform) {
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

public struct AlertPacket : INetSerializable {
	public string message;
	public bool isChatMessage;

	public AlertPacket(string message, bool isChatMessage) {
		this.message = message;
		this.isChatMessage = isChatMessage;
	}

	public void Serialize(NetDataWriter writer) {
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

	public void Serialize(NetDataWriter writer) {
		writer.Put(playerName);
	}

	public void Deserialize(NetDataReader reader) {
		playerName = reader.GetString();
	}
}

public struct PlayerInteractPacket : IClientPacket, INetSerializable {
	public FullBlockPosition interactedBlockPosition;

	public int clientPeerID { get; set; }
	
	public PlayerInteractPacket(FullBlockPosition interactedBlockPosition) {
		this.interactedBlockPosition = interactedBlockPosition;
	}

	public void Serialize(NetDataWriter writer) {
        writer.Put(interactedBlockPosition.blockPosition.X);
        writer.Put(interactedBlockPosition.blockPosition.Y);
        writer.Put(interactedBlockPosition.blockPosition.Z);
        writer.Put(interactedBlockPosition.chunkPosition.X);
        writer.Put(interactedBlockPosition.chunkPosition.Y);
        writer.Put(interactedBlockPosition.chunkPosition.Z);
    }

	public void Deserialize(NetDataReader reader) {
        int blockX = reader.GetInt();
        int blockY = reader.GetInt();
        int blockZ = reader.GetInt();
        int chunkX = reader.GetInt();
        int chunkY = reader.GetInt();
        int chunkZ = reader.GetInt();
		IntVector3 blockPosition = new IntVector3(blockX, blockY, blockZ);
        IntVector3 chunkPosition = new IntVector3(chunkX, chunkY, chunkZ);
		interactedBlockPosition = new FullBlockPosition(blockPosition, chunkPosition);
    }
}

public struct ChatSendPacket : IClientPacket, INetSerializable {
	public string chatMessage;

    public int clientPeerID { get; set; }

    public ChatSendPacket(string chatMessage) {
		this.chatMessage = chatMessage;
	}

	public void Serialize(NetDataWriter writer) {
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

	public void Serialize(NetDataWriter writer) {
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