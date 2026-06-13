namespace KiwiCubed.Api;

public interface ISingleplayerHandler {
	public void CreateServerWorld(int horizontalSize, int verticalSize);
	public void LoadServerWorld(string worldName);
	public void CreateClientWorld();
    public void ExitWorld();
	public void SaveWorld();
	public IWorld GetWorld();
	public bool IsLoadedIntoWorld();
}