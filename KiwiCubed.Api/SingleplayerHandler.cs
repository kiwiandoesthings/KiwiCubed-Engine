namespace KiwiCubed.Api;

public interface ISingleplayerHandler {
	public void CreateWorld(int horizontalSize, int verticalSize);
	public void LoadWorld(string worldName);
	public void CreateGhostWorld();
    public void ExitWorld();
	public void SaveWorld();
	public IWorld GetWorld();
	public bool IsLoadedIntoWorld();
}