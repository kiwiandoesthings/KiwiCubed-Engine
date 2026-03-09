using static KiwiCubed.Api.AssetDefinitions;

namespace KiwiCubed.Api;

public interface IAssetManager {
	public void RegisterBlock(AssetStringID stringID, Block block);
	public int GetBlockRawID(AssetStringID stringID);
	public Block GetBlock(AssetStringID stringID);
	public Block GetBlock(int rawID);
	public void EmptyAssets();
}