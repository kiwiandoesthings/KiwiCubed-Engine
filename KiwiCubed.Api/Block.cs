namespace KiwiCubed.Api;

using ArchEntity = Arch.Core.Entity;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.Util;
using System.Diagnostics.CodeAnalysis;

public abstract class Block {
    public static class BlockFace {
        private static readonly IntVector3[] faceModifiers = {
            new IntVector3(0, 0, 1),
            new IntVector3(0, 0, -1),
            new IntVector3(-1, 0, 0),
            new IntVector3(1, 0, 0),
            new IntVector3(0, 1, 0),
            new IntVector3(0, -1, 0),

            new IntVector3(0, 0, 0),
        };

		public static IntVector3 GetModifier(FaceDirection direction) {
			return faceModifiers[(byte)direction];
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

	public static float[] vertices = {
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
	};

    public static ushort[] indices = {
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
    };

	protected AssetStringID stringID = new AssetStringID("kiwicubed", "stone");
	protected MetaTexture metaTexture;
	protected IAssetManager assetManager = Meta.Get<IAssetManager>();
	protected uint totalVariants = 0;
	protected uint uniqueFaces = 1;

	// use bitmasking to pass a single byte eventually with stuff like |=
	// remember 1 << x on a byte is taking 000001 and moving it (x - 1) spaces to the left (if x is 0 the one is gone)
	// use this to pack neightbors
	public virtual GeneralMesh GetMesh(Span<bool> neighborsMask, FullBlockPosition fullPosition, List<float> vertices, List<ushort> indices) {
        IntVector3 blockPosition = fullPosition.blockPosition;
        IntVector3 chunkPosition = fullPosition.chunkPosition;
		IntVector3 blockOffset = chunkPosition * chunkSize;
		int hash = Math.Abs(fullPosition.GetHashCode());
		int variant = 0;
		if (totalVariants > 0) {
			variant = (hash % (int)totalVariants);
		}
		int faceOffset = variant * (int)uniqueFaces;
        for (int face = 0; face < 6; ++face) {
			if (neighborsMask[face] == true) {
				ushort vertexOffset = (ushort)((int)face * 20);
				int baseIndex = vertices.Count / 5;

                TextureAtlasData atlasData = metaTexture.atlasDatas[metaTexture.faceIndices[face] + faceOffset];

                for (int i = vertexOffset; i < vertexOffset + 20; i += 5) {
					vertices.Add((Block.vertices[i + 0]) + (blockPosition.X + blockOffset.X));
					vertices.Add((Block.vertices[i + 1]) + (blockPosition.Y + blockOffset.Y));
					vertices.Add((Block.vertices[i + 2]) + (blockPosition.Z + blockOffset.Z));

					float u0 = atlasData.xPosition;
					float u1 = (atlasData.xPosition + atlasData.xSize);
					float v0 = atlasData.yPosition;
					float v1 = (atlasData.yPosition + atlasData.ySize);

					switch ((i - vertexOffset) / 5 % 4) {
						case 0: {
							vertices.Add(u0);
							vertices.Add(v1);
							break;
						}
						case 1: {
							vertices.Add(u1);
							vertices.Add(v1);
							break;
						}
						case 2: {
							vertices.Add(u1);
							vertices.Add(v0);
							break;
						}
						case 3: {
							vertices.Add(u0);
							vertices.Add(v0);
							break;
						}
					}
				}
        
				for (int i = 0; i < 6; ++i) {
        			indices.Add((ushort)(baseIndex + Block.indices[i]));
        		}
        	}
        }
        
        return new GeneralMesh(vertices, indices, true);
    }

	public virtual void RandomTick() {
    }

	public virtual bool IsAir() {
		return false;
	}

    public AssetStringID GetStringID() {
        return stringID;
	}

	public MetaTexture GetMetaTexture() {
		return metaTexture;
	}

	public override string ToString() {
		return "Block " + stringID;
	}
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

    public override bool Equals(object? obj) {
		return obj is not null && obj is BlockDefinition other && other.stringID.Equals(stringID);
    }

    public override int GetHashCode() {
        return stringID.GetHashCode();
    }

    public bool IsAir() {
        return !Meta.Get<IAssetManager>().GetArchWorld().Has<BlockSolidComponent>(definition);
    }
}

public struct BlockRenderableComponent {
	public MetaTexture metaTexture;

	public BlockRenderableComponent(MetaTexture metaTexture) {
		this.metaTexture = metaTexture;
	}

	public void AddBlockMesh(Span<bool> neighborsMask, FullBlockPosition fullPosition, List<float> vertices, List<ushort> indices) {
		IntVector3 blockPosition = fullPosition.blockPosition;
		IntVector3 chunkPosition = fullPosition.chunkPosition;
		IntVector3 blockOffset = chunkPosition * chunkSize;
		int hash = Math.Abs(fullPosition.GetHashCode());
		int variant = 0;
		if (metaTexture.variants > 0) {
			variant = (hash % metaTexture.variants);
		}
		int faceOffset = variant * metaTexture.facesPerVariant;
		for (int face = 0; face < 6; ++face) {
			if (neighborsMask[face] == true) {
				ushort vertexOffset = (ushort)((int)face * 20);
				int baseIndex = vertices.Count / 5;

				TextureAtlasData atlasData = metaTexture.atlasDatas[metaTexture.faceIndices[face] + faceOffset];

				for (int i = vertexOffset; i < vertexOffset + 20; i += 5) {
					vertices.Add((Block.vertices[i + 0]) + (blockPosition.X + blockOffset.X));
					vertices.Add((Block.vertices[i + 1]) + (blockPosition.Y + blockOffset.Y));
					vertices.Add((Block.vertices[i + 2]) + (blockPosition.Z + blockOffset.Z));

					float u0 = atlasData.xPosition;
					float u1 = (atlasData.xPosition + atlasData.xSize);
					float v0 = atlasData.yPosition;
					float v1 = (atlasData.yPosition + atlasData.ySize);

					switch ((i - vertexOffset) / 5 % 4) {
						case 0: {
							vertices.Add(u0);
							vertices.Add(v1);
							break;
						}
						case 1: {
							vertices.Add(u1);
							vertices.Add(v1);
							break;
						}
						case 2: {
							vertices.Add(u1);
							vertices.Add(v0);
							break;
						}
						case 3: {
							vertices.Add(u0);
							vertices.Add(v0);
							break;
						}
					}
				}

				for (int i = 0; i < 6; ++i) {
					indices.Add((ushort)(baseIndex + Block.indices[i]));
				}
			}
		}
	}
}

public struct BlockSolidComponent {
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