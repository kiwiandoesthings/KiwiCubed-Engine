namespace KiwiCubed.Api;

using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.Util;

public abstract class Block {
    public static class BlockFace {
        private static readonly IntVector3[] faceModifiers = {
            new IntVector3(-1, 0, 0),
            new IntVector3(1, 0, 0),
            new IntVector3(0, 1, 0),
            new IntVector3(0, -1, 0),
            new IntVector3(0, 0, -1),
            new IntVector3(0, 0, 1),

            new IntVector3(0, 0, 0),
        };

		public static IntVector3 GetModifier(FaceDirection direction) {
			return faceModifiers[(byte)direction];
		}
	}

	public enum FaceDirection : byte {
		LEFT,
		RIGHT,
		TOP,
		BOTTOM,
		BACK,
		FRONT,

		INTERIOR
	}

	public struct BlockMesh {
		List<float> vertices;
		List<ushort> indices;

		public BlockMesh(List<float> vertices, List<ushort> indices) {
			this.vertices = vertices;
			this.indices = indices;
		}
	}

	public static float[] vertices = {
        // Positions       // Texture Coordinates
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

        // Back
        1.0f, 0.0f, 1.0f,  0.0f, 0.0f,
		0.0f, 0.0f, 1.0f,  1.0f, 0.0f,
		0.0f, 1.0f, 1.0f,  1.0f, 1.0f,
		1.0f, 1.0f, 1.0f,  0.0f, 1.0f,

        // Front
        0.0f, 0.0f, 0.0f,  0.0f, 0.0f,
        1.0f, 0.0f, 0.0f,  1.0f, 0.0f,
        1.0f, 1.0f, 0.0f,  1.0f, 1.0f,
        0.0f, 1.0f, 0.0f,  0.0f, 1.0f
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
    private ushort blockID = 0;
    private byte blockState = 0;

    // use bitmasking to pass a single byte eventually with stuff like |=
    // remember 1 << x on a byte is taking 000001 and moving it (x - 1) spaces to the left (if x is 0 the one is gone)
    // use this to pack neightbors
    public virtual BlockMesh GetMesh(Span<bool> neighborsMask, FullBlockPosition fullPosition) {
        List<float> vertices = new();
        List<ushort> indices = new();
        IntVector3 blockPosition = fullPosition.blockPosition;
        IntVector3 chunkPosition = fullPosition.chunkPosition;
        for (int face = 0; face < 6; face++) {
            if (neighborsMask[face] == false) {
				ushort vertexOffset = (ushort)((int)face * 20);
				int baseIndex = vertices.Count() / 5;

				for (int i = vertexOffset; i < vertexOffset + 20; i += 5) {
					vertices.Add((Block.vertices[i + 0]) + (blockPosition.X + (chunkPosition.X * chunkSize)));
					vertices.Add((Block.vertices[i + 1]) + (blockPosition.Y + (chunkPosition.Y * chunkSize)));
					vertices.Add((Block.vertices[i + 2]) + (blockPosition.Z + (chunkPosition.Z * chunkSize)));
					vertices.Add((Block.vertices[i + 3] / 4));
					vertices.Add((Block.vertices[i + 4] / 4));
				}

				for (int i = 0; i < 6; ++i) {
					indices.Add((ushort)(baseIndex + Block.indices[i]));
				}
			}
        }

        return new BlockMesh(vertices, indices);
    }

    public virtual void RandomTick() {
    }

    public void SetBlockType(ushort type) {
        blockID = type;
    }

    public ushort GetBlockType() {
        return blockID;
    }

    public bool IsAir() {
        return blockID == 0;
    }
}