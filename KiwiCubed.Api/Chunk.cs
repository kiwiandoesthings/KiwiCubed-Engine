namespace KiwiCubed.Api;

public interface IChunk {
	public abstract int chunkX { get; }
	public abstract int chunkY { get; }
	public abstract int chunkZ { get; }
	public bool IsReal();
}