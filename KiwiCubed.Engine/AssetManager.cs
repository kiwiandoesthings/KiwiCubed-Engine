namespace KiwiCubed.Engine;

using KiwiCubed.Api;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.Util;

public class AssetManager : IAssetManager {
	// Blocks
	private Dictionary<AssetStringID, int> blockRawIDs;
	private List<Block> blocks;
	private int latestBlockID = 0;
	// Items
	private Dictionary<AssetStringID, Item> items;
	// Entities
	private Dictionary<AssetStringID, Type> entityTypes;
	// Texture Atlases
	private Dictionary<AssetStringID, Texture> textureAtlases;
	// TextureAtlasDatas
	private Dictionary<AssetStringID, TextureAtlasData> atlasDatas;
	// Shaders
	private Dictionary<AssetStringID, IShader> shaders;

	public AssetManager() {
		blockRawIDs = new();
		blocks = new();
		items = new();
		entityTypes = new();
		textureAtlases = new();
		atlasDatas = new();
		shaders = new();

		SystemsManager.Register<IAssetManager>(this);

		RegisterBlock(new BlockAir());
	}

	public void RegisterBlock(Block block) {
		OVERRIDE_LOG_NAME("Asset Manager");

		AssetStringID stringID = block.GetStringID();

		if (blockRawIDs.ContainsKey(stringID)) {
			KERR("Tried to register multiple blocks with same string ID " + stringID);
			return;
		}

		blockRawIDs.Add(stringID, latestBlockID);
		blocks.Add(block);
		latestBlockID++;

		KINFO("Registered block with string ID " + stringID);

		AssetStringID itemStringID = new AssetStringID(stringID.modName, "item/" + stringID.assetName + "_block");
		RegisterItem(itemStringID, (IItem)new Item(block.GetMetaTexture(), 64));
	}

	public int GetBlockRawID(AssetStringID stringID) {
		OVERRIDE_LOG_NAME("Asse	tManager");

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
		OVERRIDE_LOG_NAME("Asset Manager");

		if (rawID < 0 || rawID >= blocks.Count) {
			KERR("Tried to get block with raw ID {" + rawID + "} that was out of bounds");
			return blocks[0];
		}
		return blocks[rawID];
	}

	public void RegisterItem(AssetStringID stringID, IItem item) {
		OVERRIDE_LOG_NAME("Asset Manager");

		if (items.ContainsKey(stringID)) {
			KERR("Tried to register multiple items with same string ID " + stringID);
			return;
		}

		items.Add(stringID, (Item)item);

		KINFO("Registered item with string ID " + stringID);
	}

	public IItem GetItem(AssetStringID stringID) {
		OVERRIDE_LOG_NAME("Asset Manager");

		if (items.TryGetValue(stringID, out Item item)) {
			return (IItem)item;
		}
		KERR("Tried to get item with string ID " + stringID + " that didn't exist");
		return null;
	}

	public void RegisterEntityType(AssetStringID stringID, Type entityType) {
		OVERRIDE_LOG_NAME("Asset Manager");

		if (entityTypes.ContainsKey(stringID)) {
			KERR("Tried to register multiple entity types with same string ID " + stringID);
			return;
		}

		entityTypes.Add(stringID, entityType);

		KINFO("Registered entity type with string ID " + stringID);
	}

	public Type GetEntityType(AssetStringID stringID) {
		OVERRIDE_LOG_NAME("Asset Manager");

		if (entityTypes.TryGetValue(stringID, out Type entityType)) {
			return entityType;
		}
		KERR("Tried to get entity type with string ID " + stringID + " that didn't exist");
		return null;
	}

	public void RegisterTextureAtlas(AssetStringID stringID, Texture texture) {
		OVERRIDE_LOG_NAME("Asset Manager");

		if (textureAtlases.ContainsKey(stringID)) {
			KERR("Tried to register multiple texture atlases with same string ID " + stringID);
			return;
		}

		textureAtlases.Add(stringID, texture);
		
		KINFO("Registered texture atlas with string ID " + stringID);
	}

	public Texture GetTextureAtlas(AssetStringID stringID) {
		OVERRIDE_LOG_NAME("Asset Manager");

		if (textureAtlases.TryGetValue(stringID, out Texture texture)) {
			return texture;
		}
		KERR("Tried to get texture atlas with string ID " + stringID + " that didn't exist");
		return null;
	}

	public void RegisterTextureAtlasData(AssetStringID stringID, TextureAtlasData atlasData) {
		OVERRIDE_LOG_NAME("Asset Manager");

		if (shaders.ContainsKey(stringID)) {
			KERR("Tried to register multiple TextureAtlasDatas with same string ID " + stringID);
			return;
		}

		atlasDatas.Add(stringID, atlasData);

		KINFO("Registered TextureAtlasData with string ID " + stringID);
	}

	public TextureAtlasData GetTextureAtlasData(AssetStringID stringID) {
		OVERRIDE_LOG_NAME("Asset Manager");

		if (atlasDatas.TryGetValue(stringID, out TextureAtlasData atlasData)) {
			return atlasData;
		}
		KERR("Tried to get TextureAtlasData with string ID " + stringID + " that didn't exist");
		return new TextureAtlasData();
	}

	public void RegisterShader(AssetStringID stringID, IShader shader) {
		OVERRIDE_LOG_NAME("Asset Manager");

		if (shaders.ContainsKey(stringID)) {
			KERR("Tried to register multiple shaders with same string ID " + stringID);
			return;
		}

		shaders.Add(stringID, shader);

		KINFO("Registered shader with string ID " + stringID);
	}

	public IShader GetShader(AssetStringID stringID) {
		OVERRIDE_LOG_NAME("Asset Manager");

		if (shaders.TryGetValue(stringID, out IShader shader)) {
			return shader;
		}
		KERR("Tried to get shader with string ID " + stringID + " that didn't exist");
		return null;
	}

	public void EmptyAssets() {
		OVERRIDE_LOG_NAME("Asset Manager");

		int totalAssets = blocks.Count;
		int blocksCount = blocks.Count;
		KINFO("Emptying {" + totalAssets + "} from AssetManager");

		blockRawIDs.Clear();
		blocks.Clear();

		KINFO(" - " + blocksCount + " blocks cleared");
	}
}

public class BlockAir : Block {
	public BlockAir() {
		stringID = new AssetStringID("kiwicubed", "air");
	}

	public override GeneralMesh GetMesh(Span<bool> neighborsMask, FullBlockPosition fullPosition, List<float> vertices, List<ushort> indices) {
		return new GeneralMesh();
	}

	public override bool IsAir() {
		return true;
	}
}