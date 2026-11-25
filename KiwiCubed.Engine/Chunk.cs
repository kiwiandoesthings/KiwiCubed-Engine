namespace KiwiCubed.Engine;

using ArchWorld = Arch.Core.World;
using KiwiCubed.Api;
using System.Buffers;
using System.Diagnostics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Block;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.Utils;

public class Chunk : IChunk, IDisposable {
    public struct ChunkHeightmap {
        public byte[,] heightmap = new byte[chunkSize, chunkSize];
        public bool[,] heightmapMask = new bool[chunkSize, chunkSize];

        public ChunkHeightmap() { }
    }

    public static int totalChunks { get; private set; }
    private readonly static uint samplesPerAxis = 8;
    private static AssetManager assetManager;
    private static ChunkHandler chunkHandler;
    private static ArchWorld archWorld;
    private static KLogger logger;
    private bool isReal = false;
    private bool awaitingDestruction = false;
    public int chunkX { get; }
    public int chunkY { get; }
    public int chunkZ { get; }
    private List<VertexTerrain> vertices;
    private List<ushort> indices;
    private List<ushort> blockPalette;
    private Dictionary<ushort, ushort> blocksToPaletteIndices;
    private ushort[] paletteIndices;
    private byte[] blockVariants;
    private byte[] blockStates;
    private ChunkHeightmap heightmap;
    private bool isMeshUploaded = false;
    private bool isMeshDirty = false;
    private bool isSaveDirty = false;
    private bool needsRemesh = false;
    private bool isGenerated = false;
    private bool isMeshed = false;
    private bool isGenerating = false;
    private bool isMeshing = false;
    private bool isEmpty = true;
    private bool isFull = false;
    private byte generationState = 0;
    private ushort totalBlocks = 0;

    public static void SetupChunks(ChunkHandler chunkHandler) {
        assetManager = (AssetManager)MetaHandler.Get<IAssetManager>();
        Chunk.chunkHandler = chunkHandler;
        archWorld = assetManager.GetArchWorld();
        logger = new KLogger("Chunk");
    }

    public Chunk(int x, int y, int z) {
        totalChunks++;
        chunkX = x;
        chunkY = y;
        chunkZ = z;

        // look into sparse storage
        blockPalette = [];
        paletteIndices = new ushort[chunkVolume];
        blocksToPaletteIndices = [];
        TryAddPalette(0);

        heightmap = new ChunkHeightmap();

        blockVariants = new byte[chunkVolume];
        blockStates = new byte[chunkVolume];
    }

    public void MakeReal() {
        totalChunks++;
        isReal = true;
    }

    public float[] GenerateDensities(int seed) {
        int totalSamplesPerAxis = (int)samplesPerAxis + 1;

        FastNoise terrainNoiseGenerator = FastNoise.FromEncodedNodeTree("KQkWAhYCFwkXCQ0AB@CkGBAsAAKBABAIXCQY@AD6RAgB@BE@AgD8Y@BQAQCKQksCQYAAEAcRggC@BDBvNzMw+FAMAACBBBAoEC@AgL8MChYCFwkNAAk@BJLAkpCf8CAAMAAIA/Cw@AEATAACAvxsAAKBAFAMAAIBCBAopAAE@BJIAAE@BCSwJCw@AHpEE@Dw@DMzM7M/Cx+Faz8TzcxsQBQD@CBMAAKDBGwAAcEIUAwAA+sMLAACWwxMAAIA/GwAAgL8E");

        float[] terrainSamples = ArrayPool<float>.Shared.Rent(totalSamplesPerAxis * totalSamplesPerAxis * totalSamplesPerAxis);

        int baseX = chunkX * chunkSize;
        int baseY = chunkY * chunkSize;
        int baseZ = chunkZ * chunkSize;

        int spacing = chunkSize / (int)samplesPerAxis;

        terrainNoiseGenerator.GenUniformGrid3D(terrainSamples, baseX, baseY, baseZ, totalSamplesPerAxis, totalSamplesPerAxis, totalSamplesPerAxis, spacing, spacing, spacing, seed);

        return terrainSamples;
    }

