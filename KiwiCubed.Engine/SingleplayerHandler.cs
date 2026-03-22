namespace KiwiCubed.Engine;

using KiwiCubed.Api;

using static KiwiCubed.Api.KLogger;

public class SingleplayerHandlerWrapper : ISingleplayerHandler {
	public void CreateWorld(int horizontalSize, int verticalSize) => SingleplayerHandler.CreateWorld(horizontalSize, verticalSize);
	public void SaveWorld() => SingleplayerHandler.SaveWorld();
	public Entity GetEntity(ulong entityAUID) => SingleplayerHandler.GetEntity(entityAUID);
	public Entity GetPlayer() => (Entity)SingleplayerHandler.GetPlayer();
	public bool IsLoadedIntoSingleplayerWorld() => SingleplayerHandler.IsLoadedIntoSingleplayerWorld();
	public void ExitWorld() => SingleplayerHandler.ExitWorld();
}

public static class SingleplayerHandler {
	private static World singleplayerWorld = null;
	//private DebugRenderer debugRenderer = null;
	private static bool isLoadedIntoSingleplayerWorld = false;
	private static bool shouldUnloadWorld = false;

	public static void CreateWorld(int horizontalSize, int verticalSize) {
		OVERRIDE_LOG_NAME("Singleplayer Handler");

		if (isLoadedIntoSingleplayerWorld) {
			KERR("Tried to create a singleplayer world while one was already loaded");
			return;
		}
		KINFO("Creating singleplayer world...");
		singleplayerWorld = new World((uint)horizontalSize, (uint)verticalSize);
		singleplayerWorld.GenerateWorld();
		isLoadedIntoSingleplayerWorld = true;
		KINFO("Starting singleplayer world...");
		singleplayerWorld.StartTickThread();

		UI ui = (UI)SystemsManager.Get<IUI>();
		ui.DisableUI();
	}

	public static void ExitWorld() {
		OVERRIDE_LOG_NAME("Singleplayer Handler");

		if (!isLoadedIntoSingleplayerWorld) {
			KERR("Tried to exit singleplayer world while one wasn't loaded");
			return;
		}
		KINFO("Shutting down singleplayer world...");
		shouldUnloadWorld = true;
	}

	public static void Update() {
		OVERRIDE_LOG_NAME("Singleplayer Handler");

		if (isLoadedIntoSingleplayerWorld) {
			singleplayerWorld.Update();
		}

		if (shouldUnloadWorld) {
			isLoadedIntoSingleplayerWorld = false;
			shouldUnloadWorld = false;
			KINFO("Exiting singleplayer world...");
			singleplayerWorld.Dispose();
		}
	}

	public static void Render() {
		if (isLoadedIntoSingleplayerWorld) {
			singleplayerWorld.Render();
		}
	}

	public static void SaveWorld() {
		OVERRIDE_LOG_NAME("Singleplayer Handler");

		KINFO("Saving world...");
	}

	public static Entity GetEntity(ulong entityAUID) {
		return singleplayerWorld.GetEntityManager().GetEntity(entityAUID);
	}

	public static Entity GetPlayer() {
		return (Entity)singleplayerWorld.GetPlayer();
	}

	public static World GetWorld() {
		return singleplayerWorld;
	}

	public static bool IsLoadedIntoSingleplayerWorld() {
		return isLoadedIntoSingleplayerWorld;
	}
};