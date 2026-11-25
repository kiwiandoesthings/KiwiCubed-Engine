namespace KiwiCubed.Engine;

using ArchWorld = Arch.Core.World;
using ArchEntity = Arch.Core.Entity;
using Arch.Core;
using KiwiCubed.Api;

using static KiwiCubed.Api.AssetDefinitions;
using System.Dynamic;

public class AssetManager : IAssetManager {
	public static AssetStringID airStringID;
	public static BlockDefinition airBlock;
	public static ItemDefinition airItem;
	public static BiomeModel voidBiome;

	AssetStringID IAssetManager.airStringID => airStringID;
    BlockDefinition IAssetManager.airBlock => airBlock;
	ItemDefinition IAssetManager.airItem => airItem;
    BiomeModel IAssetManager.voidBiome => voidBiome;

	private readonly KLogger logger;

    // Block definitions
    private readonly ArchWorld assetWorld;
	private readonly Dictionary<AssetStringID, ushort> blockDefinitionRawIDs;
	private readonly List<BlockDefinition> blockDefinitions;
	private ushort latestBlockDefinitionID = 0;
	// Item definitions
	private readonly Dictionary<AssetStringID, ItemDefinition> itemDefinitions;
	// Entities
	private readonly Dictionary<AssetStringID, EntityType> entityTypes;
	// Biomes
	private readonly Dictionary<AssetStringID, BiomeModel> biomes;
	// Texture atlases
	private readonly Dictionary<AssetStringID, Texture> textureAtlases;
	// TextureAtlasDatas
	private readonly Dictionary<AssetStringID, TextureAtlasData> atlasDatas;
	// Meshes
	private readonly Dictionary<AssetStringID, GeneralMesh<Vertex3D>> meshes;
	// Shaders
	private readonly Dictionary<AssetStringID, IShader> shaders;
	// Inventories
	private readonly Dictionary<AssetStringID, string[]> inventoryDefinitions;

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
		inventoryDefinitions = [];

		MetaHandler.Register<IAssetManager>(this);

		logger.INFO("Setting up basic/default assets...");
		airStringID = new AssetStringID("kiwicubed", "air");

		airBlock = new BlockDefinition(airStringID, CreateAssetDefinitionEntity([]));
		ushort airBlockID = RegisterBlockDefinition(airBlock);

		airItem = new ItemDefinition(airStringID, new TextureAtlasData(), "Air", CreateAssetDefinitionEntity([]));
		RegisterItem(airStringID, airItem);

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
			logger.ERR("Tried to register multiple blocks with same string ID, " + blockDefinition.stringID);
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

	public BlockDefinition[] GetBlockDefinitioons() {
		return [.. blockDefinitions];
	}

	public bool IsValidBlockDefinition(AssetStringID blockStringID) {
		return blockDefinitionRawIDs.ContainsKey(blockStringID);
	}

	public void RegisterItem(AssetStringID stringID, ItemDefinition itemDefinition) {
		RegisterAsset(itemDefinitions, stringID, itemDefinition, "item");
	}

	public ItemDefinition GetItem(AssetStringID stringID) {
		return GetAsset(itemDefinitions, stringID, "item");
	}

    public bool IsValidItem(AssetStringID itemStringID) {
		return IsValidAsset(itemDefinitions, itemStringID);
    }

    public void RegisterEntityType(AssetStringID stringID, EntityType entityType) {
        RegisterAsset(entityTypes, stringID, entityType, "entity type");
    }

    public EntityType GetEntityType(AssetStringID stringID) {
        return GetAsset(entityTypes, stringID, "entity type");
    }

    public bool IsValidEntityType(AssetStringID entityTypeStringID) {
        return IsValidAsset(entityTypes, entityTypeStringID);
    }

    public void RegisterBiomeModel(AssetStringID stringID, BiomeModel biome) {
        RegisterAsset(biomes, stringID, biome, "biome");
    }

    public BiomeModel GetBiomeModel(AssetStringID stringID) {
        return GetAsset(biomes, stringID, "biome");
    }

	public BiomeModel[] GetAllBiomeModels() {
		return [.. biomes.Values];
	}

    public bool IsValidBiomeModel(AssetStringID biomeStringID) {
        return IsValidAsset(biomes, biomeStringID);
    }

    public void RegisterTextureAtlas(AssetStringID stringID, Texture texture) {
        RegisterAsset(textureAtlases, stringID, texture, "texture atlas");
    }

    public Texture GetTextureAtlas(AssetStringID stringID) {
        return GetAsset(textureAtlases, stringID, "texture atlas");
    }

    public bool IsValidTextureAtlas(AssetStringID atlasStringID) {
        return IsValidAsset(textureAtlases, atlasStringID);
    }

