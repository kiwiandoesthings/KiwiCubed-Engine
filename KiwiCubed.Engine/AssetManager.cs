namespace KiwiCubed;

using KiwiCubed.Api;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.KLogger;

public class AssetManager : IAssetManager {
	private Dictionary<AssetStringID, int> blockRawIDs;
	private List<Block> blocks;
	private int latestBlockID = 0;

	public AssetManager() {
		blockRawIDs = new();
		blocks = new();

		RegisterBlock(new AssetStringID("kiwicubed", "air"), new BlockAir());

		SystemsManager.Register<IAssetManager>(this);
	}

	public void RegisterBlock(AssetStringID stringID, Block block) {
		OVERRIDE_LOG_NAME("AssetManager");

		blockRawIDs.Add(stringID, latestBlockID);
		blocks.Add(block);
		latestBlockID++;

		KINFO("Registered block " + stringID);
	}

	public int GetBlockRawID(AssetStringID stringID) {
		if (blockRawIDs.TryGetValue(stringID, out int rawID)) {
			return rawID;
		}
		KERR("Tried to get raw ID for block with string ID " + stringID + " that didn't exist");
		return -1;
	}

	public Block GetBlock(AssetStringID stringID) {
		return blocks[GetBlockRawID(stringID)];
	}

	public Block GetBlock(int rawID) {
		if (rawID < 0 || rawID >= blocks.Count) {
			KERR("Tried to get block with raw ID {" + rawID + "} that was out of bounds");
			return null;
		}
		return blocks[rawID];
	}

	public void EmptyAssets() {
		OVERRIDE_LOG_NAME("AssetManager");

		int totalAssets = blocks.Count;
		int blocksCount = blocks.Count;
		KINFO("Emptying {" + totalAssets + "} from AssetManager");

		blockRawIDs.Clear();

		blocks.Clear();

		KINFO(" - " + blocksCount + " blocks cleared");
	}
}

public class BlockAir : Block {}