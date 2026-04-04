namespace KiwiCubed.Api;

public static class SingleplayerHandler {
	private static ISingleplayerHandler singleplayerHandler;

	public static void Initialize(ISingleplayerHandler implementation) => singleplayerHandler = implementation;

	public static void CreateWorld(int horizontalSize, int verticalSize) => singleplayerHandler.CreateWorld(horizontalSize, verticalSize);
	public static void LoadWorld(string worldName) => singleplayerHandler.LoadWorld(worldName);
	public static void ExitWorld() => singleplayerHandler.ExitWorld();
    public static void SaveWorld() => singleplayerHandler.SaveWorld();
	public static IWorld GetWorld() => singleplayerHandler.GetWorld();
	public static bool IsLoadedIntoWorld() => singleplayerHandler.IsLoadedIntoWorld();
}

public interface ISingleplayerHandler {
	public void CreateWorld(int horizontalSize, int verticalSize);
	public void LoadWorld(string worldName);
	public void ExitWorld();
	public void SaveWorld();
	public IWorld GetWorld();
	public bool IsLoadedIntoWorld();
}