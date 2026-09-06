namespace VanillaCubed.UI;

using KiwiCubed.Api;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;

public class InventoryMenu {
    private readonly UIContainer inventoryContainer;
    private readonly UIInventory inventoryUI;
    private readonly Stack<InventoryAction> pendingActions;
    private readonly AssetStringID cursorSlotID = new AssetStringID("kiwicubed", "slot/player/hand");

    public InventoryMenu(IUI ui, AssetStringID screenID) {
        inventoryContainer = new UIContainer(new Vector2(96, 32) * 8, 0);
        inventoryUI = new UIInventory(new Vector2(96, 32) * 8, this);
        pendingActions = [];

        ui.AddElementToScreen(screenID, inventoryContainer);
        ui.AddElementToElement(inventoryContainer, inventoryUI);
    }

    public void PickUpItem(AssetStringID slotID) {
        pendingActions.Push(new SwapItemsAction(slotID, cursorSlotID));
    }

    public void PutDownItem(AssetStringID slotID) {
        pendingActions.Push(new SwapItemsAction(cursorSlotID, slotID));
    }

    public void SwitchItems(AssetStringID sourceSlotID, AssetStringID targetSlotID) {
        pendingActions.Push(new SwapItemsAction(sourceSlotID, targetSlotID));
    }

    public UIContainer GetInventoryContainer() {
        return inventoryContainer;
    }

    public UIInventory GetInventoryUI() {
        return inventoryUI;
    }
}
