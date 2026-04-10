namespace KiwiCubed.Engine;

using K4os.Compression.LZ4;
using KiwiCubed.Api;
using LiteNetLib;
using LiteNetLib.Utils;

using static KiwiCubed.Api.KLogger;

public class NetworkHandler {
	private static string connectionSecretKey = "KiwiCubed_Engine_Server_Connection_Secret_Key";
	private EventBasedNetListener listener;
	private NetManager netManager;
	private List<byte[]> queuedPackets;
	private List<NetPeer> connectedPeers;
	private bool serverOrClient;
	private bool packetReceiveCallbackSet;
	private bool clientIsConnected = false;

	public NetworkHandler() {
		listener = new EventBasedNetListener();
		netManager = new NetManager(listener);
		queuedPackets = new();
		connectedPeers = new();

		MetaHandler.Register<NetworkHandler>(this);
    }

	public bool StartServer(string address, int port) {
		OVERRIDE_LOG_NAME("NetworkHandler");

		serverOrClient = true;
		if (!netManager.Start(address, "", port)) {
			KERR("Failed to start server on port {" + port + "}");
			return false;
		}

		listener.ConnectionRequestEvent += (ConnectionRequest request) => {
			request.AcceptIfKey(connectionSecretKey);
		};
		listener.PeerConnectedEvent += (NetPeer peer) => {
			KINFO("New client connected from " + peer.Address);
			int firstOpenIndex = -1;
			for (int iterator = 0; iterator < connectedPeers.Count; iterator++) {
				if (connectedPeers[iterator] == null) {
					firstOpenIndex = iterator;
					break;
				}
			}
			if (firstOpenIndex == -1) {
				firstOpenIndex = connectedPeers.Count;
                connectedPeers.Add(peer);
			}
			connectedPeers[firstOpenIndex] = peer;
			((World)MetaHandler.Get<ISingleplayerHandler>().GetWorld()).ReceivePlayer(firstOpenIndex);
        };
        listener.PeerDisconnectedEvent += (NetPeer peer, DisconnectInfo info) => {
            KINFO("Client from " + peer.Address + " disconnected with reason: " + info.Reason);
			
			int clientIndex = connectedPeers.IndexOf(peer);
			if (clientIndex == -1) {
				KERR("Tried to remove a client from client list that wasn't found");
				KBREAK();
			}
			connectedPeers[clientIndex] = null;
        };
        listener.NetworkReceiveEvent += (NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod) => {
            KINFO("Got packet");
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

		serverOrClient = false;
		netManager.Start();

		netManager.Connect(address, port, connectionSecretKey);

		KINFO("Attempting to connect to server at \"" + address + "\" on port {" + port + "} using secret key \"" + connectionSecretKey + "\"");

        listener.PeerConnectedEvent += (NetPeer peer) => {
            KINFO("Successfully connected to server at " + peer.Address);
			clientIsConnected = true;
			MetaHandler.Get<ISingleplayerHandler>().CreateGhostWorld();
        };
        listener.PeerDisconnectedEvent += (NetPeer peer, DisconnectInfo info) => {
            KINFO("Disconnected from server with reason: " + info.Reason);
        };
		listener.NetworkReceiveEvent += (NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod) => {
			KINFO("Got packet");
			byte[] packetData = DecapsulatePacket(reader, out int packetID);
			NetDataReader packetReader = new NetDataReader(packetData);
			ChunkPacket chunkPacket = packetReader.Get<ChunkPacket>();
			((World)MetaHandler.Get<ISingleplayerHandler>().GetWorld()).ReceiveChunk(chunkPacket.X, chunkPacket.Y, chunkPacket.Z, chunkPacket.blockIndices);
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

	public bool IsServerOrClient() {
		return serverOrClient;
	}
}

public enum PacketType : int {
	HANDSHAKE,
	CONNECT_ERROR,
	PLAYER_ERROR,
	JOIN_ACCEPT,
	CHUNK_DATA,
	ENTITY_UPDATES,
	PLAYER_SERVER_EVENT,
	ALERT,
	CHAT_MESSAGE
}

public struct AlertPacket : INetSerializable {
	public string message;

	public AlertPacket(string message) {
		this.message = message;
    }

    public void Serialize(NetDataWriter writer) {
		writer.Put(message);
    }

	public void Deserialize(NetDataReader reader) {
		message = reader.GetString();
    }
}

public struct ChunkPacket : INetSerializable {
	public int X;
	public int Y;
	public int Z;

	public ushort[] blockIndices;

	public ChunkPacket(int x, int y, int z, ushort[] blockIndices) {
		X = x;
		Y = y;
		Z = z;
		this.blockIndices = blockIndices;
	}

	public void Serialize(NetDataWriter writer) {
		writer.Put(X);
		writer.Put(Y);
		writer.Put(Z);

		writer.Put(blockIndices.Length);
		for (int iterator = 0; iterator < blockIndices.Length; iterator++) {
			writer.Put(blockIndices[iterator]);
		}
	}

	public void Deserialize(NetDataReader reader) {
		X = reader.GetInt();
		Y = reader.GetInt();
		Z = reader.GetInt();

		int blockIndicesLength = reader.GetInt();
		blockIndices = new ushort[blockIndicesLength];
		for (int iterator = 0; iterator < blockIndicesLength; iterator++) {
			blockIndices[iterator] = reader.GetUShort();
        }
    }
}