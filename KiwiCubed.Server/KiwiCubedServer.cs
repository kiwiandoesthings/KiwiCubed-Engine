namespace KiwiCubed.Server;

using KiwiCubed.Api;
using KiwiCubed.Engine;
using System.Diagnostics;

public class KiwiCubedServer : Engine {
	private EventManager eventManager;
	private NetworkHandler networkHandler;
	private AssetManager assetManager;
	private WorldServerHandler worldHandler;
	private ModHandler modHandler;
	private KLogger logger;

    private Stopwatch gameTime = Stopwatch.StartNew();
    private bool isStarted = false;

	public override void StartGame() {
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

		MetaHandler.Register<Engine>(this);

        RunGame();
	}

	private void RunGame() {
		worldHandler.CreateWorld(17);
		//worldHandler.LoadWorld("debug");

		while (worldHandler.IsLoadedIntoWorld()) {
			if (shouldExit) {
				ExitGame();
				return;
			}
            worldHandler.Update();
        }
	}

	public override void ExitGame() {
		return;
	}
}