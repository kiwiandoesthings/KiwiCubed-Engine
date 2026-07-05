namespace KiwiCubed.Server;

using KiwiCubed.Api;
using KiwiCubed.Engine;

using static KiwiCubed.Api.Globals;

public class Program {
	static void Main(string[] args) {
        KLogger logger = new KLogger("Client Controller");

        Api.Meta.Initialize(new MetaHandlerWrapper());
		MetaHandler.SetupThreadMeta(GameType.SERVER);

		logger.INFO("Setting up API implementations...");
        Api.Physics.Initialize(new PhysicsWrapper());
        Api.Inventory.InventoryCreator = (slotCount) => new InventorySystem(slotCount);

		Thread.CurrentThread.Name = "KiwiCubed_Server";

		logger.INFO("Initializing KiwiCubed Engine dedicated server v" + engineVersion);

        logger.INFO("Starting server...");
		KiwiCubedServer server = new KiwiCubedServer();
		server.StartServer();

		logger.INFO("Exiting...");
	}
}