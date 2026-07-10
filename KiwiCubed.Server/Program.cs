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
        ILogger.LoggerCreator = (logName) => new KLogger(logName);
        Api.Inventory.InventoryCreator = (slotCount) => new InventorySystem(slotCount);

		Thread.CurrentThread.Name = "KiwiCubed_Server";

        if (args.Length > 0) {
            logger.INFO("Command-line arguments detected, printing detected arguments:");
            for (int iterator = 0; iterator < args.Length; iterator++) {
                string suffix = "";
                string arg = args[iterator].ToLower();
                switch (arg) {
                    case "debug":
                        isDebug = true;
                        break;
                    case "soft-errors":
                        disableCrashOnError = true;
                        break;
                    case "integrated":
                        isIntegratedGame = true;
                        break;
                    case "":
                        break;
                    default:
                        suffix = " - Unrecognized argument, ignoring";
                        break;
                }
                logger.INFO(" - \"" + args[iterator] + "\"" + suffix);
            }
        }

        logger.INFO("Initializing KiwiCubed Engine dedicated server v" + engineVersion);

        logger.INFO("Starting server...");
		KiwiCubedServer server = new KiwiCubedServer();
		server.StartGame();

		logger.INFO("Exiting...");
	}
}