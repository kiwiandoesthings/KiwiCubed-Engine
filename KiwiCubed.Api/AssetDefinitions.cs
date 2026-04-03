namespace KiwiCubed.Api;

using static KiwiCubed.Api.Logger;

public class AssetDefinitions {
	public readonly struct AssetStringID : IEquatable<AssetStringID> {
		public readonly string modName;
		public readonly string assetName;
		private readonly int hashCode;

		public string CanonicalName() {
			return modName + ":" + assetName;
		}

		public AssetStringID Prefix(string prefix) {
			string newAssetName = assetName;
			if (newAssetName.IndexOf("/") != -1) {
				newAssetName = assetName.Substring(assetName.LastIndexOf("/") + 1);
			}
			newAssetName = prefix + "/" + newAssetName;
			return new AssetStringID(modName, newAssetName);
		}

		public static AssetStringID FromString(string canonicalName) {
			string[] split = canonicalName.Split(':');
			if (split.Length != 2) {
				Logger.ERR("Tried to create an AssetStringID from an invalid string \"" + canonicalName + "\", returning default");
				return new AssetStringID("kiwicubed", "invalid");
			}
			return new AssetStringID(split[0], split[1]);
        }

		public AssetStringID(string modName, string assetName) {
			this.modName = modName;
			this.assetName = assetName;
            hashCode = HashCode.Combine(modName.GetHashCode(), assetName.GetHashCode());
        }

		public AssetStringID() {
			modName = "kiwicubed";
			assetName = "air";
			hashCode = HashCode.Combine(modName.GetHashCode(), assetName.GetHashCode());
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
			return hashCode;
		}

		public override string ToString() {
			return "\"" + CanonicalName() + "\"";
		}
	}

	public readonly struct TextureAtlasData {
		public readonly float xPosition;
		public readonly float yPosition;
		public readonly float xSize;
		public readonly float ySize;

		public TextureAtlasData(float xPosition, float yPosition, float xSize, float ySize) {
			this.xPosition = xPosition;
			this.yPosition = yPosition;
			this.xSize = xSize;
			this.ySize = ySize;
		}
	}

	public readonly struct MetaTexture {
		public readonly TextureAtlasData[] atlasDatas;
		public readonly byte[] faceIndices;

		public MetaTexture(TextureAtlasData[] atlasDatas, byte[] faceIndices) {
			this.atlasDatas = atlasDatas;
			this.faceIndices = faceIndices;
		}
	}

	public readonly struct BlockModel {
		// needs to contain actual model definition
		// this will be very complex as i will need to implement data driven face culling
		// probably dont support custom block models for a while
		public readonly TextureAtlasData[] atlasData = new TextureAtlasData[6];

		public BlockModel(TextureAtlasData[] atlasDatas) {
			atlasData = atlasDatas;
		}
	}

	public class BiomeModel {
		public readonly float temperature;
		public readonly float humidity;
		public readonly float height;
		public readonly Block topLayer;
		public readonly Block soilLayer;
		public readonly Block groundLayer;

		public BiomeModel(float temperature, float humidity, float height, Block topLayer, Block soilLayer, Block groundLayer) {
			this.temperature = temperature;
			this.humidity = humidity;
			this.height = height;
			this.topLayer = topLayer;
			this.soilLayer = soilLayer;
			this.groundLayer = groundLayer;
		}
	}
}