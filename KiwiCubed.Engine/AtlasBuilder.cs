namespace KiwiCubed.Engine;

using System.Collections.Frozen;
using System.Numerics;
using RectpackSharp;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using StbImageSharp;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.KLogger;

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

	private List<TextureSlot> textureSizes;
	private Dictionary<uint, AssetStringID> numericalIDsToStringIDs;
	private uint latestID;
	private uint atlasSize;

	public AtlasBuilder() {
		textureSizes = new();
		numericalIDsToStringIDs = new();
		latestID = 0;
		atlasSize = 0;
	}

	public void AddTexture(int width, int height, AssetStringID stringID) {
		OVERRIDE_LOG_NAME("Atlas Builder");

		if (forceSquareTextures && width != height) {
			KERR("Tried to use a non-square texture with dimensions {" + width + "x" + height + "} under the string ID " + stringID);
			return;
		}
		if (forcePowerOfTwoTextures && (!BitOperations.IsPow2(width) || !BitOperations.IsPow2(height))) {
			KERR("Tried to use a texture width dimensions {" + width + "x" + height + "} that were not powers of two");
			return;
		}
		textureSizes.Add(new TextureSlot(width, height, latestID));
		numericalIDsToStringIDs.Add(latestID, stringID);
		latestID++;
	}

	public FrozenDictionary<AssetStringID, TextureAtlasData> PackTextures() {
		OVERRIDE_LOG_NAME("Atlas Builder");

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

		KINFO("Successfully packed " + rectangles.Length + " textures into an atlas of size {" + atlasSize + "x" + atlasSize + "}");

		return stringIDsToAtlasData.ToFrozenDictionary();
	}

	public Texture CreateAtlas(List<ValueTuple<TextureAtlasData, ImageResult>> textures) {
		OVERRIDE_LOG_NAME("Altas Builder");

		if (atlasSize == 0) {
			KERR("Tried to create an atlas texture without packing textures first or with 0 textures registered");
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
		//atlas.Save("../../../atlas1234.png");

		KINFO("Successfully built atlas texture");

		return new Texture(atlas, TextureTarget.Texture2D, TextureUnit.Texture0, PixelFormat.Rgba, PixelType.UnsignedByte, true);
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