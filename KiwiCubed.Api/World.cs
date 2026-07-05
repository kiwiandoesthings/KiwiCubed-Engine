namespace KiwiCubed.Api;

using ArchEntity = Arch.Core.Entity;

public interface IWorld {
	public int GetSeed();
	public IChunkHandler GetChunkHandler();
	public IEntityManager GetEntityManager();
	public ulong GetSessionTicks();
}

public interface IWorldClient : IWorld {
	public ArchEntity GetClientPlayer();
}

public interface IWorldServer {
	public List<ArchEntity> GetPlayers();
}