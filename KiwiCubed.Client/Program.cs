namespace KiwiCubed.Client;

using KiwiCubed.Api;
using KiwiCubed.Engine;

using static KiwiCubed.Api.Globals;

public class Program {
	static void Main(string[] args) {
		KLogger logger = new KLogger("Client Controller");

		Api.Meta.Initialize(new MetaHandlerWrapper());
		MetaHandler.SetupThreadMeta(GameType.CLIENT);

		logger.INFO("Setting up API implementations...");
		Api.Physics.Initialize(new PhysicsWrapper());
		Api.Renderer.Initialize(new RendererWrapper(), new TextRendererWrapper());
		Inventory.InventoryCreator = (slotCount) => new InventorySystem(slotCount);

        Thread.CurrentThread.Name = "KiwiCubed_Client";

		if (args.Length > 0) {
			logger.INFO("Command-line arguments detected, printing detected arguments:");
			for (int iterator = 0; iterator < args.Length; iterator++) {
				string suffix = "";
				string arg = args[iterator].ToLower();
				switch (arg) {
					case "debug":
						isDebug = true;
						break;
					case "allow-npot-textures":
						forcePowerOfTwoTextures = false;
						break;
					case "force-square-textures":
						forceSquareTextures = true;
						suffix = " - Argument highly not recommended, will cause many textures to be rejected";
						break;
					case "soft-errors":
						disableCrashOnError = true;
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

		logger.INFO("Initializing KiwiCubed Engine client v" + engineVersion);

		KiwiCubedClient client = new KiwiCubedClient();
		client.StartGame();
		
		logger.INFO("Exiting...");
	}
}