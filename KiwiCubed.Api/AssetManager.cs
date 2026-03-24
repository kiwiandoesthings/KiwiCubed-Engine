namespace KiwiCubed.Api;

using static KiwiCubed.Api.AssetDefinitions;

public interface IAssetManager {
	public void RegisterBlock(Block block);
	public int GetBlockRawID(AssetStringID stringID);
	public Block GetBlock(AssetStringID stringID);
	public Block GetBlock(int rawID);
	public void RegisterItem(AssetStringID stringID, IItem item);
	public IItem GetItem(AssetStringID stringID);
	public void RegisterEntityType(AssetStringID stringID, Type entityType);
	public Type GetEntityType(AssetStringID stringID);
	public void RegisterBiomeModel(AssetStringID stringID, BiomeModel biomeModel);
	public BiomeModel GetBiomeModel(AssetStringID stringID);
	public void RegisterTextureAtlasData(AssetStringID stringID, TextureAtlasData atlasData);
	public TextureAtlasData GetTextureAtlasData(AssetStringID stringID);
	public void RegisterShader(AssetStringID stringID, IShader shader);
	public IShader GetShader(AssetStringID stringID);
	public void EmptyAssets();
}