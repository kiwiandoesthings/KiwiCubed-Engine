namespace KiwiCubed.Api;

using ArchEntity = Arch.Core.Entity;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.Utils;

public class Block {
	public static class BlockFace {
		public static readonly IntVector3[] faceModifiers = [
			new IntVector3(0, 0, 0),

			new IntVector3(0, 0, -1),
			new IntVector3(0, 0, 1),
			new IntVector3(-1, 0, 0),
			new IntVector3(1, 0, 0),
			new IntVector3(0, 1, 0),
			new IntVector3(0, -1, 0),
		];

		public static IntVector3 GetModifier(FaceDirection direction) {
			return faceModifiers[(byte)direction];
		}

        public static FaceDirection GetOpposite(FaceDirection direction) {
            return direction switch {
                FaceDirection.FRONT => FaceDirection.BACK,
                FaceDirection.BACK => FaceDirection.FRONT,
                FaceDirection.LEFT => FaceDirection.RIGHT,
                FaceDirection.RIGHT => FaceDirection.LEFT,
                FaceDirection.TOP => FaceDirection.BOTTOM,
                FaceDirection.BOTTOM => FaceDirection.TOP,
                FaceDirection.INTERIOR => FaceDirection.INTERIOR,
            };
        }
    }

	public enum FaceDirection : byte {
		INTERIOR = 0,

		FRONT,
		BACK,
		LEFT,
		RIGHT,
		TOP,
		BOTTOM,
	}

	public static float[] vertices = [
        // Positions       // Texture Coordinates
        // Front
        0.0f, 0.0f, 0.0f,  0.0f, 0.0f,
		1.0f, 0.0f, 0.0f,  1.0f, 0.0f,
		1.0f, 1.0f, 0.0f,  1.0f, 1.0f,
		0.0f, 1.0f, 0.0f,  0.0f, 1.0f,

        // Back
        1.0f, 0.0f, 1.0f,  0.0f, 0.0f,
		0.0f, 0.0f, 1.0f,  1.0f, 0.0f,
		0.0f, 1.0f, 1.0f,  1.0f, 1.0f,
		1.0f, 1.0f, 1.0f,  0.0f, 1.0f,

        // Left
        0.0f, 0.0f, 1.0f,  0.0f, 0.0f,
		0.0f, 0.0f, 0.0f,  1.0f, 0.0f,
		0.0f, 1.0f, 0.0f,  1.0f, 1.0f,
		0.0f, 1.0f, 1.0f,  0.0f, 1.0f,

        // Right
        1.0f, 0.0f, 0.0f,  0.0f, 0.0f,
		1.0f, 0.0f, 1.0f,  1.0f, 0.0f,
		1.0f, 1.0f, 1.0f,  1.0f, 1.0f,
		1.0f, 1.0f, 0.0f,  0.0f, 1.0f,

        // Top
        0.0f, 1.0f, 0.0f,  0.0f, 0.0f,
		1.0f, 1.0f, 0.0f,  1.0f, 0.0f,
		1.0f, 1.0f, 1.0f,  1.0f, 1.0f,
		0.0f, 1.0f, 1.0f,  0.0f, 1.0f,

        // Bottom
        0.0f, 0.0f, 1.0f,  0.0f, 0.0f,
		1.0f, 0.0f, 1.0f,  1.0f, 0.0f,
		1.0f, 0.0f, 0.0f,  1.0f, 1.0f,
		0.0f, 0.0f, 0.0f,  0.0f, 1.0f,
	];

	public static ushort[] indices = [
        // Front
        0, 1, 2,
		2, 3, 0,

        // Back
        4, 5, 6,
		6, 7, 4,

        // Left
        8, 9, 10,
		10, 11, 8,

        // Right
        12, 13, 14,
		14, 15, 12,

        // Top
        16, 17, 18,
		18, 19, 16,

        // Bottom
        20, 21, 22,
		22, 23, 20
	];
}

public struct FullBlockState {
	public BlockDefinition blockType;
}

public struct BlockDefinition {
	public AssetStringID stringID;
	public ArchEntity definition;

	public BlockDefinition(AssetStringID blockStringID, ArchEntity blockDefinition) {
		stringID = blockStringID;
		definition = blockDefinition;
	}

    public static bool operator ==(BlockDefinition a, BlockDefinition b) {
        return a.Equals(b);
    }

    public static bool operator !=(BlockDefinition a, BlockDefinition b) {
        return !a.Equals(b);
    }

    public override readonly bool Equals(object? obj) {
		return obj is not null && obj is BlockDefinition other && other.stringID.Equals(stringID);
    }

    public override readonly int GetHashCode() {
        return stringID.GetHashCode();
    }

    public readonly bool IsAir() {
        return !Meta.Get<IAssetManager>().GetArchWorld().Has<BlockSolidComponent>(definition);
    }
}

public struct BlockRenderableComponent {
	public BlockTexture metaTexture;

	public BlockRenderableComponent(BlockTexture metaTexture) {
		this.metaTexture = metaTexture;
	}

