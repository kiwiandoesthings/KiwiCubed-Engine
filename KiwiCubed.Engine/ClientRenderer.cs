namespace KiwiCubed.Engine;

using KiwiCubed.Api;

using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.Util;

public static class WorldRenderer {
	static Dictionary<IntVector3, ValueTuple<RenderBuffers, int>> chunkBuffers = new();

	public static void RenderWorld() {
		World world = (World)MetaHandler.Get<ISingleplayerHandler>().GetWorld();
		RenderWorldChunks(world);
		RenderWorldEntities(world);
	}

	public static void AllocateChunkData(IntVector3 chunkPosition) {
		if (chunkBuffers.ContainsKey(chunkPosition)) {
			KERR("Tried to allocate already allocated buffers for chunk at position " + chunkPosition);
			return;
		}

		chunkBuffers.Add(chunkPosition, new ValueTuple<RenderBuffers, int>(new RenderBuffers(), 0));
	}

	public static void UpdateChunkData(IntVector3 chunkPosition, List<float> vertices, List<ushort> indices) {
		if (chunkBuffers.TryGetValue(chunkPosition, out ValueTuple<RenderBuffers, int> chunkBuffersPair)) {
			Renderer.UpdateBuffers(chunkBuffersPair.Item1, vertices, indices);
			chunkBuffers[chunkPosition] = new ValueTuple<RenderBuffers, int>(chunkBuffersPair.Item1, indices.Count);
		} else {
			KERR("Tried to update none-existent buffers for chunk at position " + chunkPosition);
		}
	}

	public static void UnloadChunkData(IntVector3 chunkPosition) {
		if (!chunkBuffers.Remove(chunkPosition)) {
			KERR("Tried to unload non-existent buffers for chunk at position " + chunkPosition);
		}
	}

	private static void RenderWorldChunks(World world) {
		foreach (KeyValuePair<IntVector3, ValueTuple<RenderBuffers, int>> chunkBuffersPair in chunkBuffers) {
			Renderer.DrawElements(chunkBuffersPair.Value.Item1, chunkBuffersPair.Value.Item2);
		}
	}

	private static void RenderWorldEntities(World world) {
		
	}
}