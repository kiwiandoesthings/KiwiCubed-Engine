namespace KiwiCubed.Engine;

using ArchWorld = Arch.Core.World;
using KiwiCubed.Api;
using System.Diagnostics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Block;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.Util;

public class Chunk : IChunk, IDisposable {
    public struct ChunkHeightmap {
        public byte[,] heightmap = new byte[chunkSize, chunkSize];
        public bool[,] heightmapMask = new bool[chunkSize, chunkSize];

        public ChunkHeightmap() { }
    }

    private static int totalChunks = 0;
    private static uint samplesPerAxis = 8;
    private static AssetManager assetManager;
    private static ChunkHandler chunkHandler;
    private static ArchWorld archWorld;
    private bool isReal = false;
    private bool awaitingDestruction = false;
    public int chunkX { get; }
    public int chunkY { get; }
    public int chunkZ { get; }
    private List<float> vertices;
    private List<ushort> indices;
    private List<ushort> blockPalette;
    private Dictionary<ushort, ushort> blocksToPaletteIndices;
    private ushort[] paletteIndices;
    private byte[] blockVariants;
    private byte[] blockStates;
    private BiomeModel[,] biomes;
    private ChunkHeightmap heightmap;
    private bool dirtyBuffers = false;
    private bool isGenerated = false;
    private bool isMeshed = false;
    private bool isGenerating = false;
    private bool isMeshing = false;
    private bool isEmpty = true;
    private bool isFull = false;
    private byte chunkGenerationState = 0;
    private ushort totalBlocks = 0;

    public static void SetupChunks(ChunkHandler chunkHandler) {
        assetManager = (AssetManager)MetaHandler.Get<IAssetManager>();
        Chunk.chunkHandler = chunkHandler;
        archWorld = assetManager.GetArchWorld();
    }

    public Chunk(int x, int y, int z, ChunkHandler chunkHandler) {
        totalChunks++;
        chunkX = x;
        chunkY = y;
        chunkZ = z;

        // look into sparse storage
        blockPalette = new();
        paletteIndices = new ushort[chunkVolume];
        blocksToPaletteIndices = new();
        TryAddPalette(0);

        heightmap = new ChunkHeightmap();

        blockVariants = new byte[chunkVolume];
        blockStates = new byte[chunkVolume];
        biomes = new BiomeModel[chunkSize / 4, chunkSize / 4];
    }

    public void MakeReal() {
        totalChunks++;
        isReal = true;
    }

