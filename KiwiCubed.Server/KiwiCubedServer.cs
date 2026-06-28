namespace KiwiCubed.Server;

using KiwiCubed.Api;
using KiwiCubed.Engine;
using System.Diagnostics;

using static KiwiCubed.Api.KLogger;

public class KiwiCubedServer {
	private EventManager eventManager;
	private NetworkHandler networkHandler;
	private AssetManager assetManager;
	private WorldServerHandler worldHandler;
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

		eventManager = new EventManager();
		networkHandler = new NetworkHandler();
		assetManager = new AssetManager();
        worldHandler = new WorldServerHandler();
		modHandler = new ModHandler();

		modHandler.LoadModScripts();

		KINFO("Took " + gameTime.Elapsed.TotalMilliseconds + "ms to start KiwiCubed Engine");
        gameTime.Restart();

        RunServer();
	}

	public void RunServer() {
		worldHandler.CreateWorld(5, 10);

		while (worldHandler.IsLoadedIntoWorld()) {
            worldHandler.Update();
        }
	}
}