namespace VanillaCubed.Entities;

using ArchWorld = Arch.Core.World;
using ArchEntity = Arch.Core.Entity;
using KiwiCubed.Api;

using static KiwiCubed.Api.AssetDefinitions;

public class DroppedItemEntity {
	public readonly static AssetStringID itemStringID = new AssetStringID("kiwicubed", "dropped_item");
	private static GeneralMesh droppedItemMesh;

	public static void SetupEntityVisuals() {
		droppedItemMesh = Meta.Get<IAssetManager>().GetMesh(itemStringID.Prefix("model"));
	}

	public static void SetItemTexture(ArchWorld archWorld, ArchEntity archEntity, AssetStringID blockStringID) {
		EntityRenderableComponent renderableComponent = archWorld.Get<EntityRenderableComponent>(archEntity);
		IAssetManager assetManager = Meta.Get<IAssetManager>();
		BlockDefinition block = assetManager.GetBlockDefinition(blockStringID);
		if (!assetManager.GetArchWorld().TryGet<BlockRenderableComponent>(block.definition, out BlockRenderableComponent blockRenderableComponent)) {
			return;
		}
		TextureAtlasData atlasData = blockRenderableComponent.metaTexture.atlasDatas[0];
		List<float> newTextureCoordinates = new();
		float u0 = atlasData.xPosition;
		float u1 = (atlasData.xPosition + atlasData.xSize);
		float v0 = atlasData.yPosition;
		float v1 = (atlasData.yPosition + atlasData.ySize);
		newTextureCoordinates.Add(u0);
		newTextureCoordinates.Add(v1);
		newTextureCoordinates.Add(u1);
		newTextureCoordinates.Add(v1);
		newTextureCoordinates.Add(u1);
		newTextureCoordinates.Add(v0);
		newTextureCoordinates.Add(u0);
		newTextureCoordinates.Add(v0);
		newTextureCoordinates.Add(u0);
		newTextureCoordinates.Add(v1);
		newTextureCoordinates.Add(u1);
		newTextureCoordinates.Add(v1);
		newTextureCoordinates.Add(u1);
		newTextureCoordinates.Add(v0);
		newTextureCoordinates.Add(u0);
		newTextureCoordinates.Add(v0);
		renderableComponent.mesh.UpdateTextureCooordinates(newTextureCoordinates);
		Renderer.UpdateBuffers(renderableComponent.renderBuffers, renderableComponent.mesh);
	}

	public static void ItemEntitySetupServer(ArchWorld archWorld, ArchEntity archEntity) {
		archWorld.Set<EntityPhysicalComponent>(archEntity, new EntityPhysicalComponent());
		archWorld.Set<EntityDroppedItemComponent>(archEntity, new EntityDroppedItemComponent());
	}

	public static void ItemEntitySetupClient(ArchWorld archWorld, ArchEntity archEntity) {
		ItemEntitySetupServer(archWorld, archEntity);
		archWorld.Set<EntityRenderableComponent>(archEntity, new EntityRenderableComponent(true, droppedItemMesh));
	}

	public struct EntityDroppedItemComponent { }
}