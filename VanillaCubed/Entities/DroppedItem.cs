namespace VanillaCubed.Entities;

using System.Buffers;
using KiwiCubed.Api;
using static KiwiCubed.Api.AssetDefinitions;
using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;

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
		float[] newTextureCoordinates = ArrayPool<float>.Shared.Rent(16);
		float u0 = atlasData.xPosition;
		float u1 = (atlasData.xPosition + atlasData.xSize);
		float v0 = atlasData.yPosition;
		float v1 = (atlasData.yPosition + atlasData.ySize);
		newTextureCoordinates[0] = u0;
		newTextureCoordinates[1] = v1;
		newTextureCoordinates[2] = u1;
		newTextureCoordinates[3] = v1;
		newTextureCoordinates[4] = u1;
		newTextureCoordinates[5] = v0;
		newTextureCoordinates[6] = u0;
		newTextureCoordinates[7] = v0;
		newTextureCoordinates[8] = u0;
		newTextureCoordinates[9] = v1;
		newTextureCoordinates[10] = u1;
		newTextureCoordinates[11] = v1;
		newTextureCoordinates[12] = u1;
		newTextureCoordinates[13] = v0;
		newTextureCoordinates[14] = u0;
		newTextureCoordinates[15] = v0;
		renderableComponent.mesh.UpdateTextureCooordinates(newTextureCoordinates);
        renderableComponent.renderBuffersDirty = true;
        ArrayPool<float>.Shared.Return(newTextureCoordinates);
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