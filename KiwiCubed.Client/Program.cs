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

		if (args.Length > 0) {
			KINFO("Command-line arguments detected, printing detected arguments:");
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
				KINFO(" - \"" + args[iterator] + "\"" + suffix);
			}
		}

		KINFO("Initializing KiwiCubed Engine client v" + engineVersion);

		KINFO("Setting up static API implementations...");
        KiwiCubed.Api.Logger.Initialize(new KLoggerWrapper());
		KiwiCubed.Api.Physics.Initialize(new PhysicsWrapper());
		KiwiCubed.Api.Renderer.Initialize(new RendererWrapper(), new TextRendererWrapper());

		KiwiCubedClient client = new KiwiCubedClient();
		client.StartClient();
		
		KINFO("Exiting...");
	}
}