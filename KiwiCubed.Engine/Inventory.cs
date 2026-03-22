namespace KiwiCubed.Engine;

using KiwiCubed.Api;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.IInventory;
using static KiwiCubed.Api.KLogger;

public class Inventory : IInventory {
	private InventorySlot[] slots;
	private Dictionary<AssetStringID, int> stringIDsToSlots;

	public Inventory(List<AssetStringID> slotStringIDs) {
		slots = new InventorySlot[slotStringIDs.Count];
		stringIDsToSlots = new();

		for (int iterator = 0; iterator < slotStringIDs.Count; iterator++) {
			slots[iterator] = new InventorySlot();
			stringIDsToSlots.Add(slotStringIDs[iterator], iterator);
		}
	}

	public InventorySlot? AddItem(InventorySlot newItemSlot, int startingIndex = 0) {
		for (int iterator = startingIndex; iterator < slots.Length; iterator++) {
			ref InventorySlot slot = ref slots[iterator];
			if (slot.itemStringID != newItemSlot.itemStringID && slot.HasItem()) {
				continue;
			}
			int difference = 64 - slot.itemCount - newItemSlot.itemCount;
			if (difference < 0) {
				return AddItem(new InventorySlot(newItemSlot.itemStringID, (byte)-difference), iterator + 1);
			} else {
				return AddItemToSlot(newItemSlot, iterator);
			}
		}
		return null;
	}

	public InventorySlot? AddItemToSlot(InventorySlot newItemSlot, AssetStringID slotStringID) {
		OVERRIDE_LOG_NAME("Inventory");

		if (stringIDsToSlots.TryGetValue(slotStringID, out int slotIndex)) {
			return AddItemToSlot(newItemSlot, slotIndex);
		}
		KERR("Tried to add an item to a slot with string ID " + slotStringID + " that didn't exist");
		return null;
	}

	public InventorySlot? AddItemToSlot(InventorySlot newItemSlot, int slotIndex) {
		OVERRIDE_LOG_NAME("Inventory");

		if (slotIndex < 0 || slotIndex >= slots.Length) {
			KERR("Tried to add an item to a slot via index with out of range index {" + slotIndex + "}");
		}
		InventorySlot slot = slots[slotIndex];
		if (!slot.HasItem()) {
			slot.itemStringID = newItemSlot.itemStringID;
		}
		if (slot.itemStringID != newItemSlot.itemStringID) {
			KERR("Tried to add an item to a slot via slot index at index {" + slotIndex + "} when the old and new slot had different items");
			KERR("Old slot: " + slot);
			KERR("New slot: " + newItemSlot);
		}
		int difference = 64 - slot.itemCount - newItemSlot.itemCount;
		slot.itemCount += newItemSlot.itemCount;
		slot.itemStringID = newItemSlot.itemStringID;
		if (difference < 0) {
			slot.itemCount = 64;
			return new InventorySlot(slot.itemStringID, (byte)-difference);
		}
		slots[slotIndex] = slot;

		return new InventorySlot();
	}

	public void SetSlot(InventorySlot newItemSlot, AssetStringID slotStringID) {
		OVERRIDE_LOG_NAME("Inventory");

		if (stringIDsToSlots.TryGetValue(slotStringID, out int slotIndex)) {
			SetSlot(newItemSlot, slotIndex);
			return;
		}
		KERR("Tried to set a slot with string ID " + slotStringID + " that didn't exist");
	}

	public void SetSlot(InventorySlot newItemSlot, int slotIndex) {
		OVERRIDE_LOG_NAME("Inventory");

		if (slotIndex < 0 || slotIndex >= slots.Length) {
			KERR("Tried to set a slot via index with out of range index {" + slotIndex + "}");
		}

		slots[slotIndex] = newItemSlot;
	}

	public InventorySlot? GetSlot(AssetStringID slotStringID) {
		OVERRIDE_LOG_NAME("Inventory");
		if (stringIDsToSlots.TryGetValue(slotStringID, out int slotIndex)) {
			return GetSlot(slotIndex);
		}
		KERR("Tried to get a slot with string ID " + slotStringID + " that didn't exist");
		return null;
	}

	public InventorySlot? GetSlot(int slotIndex) {
		OVERRIDE_LOG_NAME("Inventory");

		if (slotIndex < 0 || slotIndex >= slots.Length) {
			KERR("Tried to get a slot via index with out of range index {" + slotIndex + "}");
			return null;
		}
		return slots[slotIndex];
	}

	public void ClearInventory() {
		for (int iterator = 0; iterator < slots.Length; iterator++) {
			slots[iterator].itemStringID = new AssetStringID("kiwicubed", "air");
			slots[iterator].itemCount = 0;
		}
	}

	public List<ValueTuple<AssetStringID, InventorySlot>> GetAllSlots() {
		List<ValueTuple<AssetStringID, InventorySlot>> allSlots = new();
		foreach (KeyValuePair<AssetStringID, int> slot in stringIDsToSlots) {
			allSlots.Add(new ValueTuple<AssetStringID, InventorySlot>(slot.Key, slots[slot.Value]));
		}
		return allSlots;
	}

	public List<ValueTuple<AssetStringID, InventorySlot>> GetNonEmptySlots() {
		List<ValueTuple<AssetStringID, InventorySlot>> allSlots = new();
		foreach (KeyValuePair<AssetStringID, int> slot in stringIDsToSlots) {
			if (!slots[slot.Value].HasItem()) {
				continue;
			}
			allSlots.Add(new ValueTuple<AssetStringID, InventorySlot>(slot.Key, slots[slot.Value]));
		}
		return allSlots;
	}
}