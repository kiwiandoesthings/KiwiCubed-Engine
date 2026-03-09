namespace KiwiCubed;

using KiwiCubed.Api;
using Silk.NET.OpenGL;
using System.Runtime.InteropServices;

using static KiwiCubed.Api.Block;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.Util;

public class Chunk {
    public readonly int chunkX;
    public readonly int chunkY;
    public readonly int chunkZ;
	private static GL gl = SystemsManager.Get<GL>();
	private static AssetManager assetManager = (AssetManager)SystemsManager.Get<IAssetManager>();
	private readonly List<float> vertices = new List<float>();
    private readonly List<ushort> indices = new List<ushort>();
    private readonly ChunkHandler chunkHandler = null;
	private List<Block> blockPalette;
	private ushort[] paletteIndices;
	private uint vertexArray = 0;
    private uint vertexBuffer = 0;
    private uint indexBuffer = 0;
    private bool shouldGenerate = true;
    private bool renderComponentsSetup = false;
    private bool isGenerated = false;
    private bool isMeshed = false;
    private bool isEmpty = true;
    private bool isFull = false;
    private byte generationState = 0;
    private ushort totalBlocks = 0;

    public Chunk(int x, int y, int z, ChunkHandler chunkHandler) {
        chunkX = x;
        chunkY = y;
        chunkZ = z;
        this.chunkHandler = chunkHandler;

        blockPalette = new();
        paletteIndices = new ushort[chunkVolume];
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
        
        return true;
    }

    public bool GenerateBlocks(World world, Chunk callerChunk, bool updateCallerChunk, bool debug) {
        OVERRIDE_LOG_NAME("Chunk Block Generation");
        if (isGenerated) {
            KWARN("Tried to generate blocks for chunk at {" + chunkX + ", " + chunkY + ", " + chunkZ + "} twice");
        }

        FastNoiseLite noise = world.GetNoise();

        int baseX = chunkX * chunkSize;
        int baseY = chunkY * chunkSize;
        int baseZ = chunkZ * chunkSize;

        blockPalette.Add(assetManager.GetBlock(0));

		for (byte blockZ = 0; blockZ < chunkSize; blockZ++) {
            for (byte blockY = 0; blockY < chunkSize; blockY++) {
                for (byte blockX = 0; blockX < chunkSize; blockX++) {
                    ushort block = GetBlock(blockX, blockY, blockZ);
                    float density = noise.GetNoise((float)(blockX + baseX), (float)(blockZ + baseZ));
                    int height = blockY + baseY;
                    int reach = (int)(density * 16) + 30;

                    if (!(height < reach)) {
                        continue;
                    }


                    paletteIndices[GetBlockIndex(blockX, blockY, blockZ)] = 1;
                    totalBlocks++;
                }
            }
        }

        RecalculateFullness();

        isGenerated = true;
        KINFO("Generated chunk with {" + totalBlocks + "} blocks");
        return true;
    }

