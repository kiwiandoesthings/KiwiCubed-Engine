namespace KiwiCubed.Engine;

using ArchWorld = Arch.Core.World;
using ArchEntity = Arch.Core.Entity;
using Arch.Core;
using KiwiCubed.Api;

using static KiwiCubed.Api.AssetDefinitions;

public class AssetManager : IAssetManager {
	public static AssetStringID airStringID;
	public static BlockDefinition airBlock;
	public static BiomeModel voidBiome;

	AssetStringID IAssetManager.airStringID => airStringID;
    BlockDefinition IAssetManager.airBlock => airBlock;
    BiomeModel IAssetManager.voidBiome => voidBiome;

	private KLogger logger;

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
		logger = new KLogger("AssetManager");

        assetWorld = ArchWorld.Create();
		blockDefinitionRawIDs = [];
		blockDefinitions = [];
		itemDefinitions = [];
		entityTypes = [];
		biomes = [];
		textureAtlases = [];
		meshes = [];
		atlasDatas = [];
		shaders = [];

		MetaHandler.Register<IAssetManager>(this);

		logger.INFO("Setting up basic/default assets...");
		airStringID = new AssetStringID("kiwicubed", "air");

		airBlock = new BlockDefinition(airStringID, CreateAssetDefinitionEntity(new ComponentType[] { }));
		ushort airBlockID = RegisterBlockDefinition(airBlock);

		voidBiome = new BiomeModel(0.0f, 0.0f, -192.0f, airBlockID, airBlockID, airBlockID);
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
		if (blockDefinitionRawIDs.ContainsKey(blockDefinition.stringID)) {
			logger.ERR("Tried to register multiple blocks with same string ID " + blockDefinition.stringID);
			logger.BREAK();
			return 0;
		}

		blockDefinitionRawIDs.Add(blockDefinition.stringID, latestBlockDefinitionID);
		blockDefinitions.Add(blockDefinition);
		latestBlockDefinitionID++;

		logger.INFO("Registered block with string ID " + blockDefinition.stringID);

