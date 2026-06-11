namespace KiwiCubed.Api;

using System.Numerics;

using static KiwiCubed.Api.Util;

public class Physics {
	private static IPhysics physics;

	public static void Initialize(IPhysics implementation) => physics = implementation;

	public static void ApplyPhysics(IChunkHandler virtualChunkHandler, ref EntityTransformComponent transform, ref EntityPhysicalComponent physicalComponent, double delta) => physics.ApplyPhysics(virtualChunkHandler, ref transform, ref physicalComponent, delta);
	public static BlockRayHit RaycastWorld(Vector3 origin, Vector3 direction, int maxDistance, IChunkHandler chunkHandler) => physics.RaycastWorld(origin, direction, maxDistance, chunkHandler);
	public static bool GetGrounded(ref EntityTransformComponent transform, ref EntityPhysicalComponent physicalComponent, IChunkHandler virtualChunkHandler) => physics.GetGrounded(ref transform, ref physicalComponent, virtualChunkHandler);
	public static bool CollideBlock(ref EntityTransformComponent transform, ref EntityPhysicalComponent physicalComponent, FullBlockPosition fullBlockPosition, bool resolveCollision) => physics.CollideBlock(ref transform, ref physicalComponent, fullBlockPosition, resolveCollision);
}

public interface IPhysics {
	public void ApplyPhysics(IChunkHandler virtualChunkHandler, ref EntityTransformComponent transform, ref EntityPhysicalComponent physicalComponent, double delta);
	public BlockRayHit RaycastWorld(Vector3 origin, Vector3 direction, int maxDistance, IChunkHandler chunkHandler);
	public bool GetGrounded(ref EntityTransformComponent transform, ref EntityPhysicalComponent physicalComponent, IChunkHandler virtualChunkHandler);
	public bool CollideBlock(ref EntityTransformComponent transform, ref EntityPhysicalComponent physicalComponent, FullBlockPosition fullBlockPosition, bool resolveCollision);
}