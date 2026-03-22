namespace KiwiCubed.Api;

using static KiwiCubed.Api.AssetDefinitions;

public interface IInventory {
	public abstract InventorySlot? AddItem(InventorySlot newItemSlot, int startingIndex = 0);
	public abstract InventorySlot? AddItemToSlot(InventorySlot newItemSlot, AssetStringID slotStringID);
	public abstract InventorySlot? AddItemToSlot(InventorySlot newItemSlot, int slotIndex);

	public abstract void SetSlot(InventorySlot newItemSlot, AssetStringID slotStringID);
	public abstract void SetSlot(InventorySlot newItemSlot, int slotIndex);
	public abstract InventorySlot? GetSlot(AssetStringID slotStringID);
	public abstract InventorySlot? GetSlot(int slotIndex);
	public abstract List<ValueTuple<AssetStringID, InventorySlot>> GetAllSlots();
	public abstract List<ValueTuple<AssetStringID, InventorySlot>> GetNonEmptySlots();

	public abstract void ClearInventory();

	public struct InventorySlot {
		public static AssetStringID airStringID { get; } = new AssetStringID("kiwicubed", "air");
		public AssetStringID itemStringID;
		public byte itemCount;

		public bool HasItem() {
			return itemStringID != airStringID;
		}

		public override string ToString() {
			return "InventorySlot item: " + itemStringID + ", with count: {" + itemCount + "}";
		}

		public InventorySlot(AssetStringID itemStringID, byte itemCount) {
			this.itemStringID = itemStringID;
			this.itemCount = itemCount;
		}

		public InventorySlot() {
			itemStringID = new AssetStringID("kiwicubed", "air");
			itemCount = 0;
		}
	}
}