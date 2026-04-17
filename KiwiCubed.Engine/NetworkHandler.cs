namespace KiwiCubed.Engine;

using System.Buffers;
using System.Numerics;
using K4os.Compression.LZ4;
using KiwiCubed.Api;
using LiteNetLib;
using LiteNetLib.Utils;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.Util;

public class NetworkHandler {
	private static string connectionSecretKey = "KiwiCubed_Engine_Server_Connection_Secret_Key";
	private EventManager eventManager;
	private List<Action<int, NetDataReader>> packetHandlers;
	private EventBasedNetListener listener;
	private NetManager netManager;
	private List<byte[]> queuedPackets;
	private Dictionary<int, NetPeer> connectedPeers;
	private bool packetReceiveCallbackSet;
	private bool clientIsConnected = false;

	public NetworkHandler() {
		eventManager = (EventManager)MetaHandler.Get<IEventManager>();
		packetHandlers = new();
		listener = new EventBasedNetListener();
		netManager = new NetManager(listener);
		queuedPackets = new();
		connectedPeers = new();

		MetaHandler.Register<NetworkHandler>(this);

        RegisterClientPacketType<ConnectionInfoPacket>();
        RegisterClientPacketType<ChunkDataPacket>();
        RegisterClientPacketType<EntityUpdatesPacket>();
        RegisterClientPacketType<AlertPacket>();

        RegisterServerPacketType<ConnectionRequestPacket>();
		RegisterServerPacketType<PlayerTransformPacket>();
		//RegistServererPacketType<PlayerActionsPacket>();
		RegisterServerPacketType<ChatSendPacket>();
    }

