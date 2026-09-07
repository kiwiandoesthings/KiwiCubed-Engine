namespace VanillaCubed.UI;

using KiwiCubed.Api;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.IInventory;

public class UIInventory : UIElement {
    private readonly InventoryMenu menu;
    private readonly UIInventorySlot heldSlot;
    private readonly MetaTexture inventoryTexture;

    public UIInventory(Vector2 size, InventoryMenu menu) : base(size) {
        this.menu = menu;
        heldSlot = new UIInventorySlot(new ItemStack(), Vector2.Zero);
        inventoryTexture = new MetaTexture([Meta.Get<IAssetManager>().GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/inventory_27"))]);
    }

    public override void Render() {
        IUI ui = parentScreen.GetUI();

        UIImage.Render(ui, position, size, inventoryTexture, 0);

        foreach (UIInventorySlot slot in children.Cast<UIInventorySlot>()) {
            slot.Render(ui);
        }
    }

    public override void ArrangeChildren() {
        foreach (UIInventorySlot slot in children) {
            slot.position = position + slot.containerPosition;
        }
    }

    public override void OnClickDown() {
        for (ushort iterator = 0; iterator < children.Count; iterator++) {
            if (!children[iterator].GetHovered()) {
                continue;
            }

            UIInventorySlot slot = (UIInventorySlot)children[iterator];
            if (!slot.GetHovered()) {
                return;
            }
            ItemStack slotStack = slot.GetStack();
            if (slotStack.itemCount == 0) {
                slot.SetStack(heldSlot.GetStack());
                heldSlot.SetStack(new ItemStack());
                menu.PutDownItem(iterator);
            } else {
                heldSlot.SetStack(slotStack);
                slot.SetStack(new ItemStack());
                menu.PickUpItem(iterator);
            }

            break;
        }
    }

    public void SetSlot(ushort slotID, ItemStack newItem) {
        ((UIInventorySlot)children[slotID]).SetStack(newItem);
    }

    public InventoryMenu GetInventoryMenu() {
        return menu;
    }
}
