namespace KiwiCubed.Client;

using KiwiCubed.Api;
using KiwiCubed.Engine;
using KiwiCubed.Server;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.KLogger;

public class Program {
	static void Main(string[] args) {
		KiwiCubed.Api.Meta.Initialize(new MetaHandlerWrapper());
		Thread.CurrentThread.Name = "KiwiCubed_Client";

		OVERRIDE_LOG_NAME("Pre-Initialization");

		KINFO("Initializing KiwiCubed Engine client v" + engineVersion);

		KINFO("Setting up static API implementations...");
        KiwiCubed.Api.Logger.Initialize(new KLoggerWrapper());
		KiwiCubed.Api.Physics.Initialize(new PhysicsWrapper());
		KiwiCubed.Api.Renderer.Initialize(new RendererWrapper(), new TextRendererWrapper());

        //KINFO("Starting local server...");
		//Task.Run(() => {
		//	Thread.CurrentThread.Name = "KiwiCubed_Server";
		//	KiwiCubedServer server = new KiwiCubedServer();
		//	server.StartServer();
		//});

		KiwiCubedClient client = new KiwiCubedClient();
		client.StartClient();
		
		KINFO("Exiting...");
	}
}