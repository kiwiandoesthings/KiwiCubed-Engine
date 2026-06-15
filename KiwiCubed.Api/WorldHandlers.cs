namespace KiwiCubed.Api;

public interface IWorldHandler {
    public void ExitWorld();
    public IWorld GetWorld();
    public bool IsLoadedIntoWorld();
}

public interface IWorldServerHandler : IWorldHandler {
	public void CreateWorld(int horizontalSize, int verticalSize);
	public void LoadWorld(string worldName);
	public void SaveWorld();
}

public interface IWorldClientHandler : IWorldHandler {
    public void CreateClientWorld();
}