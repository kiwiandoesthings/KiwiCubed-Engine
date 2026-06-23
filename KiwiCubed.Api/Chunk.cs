namespace KiwiCubed.Api;

using static Utils;

public interface IChunk {
	public abstract int chunkX { get; }
	public abstract int chunkY { get; }
	public abstract int chunkZ { get; }
	public bool IsReal();
	public bool SetBlock(IntVector3 blockPosition, ushort blockID);
}