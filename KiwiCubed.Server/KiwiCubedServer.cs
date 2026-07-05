namespace KiwiCubed.Server;

using KiwiCubed.Api;
using KiwiCubed.Engine;
using System.Diagnostics;

public class KiwiCubedServer {
	private EventManager eventManager;
	private NetworkHandler networkHandler;
	private AssetManager assetManager;
	private WorldServerHandler worldHandler;
	private ModHandler modHandler;
	private KLogger logger;

    private Stopwatch gameTime = Stopwatch.StartNew();
    private bool isStarted = false;

	public void StartServer() {
        logger = new KLogger("Server");

        if (isStarted) {
			logger.ERR("Server is already running!");
			logger.BREAK();
		}
		isStarted = true;

		eventManager = new EventManager();
		networkHandler = new NetworkHandler();
		assetManager = new AssetManager();
        worldHandler = new WorldServerHandler();
		modHandler = new ModHandler();

		modHandler.LoadModScripts();

		logger.INFO("Took " + gameTime.Elapsed.TotalMilliseconds + "ms to start KiwiCubed Engine");
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