namespace KiwiCubed.Engine;

using System.Collections.Frozen;
using System.Diagnostics;
using System.Numerics;
using RectpackSharp;
using Silk.NET.OpenGL;
using StbImageSharp;
using StbImageWriteSharp;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Globals;

public class AtlasBuilder {
    public readonly struct TextureSlot {
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
    private int atlasSize;

    public AtlasBuilder() {
        logger = new KLogger("AtlasBuilder");
        textureSizes = [];
        numericalIDsToStringIDs = [];
        latestID = 0;
        atlasSize = 0;
    }

    public void AddTexture(int width, int height, AssetStringID stringID) {
        if (forceSquareTextures && width != height) {
            logger.ERR("Tried to use a non-square texture with dimensions {" + width + "x" + height + "} under the string ID " + stringID);
            logger.BREAK();
        }
        if (forcePowerOfTwoTextures && (!BitOperations.IsPow2(width) || !BitOperations.IsPow2(height))) {
            logger.ERR("Tried to use a texture width dimensions {" + width + "x" + height + "} that were not powers of two");
            logger.BREAK();
        }
        textureSizes.Add(new TextureSlot(width, height, latestID));
        numericalIDsToStringIDs.Add(latestID, stringID);
        latestID++;
    }

    public FrozenDictionary<AssetStringID, TextureAtlasData> PackTextures() {
        Stopwatch stopwatch = Stopwatch.StartNew();

        PackingRectangle[] rectangles = new PackingRectangle[textureSizes.Count];
        for (int iterator = 0; iterator < textureSizes.Count; iterator++) {
            TextureSlot textureSize = textureSizes[iterator];
            rectangles[iterator] = new PackingRectangle(0, 0, textureSize.width + 2, textureSize.height + 2, (int)textureSize.id);
        }

        RectanglePacker.Pack(rectangles, out PackingRectangle bounds, PackingHints.FindBest, 1, 1);
        uint largerSize = bounds.Width > bounds.Height ? bounds.Width : bounds.Height;
        atlasSize = (int)BitOperations.RoundUpToPowerOf2(largerSize);

        List<KeyValuePair<AssetStringID, TextureAtlasData>> stringIDsToAtlasData = [];
        for (int iterator = 0; iterator < rectangles.Length; iterator++) {
            PackingRectangle rectangle = rectangles[iterator];
            TextureSlot textureSize = textureSizes[(int)rectangle.Id];
            if (numericalIDsToStringIDs.TryGetValue((uint)rectangle.Id, out AssetStringID stringID)) {
                float x = (rectangle.X + 1.0f) / atlasSize;
                float y = (rectangle.Y + 1.0f) / atlasSize;
                float width = textureSize.width / (float)atlasSize;
                float height = textureSize.height / (float)atlasSize;
                stringIDsToAtlasData.Add(new KeyValuePair<AssetStringID, TextureAtlasData>(stringID, new TextureAtlasData(x, y, width, height)));
            } else {
                return null;
            }
        }

        logger.INFO("Took " + stopwatch.ElapsedMilliseconds + "ms to successfully pack " + rectangles.Length + " textures into an atlas of size {" + atlasSize + "x" + atlasSize + "}");

        return stringIDsToAtlasData.ToFrozenDictionary();
    }

    public Texture CreateAtlas(List<ValueTuple<TextureAtlasData, ImageResult>> textures) {
        Stopwatch stopwatch = Stopwatch.StartNew();

        if (atlasSize == 0) {
            logger.ERR("Tried to create an atlas texture without packing textures first or with 0 textures registered");
            logger.BREAK();
        }
        byte[] atlasPixels = new byte[atlasSize * atlasSize * 4];
        foreach (ValueTuple<TextureAtlasData, ImageResult> texture in textures) {
            TextureAtlasData atlasData = texture.Item1;
            ImageResult imageResult = texture.Item2;

            int xPosition = (int)MathF.Round((atlasData.xPosition * atlasSize) - 1.0f);
            int yPosition = (int)MathF.Round((atlasData.yPosition * atlasSize) - 1.0f);

            CopyTextureToAtlas(atlasPixels, (int)atlasSize, imageResult.Data, imageResult.Width, imageResult.Height, xPosition, yPosition);
        }
        if (isDebug) {
            string dumpDirectory = Path.Combine(topSaveFolder, "Debug Dump");
            Directory.CreateDirectory(dumpDirectory);
            ImageWriter writer = new ImageWriter();
            using FileStream stream = File.OpenWrite(Path.Combine(dumpDirectory, "generated_atlas_dump.png"));
            writer.WritePng(atlasPixels, atlasSize, atlasSize, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, stream);
        }

        logger.INFO("Took " + stopwatch.ElapsedMilliseconds + "ms to successfully build an atlas texture");

        return new Texture(atlasPixels, atlasSize, atlasSize, TextureTarget.Texture2D, TextureUnit.Texture0, PixelFormat.Rgba, PixelType.UnsignedByte, false);
    }

    private static void CopyTextureToAtlas(byte[] atlasPixels, int atlasSize, byte[] subTextureBytes, int width, int height, int xPosition, int yPosition) {
        int atlasStride = atlasSize * 4;
        int subStride = width * 4;

        for (int y = 0; y < height + 2; y++) {
            int atlasY = yPosition + y;
            if (atlasY < 0 || atlasY >= atlasSize) {
                continue;
            }

            int sourceY;
            if (y == 0) {
                sourceY = 0;
            } else if (y == height + 1) {
                sourceY = height - 1;
            } else {
                sourceY = y - 1;
            }

            int atlasRowOffset = atlasY * atlasStride;
            int subRowOffset = sourceY * subStride;

            if (xPosition >= 0 && xPosition < atlasSize) {
                int targetIndex = atlasRowOffset + (xPosition * 4);
                int sourceIndex = subRowOffset + 0;
                CopyPixel(subTextureBytes, sourceIndex, atlasPixels, targetIndex);
            }

            for (int x = 0; x < width; x++) {
                int targetX = xPosition + 1 + x;
                if (targetX >= 0 && targetX < atlasSize) {
                    int targetIndex = atlasRowOffset + (targetX * 4);
                    int sourceIndex = subRowOffset + (x * 4);
                    CopyPixel(subTextureBytes, sourceIndex, atlasPixels, targetIndex);
                }
            }

            int rightPadX = xPosition + width + 1;
            if (rightPadX >= 0 && rightPadX < atlasSize) {
                int targetIndex = atlasRowOffset + (rightPadX * 4);
                int sourceIndex = subRowOffset + ((width - 1) * 4);
                CopyPixel(subTextureBytes, sourceIndex, atlasPixels, targetIndex);
            }
        }
    }

    private static void CopyPixel(byte[] source, int sourceIndex, byte[] destination, int destinationIndex) {
        destination[destinationIndex + 0] = source[sourceIndex + 0];
        destination[destinationIndex + 1] = source[sourceIndex + 1];
        destination[destinationIndex + 2] = source[sourceIndex + 2];
        destination[destinationIndex + 3] = source[sourceIndex + 3];
    }
}