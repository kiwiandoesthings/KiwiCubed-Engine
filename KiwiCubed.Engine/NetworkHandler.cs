namespace KiwiCubed.Engine;

using LiteNetLib;
using LiteNetLib.Utils;

using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.KLogger;

public class NetworkHandler {
	private static string connectionSecretKey = "KiwiCubed_Engine_Server_Connection_Secret_Key";
	private EventBasedNetListener listener;
	private NetManager netManager;
	private List<NetDataWriter> queuedPackets;
	private bool serverOrClient;
	private bool packetReceiveCallbackSet;

	public NetworkHandler() {
		listener = new EventBasedNetListener();
		netManager = new NetManager(listener);
		queuedPackets = new();

		MetaHandler.Register<NetworkHandler>(this);
    }

	public bool StartServer(string address, int port) {
		OVERRIDE_LOG_NAME("NetworkHandler");

		serverOrClient = true;
		netManager.Start(address, "", port);

		listener.ConnectionRequestEvent += (ConnectionRequest request) => {
			request.AcceptIfKey(connectionSecretKey);
		};
		listener.PeerConnectedEvent += (NetPeer peer) => {
			KINFO("New client connected from " + peer.Address);
		};
        listener.PeerDisconnectedEvent += (NetPeer peer, DisconnectInfo info) => {
            KINFO("Client from " + peer.Address + " disconnected with reason: " + info.Reason);
        };

        KINFO("Started server on port {" + port + "}, listening for secret key \"" + connectionSecretKey + "\"");

		return true;
	}

	public bool StartClient(string address, int port) {
		OVERRIDE_LOG_NAME("NetworkHandler");

		serverOrClient = false;
		netManager.Start();

		netManager.Connect(address, port, connectionSecretKey);

		KINFO("Attempting to connect to server at \"" + address + "\" on port {" + port + "} using secret key \"" + connectionSecretKey + "\"");

        listener.PeerConnectedEvent += (NetPeer peer) => {
            KINFO("Successfully connected to server at " + peer.Address);
        };
        listener.PeerDisconnectedEvent += (NetPeer peer, DisconnectInfo info) => {
            KINFO("Disconnected from server with reason: " + info.Reason);
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
	}

	public bool IsServerOrClient() {
		return serverOrClient;
	}
}

public struct AlertPacket {
	public string message;
}

public struct ChunkPacket {
	public int X;
	public int Y;
	public int Z;

	public ushort[] blockIndices;
}