    public bool GenerateBlocks(World world) {
        OVERRIDE_LOG_NAME("Chunk Block Generation");

        Stopwatch stopwatch = Stopwatch.StartNew();

        if (isGenerated) {
            KWARN("Tried to generate blocks for chunk at {" + chunkX + ", " + chunkY + ", " + chunkZ + "} twice");
            return false;
        }
        isGenerating = true;

        ref GenerationNoises noise = ref world.GetNoises();

        int baseX = chunkX * chunkSize;
        int baseY = chunkY * chunkSize;
        int baseZ = chunkZ * chunkSize;

        float[,,] terrainSamples = new float[samplesPerAxis + 1, samplesPerAxis + 2, samplesPerAxis + 1];
        float[,] heightSamples = new float[(samplesPerAxis / 2) + 1, (samplesPerAxis / 2) + 1];
        float[,] weirdSamples = new float[(samplesPerAxis / 2) + 1, (samplesPerAxis / 2) + 1];
        float[,] temperatureSamples = new float[samplesPerAxis + 1, samplesPerAxis + 1];
        float[,] humiditySamples = new float[samplesPerAxis + 1, samplesPerAxis + 1];
        uint totalSamplesPerAxis = samplesPerAxis + 1;
        int spacing = (int)chunkSize / (int)samplesPerAxis;
        int doubleSpacing = spacing * 2;
        for (byte sampleX = 0; sampleX < totalSamplesPerAxis; sampleX++) {
            float worldX = (float)(baseX + (sampleX * spacing));
            for (byte sampleZ = 0; sampleZ < totalSamplesPerAxis; sampleZ++) {
                float worldZ = (float)(baseZ + (sampleZ * spacing));
                for (byte sampleY = 0; sampleY < totalSamplesPerAxis + 1; sampleY++) {
                    float worldY = (float)(baseY + (sampleY * spacing));
                    terrainSamples[sampleX, sampleY, sampleZ] = noise.terrainNoise.GetNoise(worldX, worldY, worldZ);
                }

                if (sampleX % 2 == 0 && sampleZ % 2 == 0) {
                    heightSamples[sampleX / 2, sampleZ / 2] = (noise.heightNoise.GetNoise(worldX, worldZ) + 2.0f) / 2.0f;
                    weirdSamples[sampleX / 2, sampleZ / 2] = ((noise.weirdNoise.GetNoise(worldX, worldZ) + 1.0f) / 2.0f) + 0.0f;
                }
                temperatureSamples[sampleX, sampleZ] = noise.temperatureNoise.GetNoise(worldX, worldZ);
                humiditySamples[sampleX, sampleZ] = noise.humidityNoise.GetNoise(worldX, worldZ);
            }
        }

        stopwatch.Stop();
        //KINFO("--- Took " + stopwatch.Elapsed.TotalMilliseconds + "ms to sample noises for chunk ---");
        stopwatch = Stopwatch.StartNew();

        float aboveBlockDensity = 0.0f;
        for (byte blockX = 0; blockX < chunkSize; blockX++) {
            int sampleX = blockX / spacing;
            float interpolatedX = (blockX % spacing) / (float)spacing;
            int halfSampleX = blockX / doubleSpacing;
            float halfInterpolatedX = (blockX % doubleSpacing) / (float)doubleSpacing;
            for (byte blockZ = 0; blockZ < chunkSize; blockZ++) {
                int sampleZ = blockZ / spacing;
                float interpolatedZ = (blockZ % spacing) / (float)spacing;
                int halfSampleZ = blockZ / doubleSpacing;
                float halfInterpolatedZ = (blockZ % doubleSpacing) / (float)doubleSpacing;

                float height = GetInterpolatedValue2D(ref heightSamples, halfSampleX, halfInterpolatedX, halfSampleZ, halfInterpolatedZ);
                float weird = GetInterpolatedValue2D(ref weirdSamples, halfSampleX, halfInterpolatedX, halfSampleZ, halfInterpolatedZ);

                float interpolatedY2 = (chunkSize % spacing) / (float)spacing;
                float baseDensity = GetInterpolatedValue(ref terrainSamples, sampleX, interpolatedX, chunkSize / spacing, interpolatedY2, sampleZ, interpolatedZ);
                aboveBlockDensity = GetWeightedDensity(baseDensity, height, weird, baseY + 32);
                int blocksFromSurface = 0;
                for (int blockY = chunkSize - 1; blockY >= 0; blockY--) {
                    int sampleY = blockY / spacing;
                    float interpolatedY = (blockY % spacing) / (float)spacing;

                    float density = GetInterpolatedValue(ref terrainSamples, sampleX, interpolatedX, sampleY, interpolatedY, sampleZ, interpolatedZ);
                    float temperature = temperatureSamples[sampleX, sampleZ];
                    float humidity = humiditySamples[sampleX, sampleZ];
                    BiomeModel biome = ChunkGenerator.GetClosestBiome(weird, temperature, humidity);
                    biomes[blockX / 4, blockZ / 4] = biome;
                    int totalHeight = blockY + baseY;

                    float weightedDensity = GetWeightedDensity(density, height, weird, totalHeight);

                    if (weightedDensity <= 0) {
                        continue;
                    }

                    if (aboveBlockDensity <= 0.0f) {
                        paletteIndices[GetBlockPositionIndex(blockX, blockY, blockZ)] = TryAddPalette(biome.topLayerID);
                        blocksFromSurface++;
                    } else if (aboveBlockDensity <= 0.5f) {
                        paletteIndices[GetBlockPositionIndex(blockX, blockY, blockZ)] = TryAddPalette(biome.soilLayerID);
                        blocksFromSurface++;
                    } else {
                        paletteIndices[GetBlockPositionIndex(blockX, blockY, blockZ)] = TryAddPalette(biome.groundLayerID);
                    }

                    aboveBlockDensity = weightedDensity;
                    totalBlocks++;
                }
            }
        }

        isGenerated = true;
        chunkGenerationState = 2;

        GenerateHeightmap();
        RecalculateFullness();

        stopwatch.Stop();
        //KINFO("--- Took " + stopwatch.Elapsed.TotalMilliseconds + "ms to add chunk blocks and generate heightmap --- ");
        isGenerating = false;
        return true;
    }

