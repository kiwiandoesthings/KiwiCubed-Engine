namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using LiteNetLib;
using LiteNetLib.Utils;

using static KiwiCubed.Api.KLogger;

public class NetworkHandler {
	private static string connectionSecretKey = "KiwiCubed_Engine_Server_Connection_Secret_Key";
	private EventBasedNetListener listener;
	private NetManager netManager;
	private List<NetDataWriter> queuedPackets;
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
			ChunkPacket chunkPacket = reader.Get<ChunkPacket>();
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

	public void QueuePacket(NetDataWriter packet) {
		queuedPackets.Add(packet);
	}

	public void FlushPackets() {
		foreach (NetDataWriter packet in queuedPackets) {
			netManager.SendToAll(packet.Data, DeliveryMethod.ReliableOrdered);
		}
		queuedPackets.Clear();
	}

	public bool IsServerOrClient() {
		return serverOrClient;
	}
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