    public bool GenerateBlocks(ChunkGenerator generator, int seed) {
        Stopwatch stopwatch = Stopwatch.StartNew();

        if (isGenerated) {
            logger.WARN("Tried to generate blocks for chunk at {" + chunkX + ", " + chunkY + ", " + chunkZ + "} twice");
            return false;
        }
        isGenerating = true;

        int totalSamplesPerAxis = (int)samplesPerAxis + 1;
        int halfTotalSamplesPerAxis = (int)(samplesPerAxis / 2) + 1;

        FastNoise heightNoiseGenerator = FastNoise.FromEncodedNodeTree("C@BEhDE@DE");
        FastNoise weirdNoiseGenerator = FastNoise.FromEncodedNodeTree("Bh@DBA==");
        FastNoise temperatureNoiseGenerator = FastNoise.FromEncodedNodeTree("Bg@AHpEE@DE");
        FastNoise humidityNoiseGenerator = FastNoise.FromEncodedNodeTree("DQkG@BlkMQ@DQDXI/CPgs9CpdABA==");

        float[] terrainSamples = GenerateDensities(seed);
        float[] heightSamples = ArrayPool<float>.Shared.Rent(halfTotalSamplesPerAxis * halfTotalSamplesPerAxis);
        float[] weirdSamples = ArrayPool<float>.Shared.Rent(halfTotalSamplesPerAxis * halfTotalSamplesPerAxis);
        float[] temperatureSamples = ArrayPool<float>.Shared.Rent(totalSamplesPerAxis * totalSamplesPerAxis);
        float[] humiditySamples = ArrayPool<float>.Shared.Rent(totalSamplesPerAxis * totalSamplesPerAxis);

        int baseX = chunkX * chunkSize;
        int baseY = chunkY * chunkSize;
        int baseZ = chunkZ * chunkSize;

        int spacing = chunkSize / (int)samplesPerAxis;
        int doubleSpacing = spacing * 2;

        heightNoiseGenerator.GenUniformGrid2D(heightSamples, baseX, baseZ, halfTotalSamplesPerAxis, halfTotalSamplesPerAxis, doubleSpacing, doubleSpacing, seed + 1);
        weirdNoiseGenerator.GenUniformGrid2D(weirdSamples, baseX, baseZ, halfTotalSamplesPerAxis, halfTotalSamplesPerAxis, doubleSpacing, doubleSpacing, seed + 2);
        temperatureNoiseGenerator.GenUniformGrid2D(temperatureSamples, baseX, baseZ, halfTotalSamplesPerAxis, halfTotalSamplesPerAxis, doubleSpacing, doubleSpacing, seed + 3);
        humidityNoiseGenerator.GenUniformGrid2D(humiditySamples, baseX, baseZ, halfTotalSamplesPerAxis, halfTotalSamplesPerAxis, doubleSpacing, doubleSpacing, seed + 4);

        stopwatch.Stop();
        stopwatch = Stopwatch.StartNew();

        IntVector3 abovePosition = new IntVector3(chunkX, chunkY + 1, chunkZ);
        Chunk aboveChunk = (Chunk)chunkHandler.GetChunk(abovePosition, true);
        float[] aboveDensities = aboveChunk.GenerateDensities(seed);

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

                float height = GetInterpolatedValue2D(ref heightSamples, halfTotalSamplesPerAxis, halfSampleX, halfInterpolatedX, halfSampleZ, halfInterpolatedZ);
                float weird = GetInterpolatedValue2D(ref weirdSamples, halfTotalSamplesPerAxis, halfSampleX, halfInterpolatedX, halfSampleZ, halfInterpolatedZ);
                float temperature = GetInterpolatedValue2D(ref temperatureSamples, halfTotalSamplesPerAxis, halfSampleX, halfInterpolatedX, halfSampleZ, halfInterpolatedZ);
                float humidity = GetInterpolatedValue2D(ref humiditySamples, halfTotalSamplesPerAxis, halfSampleX, halfInterpolatedX, halfSampleZ, halfInterpolatedZ);

                float aboveBlockDensity = -1.0f;
                int blocksFromSurface = 0;

                for (int aboveY = 0; aboveY < chunkSize; aboveY++) {
                    int aboveSampleY = aboveY / spacing;
                    float aboveInterpolatedY = (aboveY % spacing) / (float)spacing;

                    float aboveDensity = GetInterpolatedValue(ref aboveDensities, totalSamplesPerAxis, sampleX, interpolatedX, aboveSampleY, aboveInterpolatedY, sampleZ, interpolatedZ);
                    float aboveWeightedDensity = GetWeightedDensity(aboveDensity, height, weird, baseY + chunkSize + aboveY);

                    if (aboveY == 0) {
                        aboveBlockDensity = aboveWeightedDensity;
                    }

                    if (aboveWeightedDensity > 0) {
                        blocksFromSurface++;
                    } else {
                        break;
                    }
                }

                for (int blockY = chunkSize - 1; blockY >= 0; blockY--) {
                    int sampleY = blockY / spacing;
                    float interpolatedY = (blockY % spacing) / (float)spacing;

                    float density = GetInterpolatedValue(ref terrainSamples, totalSamplesPerAxis, sampleX, interpolatedX, sampleY, interpolatedY, sampleZ, interpolatedZ);

                    int totalHeight = blockY + baseY;
                    float weightedDensity = GetWeightedDensity(density, height, weird, totalHeight);
                    if (weightedDensity <= 0) {
                        aboveBlockDensity = weightedDensity;
                        blocksFromSurface = 0;
                        continue;
                    }

                    BiomeModel biome = generator.GetClosestBiome(temperature, humidity, height);

                    if (aboveBlockDensity <= 0.0f) {
                        paletteIndices[GetBlockPositionIndex(blockX, blockY, blockZ)] = TryAddPalette(biome.topLayerID);
                        blocksFromSurface++;
                    } else if (blocksFromSurface <= 2) {
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

        ArrayPool<float>.Shared.Return(aboveDensities);
        ArrayPool<float>.Shared.Return(terrainSamples);
        ArrayPool<float>.Shared.Return(heightSamples);
        ArrayPool<float>.Shared.Return(weirdSamples);
        ArrayPool<float>.Shared.Return(temperatureSamples);
        ArrayPool<float>.Shared.Return(humiditySamples);

        isGenerated = true;
        generationState = 2;

        GenerateHeightmap();
        RecalculateFullness();

        stopwatch.Stop();
        isGenerating = false;
        isSaveDirty = true;
        return true;
    }

    private static float GetWeightedDensity(float density, float height, float weirdness, int totalHeight) {
        //float weightedDensity = density - (totalHeight * weirdness * 0.05f);
        float weightedDensity = (density * 10.0f * weirdness) - totalHeight + (height * 320.0f);

        return weightedDensity;
    }

    private static float GetInterpolatedValue(ref float[] samples, int sideLength, int sampleX, float interpolatedX, int sampleY, float interpolatedY, int sampleZ, float interpolatedZ) {
        float bottomBackLeft = samples[sampleX + sideLength * (sampleY + sideLength * sampleZ)];
        float bottomBackRight = samples[(sampleX + 1) + sideLength * (sampleY + sideLength * sampleZ)];
        float bottomFrontLeft = samples[sampleX + sideLength * (sampleY + sideLength * (sampleZ + 1))];
        float bottomFrontRight = samples[(sampleX + 1) + sideLength * (sampleY + sideLength * (sampleZ + 1))];

        float topBackLeft = samples[sampleX + sideLength * ((sampleY + 1) + sideLength * sampleZ)];
        float topBackRight = samples[(sampleX + 1) + sideLength * ((sampleY + 1) + sideLength * sampleZ)];
        float topFrontLeft = samples[sampleX + sideLength * ((sampleY + 1) + sideLength * (sampleZ + 1))];
        float topFrontRight = samples[(sampleX + 1) + sideLength * ((sampleY + 1) + sideLength * (sampleZ + 1))];

        float interpolatedBottomBack = Lerp(bottomBackLeft, bottomBackRight, interpolatedX);
        float interpolatedBottomFront = Lerp(bottomFrontLeft, bottomFrontRight, interpolatedX);

        float interpolatedTopBack = Lerp(topBackLeft, topBackRight, interpolatedX);
        float interpolatedTopFront = Lerp(topFrontLeft, topFrontRight, interpolatedX);

        float interpolatedBottomTotal = Lerp(interpolatedBottomBack, interpolatedBottomFront, interpolatedZ);
        float interpolatedTopTotal = Lerp(interpolatedTopBack, interpolatedTopFront, interpolatedZ);

        return Lerp(interpolatedBottomTotal, interpolatedTopTotal, interpolatedY);
    }

    private static float GetInterpolatedValue2D(ref float[] samples, int sideLength, int sampleX, float interpolatedX, int sampleZ, float interpolatedZ) {
        float sample00 = samples[sampleX + sampleZ * sideLength];
        float sample10 = samples[(sampleX + 1) + sampleZ * sideLength];
        float sample01 = samples[sampleX + (sampleZ + 1) * sideLength];
        float sample11 = samples[(sampleX + 1) + (sampleZ + 1) * sideLength];

        float interpolatedTerrainSample0 = Lerp(sample00, sample10, interpolatedX);
        float interpolatedTerrainSample1 = Lerp(sample01, sample11, interpolatedX);

        return Lerp(interpolatedTerrainSample0, interpolatedTerrainSample1, interpolatedZ);
    }

    public bool GenerateMesh(bool forceRemesh) { // im pretty sure this forceRemesh should be deprecated (at least in the engine code, could be left for mods) in exchange for making sure remeshes happen on their own automatically
        Stopwatch stopwatch = Stopwatch.StartNew();

        IntVector3 chunkPosition = new IntVector3(chunkX, chunkY, chunkZ);
        if (!forceRemesh && isMeshed && !needsRemesh) {
            logger.ERR("Tried to mesh already meshed chunk at position " + chunkPosition + " when it didn't need a remesh");
            return false;
        }
        if (!isGenerated) {
            logger.ERR("Tried to mesh ungenerated chunk at position " + chunkPosition);
            return false;
        }
        if (IsEmpty()) {
            vertices = [];
            indices = [];
            isMeshDirty = true;
            needsRemesh = false;
            isMeshed = true;
            generationState = 3;
            return true;
        }
        isMeshing = true;

        Chunk[] neighborChunks = new Chunk[27];
        for (int x = -1; x <= 1; x++) {
            for (int y = -1; y <= 1; y++) {
                for (int z = -1; z <= 1; z++) {
                    int index = (x + 1) * 9 + (y + 1) * 3 + (z + 1);
                    neighborChunks[index] = (Chunk)chunkHandler.GetChunk(chunkX + x, chunkY + y, chunkZ + z, false);
                }
            }
        }

        ref Chunk negativeXChunk = ref neighborChunks[0 * 9 + 1 * 3 + 1];
        ref Chunk positiveXChunk = ref neighborChunks[2 * 9 + 1 * 3 + 1];
        ref Chunk negativeYChunk = ref neighborChunks[1 * 9 + 0 * 3 + 1];
        ref Chunk positiveYChunk = ref neighborChunks[1 * 9 + 2 * 3 + 1];
        ref Chunk negativeZChunk = ref neighborChunks[1 * 9 + 1 * 3 + 0];
        ref Chunk positiveZChunk = ref neighborChunks[1 * 9 + 1 * 3 + 2];

        vertices = [];
        indices = [];

        double total = 0.0d;

        Span<bool> facesToAdd = stackalloc bool[6];
        Span<byte> faceAO = stackalloc byte[24];

        bool hasMesh = false;
        for (byte blockX = 0; blockX < chunkSize; blockX++) {
            for (byte blockZ = 0; blockZ < chunkSize; blockZ++) {
                for (byte blockY = 0; blockY < chunkSize; blockY++) {
                    ushort blockID = GetBlock(blockX, blockY, blockZ);
                    if (blockID != 0) {
                        facesToAdd.Clear();

                        for (int direction = 1; direction < 7; direction++) {
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
                                case FaceDirection.FRONT:
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
                                case FaceDirection.BACK:
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
                        for (int face = 0; face < 6; face++) {
                            if (!facesToAdd[face]) {
                                continue;
                            }
                            Stopwatch stopwatch2 = Stopwatch.StartNew();
                            bool Solid(int offsetX, int offsetY, int offsetZ) => IsBlockSolid(blockX + offsetX, blockY + offsetY, blockZ + offsetZ, neighborChunks);
                            int offset = face * 4;
                            switch (face) {
                                case 0:
                                    faceAO[offset + 0] = CalculateVertexAO(Solid(-1, 0, -1), Solid(0, -1, -1), Solid(-1, -1, -1));
                                    faceAO[offset + 1] = CalculateVertexAO(Solid(1, 0, -1), Solid(0, -1, -1), Solid(1, -1, -1));
                                    faceAO[offset + 2] = CalculateVertexAO(Solid(1, 0, -1), Solid(0, 1, -1), Solid(1, 1, -1));
                                    faceAO[offset + 3] = CalculateVertexAO(Solid(-1, 0, -1), Solid(0, 1, -1), Solid(-1, 1, -1));
                                    break;
                                case 1:
                                    faceAO[offset + 0] = CalculateVertexAO(Solid(1, 0, 1), Solid(0, -1, 1), Solid(1, -1, 1));
                                    faceAO[offset + 1] = CalculateVertexAO(Solid(-1, 0, 1), Solid(0, -1, 1), Solid(-1, -1, 1));
                                    faceAO[offset + 2] = CalculateVertexAO(Solid(-1, 0, 1), Solid(0, 1, 1), Solid(-1, 1, 1));
                                    faceAO[offset + 3] = CalculateVertexAO(Solid(1, 0, 1), Solid(0, 1, 1), Solid(1, 1, 1));
                                    break;
                                case 2:
                                    faceAO[offset + 0] = CalculateVertexAO(Solid(-1, 0, 1), Solid(-1, -1, 0), Solid(-1, -1, 1));
                                    faceAO[offset + 1] = CalculateVertexAO(Solid(-1, 0, -1), Solid(-1, -1, 0), Solid(-1, -1, -1));
                                    faceAO[offset + 2] = CalculateVertexAO(Solid(-1, 0, -1), Solid(-1, 1, 0), Solid(-1, 1, -1));
                                    faceAO[offset + 3] = CalculateVertexAO(Solid(-1, 0, 1), Solid(-1, 1, 0), Solid(-1, 1, 1));
                                    break;
                                case 3:
                                    faceAO[offset + 0] = CalculateVertexAO(Solid(1, 0, -1), Solid(1, -1, 0), Solid(1, -1, -1));
                                    faceAO[offset + 1] = CalculateVertexAO(Solid(1, 0, 1), Solid(1, -1, 0), Solid(1, -1, 1));
                                    faceAO[offset + 2] = CalculateVertexAO(Solid(1, 0, 1), Solid(1, 1, 0), Solid(1, 1, 1));
                                    faceAO[offset + 3] = CalculateVertexAO(Solid(1, 0, -1), Solid(1, 1, 0), Solid(1, 1, -1));
                                    break;
                                case 4:
                                    faceAO[offset + 0] = CalculateVertexAO(Solid(-1, 1, 0), Solid(0, 1, -1), Solid(-1, 1, -1));
                                    faceAO[offset + 1] = CalculateVertexAO(Solid(1, 1, 0), Solid(0, 1, -1), Solid(1, 1, -1));
                                    faceAO[offset + 2] = CalculateVertexAO(Solid(1, 1, 0), Solid(0, 1, 1), Solid(1, 1, 1));
                                    faceAO[offset + 3] = CalculateVertexAO(Solid(-1, 1, 0), Solid(0, 1, 1), Solid(-1, 1, 1));
                                    break;
                                case 5:
                                    faceAO[offset + 0] = CalculateVertexAO(Solid(-1, -1, 0), Solid(0, -1, 1), Solid(-1, -1, 1));
                                    faceAO[offset + 1] = CalculateVertexAO(Solid(1, -1, 0), Solid(0, -1, 1), Solid(1, -1, 1));
                                    faceAO[offset + 2] = CalculateVertexAO(Solid(1, -1, 0), Solid(0, -1, -1), Solid(1, -1, -1));
                                    faceAO[offset + 3] = CalculateVertexAO(Solid(-1, -1, 0), Solid(0, -1, -1), Solid(-1, -1, -1));
                                    break;
                            }
                            total += stopwatch2.Elapsed.TotalMilliseconds;
                        }
                        renderableComponent.AddBlockMesh(facesToAdd, faceAO, new FullBlockPosition(new IntVector3(blockX, blockY, blockZ), new IntVector3(chunkX, chunkY, chunkZ)), vertices, indices);
                    }
                }
            }
        }

        //Console.WriteLine("took " + total + " of " + stopwatch.Elapsed.TotalMilliseconds + "ms on ao");

        if (vertices.Count > 0) {
            hasMesh = true;
        }

        isMeshDirty = true;
        needsRemesh = false;
        isMeshed = true;
        generationState = 3;
        stopwatch.Stop();
        //logger.INFO("Took " + stopwatch.Elapsed.TotalMilliseconds + "ms to generate mesh for chunk with " + totalBlocks + " blocks");
        isMeshing = false;

        return hasMesh;
    }

    private static byte CalculateVertexAO(bool side1, bool side2, bool corner) {
        if (side1 && side2) {
            return 0;
        }
        return (byte)(3 - (side1 ? 1 : 0) - (side2 ? 1 : 0) - (corner ? 1 : 0));
    }

    private bool IsBlockSolid(int blockX, int blockY, int blockZ, Chunk[] neighborChunks) {
        if (blockX >= 0 && blockX < chunkSize && blockY >= 0 && blockY < chunkSize && blockZ >= 0 && blockZ < chunkSize) {
            return GetBlock((byte)blockX, (byte)blockY, (byte)blockZ) != 0;
        }

        int targetChunkX = FloorDiv(blockX, chunkSize);
        int targetChunkY = FloorDiv(blockY, chunkSize);
        int targetChunkZ = FloorDiv(blockZ, chunkSize);

        int targetX = FloorMod(blockX, chunkSize);
        int targetY = FloorMod(blockY, chunkSize);
        int targetZ = FloorMod(blockZ, chunkSize);
        IntVector3 targetBlock = new IntVector3(targetX, targetY, targetZ);

        int cacheX = targetChunkX + 1;
        int cacheY = targetChunkY + 1;
        int cacheZ = targetChunkZ + 1;

        if (cacheX >= 0 && cacheX < 3 && cacheY >= 0 && cacheY < 3 && cacheZ >= 0 && cacheZ < 3) {
            int index = cacheX * 9 + cacheY * 3 + cacheZ;
            Chunk targetChunk = neighborChunks[index];
            if (targetChunk != null && targetChunk.isGenerated) {
                return targetChunk.GetBlock(targetBlock) != 0;
            }
        }

        return false;
    }

    public void GenerateHeightmap() {
        if (!isGenerated) {
            logger.WARN("Tried to generate heightmap for ungenerated chunk " + new IntVector3(chunkX, chunkY, chunkZ));
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
        if (!(isReal && isGenerated && !isEmpty && (!isMeshed || needsRemesh))) {
            return false;
        }

        Chunk positiveXChunk = (Chunk)chunkHandler.GetChunk(chunkX + 1, chunkY, chunkZ, false);
        Chunk negativeXChunk = (Chunk)chunkHandler.GetChunk(chunkX - 1, chunkY, chunkZ, false);
        Chunk positiveYChunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY + 1, chunkZ, false);
        Chunk negativeYChunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY - 1, chunkZ, false);
        Chunk positiveZChunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY, chunkZ + 1, false);
        Chunk negativeZChunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY, chunkZ - 1, false);

        return positiveXChunk.isGenerated && negativeXChunk.isGenerated && positiveYChunk.isGenerated && negativeYChunk.isGenerated && positiveZChunk.isGenerated && negativeZChunk.isGenerated;
    }

