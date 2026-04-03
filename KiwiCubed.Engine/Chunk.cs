namespace KiwiCubed.Engine;

using ImGuiNET;
using KiwiCubed.Api;
using Silk.NET.OpenGL;
using System.Diagnostics;
using System.Runtime.InteropServices;

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

    private static GL gl = SystemsManager.Get<GL>();
    private static int totalChunks = 0;
    private static uint samplesPerAxis = 8;
    private bool isReal = false;
    private bool awaitingDestruction = false;
    public int chunkX { get; }
    public int chunkY { get; }
    public int chunkZ { get; }
    private AssetManager assetManager = (AssetManager)SystemsManager.Get<IAssetManager>();
    private ChunkHandler chunkHandler;
    private List<float> vertices = new List<float>();
    private List<ushort> indices = new List<ushort>();
    private List<Block> blockPalette;
    private ushort[] paletteIndices;
    private Dictionary<Block, ushort> blocksToPaletteIndices;
    private byte[] blockVariants;
    private byte[] blockStates;
    private BiomeModel[,] biomes;
    private ChunkHeightmap heightmap;
    private uint vertexArray = 0;
    private uint vertexBuffer = 0;
    private uint indexBuffer = 0;
    private bool dirtyBuffers = true;
    private bool shouldGenerate = true;
    private bool shouldRender = true;
    private bool renderComponentsSetup = false;
    private bool isGenerated = false;
    private bool isMeshed = false;
    private bool isGenerating = false;
    private bool isMeshing = false;
    private bool isEmpty = true;
    private bool isFull = false;
    private byte chunkGenerationState = 0;
    private byte blockGenerationState = 0;
    private ushort totalBlocks = 0;

    public Chunk(int x, int y, int z, ChunkHandler chunkHandler) {
        totalChunks++;
        isReal = true;
        chunkX = x;
        chunkY = y;
        chunkZ = z;
        this.chunkHandler = chunkHandler;

        blockPalette = new();
        paletteIndices = new ushort[chunkVolume];
        blocksToPaletteIndices = new();

        heightmap = new ChunkHeightmap();

        blockPalette.Add(assetManager.GetBlock(0));
        blockVariants = new byte[chunkVolume];
        blockStates = new byte[chunkVolume];
        biomes = new BiomeModel[chunkSize / 4, chunkSize / 4];
    }

    public unsafe bool SetupRenderComponents() {
        if (renderComponentsSetup) {
            KERR("Tried to setup render components twice for chunk at " + new IntVector3(chunkX, chunkY, chunkZ));
            return false;
        }

        vertexArray = gl.GenVertexArray();
        vertexBuffer = gl.GenBuffer();
        indexBuffer = gl.GenBuffer();

        uint stride = 5 * sizeof(float);
        gl.BindVertexArray(vertexArray);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBuffer);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(sizeof(float) * 3));
        gl.EnableVertexAttribArray(0);
        gl.EnableVertexAttribArray(1);
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, indexBuffer);

        UpdateBuffers();

        renderComponentsSetup = true;
        chunkGenerationState = 1;

        return true;
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
        KINFO("--- Took " + stopwatch.Elapsed.TotalMilliseconds + "ms to sample noises for chunk ---");
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
                        paletteIndices[GetBlockPositionIndex(blockX, blockY, blockZ)] = AddBlockToPalette(biome.topLayer);
                        blocksFromSurface++;
                    } else if (aboveBlockDensity <= 0.5f) {
                        paletteIndices[GetBlockPositionIndex(blockX, blockY, blockZ)] = AddBlockToPalette(biome.soilLayer);
                        blocksFromSurface++;
					} else {
                        paletteIndices[GetBlockPositionIndex(blockX, blockY, blockZ)] = AddBlockToPalette(biome.groundLayer);
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
        KINFO("--- Took " + stopwatch.Elapsed.TotalMilliseconds + "ms to add chunk blocks and generate heightmap --- ");
        isGenerating = false;
        return true;
    }

    private float GetWeightedDensity(float density, float height, float weirdness, int totalHeight) {
        //float baseDensity = density - (totalHeight * weirdness * 0.05f);
        float baseDensity = (density * 10.0f * weirdness) - totalHeight + (height * 32.0f * 4.0f);
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

        vertices.Clear();
        indices.Clear();
        List<FaceDirection> facesToAdd = new List<FaceDirection>(6);

        bool hasMesh = false;
        for (byte blockX = 0; blockX < chunkSize; blockX++) {
            for (byte blockZ = 0; blockZ < chunkSize; blockZ++) {
                for (byte blockY = 0; blockY < chunkSize; blockY++) {
                    if (!GetBlock(blockX, blockY, blockZ).IsAir()) {
                        facesToAdd.Clear();

                        for (int direction = 0; direction < 6; direction++) {
                            FaceDirection faceDirection = (FaceDirection)direction;
                            switch (faceDirection) {
                                case FaceDirection.RIGHT:
                                    if (blockX < chunkSize - 1) {
                                        if (GetBlock((byte)(blockX + 1), blockY, blockZ).IsAir()) {
                                            facesToAdd.Add(FaceDirection.RIGHT);
                                        }
                                    } else if (blockX == chunkSize - 1 && positiveXChunk.isGenerated) {
                                        if (positiveXChunk.GetBlock(0, blockY, blockZ).IsAir()) {
                                            facesToAdd.Add(FaceDirection.RIGHT);
                                        }
                                    }
                                    break;
                                case FaceDirection.LEFT:
                                    if (blockX > 0) {
                                        if (GetBlock((byte)(blockX - 1), blockY, blockZ).IsAir()) {
                                            facesToAdd.Add(FaceDirection.LEFT);
                                        }
                                    } else if (blockX == 0 && negativeXChunk.isGenerated) {
                                        if (negativeXChunk.GetBlock((byte)(chunkSize - 1), blockY, blockZ).IsAir()) {
                                            facesToAdd.Add(FaceDirection.LEFT);
                                        }
                                    }
                                    break;
                                case FaceDirection.TOP:
                                    if (blockY < chunkSize - 1) {
                                        if (GetBlock(blockX, (byte)(blockY + 1), blockZ).IsAir()) {
                                            facesToAdd.Add(FaceDirection.TOP);
                                        }
                                    } else if (blockY == chunkSize - 1 && positiveYChunk.isGenerated) {
                                        if (positiveYChunk.GetBlock(blockX, 0, blockZ).IsAir()) {
                                            facesToAdd.Add(FaceDirection.TOP);
                                        }
                                    }
                                    break;
                                case FaceDirection.BOTTOM:
                                    if (blockY > 0) {
                                        if (GetBlock(blockX, (byte)(blockY - 1), blockZ).IsAir()) {
                                            facesToAdd.Add(FaceDirection.BOTTOM);
                                        }
                                    } else if (blockY == 0 && negativeYChunk.isGenerated) {
                                        if (negativeYChunk.GetBlock(blockX, (byte)(chunkSize - 1), blockZ).IsAir()) {
                                            facesToAdd.Add(FaceDirection.BOTTOM);
                                        }
                                    }
                                    break;
                                case FaceDirection.BACK:
                                    if (blockZ < chunkSize - 1) {
                                        if (GetBlock(blockX, blockY, (byte)(blockZ + 1)).IsAir()) {
                                            facesToAdd.Add(FaceDirection.BACK);
                                        }
                                    } else if (blockZ == chunkSize - 1 && positiveZChunk.isGenerated) {
                                        if (positiveZChunk.GetBlock(blockX, blockY, 0).IsAir()) {
                                            facesToAdd.Add(FaceDirection.BACK);
                                        }
                                    }
                                    break;
                                case FaceDirection.FRONT:
                                    if (blockZ > 0) {
                                        if (GetBlock(blockX, blockY, (byte)(blockZ - 1)).IsAir()) {
                                            facesToAdd.Add(FaceDirection.FRONT);
                                        }
                                    } else if (blockZ == 0 && negativeZChunk.isGenerated) {
                                        if (negativeZChunk.GetBlock(blockX, blockY, (byte)(chunkSize - 1)).IsAir()) {
                                            facesToAdd.Add(FaceDirection.FRONT);
                                        }
                                    }
                                    break;
                            }
                        }

                        Span<bool> neighborsMask = [
                            facesToAdd.Contains(FaceDirection.FRONT),
                            facesToAdd.Contains(FaceDirection.BACK),
                            facesToAdd.Contains(FaceDirection.LEFT),
                            facesToAdd.Contains(FaceDirection.RIGHT),
                            facesToAdd.Contains(FaceDirection.TOP),
                            facesToAdd.Contains(FaceDirection.BOTTOM),
                        ];

                        Block block = GetBlock(blockX, blockY, blockZ);
                        GeneralMesh blockMesh = block.GetMesh(neighborsMask, new FullBlockPosition(new IntVector3(blockX, blockY, blockZ), new IntVector3(chunkX, chunkY, chunkZ)), vertices, indices);
                    }
                }
            }
        }

        if (vertices.Count > 0) {
            UpdateBuffers();
        }

        isMeshed = true;
        chunkGenerationState = 3;
        dirtyBuffers = true;
		stopwatch.Stop();
        KINFO("Took " + stopwatch.Elapsed.TotalMilliseconds + "ms to generate mesh for chunk");
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
					if (!GetBlock(blockX, blockY, blockZ).IsAir()) {
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

	public unsafe bool Render() {
        if (!isMeshed || !shouldRender || vertices.Count == 0) {
            return false;
        }
        if (!renderComponentsSetup) {
            SetupRenderComponents();
            return false;
        }

        if (dirtyBuffers) {
            UpdateBuffers();
        }
        gl.BindVertexArray(vertexArray);
        gl.DrawElements(PrimitiveType.Triangles, (uint)(indices.Count), DrawElementsType.UnsignedShort, (void*)0);

        return true;
    }

    public string GetImGuiText() {
        return "Chunk, position: " + new IntVector3(chunkX, chunkY, chunkZ) + ", generation state: " + chunkGenerationState + ", is generated, meshed, render components setup: {" + isGenerated + ", " + isMeshed + ", " + renderComponentsSetup + "}, total blocks: {" + totalBlocks + "}, vertices: {" + vertices.Count + "}, indices: {" + indices.Count + "}";
    }

    public void RecalculateFullness() {
        isFull = (totalBlocks == chunkVolume);
        if (!isFull) {
            isEmpty = (totalBlocks == 0);
        } else {
            isEmpty = false;
        }
    }

    public bool SetBlock(IntVector3 blockPosition, Block newBlock) {
        Block originalBlock = GetBlock(blockPosition);
        if (originalBlock.IsAir() ^ newBlock.IsAir()) {
            if (newBlock.IsAir()) {
                totalBlocks--;
            } else {
                totalBlocks++;
            }
            RecalculateFullness();
        }
        if (originalBlock == newBlock) {
            KCRITICAL("Just replaced a block at chunk position " + new IntVector3(chunkX, chunkY, chunkZ) + " and block position " + blockPosition + " with with a new identical block \"" + newBlock + "\". This should currently be impossible, please report a bug if you encounter this, thanks");
            return false;
        }
        paletteIndices[GetBlockPositionIndex(blockPosition)] = AddBlockToPalette(newBlock);
        return true;
    }

	public ushort AddBlockToPalette(Block block) {
		if (blocksToPaletteIndices.TryGetValue(block, out ushort index)) {
			return index;
		}

		ushort newIndex = (ushort)blockPalette.Count;
		blockPalette.Add(block);
		blocksToPaletteIndices.Add(block, newIndex);
		return newIndex;
	}

	public ushort GetBlockPaletteIndex(int blockX, int blockY, int blockZ) {
        return paletteIndices[GetBlockPositionIndex(blockX, blockY, blockZ)];
    }

    public ushort GetBlockPaletteIndex(IntVector3 blockPosition) {
        return paletteIndices[GetBlockPositionIndex(blockPosition.X, blockPosition.Y, blockPosition.Z)];
    }

    public Block GetBlock(int blockX, int blockY, int blockZ) {
        int paletteIndex = GetBlockPaletteIndex(blockX, blockY, blockZ);
        return blockPalette[paletteIndex];
    }

    public Block GetBlock(IntVector3 blockPosition) {
        int paletteIndex = GetBlockPaletteIndex(blockPosition);
        return blockPalette[paletteIndex];
    }

    public Block GetBlock(ushort index) {
        return blockPalette[paletteIndices[index]];
    }

    public Span<float> GetVertices() {
        return CollectionsMarshal.AsSpan(vertices);
    }

    public Span<ushort> GetIndices() {
        return CollectionsMarshal.AsSpan(indices);
    }

    public List<Block> GetBlockPalette() {
        return blockPalette;
    }

    public ushort[] GetPaletteIndices() {
        return paletteIndices;
    }

    public List<ushort> SaveChunkData(ref List<Block> globalPalette) {
        List<ushort> globalIndices = new();
        for (byte blockX = 0; blockX < chunkSize; blockX++) {
            for (byte blockZ = 0; blockZ < chunkSize; blockZ++) {
                for (byte blockY = 0; blockY < chunkSize; blockY++) {
                    if (globalPalette.Contains(GetBlock(blockX, blockY, blockZ))) {
                        globalIndices.Add((ushort)globalPalette.IndexOf(GetBlock(blockX, blockY, blockZ)));
                    } else {
                        globalPalette.Add(GetBlock(blockX, blockY, blockZ));
                        globalIndices.Add((ushort)(globalPalette.Count - 1));
                    }
                }
            }
        }
        return globalIndices;
    }

    public void LoadChunkData(List<Block> newBlockPalette, ushort[] newPaletteIndices, int totalBlocks) {
        blockPalette = newBlockPalette;
        paletteIndices = newPaletteIndices;
        for (int iterator = 0; iterator < blockPalette.Count; iterator++) {
            blocksToPaletteIndices.Add(blockPalette[iterator], (ushort)iterator);
        }
        blockGenerationState = 2;
        isGenerated = true;
        this.totalBlocks = (ushort)totalBlocks;
        RecalculateFullness();
    }

    public bool ShouldGenerate() {
        return shouldGenerate;
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

    public bool GetMeshable() {
        if (isMeshed || !isGenerated || isEmpty) {
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

    public bool IsReal() {
        return isReal;
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

    private unsafe void UpdateBuffers() {
        if (!renderComponentsSetup) {
            return;
        }

        gl.BindVertexArray(vertexArray);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBuffer);
        fixed (void* data = GetVertices()) {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Count * sizeof(float)), data, BufferUsageARB.StaticDraw);
        }
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, indexBuffer);
        fixed (void* data = GetIndices()) {
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Count * sizeof(ushort)), data,
                BufferUsageARB.StaticDraw);
        }
        dirtyBuffers = false;

        // proper handling of vertices/indices (not storing, just creating + uploading)
    }

    public void Dispose() {
        totalChunks--;
        if (!renderComponentsSetup) {
            return;
		}

        unsafe {
            gl.DeleteVertexArray(vertexArray);
            gl.DeleteBuffer(vertexBuffer);
            gl.DeleteBuffer(indexBuffer);
		}

        blockPalette = null;
        paletteIndices = null;
        blocksToPaletteIndices = null;
        blockVariants = null;
        blockStates = null;

        assetManager = null;
        chunkHandler = null;

        vertices.Clear();
        indices.Clear();
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