		return (ushort)(latestBlockDefinitionID - 1);
	}

	public ushort GetBlockDefinitionRawID(AssetStringID stringID) {
		if (blockDefinitionRawIDs.TryGetValue(stringID, out ushort rawID)) {
			return rawID;
		}
		logger.ERR("Tried to get raw ID for block with string ID " + stringID + " that didn't exist");
		logger.BREAK();

		return 0;
	}

	public BlockDefinition GetBlockDefinition(AssetStringID stringID) {
		return blockDefinitions[GetBlockDefinitionRawID(stringID)];
	}

	public BlockDefinition GetBlockDefinition(ushort rawID) {
		return blockDefinitions[rawID];
	}

	public bool IsValidBlockDefinition(AssetStringID blockStringID) {
		return blockDefinitionRawIDs.ContainsKey(blockStringID);
	}

	public void RegisterItem(AssetStringID stringID, ItemDefinition itemDefinition) {
		if (itemDefinitions.ContainsKey(stringID)) {
			logger.ERR("Tried to register multiple items with same string ID " + stringID);
			logger.BREAK();
			return;
		}

		itemDefinitions.Add(stringID, itemDefinition);

		logger.INFO("Registered item with string ID " + stringID);
	}

	public ItemDefinition GetItem(AssetStringID stringID) {
		if (itemDefinitions.TryGetValue(stringID, out ItemDefinition item)) {
			return item;
		}
		logger.ERR("Tried to get item with string ID " + stringID + " that didn't exist");
		logger.BREAK();

		return default;
	}

    public bool IsValidItem(AssetStringID itemStringID) {
		return itemDefinitions.ContainsKey(itemStringID);
    }

    public void RegisterEntityType(AssetStringID stringID, EntityType entityType) {
		if (entityTypes.ContainsKey(stringID)) {
			logger.ERR("Tried to register multiple entity types with same string ID " + stringID);
			logger.BREAK();
			return;
		}

		entityTypes.Add(stringID, entityType);

		logger.INFO("Registered entity type with string ID " + stringID);
	}

	public EntityType GetEntityType(AssetStringID stringID) {
		if (entityTypes.TryGetValue(stringID, out EntityType entityType)) {
			return entityType;
		}
		logger.ERR("Tried to get entity type with string ID " + stringID + " that didn't exist");
		logger.BREAK();

		return null;
	}

    public bool IsValidEntityType(AssetStringID entityTypeStringID) {
        return entityTypes.ContainsKey(entityTypeStringID);
    }

    public void RegisterBiomeModel(AssetStringID stringID, BiomeModel biome) {
		if (biomes.ContainsKey(stringID)) {
			logger.ERR("Tried to register multiple biomes with same string ID " + stringID);
			logger.BREAK();
			return;
		}

		biomes.Add(stringID, biome);

		logger.INFO("Registered biome with string ID " + stringID);
	}

	public BiomeModel GetBiomeModel(AssetStringID stringID) {
		if (biomes.TryGetValue(stringID, out BiomeModel biome)) {
			return biome;
		}
		logger.ERR("Tried to get biome with string ID " + stringID + " that didn't exist");
		logger.BREAK();

		return default;
	}

	public List<BiomeModel> GetAllBiomeModels() {
		List<BiomeModel> allBiomes = new List<BiomeModel>();
		foreach (KeyValuePair<AssetStringID, BiomeModel> biome in biomes) {
			allBiomes.Add(biome.Value);
		}
		return allBiomes;
	}

    public bool IsValidBiomeModel(AssetStringID biomeStringID) {
        return biomes.ContainsKey(biomeStringID);
    }

    public void RegisterTextureAtlas(AssetStringID stringID, Texture texture) {
		if (textureAtlases.ContainsKey(stringID)) {
			logger.ERR("Tried to register multiple texture atlases with same string ID " + stringID);
			logger.BREAK();
			return;
		}

		textureAtlases.Add(stringID, texture);

		logger.INFO("Registered texture atlas with string ID " + stringID);
	}

	public Texture GetTextureAtlas(AssetStringID stringID) {
		if (textureAtlases.TryGetValue(stringID, out Texture texture)) {
			return texture;
		}
		logger.ERR("Tried to get texture atlas with string ID " + stringID + " that didn't exist");
		logger.BREAK();

		return null;
	}

    public bool IsValidTextureAtlas(AssetStringID atlasStringID) {
        return textureAtlases.ContainsKey(atlasStringID);
    }

    public void RegisterTextureAtlasData(AssetStringID stringID, TextureAtlasData atlasData) {
		if (atlasDatas.ContainsKey(stringID)) {
			logger.ERR("Tried to register multiple TextureAtlasData with same string ID " + stringID);
			logger.BREAK();
			return;
		}

		atlasDatas.Add(stringID, atlasData);

		logger.INFO("Registered TextureAtlasData with string ID " + stringID);
	}

	public TextureAtlasData GetTextureAtlasData(AssetStringID stringID) {
		if (atlasDatas.TryGetValue(stringID, out TextureAtlasData atlasData)) {
			return atlasData;
		}
		logger.ERR("Tried to get TextureAtlasData with string ID " + stringID + " that didn't exist");
		logger.BREAK();

		return new TextureAtlasData();
	}

    public bool IsValidTextureAtlasData(AssetStringID textureAtlasDataStringID) {
        return atlasDatas.ContainsKey(textureAtlasDataStringID);
    }

    public void RegisterMesh(AssetStringID stringID, GeneralMesh mesh) {
		if (meshes.ContainsKey(stringID)) {
			logger.ERR("Tried to register multiple GeneralMesh with same string ID " + stringID);
			logger.BREAK();
			return;
		}

		meshes.Add(stringID, mesh);

		logger.INFO("Registered GeneralMesh with string ID " + stringID + " with {" + (mesh.vertices.Length / 5) + "} vertices and {" + mesh.indices.Length + "} indices");
	}

	public GeneralMesh GetMesh(AssetStringID stringID) {
		if (meshes.TryGetValue(stringID, out GeneralMesh mesh)) {
			return mesh;
		}
		logger.ERR("Tried to get GeneralMesh with string ID " + stringID + " that didn't exist");
		logger.BREAK();

		return new GeneralMesh();
	}

    public bool IsValidMesh(AssetStringID meshStringID) {
		return meshes.ContainsKey(meshStringID);
    }

    public void RegisterShader(AssetStringID stringID, IShader shader) {
		if (shaders.ContainsKey(stringID)) {
			logger.ERR("Tried to register multiple shaders with same string ID " + stringID);
			logger.BREAK();
			return;
		}

		shaders.Add(stringID, shader);

		logger.INFO("Registered shader with string ID " + stringID);
	}

	public IShader GetShader(AssetStringID stringID) {
		if (shaders.TryGetValue(stringID, out IShader shader)) {
			return shader;
		}
		logger.ERR("Tried to get shader with string ID " + stringID + " that didn't exist");
		logger.BREAK();

		return null;
	}

    public bool IsValidShader(AssetStringID shaderStringID) {
        return shaders.ContainsKey(shaderStringID);
    }

    public void ClearAssets() {
		int totalAssets = blockDefinitions.Count + itemDefinitions.Count + entityTypes.Count + biomes.Count + textureAtlases.Count + atlasDatas.Count + meshes.Count + shaders.Count;
		logger.INFO("Clearing {" + totalAssets + "}");
		
		logger.INFO(" - " + blockDefinitions.Count + " blocks");
		logger.INFO(" - " + itemDefinitions.Count + " items");
		logger.INFO(" - " + entityTypes.Count + " entity types");
		logger.INFO(" - " + biomes.Count + " biomes");
		logger.INFO(" - " + atlasDatas.Count + " TextureAtlasDatas");
		logger.INFO(" - " + meshes.Count + " meshes");
		logger.INFO(" - " + shaders.Count + " shaders");

		assetWorld.Clear();
        blockDefinitionRawIDs.Clear();
		blockDefinitions.Clear();
		latestBlockDefinitionID = 0;
		entityTypes.Clear();
		biomes.Clear();
		textureAtlases.Clear();
		atlasDatas.Clear();
		meshes.Clear();
		shaders.Clear();

		logger.INFO("Finished clearing assets");
	}
}