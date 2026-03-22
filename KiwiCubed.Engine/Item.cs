namespace KiwiCubed.Engine;

using KiwiCubed.Api;

using static KiwiCubed.Api.AssetDefinitions;

public class Item : IItem {
	private MetaTexture texture;
	private uint stackSize;

	public Item(MetaTexture itemTexture, int stackSize) {
		texture = itemTexture;
		this.stackSize = (uint)stackSize;
	}

	public MetaTexture GetTexture() {
		return texture;
	}

	public int GetStackSize() {
		return (int)stackSize;
	}
}