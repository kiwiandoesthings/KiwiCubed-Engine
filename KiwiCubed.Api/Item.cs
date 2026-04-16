namespace KiwiCubed.Api;

using ArchEntity = Arch.Core.Entity;

using static AssetDefinitions;

public struct ItemDefinition {
	public readonly AssetStringID stringID;
	public readonly ArchEntity definition;

	public ItemDefinition(AssetStringID itemStringID, ArchEntity itemDefinition) {
		stringID = itemStringID;
		definition = itemDefinition;
	}

	public static bool operator ==(ItemDefinition a, ItemDefinition b) {
		return a.Equals(b);
	}

	public static bool operator !=(ItemDefinition a, ItemDefinition b) {
		return !a.Equals(b);
	}

	public override bool Equals(object? obj) {
		return obj is not null && obj is ItemDefinition other && other.stringID.Equals(stringID);
	}

	public override int GetHashCode() {
		return stringID.GetHashCode();
	}
}

public struct ItemRenderableComponent {
	public GeneralMesh mesh;
}

public struct ItemPlaceableComponent {
}

public struct ItemEdibleComponent {
}