namespace BaseMod;

using KiwiCubed.Api;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Block;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.Util;

public class KiwiCubedMod : IMod {
	public override void Initialize() {
		OVERRIDE_LOG_NAME("KiwiCubed initialization");
		INFO("Initializing KiwiCubed base mod...");

		Systems.Get<IAssetManager>().RegisterBlock(new AssetStringID("kiwicubed", "stone"), new BlockStone());

		INFO("Initialized KiwiCubed base mod");
	}

	public override void Unload() {
	}
}

public class BlockStone : Block {
	public override BlockMesh GetMesh(Span<bool> neighborsMask, FullBlockPosition fullPosition) {
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
					vertices.Add((Block.vertices[i + 3] + 1 / 4));
					vertices.Add((Block.vertices[i + 4] + 1 / 4));
				}

				for (int i = 0; i < 6; ++i) {
					indices.Add((ushort)(baseIndex + Block.indices[i]));
				}
			}
		}

		return new BlockMesh(vertices, indices);
	}
}