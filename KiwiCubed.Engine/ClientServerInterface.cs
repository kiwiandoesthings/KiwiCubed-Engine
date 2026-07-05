namespace KiwiCubed.Engine;

using KiwiCubed.Api;

using static KiwiCubed.Api.Globals;

public class ClientServerInterface : IClientServerInterface, IDisposable {
    public ClientServerInterface() {
        MetaHandler.Register<IClientServerInterface>(this);
    }

    public bool InitializeServerConnection(string address) {
        return MetaHandler.Get<NetworkHandler>().StartClient(address, (int)defaultPort);
    }

    public void Dispose() {
        MetaHandler.Deregister<IClientServerInterface>();
    }
}