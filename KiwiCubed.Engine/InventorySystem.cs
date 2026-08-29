namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using static KiwiCubed.Api.IInventory;

public class InventorySystem : IInventory {
	private static KLogger logger = new KLogger("Inventory");
	private ItemStack[] stacks;

	public InventorySystem(ushort slotCount) {
		stacks = new ItemStack[slotCount];

		for (int iterator = 0; iterator < slotCount; iterator++) {
			stacks[iterator] = new ItemStack(AssetManager.airStringID, 0);
		}
	}

	public ItemStack? AddItem(ItemStack newItemStack, ushort startingIndex = 0) {
		ItemStack remainingStack = newItemStack;

		for (ushort iterator = startingIndex; iterator < stacks.Length; iterator++) {
			ref ItemStack stack = ref stacks[iterator];
            if (stack.HasItem() && stack.itemStringID != remainingStack.itemStringID) {
                continue;
            }
            ItemStack? result = AddItemToStack(remainingStack, iterator);
            if (result == null) {
                return null;
            }
            remainingStack = result.Value;
            if (!remainingStack.HasItem()) {
                return new ItemStack();
            }
        }

        return remainingStack;
    }

	public ItemStack? AddItemToStack(ItemStack newItemStack, ushort stackIndex) {
		if (stackIndex < 0 || stackIndex >= stacks.Length) {
			logger.ERR("Tried to add an item to a slot at index {" + stackIndex + "} that didn't exsit");
			return null;
		}
		ref ItemStack stack = ref stacks[stackIndex];
		if (!stack.HasItem()) {
			stack = newItemStack;
			return null;
		}
		if (stack.itemStringID != newItemStack.itemStringID) {
			logger.ERR("Tried to add an item to a stack at index {" + stackIndex + "} when the old and new stack had different items");
			logger.ERR("Old stack: " + stack);
			logger.ERR("New stack: " + newItemStack);
			return null;
		}
		int difference = 64 - stack.itemCount - newItemStack.itemCount;
		stack = stack.Increment(newItemStack.itemCount);
		if (difference < 0) {
			stack = stack.WithCount(64);
			return new ItemStack(stack.itemStringID, (byte)-difference);
		}

		return new ItemStack();
	}

	public void SetStack(ItemStack newItemStack, ushort stackIndex) {
		if (stackIndex >= stacks.Length) {
			logger.ERR("Tried to set a stack at index {" + stackIndex + "} that didn't exist");
			return;
		}

		stacks[stackIndex] = newItemStack;
	}

	public ItemStack? GetStack(ushort stackIndex) {
		if (stackIndex >= stacks.Length) {
			logger.ERR("Tried to get a stack at index {" + stackIndex + "} that didn't exist");
			return null;
		}

		return stacks[stackIndex];
	}

	public void ClearInventory() {
		for (int iterator = 0; iterator < stacks.Length; iterator++) {
			stacks[iterator] = new ItemStack();
		}
	}

	public ItemStack[] GetAllStacks() {
		return stacks;
	}

	public ItemStack[] GetNonEmptyStacks() {
		List<ItemStack> nonEmptyStacks = [];
		foreach (ItemStack stack in stacks) {
			if (!stack.HasItem()) {
				continue;
			}
            nonEmptyStacks.Add(stack);
		}

		return [.. nonEmptyStacks];
	}
}