    public void RegisterTextureAtlasData(AssetStringID stringID, TextureAtlasData atlasData) {
        RegisterAsset(atlasDatas, stringID, atlasData, "TextureAtlasData");
    }

    public TextureAtlasData GetTextureAtlasData(AssetStringID stringID) {
        return GetAsset(atlasDatas, stringID, "TextureAtlasData", new TextureAtlasData());
    }

    public bool IsValidTextureAtlasData(AssetStringID textureAtlasDataStringID) {
        return IsValidAsset(atlasDatas, textureAtlasDataStringID);
    }

    public void RegisterMesh(AssetStringID stringID, GeneralMesh<Vertex3D> mesh) {
		if (meshes.ContainsKey(stringID)) {
			logger.ERR("Tried to register multiple GeneralMesh with same string ID, " + stringID);
			logger.BREAK();
			return;
		}

		meshes.Add(stringID, mesh);

		logger.INFO("Registered GeneralMesh with string ID " + stringID + " with {" + (mesh.vertices.Length / 5) + "} vertices and {" + mesh.indices.Length + "} indices");
	}

    public GeneralMesh<Vertex3D> GetMesh(AssetStringID stringID) {
        return GetAsset(meshes, stringID, "GeneralMesh", new GeneralMesh<Vertex3D>());
    }

    public bool IsValidMesh(AssetStringID meshStringID) {
        return IsValidAsset(meshes, meshStringID);
    }

    public void RegisterShader(AssetStringID stringID, IShader shader) {
		RegisterAsset(shaders, stringID, shader, "shader");
	}

	public IShader GetShader(AssetStringID stringID) {
		return GetAsset(shaders, stringID, "shader", null);
	}

	public bool IsValidShader(AssetStringID shaderStringID) {
		return IsValidAsset(shaders, shaderStringID);
	}

    public void RegisterInventoryDefinition(AssetStringID stringID, string[] slotNames) {
        RegisterAsset(inventoryDefinitions, stringID, slotNames, "inventory definition");
    }

	public int GetSlotNumericalID(AssetStringID inventoryStringID, string slotName) {
		if (!inventoryDefinitions.TryGetValue(inventoryStringID, out string[] slots)) {
			logger.ERR("Tried to get numerical ID for slot from inventory with string ID " + inventoryStringID + " that didn't exist");
			logger.BREAK();
		}

		for (int iterator = 0; iterator < slots.Length; iterator++) {
			if (slots[iterator] == slotName) {
				return iterator;
			}
		}
		logger.ERR("Tried to get numerical ID for slot with name \"" + slotName + "\" that didn't exist in inventory with string ID " + inventoryStringID);
		logger.BREAK();

		return -1;
	}

	public bool IsValidInventoryDefinition(AssetStringID stringID) {
        return IsValidAsset(inventoryDefinitions, stringID);
    }

    private void RegisterAsset<T>(Dictionary<AssetStringID, T> registry, AssetStringID stringID, T asset, string typeName) {
        if (registry.ContainsKey(stringID)) {
            logger.ERR("Tried to register multiple " + typeName + " with same string ID, " + stringID);
            logger.BREAK();
            return;
        }

        registry.Add(stringID, asset);
        logger.INFO("Registered " + typeName + " with string ID " + stringID);
    }

    private T GetAsset<T>(Dictionary<AssetStringID, T> registry, AssetStringID stringID, string typeName, T fallback = default) {
        if (registry.TryGetValue(stringID, out T asset)) {
            return asset;
        }
        logger.ERR("Tried to get " + typeName + " with string ID " + stringID + " that didn't exist");
        logger.BREAK();
        return fallback;
    }

    private bool IsValidAsset<T>(Dictionary<AssetStringID, T> registry, AssetStringID stringID) {
        return registry.ContainsKey(stringID);
    }

    public void ClearAssets() {
		int totalAssets = blockDefinitions.Count + itemDefinitions.Count + entityTypes.Count + biomes.Count + textureAtlases.Count + atlasDatas.Count + meshes.Count + shaders.Count + inventoryDefinitions.Count;
		logger.INFO("Clearing {" + totalAssets + "}");
		
		logger.INFO(" - " + blockDefinitions.Count + " block definitions");
		logger.INFO(" - " + itemDefinitions.Count + " item definitions");
		logger.INFO(" - " + entityTypes.Count + " entity types");
		logger.INFO(" - " + biomes.Count + " biomes");
		logger.INFO(" - " + atlasDatas.Count + " TextureAtlasDatas");
		logger.INFO(" - " + meshes.Count + " meshes");
		logger.INFO(" - " + shaders.Count + " shaders");
		logger.INFO(" - " + inventoryDefinitions.Count + " inventory definitions");

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
		inventoryDefinitions.Clear();

		logger.INFO("Finished clearing assets");
	}
}