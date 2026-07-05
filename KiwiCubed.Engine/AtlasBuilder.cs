namespace KiwiCubed.Engine;

using System.Collections.Frozen;
using System.Numerics;
using KiwiCubed.Api;
using RectpackSharp;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using StbImageSharp;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Globals;

public class AtlasBuilder {
	public struct TextureSlot {
		public readonly uint width = 0;
		public readonly uint height = 0;
		public readonly uint id;

		public TextureSlot(int width, int height, uint id) {
			this.width = (uint)width;
			this.height = (uint)height;
			this.id = id;
		}
	}

	private KLogger logger;
	private List<TextureSlot> textureSizes;
	private Dictionary<uint, AssetStringID> numericalIDsToStringIDs;
	private uint latestID;
	private uint atlasSize;

	public AtlasBuilder() {
		logger = new KLogger("AtlasBuilder");
		textureSizes = new();
		numericalIDsToStringIDs = new();
		latestID = 0;
		atlasSize = 0;
	}

	public void AddTexture(int width, int height, AssetStringID stringID) {
		if (forceSquareTextures && width != height) {
			logger.ERR("Tried to use a non-square texture with dimensions {" + width + "x" + height + "} under the string ID " + stringID);
			return;
		}
		if (forcePowerOfTwoTextures && (!BitOperations.IsPow2(width) || !BitOperations.IsPow2(height))) {
			logger.ERR("Tried to use a texture width dimensions {" + width + "x" + height + "} that were not powers of two");
			return;
		}
		textureSizes.Add(new TextureSlot(width, height, latestID));
		numericalIDsToStringIDs.Add(latestID, stringID);
		latestID++;
	}

	public FrozenDictionary<AssetStringID, TextureAtlasData> PackTextures() {
		PackingRectangle[] rectangles = new PackingRectangle[textureSizes.Count];
		for (int iterator = 0; iterator < textureSizes.Count; iterator++) {
			TextureSlot textureSize = textureSizes[iterator];
			rectangles[iterator] = new PackingRectangle(0, 0, textureSize.width, textureSize.height, (int)textureSize.id);
		}

		RectanglePacker.Pack(rectangles, out PackingRectangle bounds, PackingHints.FindBest, 1, 1);
		uint largerSize = bounds.Width > bounds.Height ? bounds.Width : bounds.Height;
		atlasSize = BitOperations.RoundUpToPowerOf2(largerSize);

		Dictionary<AssetStringID, TextureAtlasData> stringIDsToAtlasData = new();
		foreach (PackingRectangle rectangle in rectangles) {
			if (numericalIDsToStringIDs.TryGetValue((uint)rectangle.Id, out AssetStringID stringID)) {
				stringIDsToAtlasData.Add(stringID, new TextureAtlasData(rectangle.X / (float)atlasSize, rectangle.Y / (float)atlasSize, rectangle.Width / (float)atlasSize, rectangle.Height / (float)atlasSize));
			} else {
				return null;
			}
		}

		logger.INFO("Successfully packed " + rectangles.Length + " textures into an atlas of size {" + atlasSize + "x" + atlasSize + "}");

		return stringIDsToAtlasData.ToFrozenDictionary();
	}

	public Texture CreateAtlas(List<ValueTuple<TextureAtlasData, ImageResult>> textures) {
		if (atlasSize == 0) {
			logger.ERR("Tried to create an atlas texture without packing textures first or with 0 textures registered");
			return null;
		}
		Image<Rgba32> atlas = new Image<Rgba32>((int)atlasSize, (int)atlasSize);
		foreach (ValueTuple<TextureAtlasData, ImageResult> texture in textures) {
			TextureAtlasData atlasData = texture.Item1;
			ImageResult imageResult = texture.Item2;
			byte[] textureData = imageResult.Data;
			Image<Rgba32> subTexture = Image.LoadPixelData<Rgba32>(textureData, imageResult.Width, imageResult.Height);
			CopyTextureToAtlas(atlas, subTexture, (uint)(atlasData.xPosition * atlasSize), (uint)(atlasData.yPosition * atlasSize));
		}
		if (isDebug) {
			string dumpDirectory = Path.Combine(topSaveFolder, "Debug Dump");
            Directory.CreateDirectory(dumpDirectory);
            atlas.Save(Path.Combine(dumpDirectory, "generated_atlas_dump.png"));
		}

		logger.INFO("Successfully built atlas texture");

		return new Texture(atlas, TextureTarget.Texture2D, TextureUnit.Texture0, PixelFormat.Rgba, PixelType.UnsignedByte, false);
	}

	private void CopyTextureToAtlas(Image<Rgba32> atlas, Image<Rgba32> subTexture, uint xPosition, uint yPosition) {
		subTexture.ProcessPixelRows(atlas, (textureAccessor, atlasAccessor) => {
			for (int y = 0; y < textureAccessor.Height; y++) {
				Span<Rgba32> textureRow = textureAccessor.GetRowSpan(y);
				Span<Rgba32> atlasRow = atlasAccessor.GetRowSpan(y + (int)yPosition);
				
				textureRow.CopyTo(atlasRow.Slice((int)xPosition, subTexture.Width));
			}
		});
	}
}