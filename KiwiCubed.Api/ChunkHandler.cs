namespace KiwiCubed.Api;

using static KiwiCubed.Api.Util;

public interface IChunkHandler {
	public abstract IChunk AddChunk(int chunkX, int chunkY, int chunkZ);
	public abstract bool RemeshChunk(int chunkX, int chunkY, int chunkZ, bool updateNeighbors);
	public abstract IChunk GetChunk(int chunkX, int chunkY, int chunkZ, bool addIfNotFound);
	public abstract IChunk GetChunk(IntVector3 chunkPosition, bool addIfNotFound);
	public abstract bool AddBlock(FullBlockPosition fullPosition, ushort newBlockID);
	public abstract bool RemoveBlock(FullBlockPosition fullPosition);
	public abstract Dictionary<IntVector3, IChunk> GetChunks();
	public abstract IChunk GetDefaultChunk();
}