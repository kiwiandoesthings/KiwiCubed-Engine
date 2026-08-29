namespace VanillaCubed.UI;

using KiwiCubed.Api;
using System.Drawing;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.IInventory;

public class UIInventorySlot : UIElement {
    private static Vector2 slotSize = new Vector2(16) * 4;
    public Vector2 containerPosition;
    private ItemStack storedStack;
    private MetaTexture itemTexture;
    private string itemName;

    public UIInventorySlot(ItemStack stack, Vector2 containerPosition) : base(slotSize) {
        storedStack = stack;
        this.containerPosition = containerPosition;

        ResetItemRenderData();
    }

    public void Render(IUI ui) {
        string itemCountString = storedStack.itemCount.ToString();
        UIImage.Render(ui, position, size, itemTexture, 0);
        if (storedStack.itemCount != 0) {
            Vector2 textPosition = position + slotSize;
            textPosition.X -= Renderer.MeasureText(itemCountString).X;
            Renderer.RenderText(itemCountString, textPosition, new Vector2(1), Color.Black);
        }
    }

    public ItemStack GetStack() {
        return storedStack;
    }

    public void SetStack(ItemStack newStack) {
        storedStack = newStack;
        ResetItemRenderData();
    }

    private void ResetItemRenderData() {
        ItemDefinition itemDefinition = Meta.Get<IAssetManager>().GetItem(storedStack.itemStringID);
        itemTexture = new MetaTexture([itemDefinition.atlasData]);
        itemName = itemDefinition.name;
    }
}