    private float GetWeightedDensity(float density, float height, float weirdness, int totalHeight) {
        //float baseDensity = density - (totalHeight * weirdness * 0.05f);
        //float baseDensity = (density * 10.0f * weirdness) - totalHeight + (height * 32.0f);
        float baseDensity = 1.0f;
        if (totalHeight > 80) {
            baseDensity = 0.0f;
        }
        float weightedDensity = baseDensity;

        return weightedDensity;
    }

    private float GetInterpolatedValue(ref float[,,] samples, int sampleX, float interpolatedX, int sampleY, float interpolatedY, int sampleZ, float interpolatedZ) {
        float bottomBackLeft = samples[sampleX, sampleY, sampleZ];
        float bottomBackRight = samples[sampleX + 1, sampleY, sampleZ];
        float bottomFrontLeft = samples[sampleX, sampleY, sampleZ + 1];
        float bottomFrontRight = samples[sampleX + 1, sampleY, sampleZ + 1];

        float topBackLeft = samples[sampleX, sampleY + 1, sampleZ];
        float topBackRight = samples[sampleX + 1, sampleY + 1, sampleZ];
        float topFrontLeft = samples[sampleX, sampleY + 1, sampleZ + 1];
        float topFrontRight = samples[sampleX + 1, sampleY + 1, sampleZ + 1];

        float interpolatedBottomBack = Lerp(bottomBackLeft, bottomBackRight, interpolatedX);
        float interpolatedBottomFront = Lerp(bottomFrontLeft, bottomFrontRight, interpolatedX);

        float interpolatedTopBack = Lerp(topBackLeft, topBackRight, interpolatedX);
        float interpolatedTopFront = Lerp(topFrontLeft, topFrontRight, interpolatedX);

        float interpolatedBottomTotal = Lerp(interpolatedBottomBack, interpolatedBottomFront, interpolatedZ);
        float interpolatedTopTotal = Lerp(interpolatedTopBack, interpolatedTopFront, interpolatedZ);

        return Lerp(interpolatedBottomTotal, interpolatedTopTotal, interpolatedY);
    }

    private float GetInterpolatedValue2D(ref float[,] samples, int sampleX, float interpolatedX, int sampleZ, float interpolatedZ) {
        float sample00 = samples[sampleX, sampleZ];
        float sample10 = samples[sampleX + 1, sampleZ];
        float sample01 = samples[sampleX, sampleZ + 1];
        float sample11 = samples[sampleX + 1, sampleZ + 1];

        float interpolatedTerrainSample0 = Lerp(sample00, sample10, interpolatedX);
        float interpolatedTerrainSample1 = Lerp(sample01, sample11, interpolatedX);
        return Lerp(interpolatedTerrainSample0, interpolatedTerrainSample1, interpolatedZ);
    }

