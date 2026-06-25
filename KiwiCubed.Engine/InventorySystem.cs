namespace KiwiCubed.Engine;

using KiwiCubed.Api;

using static KiwiCubed.Api.IInventory;
using static KiwiCubed.Api.KLogger;

public class InventorySystem : IInventory {
	private InventorySlot[] slots;

	public InventorySystem(ushort slotCount) {
		slots = new InventorySlot[slotCount];

		for (int iterator = 0; iterator < slotCount; iterator++) {
			slots[iterator] = new InventorySlot(AssetManager.airStringID, 0);
		}
	}

	public InventorySlot? AddItem(InventorySlot newItemSlot, ushort startingIndex = 0) {
		for (ushort iterator = startingIndex; iterator < slots.Length; iterator++) {
			ref InventorySlot slot = ref slots[iterator];
			if (slot.itemStringID != newItemSlot.itemStringID && slot.HasItem()) {
				continue;
			}
			int difference = 64 - slot.itemCount - newItemSlot.itemCount;
			if (difference < 0) {
				return AddItem(new InventorySlot(newItemSlot.itemStringID, (byte)-difference), (ushort)(iterator + 1));
			} else {
				return AddItemToSlot(newItemSlot, iterator);
			}
		}

		return null;
	}

	public InventorySlot? AddItemToSlot(InventorySlot newItemSlot, ushort slotIndex) {
		OVERRIDE_LOG_NAME("Inventory");

		if (slotIndex < 0 || slotIndex >= slots.Length) {
			KERR("Tried to add an item to a slot at index {" + slotIndex + "} that didn't exsit");
			return null;
		}
		ref InventorySlot slot = ref slots[slotIndex];
		if (!slot.HasItem()) {
			slot.itemStringID = newItemSlot.itemStringID;
		}
		if (slot.itemStringID != newItemSlot.itemStringID) {
			KERR("Tried to add an item to a slot at index {" + slotIndex + "} when the old and new slot had different items");
			KERR("Old slot: " + slot);
			KERR("New slot: " + newItemSlot);
			return null;
		}
		int difference = 64 - slot.itemCount - newItemSlot.itemCount;
		slot.itemCount += newItemSlot.itemCount;
		slot.itemStringID = newItemSlot.itemStringID;
		if (difference < 0) {
			slot.itemCount = 64;
			return new InventorySlot(slot.itemStringID, (byte)-difference);
		}

		return new InventorySlot();
	}

	public void SetSlot(InventorySlot newItemSlot, ushort slotIndex) {
		OVERRIDE_LOG_NAME("Inventory");

		if (slotIndex >= slots.Length) {
			KERR("Tried to set a slot at index {" + slotIndex + "} that didn't exist");
			return;
		}

		slots[slotIndex] = newItemSlot;
	}

	public InventorySlot? GetSlot(ushort slotIndex) {
		OVERRIDE_LOG_NAME("Inventory");

		if (slotIndex >= slots.Length) {
			KERR("Tried to get a slot at index {" + slotIndex + "} that didn't exist");
			return null;
		}

		return slots[slotIndex];
	}

	public void ClearInventory() {
		for (int iterator = 0; iterator < slots.Length; iterator++) {
			slots[iterator].itemStringID = MetaHandler.Get<IAssetManager>().airStringID;
			slots[iterator].itemCount = 0;
		}
	}

	public InventorySlot[] GetAllSlots() {
		return slots;
	}

	public InventorySlot[] GetNonEmptySlots() {
		List<InventorySlot> nonEmptySlots = new();
		foreach (InventorySlot slot in slots) {
			if (!slot.HasItem()) {
				continue;
			}
            nonEmptySlots.Add(slot);
		}

		return nonEmptySlots.ToArray();
	}
}