namespace KiwiCubed.Server;

using KiwiCubed.Api;
using KiwiCubed.Engine;

using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.KLogger;

public class Program {
	static void Main(string[] args) {
		OVERRIDE_LOG_NAME("Pre-Initialization");

		KINFO("Initializing KiwiCubed Engine dedicated server v" + engineVersion);

		KINFO("Setting up static API implementations...");
		KiwiCubed.Api.Logger.Initialize(new KLoggerWrapper());
		KiwiCubed.Api.Meta.Initialize(new MetaHandlerWrapper());

        KINFO("Starting server...");
		KiwiCubedServer server = new KiwiCubedServer();
		server.StartServer();

		KINFO("Exiting...");
	}
}