	public bool StartServer(string address, int port) {
		OVERRIDE_LOG_NAME("NetworkHandler");

		if (!netManager.Start(address, "", port)) {
			KERR("Failed to start server on port {" + port + "}");
			return false;
		}

		eventManager.SubscribeToEvent<ConnectionRequestPacket>((ConnectionRequestPacket packet) => {
			KINFO("Got connection request from client with ID {" + packet.clientPeerID + "} and username \"" + packet.playerName + "\"");

			ConnectionInfoPacket connectionInfoPacket = new ConnectionInfoPacket(0);
			NetDataWriter writer = new NetDataWriter();
			connectionInfoPacket.Serialize(writer);
			QueuePacket(writer, (int)PacketType.CONNECTION_INFO);
        });

        listener.ConnectionRequestEvent += (ConnectionRequest request) => {
			request.AcceptIfKey(connectionSecretKey);
		};
		listener.PeerConnectedEvent += (NetPeer peer) => {
			KINFO("New client connected from " + peer.Address + " with client ID {" + peer.Id + "}");
			int firstOpenIndex = -1;
			for (int iterator = 0; iterator < connectedPeers.Count; iterator++) {
				if (connectedPeers[iterator] == null) {
					firstOpenIndex = iterator;
					break;
				}
			}
			if (firstOpenIndex == -1) {
				firstOpenIndex = connectedPeers.Count;
                connectedPeers.Add(peer.Id, peer);
			}
			connectedPeers[firstOpenIndex] = peer;
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
			NetDataWriter writer = new NetDataWriter();
			connectionRequestPacket.Serialize(writer);
			QueuePacket(writer, (int)PacketType.CONNECTION_REQUEST);
			FlushPackets();

			eventManager.SubscribeToEvent<ConnectionInfoPacket>((ConnectionInfoPacket packet) => {
				KINFO("Got connection response from server with status code {" + packet.statusCode + "}");
				if (packet.statusCode == 0) {
					KINFO("Joining server...");
					MetaHandler.Get<ISingleplayerHandler>().CreateGhostWorld();
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

	private byte[] EncapsulatePacket(NetDataWriter packet, int packetID) {
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

	private byte[] DecapsulatePacket(NetPacketReader reader, out int packetID) {
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

	public void QueuePacket(NetDataWriter packet, int packetID) {
		byte[] finalPacket = EncapsulatePacket(packet, packetID);
		queuedPackets.Add(finalPacket);
	}

	public void FlushPackets() {
		foreach (byte[] packet in queuedPackets) {
			netManager.SendToAll(packet, DeliveryMethod.ReliableOrdered);
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
	CONNECTION_INFO,    // Sends different status codes to players describing the state of the client in the world
	CHUNK_DATA,         // Holds chunk data
	ENTITY_UPDATES,     // Holds updated data about all entities in radius of the player
	ALERT_BROADCAST,    // Alerts and chat messages from the server

	                    // Client->Server
	CONNECTION_REQUEST, // Request to join the server
	PLAYER_MOVEMENT,    // Info about the player's movement and position
	PLAYER_ACTIONS,     // Info about player actions (breaking blocks & stuff)
	CHAT_SEND           // Chat messages sent by the player
}

public struct ConnectionInfoPacket : INetSerializable {
	// 0 - Connection reqeust accepted
	// 1 - Connection reqeust denied
	// 2 - Server is ready to start sending world data
	// 3 - Player has been forcefully disconnected
	public int statusCode;

	public ConnectionInfoPacket(int statusCode) {
		this.statusCode = statusCode;
	}

	public void Serialize(NetDataWriter writer) {
		writer.Put(statusCode);
	}

	public void Deserialize(NetDataReader reader) {
		statusCode = reader.GetInt();
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

public struct EntityUpdatesPacket : INetSerializable {
	public List<ulong> entityAUIDs;
	public List<SimpleTransform> entityTransforms;

	public EntityUpdatesPacket(List<ulong> entityAUIDs, List<SimpleTransform> entityTransforms) {
		this.entityAUIDs = entityAUIDs;
		this.entityTransforms = entityTransforms;
	}

	public void Serialize(NetDataWriter writer) {
		writer.Put(entityAUIDs.Count);
		for (int iterator = 0; iterator < entityAUIDs.Count; iterator++) {
			writer.Put(entityAUIDs[iterator]);
		}
		for (int iterator = 0; iterator < entityAUIDs.Count; iterator++) {
			SimpleTransform transform = entityTransforms[iterator];
			writer.Put(transform.position.X);
			writer.Put(transform.position.Y);
			writer.Put(transform.position.Z);
			writer.Put(transform.orientation.X);
			writer.Put(transform.orientation.Y);
			writer.Put(transform.orientation.Z);
		}
	}

	public void Deserialize(NetDataReader reader) {
		int entities = reader.GetInt();
		for (int iterator = 0; iterator < entities; iterator++) {
            entityAUIDs[iterator] = reader.GetULong();
        }
		for (int iterator = 0; iterator < entities; iterator++) {
			float positionX = reader.GetFloat();
			float positionY = reader.GetFloat();
			float positionZ = reader.GetFloat();
			float orientationX = reader.GetFloat();
			float orientationY = reader.GetFloat();
			float orientationZ = reader.GetFloat();
			entityTransforms[iterator] = new SimpleTransform(new Vector3(positionX, positionY, positionZ), new Vector3(orientationX, orientationY, orientationZ));
		}
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

public struct PlayerTransformPacket : IClientPacket, INetSerializable {
	public ulong AUID;
	public Vector3 position;
	public Vector3 orientation;
	public Vector3 velocity;

    public int clientPeerID { get; set; }

    public void Serialize(NetDataWriter writer) {
		writer.Put(AUID);
		writer.Put(position.X);
		writer.Put(position.Y);
		writer.Put(position.Z);
		writer.Put(orientation.X);
		writer.Put(orientation.Y);
		writer.Put(orientation.Z);
		writer.Put(velocity.X);
		writer.Put(velocity.Y);
		writer.Put(velocity.Z);
	}

	public void Deserialize(NetDataReader reader) {
		AUID = reader.GetULong();
		float positionX = reader.GetFloat();
		float positionY = reader.GetFloat();
		float positionZ = reader.GetFloat();
		float orientationX = reader.GetFloat();
		float orientationY = reader.GetFloat();
		float orientationZ = reader.GetFloat();
		float velocityX = reader.GetFloat();
		float velocityY = reader.GetFloat();
		float velocityZ = reader.GetFloat();
		position = new Vector3(positionX, positionY, positionZ);
		orientation = new Vector3(orientationX, orientationY, orientationZ);
		velocity = new Vector3(velocityX, velocityY, velocityZ);
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