    public bool GenerateMesh(bool remesh) {
        OVERRIDE_LOG_NAME("Chunk Mesh Generation");

        Stopwatch stopwatch = Stopwatch.StartNew();

        IntVector3 chunkPosition = new IntVector3(chunkX, chunkY, chunkZ);
        if (isMeshed && !remesh) {
            KERR("Tried to mesh already meshed chunk at position " + chunkPosition + " when remesh was specified as false");
            return false;
        }
        if (!isGenerated) {
            KERR("Tried to mesh ungenerated chunk at position " + chunkPosition);
            return false;
        }
        if (IsEmpty()) {
            return false;
        }
        isMeshing = true;

        Chunk positiveXChunk = ((Chunk)chunkHandler.GetChunk(chunkX + 1, chunkY, chunkZ, false));
        Chunk negativeXChunk = ((Chunk)chunkHandler.GetChunk(chunkX - 1, chunkY, chunkZ, false));
        Chunk positiveYChunk = ((Chunk)chunkHandler.GetChunk(chunkX, chunkY + 1, chunkZ, false));
        Chunk negativeYChunk = ((Chunk)chunkHandler.GetChunk(chunkX, chunkY - 1, chunkZ, false));
        Chunk positiveZChunk = ((Chunk)chunkHandler.GetChunk(chunkX, chunkY, chunkZ + 1, false));
        Chunk negativeZChunk = ((Chunk)chunkHandler.GetChunk(chunkX, chunkY, chunkZ - 1, false));

        vertices = new();
        indices = new();
        Span<bool> facesToAdd = stackalloc bool[6];

        bool hasMesh = false;
        for (byte blockX = 0; blockX < chunkSize; blockX++) {
            for (byte blockZ = 0; blockZ < chunkSize; blockZ++) {
                for (byte blockY = 0; blockY < chunkSize; blockY++) {
                    ushort blockID = GetBlock(blockX, blockY, blockZ);
                    if (blockID != 0) {
                        facesToAdd.Clear();

                        for (int direction = 0; direction < 6; direction++) {
                            FaceDirection faceDirection = (FaceDirection)direction;
                            switch (faceDirection) {
                                case FaceDirection.RIGHT:
                                    if (blockX < chunkSize - 1) {
                                        if (GetBlock((byte)(blockX + 1), blockY, blockZ) == 0) {
                                            facesToAdd[3] = true;
                                        }
                                    } else if (blockX == chunkSize - 1 && positiveXChunk.isGenerated) {
                                        if (positiveXChunk.GetBlock(0, blockY, blockZ) == 0) {
                                            facesToAdd[3] = true;
                                        }
                                    }
                                    break;
                                case FaceDirection.LEFT:
                                    if (blockX > 0) {
                                        if (GetBlock((byte)(blockX - 1), blockY, blockZ) == 0) {
                                            facesToAdd[2] = true;
                                        }
                                    } else if (blockX == 0 && negativeXChunk.isGenerated) {
                                        if (negativeXChunk.GetBlock((byte)(chunkSize - 1), blockY, blockZ) == 0) {
                                            facesToAdd[2] = true;
                                        }
                                    }
                                    break;
                                case FaceDirection.TOP:
                                    if (blockY < chunkSize - 1) {
                                        if (GetBlock(blockX, (byte)(blockY + 1), blockZ) == 0) {
                                            facesToAdd[4] = true;
                                        }
                                    } else if (blockY == chunkSize - 1 && positiveYChunk.isGenerated) {
                                        if (positiveYChunk.GetBlock(blockX, 0, blockZ) == 0) {
                                            facesToAdd[4] = true;
                                        }
                                    }
                                    break;
                                case FaceDirection.BOTTOM:
                                    if (blockY > 0) {
                                        if (GetBlock(blockX, (byte)(blockY - 1), blockZ) == 0) {
                                            facesToAdd[5] = true;
                                        }
                                    } else if (blockY == 0 && negativeYChunk.isGenerated) {
                                        if (negativeYChunk.GetBlock(blockX, (byte)(chunkSize - 1), blockZ) == 0) {
                                            facesToAdd[5] = true;
                                        }
                                    }
                                    break;
                                case FaceDirection.BACK:
                                    if (blockZ < chunkSize - 1) {
                                        if (GetBlock(blockX, blockY, (byte)(blockZ + 1)) == 0) {
                                            facesToAdd[1] = true;
                                        }
                                    } else if (blockZ == chunkSize - 1 && positiveZChunk.isGenerated) {
                                        if (positiveZChunk.GetBlock(blockX, blockY, 0) == 0) {
                                            facesToAdd[1] = true;
                                        }
                                    }
                                    break;
                                case FaceDirection.FRONT:
                                    if (blockZ > 0) {
                                        if (GetBlock(blockX, blockY, (byte)(blockZ - 1)) == 0) {
                                            facesToAdd[0] = true;
                                        }
                                    } else if (blockZ == 0 && negativeZChunk.isGenerated) {
                                        if (negativeZChunk.GetBlock(blockX, blockY, (byte)(chunkSize - 1)) == 0) {
                                            facesToAdd[0] = true;
                                        }
                                    }
                                    break;
                            }
                        }

                        BlockDefinition blockDefinition = assetManager.GetBlockDefinition(blockID);
                        BlockRenderableComponent renderableComponent = archWorld.Get<BlockRenderableComponent>(blockDefinition.definition);
                        renderableComponent.AddBlockMesh(facesToAdd, new FullBlockPosition(new IntVector3(blockX, blockY, blockZ), new IntVector3(chunkX, chunkY, chunkZ)), vertices, indices);
                    }
                }
            }
        }

        if (vertices.Count > 0) {
            dirtyBuffers = true;
        }

        isMeshed = true;
        chunkGenerationState = 3;
        stopwatch.Stop();
        //KINFO("Took " + stopwatch.Elapsed.TotalMilliseconds + "ms to generate mesh for chunk");
        isMeshing = false;
        return hasMesh;
    }

