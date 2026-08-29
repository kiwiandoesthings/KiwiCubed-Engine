namespace VanillaCubed.UI;

using KiwiCubed.Api;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;

public class InventoryMenu {
    private UIContainer inventoryContainer;
    private UIInventory inventoryUI;

    public InventoryMenu(IUI ui, AssetStringID screenID) {
        inventoryContainer = new UIContainer(new Vector2(96, 32) * 8, 0);
        inventoryUI = new UIInventory(new Vector2(96, 32) * 8, this);

        ui.AddElementToScreen(screenID, inventoryContainer);
        ui.AddElementToElement(inventoryContainer, inventoryUI);
    }

    public void PickUpItem() {

    }

    public void PutDownItem() {

    }

    public void SwitchItems() {

    }

    public UIContainer GetInventoryContainer() {
        return inventoryContainer;
    }

    public UIInventory GetInventoryUI() {
        return inventoryUI;
    }
}
