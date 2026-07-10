namespace KiwiCubed.Api;

public interface IWorldHandler {
    public void ExitWorld();
    public bool IsLoadedIntoWorld();
}

public interface IWorldServerHandler : IWorldHandler {
    public IWorldServer GetWorld();
	public IWorldServer CreateWorld(int seed);
	public IWorldServer LoadWorld(string worldName);
	public void SaveWorld();
}

public interface IWorldClientHandler : IWorldHandler {
    public IWorldClient GetWorld();
    public IWorldClient CreateClientWorld();
}