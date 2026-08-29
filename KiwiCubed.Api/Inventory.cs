namespace KiwiCubed.Api;

using static KiwiCubed.Api.AssetDefinitions;

public static class Inventory {
	public static Func<ushort, IInventory> InventoryCreator;

	public static IInventory CreateInventory(ushort stackCount) {
		return InventoryCreator(stackCount);
	}
}

public interface IInventory {
	public abstract ItemStack? AddItem(ItemStack newItemSlot, ushort startingIndex = 0);
	public abstract ItemStack? AddItemToStack(ItemStack newItemSlot, ushort slotIndex);

	public abstract void SetStack(ItemStack newItemSlot, ushort slotIndex);
	public abstract ItemStack? GetStack(ushort slotIndex);
	public abstract ItemStack[] GetAllStacks();
	public abstract ItemStack[] GetNonEmptyStacks();

	public abstract void ClearInventory();

	public readonly struct ItemStack {
		public static readonly AssetStringID airStringID = Meta.Get<IAssetManager>().airStringID;
        public readonly AssetStringID itemStringID;
		public readonly byte itemCount;

		public ItemStack(AssetStringID itemStringID, byte itemCount) {
			this.itemStringID = itemStringID;
			this.itemCount = itemCount;
		}

		public ItemStack() {
			itemStringID = airStringID;
			itemCount = 0;
		}

        public bool HasItem() {
            return itemStringID != airStringID;
        }

        public ItemStack WithCount(byte newCount) {
            return new ItemStack(itemStringID, newCount);
        }

        public ItemStack Decrement(byte amount = 1) {
            return new ItemStack(itemStringID, (byte)Math.Max(0, itemCount - amount));
        }

        public ItemStack Increment(byte amount = 1) {
            return new ItemStack(itemStringID, (byte)Math.Min(byte.MaxValue, itemCount + amount));
        }

        public override string ToString() {
            return "ItemStack item: " + itemStringID + ", with count: {" + itemCount + "}";
        }
    }
}