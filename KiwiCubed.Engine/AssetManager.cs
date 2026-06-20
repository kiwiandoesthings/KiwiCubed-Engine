namespace KiwiCubed.Engine;

using ArchWorld = Arch.Core.World;
using ArchEntity = Arch.Core.Entity;
using Arch.Core;
using KiwiCubed.Api;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.Utils;

public class AssetManager : IAssetManager {
	public static BlockDefinition airBlock;
	public static BiomeModel voidBiome;

	// Blocks
	private ArchWorld assetWorld;
	private Dictionary<AssetStringID, ushort> blockDefinitionRawIDs;
	private List<BlockDefinition> blockDefinitions;
	private ushort latestBlockDefinitionID = 0;
	// Items
	private Dictionary<AssetStringID, ItemDefinition> itemDefinitions;
	// Entities
	private Dictionary<AssetStringID, EntityType> entityTypes;
	// Biomes
	private Dictionary<AssetStringID, BiomeModel> biomes;
	// Texture Atlases
	private Dictionary<AssetStringID, Texture> textureAtlases;
	// TextureAtlasDatas
	private Dictionary<AssetStringID, TextureAtlasData> atlasDatas;
	// Meshes
	private Dictionary<AssetStringID, GeneralMesh> meshes;
	// Shaders
	private Dictionary<AssetStringID, IShader> shaders;

	public AssetManager() {
		OVERRIDE_LOG_NAME("AssetManager");

		assetWorld = ArchWorld.Create();
		blockDefinitionRawIDs = new();
		blockDefinitions = new();
		itemDefinitions = new();
		entityTypes = new();
		biomes = new();
		textureAtlases = new();
		meshes = new();
		atlasDatas = new();
		shaders = new();

		MetaHandler.Register<IAssetManager>(this);

		KINFO("Setting up basic/default assets...");
		airBlock = new BlockDefinition(new AssetStringID("kiwicubed", "air"), CreateAssetDefinitionEntity(new ComponentType[] { }));
		ushort airBlockID = RegisterBlockDefinition(airBlock);
		voidBiome = new BiomeModel(0.0f, 0.0f, -8192.0f, airBlockID, airBlockID, airBlockID);

		AssetStringID voidStringID = new AssetStringID("kiwicubed", "void");
		RegisterBiomeModel(voidStringID, voidBiome);
	}

	public ArchWorld GetArchWorld() {
		return assetWorld;
	}

	public ArchEntity CreateAssetDefinitionEntity(ComponentType[] components) {
		return assetWorld.Create(components);
	}

	public ushort RegisterBlockDefinition(BlockDefinition blockDefinition) {
		OVERRIDE_LOG_NAME("AssetManager");

		if (blockDefinitionRawIDs.ContainsKey(blockDefinition.stringID)) {
			KERR("Tried to register multiple blocks with same string ID " + blockDefinition.stringID);
			KBREAK();
			return 0;
		}

		blockDefinitionRawIDs.Add(blockDefinition.stringID, latestBlockDefinitionID);
		blockDefinitions.Add(blockDefinition);
		latestBlockDefinitionID++;

		KINFO("Registered block with string ID " + blockDefinition.stringID);

		return (ushort)(latestBlockDefinitionID - 1);
	}

	public ushort GetBlockDefinitionRawID(AssetStringID stringID) {
		OVERRIDE_LOG_NAME("AssetManager");

		if (blockDefinitionRawIDs.TryGetValue(stringID, out ushort rawID)) {
			return rawID;
		}
		KERR("Tried to get raw ID for block with string ID " + stringID + " that didn't exist");
		KBREAK();

		return 0;
	}

	public BlockDefinition GetBlockDefinition(AssetStringID stringID) {
		return blockDefinitions[GetBlockDefinitionRawID(stringID)];
	}

	public BlockDefinition GetBlockDefinition(ushort rawID) {
		return blockDefinitions[rawID];
	}

	public void RegisterItem(AssetStringID stringID, ItemDefinition itemDefinition) {
		OVERRIDE_LOG_NAME("AssetManager");

		if (itemDefinitions.ContainsKey(stringID)) {
			KERR("Tried to register multiple items with same string ID " + stringID);
			KBREAK();
			return;
		}

		itemDefinitions.Add(stringID, itemDefinition);

		KINFO("Registered item with string ID " + stringID);
	}

	public ItemDefinition GetItem(AssetStringID stringID) {
		OVERRIDE_LOG_NAME("AssetManager");

		if (itemDefinitions.TryGetValue(stringID, out ItemDefinition item)) {
			return item;
		}
		KERR("Tried to get item with string ID " + stringID + " that didn't exist");
		KBREAK();

		return default;
	}

	public void RegisterEntityType(AssetStringID stringID, EntityType entityType) {
		OVERRIDE_LOG_NAME("AssetManager");

		if (entityTypes.ContainsKey(stringID)) {
			KERR("Tried to register multiple entity types with same string ID " + stringID);
			KBREAK();
			return;
		}

		entityTypes.Add(stringID, entityType);

		KINFO("Registered entity type with string ID " + stringID);
	}

