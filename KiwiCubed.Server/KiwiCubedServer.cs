namespace KiwiCubed.Server;

using KiwiCubed.Api;
using KiwiCubed.Engine;
using System.Diagnostics;

using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.KLogger;

public class KiwiCubedServer {
	private EventManager eventManager;
	private NetworkHandler networkHandler;
	private AssetManager assetManager;
	private SingleplayerHandler singleplayerHandler;
	private ModHandler modHandler;

    private Stopwatch gameTime = Stopwatch.StartNew();
    private bool isStarted = false;

	public void StartServer() {
		OVERRIDE_LOG_NAME("Initialization");

		if (isStarted) {
			KERR("Server is already running!");
			KBREAK();
		}
		isStarted = true;

        MetaHandler.SetupThreadMeta(GameType.SERVER);

		eventManager = new EventManager();
		networkHandler = new NetworkHandler();
		assetManager = new AssetManager();
		singleplayerHandler = new SingleplayerHandler();
		modHandler = new ModHandler();

		modHandler.LoadModScripts();

		if (!networkHandler.StartServer("0.0.0.0", (int)defaultPort)) {
			KERR("Failed to start network interface for server");
			KBREAK();
		}

		KINFO("Took " + gameTime.Elapsed.TotalMilliseconds + "ms to start KiwiCubed Engine");
        gameTime.Restart();

        RunServer();
	}

	public void RunServer() {
		singleplayerHandler.CreateWorld(5, 10);

		while (singleplayerHandler.IsLoadedIntoWorld()) {
            singleplayerHandler.Update();
        }
	}
}