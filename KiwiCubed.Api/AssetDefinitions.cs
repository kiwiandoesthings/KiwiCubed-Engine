namespace KiwiCubed.Api;

public class AssetDefinitions {
	public readonly struct AssetStringID : IEquatable<AssetStringID> {
		public readonly string modName;
		public readonly string assetName;

		public string CanonicalName() {
			return modName + ":" + assetName;
		}
		public AssetStringID(string modName, string assetName) {
			this.modName = modName;
			this.assetName = assetName;
		}

		public AssetStringID() {
			modName = "kiwicubed";
			assetName = "air";
		}

		public static bool operator ==(AssetStringID a, AssetStringID b) {
			return a.Equals(b);
		}

		public static bool operator !=(AssetStringID a, AssetStringID b) {
			return !a.Equals(b);
		}

		public readonly bool Equals(AssetStringID other) {
			return modName == other.modName && assetName == other.assetName;
		}

		public override readonly bool Equals(object? other) {
			return other != null && other.GetType() == typeof(AssetStringID) && this.Equals((AssetStringID)other);
		}

		public override int GetHashCode() {
			return HashCode.Combine(modName, assetName);
		}

		public override string ToString() {
			return "\"" + CanonicalName() + "\"";
		}
	}

	public readonly struct TextureAtlasData {
		readonly ushort variant;
		readonly ushort xPosition;
		readonly ushort yPosition;
		readonly ushort width;
		readonly ushort height;
	}

	public readonly struct BlockModel {
		// needs to contain actual model definition
		// this will be very complex as i will need to implement data driven face culling
		// probably dont support custom block models for a while
		readonly TextureAtlasData[] atlasData = new TextureAtlasData[6];

		public BlockModel(TextureAtlasData[] atlasDatas) {
			atlasData = atlasDatas;
		}
	}
}