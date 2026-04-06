namespace KiwiCubed.Api;

using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;
using Arch.Core;

using static KiwiCubed.Api.AssetDefinitions;

public interface IAssetManager {
	public ArchWorld GetArchWorld();
	public ArchEntity CreateBlockDefinition(ComponentType[] components);
	public ushort RegisterBlockDefinition(AssetStringID stringID, ArchEntity blockDefinition);
	public ArchEntity GetBlockDefinition(AssetStringID stringID);
	public ushort GetBlockDefinitionRawID(AssetStringID stringID);

	public ushort RegisterBlock(Block block);
	public ushort GetBlockRawID(AssetStringID stringID);
	public Block GetBlock(AssetStringID stringID);
	public Block GetBlock(ushort rawID);
	public void RegisterItem(AssetStringID stringID, IItem item);
	public IItem GetItem(AssetStringID stringID);
	public void RegisterEntityType(AssetStringID stringID, EntityType entityType);
	public EntityType GetEntityType(AssetStringID stringID);
	public void RegisterBiomeModel(AssetStringID stringID, BiomeModel biomeModel);
	public BiomeModel GetBiomeModel(AssetStringID stringID);
	public void RegisterTextureAtlasData(AssetStringID stringID, TextureAtlasData atlasData);
	public TextureAtlasData GetTextureAtlasData(AssetStringID stringID);
	public void RegisterMesh(AssetStringID stringID, GeneralMesh mesh);
	public GeneralMesh GetMesh(AssetStringID stringID);
	public void RegisterShader(AssetStringID stringID, IShader shader);
	public IShader GetShader(AssetStringID stringID);
	public void EmptyAssets();
}