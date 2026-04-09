namespace KiwiCubed.Server;

using KiwiCubed.Api;
using KiwiCubed.Engine;

using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.KLogger;

public class KiwiCubedServer {
	private NetworkHandler networkHandler;
	private EventManager eventManager;
	private AssetManager assetManager;
	private SingleplayerHandler singleplayerHandler;
	private ModHandler modHandler;
	private bool isStarted = false;

	public void StartServer() {
		OVERRIDE_LOG_NAME("Initialization");

		if (isStarted) {
			KERR("Server is already running!");
			KBREAK();
		}
		isStarted = true;

        MetaHandler.SetupThreadMeta(GameType.SERVER);

        networkHandler = new NetworkHandler();
		if (!networkHandler.StartServer("localhost", (int)defaultPort)) {
			KERR("Failed to start network interface for server");
			KBREAK();
		}

		eventManager = new EventManager();
		assetManager = new AssetManager();
		singleplayerHandler = new SingleplayerHandler();
		modHandler = new ModHandler();

		modHandler.LoadModScripts();

		RunServer();
	}

	public void RunServer() {
		singleplayerHandler.CreateWorld(5, 4);

		while (singleplayerHandler.IsLoadedIntoWorld()) {
            networkHandler.PollEvents();
            singleplayerHandler.Update();

        }
	}
}