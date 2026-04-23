namespace KiwiCubed.Api;

using ArchEntity = Arch.Core.Entity;

public interface IWorld {
	public int GetSeed();
	public List<ArchEntity> GetPlayers();
	public IChunkHandler GetChunkHandler();
	public IEntityManager GetEntityManager();
	public ulong GetSessionTicks();
}