    public bool GenerateMesh(bool remesh) {
        OVERRIDE_LOG_NAME("Chunk Mesh Generation");
        IntVector3 chunkPosition = new IntVector3(chunkX, chunkY, chunkZ);
        if (!renderComponentsSetup) {
            KERR("Tried to mesh chunk at position " + chunkPosition + " without render components setup");
            return false;
        }
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

		Chunk positiveXChunk = chunkHandler.GetChunk(chunkX + 1, chunkY, chunkZ, true);
		Chunk negativeXChunk = chunkHandler.GetChunk(chunkX - 1, chunkY, chunkZ, true);
		Chunk positiveYChunk = chunkHandler.GetChunk(chunkX, chunkY + 1, chunkZ, true);
		Chunk negativeYChunk = chunkHandler.GetChunk(chunkX, chunkY - 1, chunkZ, true);
		Chunk positiveZChunk = chunkHandler.GetChunk(chunkX, chunkY, chunkZ + 1, true);
		Chunk negativeZChunk = chunkHandler.GetChunk(chunkX, chunkY, chunkZ - 1, true);

		vertices.Clear();
        indices.Clear();
        List<FaceDirection> facesToAdd = new List<FaceDirection>(6);
        
        bool hasMesh = false;
		for (byte blockZ = 0; blockZ < chunkSize; blockZ++) {
			for (byte blockY = 0; blockY < chunkSize; blockY++) {
				for (byte blockX = 0; blockX < chunkSize; blockX++) {
					if (GetBlock(blockX, blockY, blockZ) != 0) {
                        hasMesh = true;

                        facesToAdd.Clear();

						for (int direction = 0; direction < 6; direction++) {
							FaceDirection faceDirection = (FaceDirection)direction;
							switch (faceDirection) {
								case FaceDirection.RIGHT:
									if (blockX < chunkSize - 1) {
										if (GetBlock((byte)(blockX + 1), blockY, blockZ) == 0) {
											facesToAdd.Add(FaceDirection.RIGHT);
										}
									} else if (blockX == chunkSize - 1 && positiveXChunk.isGenerated) {
										if (positiveXChunk.GetBlock(0, blockY, blockZ) == 0) {
											facesToAdd.Add(FaceDirection.RIGHT);
										}
									}
									break;
								case FaceDirection.LEFT:
									if (blockX > 0) {
										if (GetBlock((byte)(blockX - 1), blockY, blockZ) == 0) {
											facesToAdd.Add(FaceDirection.LEFT);
										}
									} else if (blockX == 0 && negativeXChunk.isGenerated) {
										if (negativeXChunk.GetBlock((byte)(chunkSize - 1), blockY, blockZ) == 0) {
											facesToAdd.Add(FaceDirection.LEFT);
										}
									}
									break;
								case FaceDirection.TOP:
									if (blockY < chunkSize - 1) {
										if (GetBlock(blockX, (byte)(blockY + 1), blockZ) == 0) {
											facesToAdd.Add(FaceDirection.TOP);
										}
									} else if (blockY == chunkSize - 1 && positiveYChunk.isGenerated) {
										if (positiveYChunk.GetBlock(blockX, 0, blockZ) == 0) {
											facesToAdd.Add(FaceDirection.TOP);
										}
									}
									break;
								case FaceDirection.BOTTOM:
									if (blockY > 0) {
										if (GetBlock(blockX, (byte)(blockY - 1), blockZ) == 0) {
											facesToAdd.Add(FaceDirection.BOTTOM);
										}
									} else if (blockY == 0 && negativeYChunk.isGenerated) {
										if (negativeYChunk.GetBlock(blockX, (byte)(chunkSize - 1), blockZ) == 0) {
											facesToAdd.Add(FaceDirection.BOTTOM);
										}
									}
									break;
								case FaceDirection.BACK:
									if (blockZ < chunkSize - 1) {
										if (GetBlock(blockX, blockY, (byte)(blockZ + 1)) == 0) {
											facesToAdd.Add(FaceDirection.BACK);
										}
									} else if (blockZ == chunkSize - 1 && positiveZChunk.isGenerated) {
										if (positiveZChunk.GetBlock(blockX, blockY, 0) == 0) {
											facesToAdd.Add(FaceDirection.BACK);
										}
									}
									break;
								case FaceDirection.FRONT:
									if (blockZ > 0) {
										if (GetBlock(blockX, blockY, (byte)(blockZ - 1)) == 0) {
											facesToAdd.Add(FaceDirection.FRONT);
										}
									} else if (blockZ == 0 && negativeZChunk.isGenerated) {
										if (negativeZChunk.GetBlock(blockX, blockY, (byte)(chunkSize - 1)) == 0) {
											facesToAdd.Add(FaceDirection.FRONT);
										}
									}
									break;
							}
						}

                        for (int iterator = 0; iterator < facesToAdd.Count(); iterator++) {
                            ushort vertexOffset = (ushort)((int)facesToAdd[iterator] * 20);
                            int baseIndex = vertices.Count() / 5;

                            for (int i = vertexOffset; i < vertexOffset + 20; i += 5) {
                                vertices.Add((Block.vertices[i + 0]) + (blockX + (chunkX * chunkSize)));
                                vertices.Add((Block.vertices[i + 1]) + (blockY + (chunkY * chunkSize)));
                                vertices.Add((Block.vertices[i + 2]) + (blockZ + (chunkZ * chunkSize)));
                                vertices.Add((Block.vertices[i + 3] / 4));
                                vertices.Add((Block.vertices[i + 4] / 4));
                            }

                            for (int i = 0; i < 6; ++i) {
                                indices.Add((ushort)(baseIndex + Block.indices[i]));
                            }
                        }
                    }
                }
            }
        }

        if (hasMesh) {
            UpdateBuffers();
        }

        isMeshed = true;
        KINFO("Meshed chunk. Has mesh:? " + hasMesh);
        return hasMesh;
    }

    public unsafe bool Render() {
        if (!renderComponentsSetup) {
            return false;
        }
        
        gl.BindVertexArray(vertexArray);
        gl.DrawElements(PrimitiveType.Triangles, (uint)(indices.Count), DrawElementsType.UnsignedShort, (void*)0);
        
        return true;
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
		ushort blockID = GetBlock(blockPosition);
        if (blockID == 0 ^ (newBlockID == 0)) {
			if (newBlockID == 0) {
				totalBlocks--;
			} else {
				totalBlocks++;
			}
            RecalculateFullness();
		}
		if (blockID == newBlockID) {
			KCRITICAL("Just replaced a block at chunk position " + new IntVector3(chunkX, chunkY, chunkZ) + " and block position " + blockPosition + " with a new block that has an identical numerical ID {" + newBlockID + "}. This should currently be impossible, please report a bug if you encounter this, thanks");
            return false;
        }
        //blockIDs[GetBlockIndex(blockPosition)] = newBlockID;
		return true;
    }

	public ushort GetBlock(int blockX, int blockY, int blockZ) {
		return paletteIndices[blockX + chunkSize * (blockY + chunkSize * blockZ)];
	}

	public ushort GetBlock(IntVector3 blockPosition) {
		return paletteIndices[blockPosition.X + chunkSize * (blockPosition.Y + chunkSize * blockPosition.Z)];
	}

	public Span<float> GetVertices() {
        return CollectionsMarshal.AsSpan(vertices);
    }

    public Span<ushort> GetIndices() {
        return CollectionsMarshal.AsSpan(indices);
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

    public bool IsEmpty() {
        return isEmpty;
    }

    public bool IsFull() {
        return isFull;
    }

    public int GetTotalBlocks() {
        return (int)totalBlocks;
    }

	private int GetBlockIndex(IntVector3 position) {
		return position.X + chunkSize * (position.Y + chunkSize * position.Z);
	}

	private int GetBlockIndex(int x, int y, int z) {
        return x + chunkSize * (y + chunkSize * z);
	}

    private unsafe void UpdateBuffers() {
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
    }
}