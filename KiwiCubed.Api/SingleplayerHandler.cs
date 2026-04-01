namespace KiwiCubed.Api;

public static class SingleplayerHandler {
	private static ISingleplayerHandler singleplayerHandler;

	public static void Initialize(ISingleplayerHandler implementation) => singleplayerHandler = implementation;

	public static void CreateWorld(int horizontalSize, int verticalSize) => singleplayerHandler.CreateWorld(horizontalSize, verticalSize);
	public static void LoadWorld(string worldName) => singleplayerHandler.LoadWorld(worldName);
    public static void SaveWorld() => singleplayerHandler.SaveWorld();
	public static Entity GetEntity(ulong entityAUID) => singleplayerHandler.GetEntity(entityAUID);
	public static IPlayer GetPlayer() => singleplayerHandler.GetPlayer();
	public static bool IsLoadedIntoSingleplayerWorld() => singleplayerHandler.IsLoadedIntoSingleplayerWorld();
	public static void ExitWorld() => singleplayerHandler.ExitWorld();
}

public interface ISingleplayerHandler {
	public void CreateWorld(int horizontalSize, int verticalSize);
	public void LoadWorld(string worldName);
	public void SaveWorld();
	public Entity GetEntity(ulong entityAUID);
	public IPlayer GetPlayer();
	public bool IsLoadedIntoSingleplayerWorld();
	public void ExitWorld();
}