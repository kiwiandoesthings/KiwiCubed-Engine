namespace KiwiCubed.Api;

using static KiwiCubed.Api.AssetDefinitions;

public static class Inventory {
	public static Func<ushort, IInventory> InventoryCreator;

	public static IInventory CreateInventory(ushort slotCount) {
		return InventoryCreator(slotCount);
	}
}

public interface IInventory {
	public abstract InventorySlot? AddItem(InventorySlot newItemSlot, ushort startingIndex = 0);
	public abstract InventorySlot? AddItemToSlot(InventorySlot newItemSlot, ushort slotIndex);

	public abstract void SetSlot(InventorySlot newItemSlot, ushort slotIndex);
	public abstract InventorySlot? GetSlot(ushort slotIndex);
	public abstract InventorySlot[] GetAllSlots();
	public abstract InventorySlot[] GetNonEmptySlots();

	public abstract void ClearInventory();

	public struct InventorySlot {
		public AssetStringID itemStringID;
		public byte itemCount;

		public bool HasItem() {
			return itemStringID != Meta.Get<IAssetManager>().airStringID;
		}

		public override string ToString() {
			return "InventorySlot item: " + itemStringID + ", with count: {" + itemCount + "}";
		}

		public InventorySlot(AssetStringID itemStringID, byte itemCount) {
			this.itemStringID = itemStringID;
			this.itemCount = itemCount;
		}

		public InventorySlot() {
			itemStringID = Meta.Get<IAssetManager>().airStringID;
			itemCount = 0;
		}
	}
}