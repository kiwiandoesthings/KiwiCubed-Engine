namespace KiwiCubed.Server;

using KiwiCubed.Api;
using KiwiCubed.Engine;

using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.KLogger;

public class Program {
	static void Main(string[] args) {
		OVERRIDE_LOG_NAME("Pre-Initialization");

		KINFO("Setting up API implementations...");
        KiwiCubed.Api.Meta.Initialize(new MetaHandlerWrapper());
        KiwiCubed.Api.Logger.Initialize(new KLoggerWrapper());
        KiwiCubed.Api.Physics.Initialize(new PhysicsWrapper());
        KiwiCubed.Api.Inventory.InventoryCreator = (slotCount) => new InventorySystem(slotCount);

		Thread.CurrentThread.Name = "KiwiCubed_Server";

		KINFO("Initializing KiwiCubed Engine dedicated server v" + engineVersion);

        KINFO("Starting server...");
		KiwiCubedServer server = new KiwiCubedServer();
		server.StartServer();

		KINFO("Exiting...");
	}
}