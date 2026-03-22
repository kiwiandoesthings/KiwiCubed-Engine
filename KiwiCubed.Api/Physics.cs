namespace KiwiCubed.Api;

using System.Numerics;

using static KiwiCubed.Api.Util;

public class Physics {
	private static IPhysics physics;

	public static void Initialize(IPhysics implementation) => physics = implementation;

	public static bool ApplyPhysics(Entity entity, IChunkHandler chunkHandler) => physics.ApplyPhysics(entity, chunkHandler);
	public static BlockRayHit RaycastWorld(Vector3 origin, Vector3 direction, int maxDistance, IChunkHandler chunkHandler) => physics.RaycastWorld(origin, direction, maxDistance, chunkHandler);
	public static bool GetGrounded(Entity entity, IChunkHandler chunkHandler) => physics.GetGrounded(entity, chunkHandler);
	public static bool CollideBlock(Entity entity, FullBlockPosition fullBlockPosition, bool resolveCollision) => physics.CollideBlock(entity, fullBlockPosition, resolveCollision);
}

public interface IPhysics {
	public bool ApplyPhysics(Entity entity, IChunkHandler chunkHandler);
	public BlockRayHit RaycastWorld(Vector3 origin, Vector3 direction, int maxDistance, IChunkHandler chunkHandler);
	public bool GetGrounded(Entity entity, IChunkHandler chunkHandler);
	public bool CollideBlock(Entity entity, FullBlockPosition fullBlockPosition, bool resolveCollision);
}