    public void GenerateHeightmap() {
        OVERRIDE_LOG_NAME("Chunk Heightmap Generation");

        if (!isGenerated) {
            KWARN("Tried to generate heightmap for ungenerated chunk " + new IntVector3(chunkX, chunkY, chunkZ));
        }

        for (int blockX = 0; blockX < chunkSize; blockX++) {
            for (int blockZ = 0; blockZ < chunkSize; blockZ++) {
                bool foundLevel = false;
                for (int blockY = chunkSize - 1; blockY >= 0 && foundLevel == false; blockY--) {
                    if (GetBlock(blockX, blockY, blockZ) != 0) {
                        if (blockY == chunkSize - 1) {
                            heightmap.heightmap[blockX, blockZ] = 0;
                            heightmap.heightmapMask[blockX, blockZ] = true;
                        } else {
                            heightmap.heightmap[blockX, blockZ] = (byte)(blockY + 1);
                        }

                        foundLevel = true;
                    }
                }

                if (!foundLevel) {
                    heightmap.heightmap[blockX, blockZ] = 0;
                }
            }
        }
    }

    public int GetHeightmapLevelAt(int blockX, int blockZ) {
        if (!isGenerated) {
            return -2;
        }

        if (heightmap.heightmapMask[blockX, blockZ]) {
            return chunkSize;
        } else {
            return heightmap.heightmap[blockX, blockZ] - 1;
        }
    }

    public string GetImGuiText() {
        return "Chunk, position: " + new IntVector3(chunkX, chunkY, chunkZ) + ", generation state: " + chunkGenerationState + ", is generated, meshed, real: {" + isGenerated + ", " + isMeshed + ", " + isReal + "}, total blocks: {" + totalBlocks + "}, is real: {" + isReal + "}";
    }

    public void RecalculateFullness() {
        isFull = (totalBlocks == chunkVolume);
        if (!isFull) {
            isEmpty = (totalBlocks == 0);
        } else {
            isEmpty = false;
        }
    }

    public bool SetBlock(IntVector3 blockPosition, ushort newBlockID) {
        ushort originalBlockID = GetBlock(blockPosition);
        if ((originalBlockID == 0) ^ newBlockID == 0) {
            if (newBlockID == 0) {
                totalBlocks--;
            } else {
                totalBlocks++;
            }
            RecalculateFullness();
        }
        if (originalBlockID == newBlockID) {
            KCRITICAL("Just replaced a block at chunk position " + new IntVector3(chunkX, chunkY, chunkZ) + " and block position " + blockPosition + " with with a new identical block \"" + newBlockID + "\". This should currently be impossible, please report a bug if you encounter this, thanks");
            return false;
        }
        paletteIndices[GetBlockPositionIndex(blockPosition)] = TryAddPalette(newBlockID);
        return true;
    }

