namespace KiwiCubed.Server;

using KiwiCubed.Engine;

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
			KCRITICAL("Server is already running!");
			KBREAK();
		}
		isStarted = true;

		networkHandler = new NetworkHandler();
		networkHandler.StartServer(7072);

		eventManager = new EventManager();
		assetManager = new AssetManager();
		singleplayerHandler = new SingleplayerHandler();
		modHandler = new ModHandler();

		modHandler.LoadModScripts();
	}
}