    public bool IsNeededForMeshing() {
        if (!isReal || !isGenerated) {
            logger.WARN("Trying to get whether chunk at position " + new IntVector3(chunkX, chunkY, chunkZ) + " is needed for meshing when it is not real or not generated, returning true just in case");
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

    public ValueTuple<List<VertexTerrain>, List<ushort>> LiftMeshData() {
        List<VertexTerrain> chunkVertices = vertices;
        List<ushort> chunkIndices = indices;

        vertices = null;
        indices = null;

        isMeshUploaded = true;
        isMeshDirty = false;

        return new ValueTuple<List<VertexTerrain>, List<ushort>>(chunkVertices, chunkIndices);
    }

    public string GetImGuiText() {
        return "Chunk, position: " + new IntVector3(chunkX, chunkY, chunkZ) + ", generation state: " + generationState + ", is generated, meshed, real: {" + isGenerated + ", " + isMeshed + ", " + isReal + "}, total blocks: {" + totalBlocks + "}, is real: {" + isReal + "}";
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
        if (originalBlockID == newBlockID) {
            logger.CRITICAL("Tried to replace a block at chunk position " + new IntVector3(chunkX, chunkY, chunkZ) + " and block position " + blockPosition + " with with a new identical block \"" + newBlockID + "\". This should currently be impossible, please report a bug if you encounter this, thanks");
            return false;
        }
        if ((originalBlockID == 0) ^ newBlockID == 0) {
            if (newBlockID == 0) {
                totalBlocks--;
            } else {
                totalBlocks++;
            }
            RecalculateFullness();
        }
        paletteIndices[GetBlockPositionIndex(blockPosition)] = TryAddPalette(newBlockID);

        needsRemesh = true;
        isSaveDirty = true;

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
            logger.BREAK();
        }
        return blockPalette[paletteIndex];
    }

    public ushort GetBlock(IntVector3 blockPosition) {
        ushort paletteIndex = GetBlockPaletteIndex(blockPosition);
        if (paletteIndex >= blockPalette.Count) {
            logger.BREAK();
        }
        return blockPalette[paletteIndex];
    }

    public ushort GetBlock(ushort paletteIndex) {
        if (paletteIndex >= blockPalette.Count) {
            logger.BREAK();
        }
        return blockPalette[paletteIndex];
    }

    public void LoadChunkData(ushort[] newBlockPalette, ushort[] newPaletteIndices, ushort totalBlocks) {
        blockPalette.AddRange(newBlockPalette.AsSpan(1));

        paletteIndices = newPaletteIndices;
        this.totalBlocks = totalBlocks;
        isGenerated = true;
        needsRemesh = true;
        generationState = 2;
        RecalculateFullness();
        GenerateHeightmap();
    }

    public IntVector3 GetPosition() {
        return new IntVector3(chunkX, chunkY, chunkZ);
    }

    public ushort[] GetBlockPalette() {
        return [.. blockPalette];
    }

    public ushort[] GetPaletteIndices() {
        return paletteIndices;
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
        return generationState;
    }

    public bool IsEmpty() {
        return isEmpty;
    }

    public bool IsFull() {
        return isFull;
    }

    public ushort GetTotalBlocks() {
        return totalBlocks;
    }

    public bool IsReal() {
        return isReal;
    }

    public bool IsMeshUploaded() {
        return isMeshUploaded;
    }

    public void SetMeshUploadStatus(bool isUploaded) {
        isMeshUploaded = isUploaded;
    }

    public bool IsMeshDirty() {
        return isMeshDirty;
    }

    public bool IsSaveDirty() {
        return isSaveDirty;
    }

    public void SetSaved() {
        isSaveDirty = false;
    }

    public bool NeedsRemesh() {
        return needsRemesh;
    }

    public void ReadyDestroy() {
        awaitingDestruction = true;
	}

    public bool IsAwaitingDestruction() {
        return awaitingDestruction;
	}

	private static int GetBlockPositionIndex(IntVector3 position) {
        return position.Y + chunkSize * (position.Z + chunkSize * position.X);
    }

    private static int GetBlockPositionIndex(int x, int y, int z) {
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

    public void Dispose() {
        totalChunks--;

        if (MetaHandler.GetGameType() == GameType.CLIENT && isMeshUploaded) {
            ClientRenderer.UnloadChunkData(this);
        }

        blockPalette = null;
        blocksToPaletteIndices = null;
		paletteIndices = null;
        blockVariants = null;
        blockStates = null;
        
        isGenerated = false;
        isMeshed = false;
        isEmpty = true;
        isFull = false;
        generationState = 0;
        totalBlocks = 0;
	}

    public static void DisposeAll() {
        totalChunks = 0;

		assetManager = null;
		chunkHandler = null;
		archWorld = null;
	}
}