    public ushort GetBlockPaletteIndex(int blockX, int blockY, int blockZ) {
        return paletteIndices[GetBlockPositionIndex(blockX, blockY, blockZ)];
    }

    public ushort GetBlockPaletteIndex(IntVector3 blockPosition) {
        return paletteIndices[GetBlockPositionIndex(blockPosition.X, blockPosition.Y, blockPosition.Z)];
    }

    public ushort GetBlock(int blockX, int blockY, int blockZ) {
        ushort paletteIndex = GetBlockPaletteIndex(blockX, blockY, blockZ);
        if (paletteIndex >= blockPalette.Count) {
            KBREAK();
        }
        return blockPalette[paletteIndex];
    }

    public ushort GetBlock(IntVector3 blockPosition) {
        ushort paletteIndex = GetBlockPaletteIndex(blockPosition);
        if (paletteIndex >= blockPalette.Count) {
            KBREAK();
        }
        return blockPalette[paletteIndex];
    }

    public ushort GetBlock(ushort paletteIndex) {
        if (paletteIndex >= blockPalette.Count) {
            KBREAK();
        }
        return blockPalette[paletteIndex];
    }

    public ushort[] GetBlockPalette() {
        return blockPalette.ToArray();
    }

    public ushort[] GetPaletteIndices() {
        return paletteIndices;
    }

    public void LoadChunkData(ushort[] newBlockPalette, ushort[] newPaletteIndices) {
        foreach (ushort paletteIndex in newPaletteIndices) {
            if (paletteIndex != 0) {
                totalBlocks++;
            }
        }
        blockPalette.AddRange(newBlockPalette.AsSpan(1));

        paletteIndices = newPaletteIndices;
        isGenerated = true;
        chunkGenerationState = 2;
        RecalculateFullness();
    }

    public bool IsGenerated() {
        return isGenerated;
    }

    public bool IsMeshed() {
        return isMeshed;
    }

    public bool IsGenerating() {
        return isGenerating;
    }

    public bool IsMeshing() {
        return isMeshing;
    }

    public int GetGenerationState() {
        return chunkGenerationState;
    }

    public bool IsEmpty() {
        return isEmpty;
    }

    public bool IsFull() {
        return isFull;
    }

    public int GetTotalBlocks() {
        return (int)totalBlocks;
    }

