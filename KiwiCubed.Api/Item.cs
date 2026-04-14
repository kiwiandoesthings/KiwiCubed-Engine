namespace KiwiCubed.Api;

using static KiwiCubed.Api.AssetDefinitions;

public class Item {
	private static IAssetManager assetManager;
	private bool isBlock;
	private AssetStringID referencedObjectStringID;

	public static void SetupItems() {
		assetManager = Meta.Get<IAssetManager>();
	}

	public GeneralMesh GetMesh() {
		if (isBlock) {
			BlockDefinition blockDefinition = assetManager.GetBlockDefinition(referencedObjectStringID);
			if (assetManager.GetArchWorld().TryGet<BlockRenderableComponent>(blockDefinition.definition, out BlockRenderableComponent renderableComponent)) {
				return renderableComponent.GetBlockMesh();
			}
		}
	}

	public MetaTexture GetTexture() {
	}

	public int GetStackSize() {
	}
}