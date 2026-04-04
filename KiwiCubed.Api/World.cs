namespace KiwiCubed.Api;

public interface IWorld {
	public int GetSeed();
	public IPlayer GetPlayer();
	public IChunkHandler GetChunkHandler();
	public IEntityManager GetEntityManager();
}