    public bool IsVisibleBasic() {
        if (!isReal || !isGenerated || !isFull) {
            return true;
        }

        Chunk positiveXChunk = (Chunk)chunkHandler.GetChunk(chunkX + 1, chunkY, chunkZ, false);
        Chunk negativeXChunk = (Chunk)chunkHandler.GetChunk(chunkX - 1, chunkY, chunkZ, false);
        Chunk positiveYChunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY + 1, chunkZ, false);
        Chunk negativeYChunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY - 1, chunkZ, false);
        Chunk positiveZChunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY, chunkZ + 1, false);
        Chunk negativeZChunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY, chunkZ - 1, false);

        return positiveXChunk.isFull && negativeXChunk.isFull && positiveYChunk.isFull && negativeYChunk.isFull && positiveZChunk.isFull && negativeZChunk.isFull;
    }

    public bool IsMeshable() {
        if (!isReal || !isGenerated /*|| isEmpty */|| isMeshed) {
            return false;
        } else {
            Chunk positiveXChunk = (Chunk)chunkHandler.GetChunk(chunkX + 1, chunkY, chunkZ, false);
            Chunk negativeXChunk = (Chunk)chunkHandler.GetChunk(chunkX - 1, chunkY, chunkZ, false);
            Chunk positiveYChunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY + 1, chunkZ, false);
            Chunk negativeYChunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY - 1, chunkZ, false);
            Chunk positiveZChunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY, chunkZ + 1, false);
            Chunk negativeZChunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY, chunkZ - 1, false);

            return positiveXChunk.isGenerated && negativeXChunk.isGenerated && positiveYChunk.isGenerated && negativeYChunk.isGenerated && positiveZChunk.isGenerated && negativeZChunk.isGenerated;
        }
    }

    public bool IsNeededForMeshing() {
        if (!isReal || !isGenerated) {
            KWARN("Trying to get whether chunk at position " + new IntVector3(chunkX, chunkY, chunkZ) + " is needed for meshing when it is not real or not generated, returning true just in case");
        }
        if (!isEmpty) {
            return true;
        }

        Chunk positiveXChunk = (Chunk)chunkHandler.GetChunk(chunkX + 1, chunkY, chunkZ, false);
        Chunk negativeXChunk = (Chunk)chunkHandler.GetChunk(chunkX - 1, chunkY, chunkZ, false);
        Chunk positiveYChunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY + 1, chunkZ, false);
        Chunk negativeYChunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY - 1, chunkZ, false);
        Chunk positiveZChunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY, chunkZ + 1, false);
        Chunk negativeZChunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY, chunkZ - 1, false);

        return !positiveXChunk.isEmpty || !negativeXChunk.isEmpty || !positiveYChunk.isEmpty || !negativeYChunk.isEmpty || !positiveZChunk.isEmpty || !negativeZChunk.isEmpty;
    }

    public bool IsReal() {
        return isReal;
    }

    public bool IsDirty() {
        return dirtyBuffers;
    }

    public void ReadyDestroy() {
        awaitingDestruction = true;
	}

    public bool IsAwaitingDestruction() {
        return awaitingDestruction;
	}

	private int GetBlockPositionIndex(IntVector3 position) {
        return position.Y + chunkSize * (position.Z + chunkSize * position.X);
    }

    private int GetBlockPositionIndex(int x, int y, int z) {
        return y + chunkSize * (z + chunkSize * x);
    }

    private ushort TryAddPalette(ushort blockID) {
        if (blocksToPaletteIndices.TryGetValue(blockID, out ushort paletteIndex)) {
            return paletteIndex;
        } else {
            blockPalette.Add(blockID);
            ushort newPaletteIndex = (ushort)(blockPalette.Count - 1);
            blocksToPaletteIndices.Add(blockID, newPaletteIndex);
            return newPaletteIndex;
        }
    }

    public ValueTuple<List<float>, List<ushort>> LiftMeshData() {
        List<float> chunkVertices = vertices;
        List<ushort> chunkIndices = indices;

        vertices = null;
        indices = null;

        dirtyBuffers = false;

        return new ValueTuple<List<float>, List<ushort>>(chunkVertices, chunkIndices);
    }

    public void Dispose() {
        totalChunks--;

        if (MetaHandler.GetGameType() == GameType.CLIENT && isMeshed) {
            ClientRenderer.UnloadChunkData(new IntVector3(chunkX, chunkY, chunkZ));
		}

		paletteIndices = null;
        blockVariants = null;
        blockStates = null;
	}

    public static void DisposeAll() {
        totalChunks = 0;

		assetManager = null;
		chunkHandler = null;
		archWorld = null;
	}

    public readonly struct GenerationNoises {
        public readonly FastNoiseLite terrainNoise;
        public readonly FastNoiseLite heightNoise;
        public readonly FastNoiseLite weirdNoise;
        public readonly FastNoiseLite temperatureNoise;
        public readonly FastNoiseLite humidityNoise;

        public GenerationNoises(FastNoiseLite terrainNoise, FastNoiseLite heightNoise, FastNoiseLite weirdNoise, FastNoiseLite temperatureNoise, FastNoiseLite humidityNoise) {
            this.terrainNoise = terrainNoise;
            this.heightNoise = heightNoise;
            this.weirdNoise = weirdNoise;
            this.temperatureNoise = temperatureNoise;
            this.humidityNoise = humidityNoise;
        }
    }
}