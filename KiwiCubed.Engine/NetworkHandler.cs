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
	}

	public void StartServer(int port) {
		serverOrClient = true;
		netManager.Start(port);

		listener.ConnectionRequestEvent += (ConnectionRequest request) => {
			request.AcceptIfKey(connectionSecretKey);
		};
	}

	public void StartClient(string address, int port) {
		serverOrClient = false;
		netManager.Start();

		netManager.Connect(address, port, connectionSecretKey);
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