	public EntityType GetEntityType(AssetStringID stringID) {
		OVERRIDE_LOG_NAME("AssetManager");

		if (entityTypes.TryGetValue(stringID, out EntityType entityType)) {
			return entityType;
		}
		KERR("Tried to get entity type with string ID " + stringID + " that didn't exist");
		KBREAK();

		return null;
	}

	public void RegisterBiomeModel(AssetStringID stringID, BiomeModel biome) {
		OVERRIDE_LOG_NAME("AssetManager");

		if (biomes.ContainsKey(stringID)) {
			KERR("Tried to register multiple biomes with same string ID " + stringID);
			KBREAK();
			return;
		}

		biomes.Add(stringID, biome);

		KINFO("Registered biome with string ID " + stringID);
	}

	public BiomeModel GetBiomeModel(AssetStringID stringID) {
		OVERRIDE_LOG_NAME("AssetManager");

		if (biomes.TryGetValue(stringID, out BiomeModel biome)) {
			return biome;
		}
		KERR("Tried to get biome with string ID " + stringID + " that didn't exist");
		KBREAK();

		return default;
	}

	public List<BiomeModel> GetAllBiomeModels() {
		List<BiomeModel> allBiomes = new List<BiomeModel>();
		foreach (KeyValuePair<AssetStringID, BiomeModel> biome in biomes) {
			allBiomes.Add(biome.Value);
		}
		return allBiomes;
	}

	public void RegisterTextureAtlas(AssetStringID stringID, Texture texture) {
		OVERRIDE_LOG_NAME("AssetManager");

		if (textureAtlases.ContainsKey(stringID)) {
			KERR("Tried to register multiple texture atlases with same string ID " + stringID);
			KBREAK();
			return;
		}

		textureAtlases.Add(stringID, texture);

		KINFO("Registered texture atlas with string ID " + stringID);
	}

	public Texture GetTextureAtlas(AssetStringID stringID) {
		OVERRIDE_LOG_NAME("AssetManager");

		if (textureAtlases.TryGetValue(stringID, out Texture texture)) {
			return texture;
		}
		KERR("Tried to get texture atlas with string ID " + stringID + " that didn't exist");
		KBREAK();

		return null;
	}

	public void RegisterTextureAtlasData(AssetStringID stringID, TextureAtlasData atlasData) {
		OVERRIDE_LOG_NAME("AssetManager");

		if (atlasDatas.ContainsKey(stringID)) {
			KERR("Tried to register multiple TextureAtlasData with same string ID " + stringID);
			KBREAK();
			return;
		}

		atlasDatas.Add(stringID, atlasData);

		KINFO("Registered TextureAtlasData with string ID " + stringID);
	}

	public TextureAtlasData GetTextureAtlasData(AssetStringID stringID) {
		OVERRIDE_LOG_NAME("AssetManager");

		if (atlasDatas.TryGetValue(stringID, out TextureAtlasData atlasData)) {
			return atlasData;
		}
		KERR("Tried to get TextureAtlasData with string ID " + stringID + " that didn't exist");
		KBREAK();

		return new TextureAtlasData();
	}

	public void RegisterMesh(AssetStringID stringID, GeneralMesh mesh) {
		OVERRIDE_LOG_NAME("AssetManager");

		if (meshes.ContainsKey(stringID)) {
			KERR("Tried to register multiple GeneralMesh with same string ID " + stringID);
			KBREAK();
			return;
		}

		meshes.Add(stringID, mesh);

		KINFO("Registered GeneralMesh with string ID " + stringID + " with {" + (mesh.vertices.Length / 5) + "} vertices and {" + mesh.indices.Length + "} indices");
	}

	public GeneralMesh GetMesh(AssetStringID stringID) {
		OVERRIDE_LOG_NAME("AssetManager");

		if (meshes.TryGetValue(stringID, out GeneralMesh mesh)) {
			return mesh;
		}
		KERR("Tried to get GeneralMesh with string ID " + stringID + " that didn't exist");
		KBREAK();

		return new GeneralMesh();
	}

	public void RegisterShader(AssetStringID stringID, IShader shader) {
		OVERRIDE_LOG_NAME("AssetManager");

		if (shaders.ContainsKey(stringID)) {
			KERR("Tried to register multiple shaders with same string ID " + stringID);
			KBREAK();
			return;
		}

		shaders.Add(stringID, shader);

		KINFO("Registered shader with string ID " + stringID);
	}

	public IShader GetShader(AssetStringID stringID) {
		OVERRIDE_LOG_NAME("AssetManager");

		if (shaders.TryGetValue(stringID, out IShader shader)) {
			return shader;
		}
		KERR("Tried to get shader with string ID " + stringID + " that didn't exist");
		KBREAK();

		return null;
	}

	public void EmptyAssets() {
		OVERRIDE_LOG_NAME("AssetManager");

		//int totalAssets = blocks.Count;
		//int blocksCount = blocks.Count;
		//KINFO("Emptying {" + totalAssets + "} from AssetManager");
		//
		//blockRawIDs.Clear();
		//blocks.Clear();
		//
		//KINFO(" - " + blocksCount + " blocks cleared");
	}
}