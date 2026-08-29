namespace KiwiCubed.Api;

using ArchEntity = Arch.Core.Entity;

using static AssetDefinitions;

public struct ItemDefinition {
	public readonly AssetStringID stringID;
	public readonly TextureAtlasData atlasData;
	public readonly string name;
	public readonly ArchEntity definition;

	public ItemDefinition(AssetStringID itemStringID, TextureAtlasData atlasData, string name, ArchEntity itemDefinition) {
		stringID = itemStringID;
		this.atlasData = atlasData;
		this.name = name;
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

public struct ItemPlaceableComponent {
}

public struct ItemEdibleComponent {
}