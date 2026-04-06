namespace KiwiCubed.Engine;

using KiwiCubed.Api;

using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.Globals;

class Program {
    static void Main(String[] args) {
        OVERRIDE_LOG_NAME("Pre-Initialization");

        KINFO("Setting up static API implementations...");
        KiwiCubed.Api.Logger.Initialize(new KLoggerWrapper());
        KiwiCubed.Api.Systems.Initialize(new SystemsWrapper());
        KiwiCubed.Api.Physics.Initialize(new PhysicsWrapper());
        KiwiCubed.Api.Renderer.Initialize(new RendererWrapper(), new TextRendererWrapper());

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

        KiwiCubedEngine engine = new KiwiCubedEngine();
        engine.StartEngine();
    }
}