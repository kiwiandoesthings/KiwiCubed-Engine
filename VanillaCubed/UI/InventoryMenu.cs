namespace VanillaCubed.UI;

using KiwiCubed.Api;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.IInventory;

public class InventoryMenu : IDisposable {
    private readonly ILogger logger;
    private readonly UIContainer inventoryContainer;
    private readonly UIInventory inventoryUI;
    private readonly ItemStack[] trueInventory;
    private readonly Queue<InventoryAction> pendingActions;
    private readonly ushort cursorSlotID;
    private readonly Action[] eventUnsubscriptionActions;

    public InventoryMenu(ushort inventorySlotCount) {
        logger = ILogger.CreateLogger("InventoryMenu");
        inventoryContainer = new UIContainer(new Vector2(96, 32) * 8, 0);
        inventoryUI = new UIInventory(new Vector2(96, 32) * 8, this);
        trueInventory = new ItemStack[inventorySlotCount];
        pendingActions = [];
        eventUnsubscriptionActions = new Action[3];
        cursorSlotID = inventorySlotCount;

        inventoryContainer.AddChildElement(inventoryUI);

        IEventManager eventManager = Meta.Get<IEventManager>();
        eventUnsubscriptionActions[0] = eventManager.SubscribeToEvent((ServerSetInventoryEvent eventData) => {
            pendingActions.Clear();
            for (ushort iterator = 0; iterator < trueInventory.Length; iterator++) {
                trueInventory[iterator] = eventData.stacks[iterator];
                inventoryUI.SetSlot(iterator, eventData.stacks[iterator]);
            }
        });

        eventUnsubscriptionActions[1] = eventManager.SubscribeToEvent((ServerChangedInventoryEvent eventData) => {
            foreach (ValueTuple<ushort, ItemStack> newItem in eventData.newItems) {
                trueInventory[newItem.Item1] = newItem.Item2;
                inventoryUI.SetSlot(newItem.Item1, newItem.Item2);
            }
        });

        //eventUnsubscriptionActions[2] = eventManager.SubscribeToEvent((ServerVerifyInventoryEvent eventData) = > {
        //});
    }

    public void PickUpItem(ushort slotID) {
        pendingActions.Enqueue(new SwapItemsAction(slotID, cursorSlotID));
    }

    public void PutDownItem(ushort slotID) {
        pendingActions.Enqueue(new SwapItemsAction(cursorSlotID, slotID));
    }

    public void SwitchItems(ushort sourceSlotID, ushort targetSlotID) {
        pendingActions.Enqueue(new SwapItemsAction(sourceSlotID, targetSlotID));
    }

    public UIContainer GetInventoryContainer() {
        return inventoryContainer;
    }

    public UIInventory GetInventoryUI() {
        return inventoryUI;
    }

    public void Dispose() {
        eventUnsubscriptionActions[0]();
        eventUnsubscriptionActions[1]();

        GC.SuppressFinalize(this);
    }
}
