namespace KiwiCubed;

using KiwiCubed.Api;

using static KiwiCubed.Api.KLogger;

class Program {
    static void Main(String[] args) {
        KiwiCubed.Api.IMod.logger = new KLogger();
        KiwiCubed.Api.Systems.Initialize(new SystemsWrapper());
        OVERRIDE_LOG_NAME("Pre-Initialization");
        if (args.Length > 0) {
            KINFO("Command-line arguments detected, printing detected arguments:");
            for (int iterator = 0; iterator < args.Length; iterator++) {
                KINFO(" - " + args[iterator]);
            }
        }
        KiwiCubedEngine engine = new KiwiCubedEngine();
        engine.StartEngine();
    }
}