    public readonly void AddBlockMesh(Span<bool> neighborsMask, ReadOnlySpan<byte> faceAO, FullBlockPosition fullPosition, List<VertexTerrain> vertices, List<ushort> indices) {
		IntVector3 blockPosition = fullPosition.blockPosition;
		IntVector3 chunkPosition = fullPosition.chunkPosition;
		IntVector3 blockOffset = chunkPosition * chunkSize;
		int hash = fullPosition.GetHashCode() & int.MaxValue;
		int variant = 0;
		if (metaTexture.variants > 0) {
			variant = (hash % metaTexture.variants);
		}
		int faceOffset = variant * metaTexture.facesPerVariant;
		for (int face = 0; face < 6; ++face) {
			if (neighborsMask[face] == true) {
                ushort vertexOffset = (ushort)(face * 20);
                ushort baseIndex = (ushort)(vertices.Count);

                TextureAtlasData atlasData = metaTexture.atlasDatas[metaTexture.faceIndices[face] + faceOffset];

                for (int vertexIndex = 0; vertexIndex < 4; vertexIndex++) {
                    int rawIndex = vertexOffset + (vertexIndex * 5);

                    float x = Block.vertices[rawIndex + 0] + (blockPosition.X + blockOffset.X);
                    float y = Block.vertices[rawIndex + 1] + (blockPosition.Y + blockOffset.Y);
                    float z = Block.vertices[rawIndex + 2] + (blockPosition.Z + blockOffset.Z);

                    float u0 = atlasData.xPosition;
					float u1 = (atlasData.xPosition + atlasData.xSize);
					float v0 = atlasData.yPosition;
					float v1 = (atlasData.yPosition + atlasData.ySize);

					float u = 0.0f;
					float v = 0.0f;

                    switch (vertexIndex) {
                        case 0: {
								u = u0;
								v = v1;
							break;
						}
						case 1: {
								u = u1;
								v = v1;
							break;
						}
						case 2: {
								u = u1;
								v = v0;
							break;
						}
						case 3: {
								u = u0;
								v = v0;
							break;
						}
					}

                    byte aoValue = faceAO[(face * 4) + vertexIndex];
					vertices.Add(new VertexTerrain(x, y, z, u, v, aoValue));
                }

                float ao0 = faceAO[(face * 4) + 0];
                float ao1 = faceAO[(face * 4) + 1];
                float ao2 = faceAO[(face * 4) + 2];
                float ao3 = faceAO[(face * 4) + 3];
				
                if (ao0 + ao2 < ao1 + ao3) {
                    indices.Add((ushort)(baseIndex + 0));
                    indices.Add((ushort)(baseIndex + 1));
                    indices.Add((ushort)(baseIndex + 3));
                    indices.Add((ushort)(baseIndex + 1));
                    indices.Add((ushort)(baseIndex + 2));
                    indices.Add((ushort)(baseIndex + 3));
                } else {
                    indices.Add((ushort)(baseIndex + 0));
                    indices.Add((ushort)(baseIndex + 1));
                    indices.Add((ushort)(baseIndex + 2));
                    indices.Add((ushort)(baseIndex + 2));
                    indices.Add((ushort)(baseIndex + 3));
                    indices.Add((ushort)(baseIndex + 0));
                }
            }
		}
	}

    public readonly void AddBlockMesh(FullBlockPosition fullPosition, List<Vertex3D> vertices, List<ushort> indices) { // remember quantum developer vertex size video model buffers
        IntVector3 blockPosition = fullPosition.blockPosition;
        IntVector3 chunkPosition = fullPosition.chunkPosition;
        IntVector3 blockOffset = chunkPosition * chunkSize;
        int hash = fullPosition.GetHashCode() & int.MaxValue;
        int variant = 0;
        if (metaTexture.variants > 0) {
            variant = (hash % metaTexture.variants);
        }
        int faceOffset = variant * metaTexture.facesPerVariant;
        for (int face = 0; face < 6; ++face) {
                ushort vertexOffset = (ushort)(face * 20);
                int baseIndex = vertices.Count / 6;

                TextureAtlasData atlasData = metaTexture.atlasDatas[metaTexture.faceIndices[face] + faceOffset];

                for (int vertexIndex = 0; vertexIndex < 4; vertexIndex++) {
                    int rawIndex = vertexOffset + (vertexIndex * 5);

                    float x = Block.vertices[rawIndex + 0] + (blockPosition.X + blockOffset.X);
                    float y = Block.vertices[rawIndex + 1] + (blockPosition.Y + blockOffset.Y);
                    float z = Block.vertices[rawIndex + 2] + (blockPosition.Z + blockOffset.Z);

                    float u0 = atlasData.xPosition;
                    float u1 = (atlasData.xPosition + atlasData.xSize);
                    float v0 = atlasData.yPosition;
                    float v1 = (atlasData.yPosition + atlasData.ySize);

                    float u = 0.0f;
                    float v = 0.0f;

                    switch (vertexIndex) {
                        case 0: {
                                u = u0;
                                v = v1;
                                break;
                            }
                        case 1: {
                                u = u1;
                                v = v1;
                                break;
                            }
                        case 2: {
                                u = u1;
                                v = v0;
                                break;
                            }
                        case 3: {
                                u = u0;
                                v = v0;
                                break;
                            }
                    }
                }
             
                indices.Add((ushort)(baseIndex + 0));
                indices.Add((ushort)(baseIndex + 1));
                indices.Add((ushort)(baseIndex + 2));
                indices.Add((ushort)(baseIndex + 2));
                indices.Add((ushort)(baseIndex + 3));
                indices.Add((ushort)(baseIndex + 0));
        }
    }

    public readonly GeneralMesh<Vertex3D> GetBlockMesh() {
		List<Vertex3D> vertices = [];
		List<ushort> indices = [];
		AddBlockMesh(new FullBlockPosition(), vertices, indices);

		return new GeneralMesh<Vertex3D>(vertices, indices);
	}
}

public readonly struct BlockSolidComponent { //could probably hold collision aabbs
	public readonly bool isFull = true;
	public BlockSolidComponent(bool isFullBlock) {
		isFull = isFullBlock;
    }
}

public struct BlockPhysicalComponent {
	public float frictionMultiplier = 1.0f;
	public float bounciness = 0.0f;

	public BlockPhysicalComponent(float frictionMultiplier, float bounciness) {
		this.frictionMultiplier = frictionMultiplier;
		this.bounciness = bounciness;
	}
}