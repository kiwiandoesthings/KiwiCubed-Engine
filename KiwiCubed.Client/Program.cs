namespace KiwiCubed.Client;

using KiwiCubed.Api;
using KiwiCubed.Engine;

using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.KLogger;

public class Program {
	static void Main(string[] args) {
		OVERRIDE_LOG_NAME("Pre-Initialization");

		KINFO("Initializing KiwiCubed Engine client v" + engineVersion);
		isServerOrClient = false;

		KINFO("Setting up static API implementations...");
        KiwiCubed.Api.Logger.Initialize(new KLoggerWrapper());
        KiwiCubed.Api.Systems.Initialize(new SystemsWrapper());
        KiwiCubed.Api.Physics.Initialize(new PhysicsWrapper());
        KiwiCubed.Api.Renderer.Initialize(new RendererWrapper(), new TextRendererWrapper());

        KINFO("Starting server...");
		KiwiCubedClient client = new KiwiCubedClient();
		client.StartClient();
		
		KINFO("Exiting...");
	}
}