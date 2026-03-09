namespace KiwiCubed;

using KiwiCubed.Api;
using System.Numerics;

using static KiwiCubed.Api.Block;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.Util;

public class Physics {
	static bool ApplyPhysics(Entity entity, ChunkHandler chunkHandler) {
		EntityData data = entity.GetEntityData();
		EntityTransform transform = entity.GetEntityTransform();

		bool grounded = false;

		ClipVelocity(transform, data, 0);
		ClipVelocity(transform, data, 1);
		ClipVelocity(transform, data, 2);

		if (data.applyGravity) {
			ApplyGravity(transform);
			ClipVelocity(transform, data, 1);
		}
		if (data.applyCollision) {
			ApplyTerrainCollision(transform, data, chunkHandler);
		} else {
			transform.position.X += transform.velocity.X * 0.016f; // TODO: Same thing with deltatime
			transform.position.Y += transform.velocity.Y * 0.016f;
			transform.position.Z += transform.velocity.Z * 0.016f;
		}

		entity.SetEntityData(data);
		entity.SetEntityTransform(transform);
		return grounded;
	}

	// Literally what the fuck even is this functionn I hate raycasting so much
	// TODO: stop using the out parameters and just keep the return struct
	public static BlockRayHit RaycastWorld(Vector3 origin, Vector3 direction, int maxDistance, ChunkHandler chunkHandler) {
		IntVector3 currentBlock = new IntVector3(origin);
		IntVector3 currentChunk = new IntVector3((int)Math.Floor((float)currentBlock.X / chunkSize), (int)Math.Floor((float)currentBlock.Y / chunkSize), (int)Math.Floor((float)currentBlock.Z / chunkSize));

		IntVector3 stepDirection = new IntVector3(direction.X > 0 ? 1 : -1, direction.Y > 0 ? 1 : -1, direction.Z > 0 ? 1 : -1);
		int lastStepAxis = 0;

		Vector3 stepsilonize = new Vector3(Math.Abs(1.0f / direction.X), Math.Abs(1.0f / direction.Y), Math.Abs(1.0f / direction.Z));
		Vector3 distanceToBlockBoundary = new Vector3((currentBlock.X + (stepDirection.X > 0 ? 1 : 0) - origin.X) / direction.X, (currentBlock.Y + (stepDirection.Y > 0 ? 1 : 0) - origin.Y) / direction.Y, (currentBlock.Z + (stepDirection.Z > 0 ? 1 : 0) - origin.Z) / direction.Z);

		BlockRayHit rayHit = new BlockRayHit();
		rayHit.faceHitIndex = FaceDirection.INTERIOR;

		for (int i = 0; i<maxDistance; ++i) {
			IntVector3 localBlockPos = new IntVector3(PositiveModulo((float)currentBlock.X, chunkSize), PositiveModulo((float)currentBlock.Y, chunkSize), PositiveModulo((float)currentBlock.Z, chunkSize));

			Chunk chunk = chunkHandler.GetChunk(currentChunk.X, currentChunk.Y, currentChunk.Z, false);
			if (chunk.IsGenerated()) {
				ushort block = chunk.GetBlock(localBlockPos.X, localBlockPos.Y, localBlockPos.Z);
				if (block != 0) {
					rayHit.hit = true;
					rayHit.blockHitPosition.blockPosition = new IntVector3(PositiveModulo((float)currentBlock.X, chunkSize), PositiveModulo((float)currentBlock.Y, chunkSize), PositiveModulo((float)currentBlock.Z, chunkSize));
					rayHit.blockHitPosition.chunkPosition = currentChunk;
					if (lastStepAxis == 0) {
						rayHit.faceHitIndex = (direction.X) > 0 ? FaceDirection.LEFT : FaceDirection.RIGHT;
					} else if (lastStepAxis == 1) {
						rayHit.faceHitIndex = (direction.Y) > 0 ? FaceDirection.BOTTOM : FaceDirection.TOP;
					} else {
						rayHit.faceHitIndex = (direction.Z) > 0 ? FaceDirection.BACK : FaceDirection.FRONT;
					}
					return rayHit;
				}
			}

			if (distanceToBlockBoundary.X < distanceToBlockBoundary.Y && distanceToBlockBoundary.X < distanceToBlockBoundary.Z) {
				currentBlock += new IntVector3(stepDirection.X, 0, 0);
				distanceToBlockBoundary.X += stepsilonize.X;
				lastStepAxis = 0;

				int newChunkX = (int)Math.Floor((float)currentBlock.X / (float)chunkSize);
				if (newChunkX != currentChunk.X) {
					currentChunk = new IntVector3(newChunkX, currentChunk.Y, currentChunk.Z);
				}
			} else if (distanceToBlockBoundary.Y < distanceToBlockBoundary.Z) {
				currentBlock += new IntVector3(0, stepDirection.Y, 0);
				distanceToBlockBoundary.Y += stepsilonize.Y;
				lastStepAxis = 1;

				int newChunkY = (int)Math.Floor((float)currentBlock.Y / (float)chunkSize);
				if (newChunkY != currentChunk.Y) {
					currentChunk = new IntVector3(currentChunk.X, newChunkY, currentChunk.Z);
				}
			} else {
				currentBlock += new IntVector3(0, 0, stepDirection.Z);
				distanceToBlockBoundary.Z += stepsilonize.Z;
				lastStepAxis = 2;

				int newChunkZ = (int)Math.Floor((float)currentBlock.Z / (float)chunkSize);
				if (newChunkZ != currentChunk.Z) {
					currentChunk = new IntVector3(currentChunk.X, currentChunk.Y, newChunkZ);
				}
			}
		}
		return rayHit;
	}

	public static bool GetGrounded(Entity entity, ChunkHandler chunkHandler) {
		EntityData newEntityData = entity.GetEntityData();
		EntityTransform newTransform = entity.GetEntityTransform();
		Span<FullBlockPosition> collisionQueue = stackalloc FullBlockPosition[3 * 3 * 3];
		Span<FullBlockPosition> usingQueue = FillCollisionQueue(collisionQueue, newEntityData, newTransform, chunkHandler);
		return CollideAxisFloat(1, newTransform, newEntityData, chunkHandler, usingQueue) > 0;
	}

	public static bool CollideBlock(Entity entity, FullBlockPosition fullBlockPosition, bool resolveCollision) {
		EntityData newEntityData = entity.GetEntityData();
		EntityTransform newTransform = entity.GetEntityTransform();

		for (int axis = 0; axis < 3; axis++) {
			Vector3 min1 = new Vector3(newEntityData.physicsBoundingBox.Corner1().X + newTransform.position.X, newEntityData.physicsBoundingBox.Corner1().Y + newTransform.position.Y, newEntityData.physicsBoundingBox.Corner1().Z + newTransform.position.Z);
			Vector3 max1 = new Vector3(newEntityData.physicsBoundingBox.Corner2().X + newTransform.position.X, newEntityData.physicsBoundingBox.Corner2().Y + newTransform.position.Y, newEntityData.physicsBoundingBox.Corner2().Z + newTransform.position.Z);
			Vector3 min2 = new Vector3(fullBlockPosition.blockPosition.X + (fullBlockPosition.chunkPosition.X * chunkSize), fullBlockPosition.blockPosition.Y + (fullBlockPosition.chunkPosition.Y * chunkSize), fullBlockPosition.blockPosition.Z + (fullBlockPosition.chunkPosition.Z * chunkSize));
			Vector3 max2 = min2 + new Vector3(1.0f);

			bool isColliding =
				(min1.X < max2.X && max1.X > min2.X) &&
				(min1.Y < max2.Y && max1.Y > min2.Y) &&
				(min1.Z < max2.Z && max1.Z > min2.Z);

			if (isColliding) {
				if (!resolveCollision) {
					return true;
				}
				float collision = min1[axis] - min2[axis];
				if (collision < 0) {
					newTransform.position[axis] = min2[axis] - newEntityData.physicsBoundingBox.Corner2()[axis];
				} else {
					newTransform.position[axis] = max2[axis] - newEntityData.physicsBoundingBox.Corner1()[axis];
				}
				newTransform.velocity[axis] = 0;
				return true;
			}
		}
		return false;
	}

	private static bool ApplyTerrainCollision(EntityTransform transform, EntityData data, ChunkHandler chunkHandler) {
		Span<FullBlockPosition> collisionQueue = stackalloc FullBlockPosition[3 * 3 * 3];
		Span<FullBlockPosition> usingQueue = FillCollisionQueue(collisionQueue, data, transform, chunkHandler);

		// TODO: 0.016 should be globals.deltaTime from C++ (needs equvialent)
		transform.position.X += transform.velocity.X * 0.016f;
		bool xAxis = CollideAxis(0, transform, data, chunkHandler, usingQueue);
		transform.position.Y += transform.velocity.Y * 0.016f;
		float yAxis = CollideAxisFloat(1, transform, data, chunkHandler, usingQueue);
		transform.position.Z += transform.velocity.Z * 0.016f;
		bool zAxis = CollideAxis(2, transform, data, chunkHandler, usingQueue);

		data.isGrounded = (yAxis > 0);

		if (xAxis || yAxis != 0 || zAxis) {
			return true;
		}

		return false;
	}

	private static void ApplyGravity(EntityTransform transform) {
		float gravity = 9.81f;

		transform.velocity.Y -= gravity * (60.0f / 1000.0f);
	}

	private static void ClipVelocity(EntityTransform transform, EntityData data, int axis) {
		if (Math.Abs(transform.velocity[axis]) > data.terminalVelocity) {
			if (transform.velocity[axis] > 0) {
				transform.velocity[axis] = data.terminalVelocity;
			} else {
				transform.velocity[axis] = -data.terminalVelocity;
			}
		}
	}

	private static Span<FullBlockPosition> FillCollisionQueue(Span<FullBlockPosition> blockCollisionQueue, EntityData data, EntityTransform transform, ChunkHandler chunkHandler) {
		blockCollisionQueue.Clear();
		int index = 0;
		Vector3 minCorner = Vector3.Min(data.physicsBoundingBox.Corner1() + transform.position, data.physicsBoundingBox.Corner2() + transform.position);
		Vector3 maxCorner = Vector3.Max(data.physicsBoundingBox.Corner1() + transform.position, data.physicsBoundingBox.Corner2() + transform.position);

		for (int blockX = (int)(Math.Floor(minCorner.X)) - 1; blockX <= (int)(Math.Ceiling(maxCorner.X)) + 1; ++blockX) {
			for (int blockY = (int)(Math.Floor(minCorner.Y)) - 1; blockY <= (int)(Math.Ceiling(maxCorner.Y)) + 1; ++blockY) {
				for (int blockZ = (int)(Math.Floor(minCorner.Z)) - 1; blockZ <= (int)(Math.Ceiling(maxCorner.Z)) + 1; ++blockZ) {
					if (index >= blockCollisionQueue.Length) {
						return blockCollisionQueue.Slice(0, index);
					}

					IntVector3 chunkPosition = new IntVector3(
						FloorDiv((float)(blockX), chunkSize),
						FloorDiv((float)(blockY), chunkSize),
						FloorDiv((float)(blockZ), chunkSize)
					);

					IntVector3 blockPosition = new IntVector3(
						PositiveModulo((float)(blockX), chunkSize),
						PositiveModulo((float)(blockY), chunkSize),
						PositiveModulo((float)(blockZ), chunkSize)
					);

					if (chunkHandler.GetChunk(chunkPosition.X, chunkPosition.Y, chunkPosition.Z, false).GetBlock(blockPosition.X, blockPosition.Y, blockPosition.Z) != 0) {
						blockCollisionQueue[index] = new FullBlockPosition(blockPosition, chunkPosition);
						index++;
					}
				}
			}
		}

		return blockCollisionQueue.Slice(0, index);
	}

	private static bool CollideAxis(int axis, EntityTransform transform, EntityData data, ChunkHandler chunkHandler, ReadOnlySpan<FullBlockPosition> collisionQueue) {
		Chunk currentChunk = (Chunk)data.currentChunk;
		if (currentChunk == null || !currentChunk.IsGenerated()) {
			return false;
		}

		Vector3 min1 = new Vector3(data.physicsBoundingBox.Corner1().X + transform.position.X, data.physicsBoundingBox.Corner1().Y + transform.position.Y, data.physicsBoundingBox.Corner1().Z + transform.position.Z);
		Vector3 max1 = new Vector3(data.physicsBoundingBox.Corner2().X + transform.position.X, data.physicsBoundingBox.Corner2().Y + transform.position.Y, data.physicsBoundingBox.Corner2().Z + transform.position.Z);
		foreach (FullBlockPosition blockPosition in collisionQueue) {
			Chunk targetChunk = null;
			if (blockPosition.chunkPosition.X != transform.globalChunkPosition.X || blockPosition.chunkPosition.Y != transform.globalChunkPosition.Y || blockPosition.chunkPosition.Z != transform.globalChunkPosition.Z) {
				targetChunk = chunkHandler.GetChunk(blockPosition.chunkPosition.X, blockPosition.chunkPosition.Y, blockPosition.chunkPosition.Z, false);
				if (!targetChunk.IsGenerated()) {
					continue;
				}
			} else {
				targetChunk = currentChunk;
			}
			
			ushort block = targetChunk.GetBlock(blockPosition.blockPosition.X, blockPosition.blockPosition.Y, blockPosition.blockPosition.Z);

			if (block != 0) {
				Vector3 min2 = new Vector3(blockPosition.blockPosition.X + (blockPosition.chunkPosition.X * chunkSize), blockPosition.blockPosition.Y + (blockPosition.chunkPosition.Y * chunkSize), blockPosition.blockPosition.Z + (blockPosition.chunkPosition.Z * chunkSize));
				Vector3 max2 = min2 + new Vector3(1.0f);

				const float epsilon = 1e-5f;
				bool isColliding =
					(min1.X < max2.X - epsilon && max1.X > min2.X + epsilon) &&
					(min1.Y < max2.Y - epsilon && max1.Y > min2.Y + epsilon) &&
					(min1.Z < max2.Z - epsilon && max1.Z > min2.Z + epsilon);

				if (isColliding) {
					float collision = min1[axis] - min2[axis];
					if (collision < 0) {
						transform.position[axis] = min2[axis] - data.physicsBoundingBox.Corner2()[axis];
					} else {
						transform.position[axis] = max2[axis] - data.physicsBoundingBox.Corner1()[axis];
					}
					transform.velocity[axis] = 0;
					return true;
				}
			}
		}
		return false;
	}

	private static float CollideAxisFloat(int axis, EntityTransform transform, EntityData data, ChunkHandler chunkHandler, ReadOnlySpan<FullBlockPosition> collisionQueue) {
		Chunk currentChunk = (Chunk)data.currentChunk;
		if (currentChunk == null || !currentChunk.IsGenerated()) {
			return 0.0f;
		}

		Vector3 min1 = new Vector3(data.physicsBoundingBox.Corner1().X + transform.position.X, data.physicsBoundingBox.Corner1().Y + transform.position.Y, data.physicsBoundingBox.Corner1().Z + transform.position.Z);
		Vector3 max1 = new Vector3(data.physicsBoundingBox.Corner2().X + transform.position.X, data.physicsBoundingBox.Corner2().Y + transform.position.Y, data.physicsBoundingBox.Corner2().Z + transform.position.Z);
		foreach (FullBlockPosition blockPosition in collisionQueue) {
			Chunk targetChunk = null;
			if (blockPosition.chunkPosition.X != transform.globalChunkPosition.X || blockPosition.chunkPosition.Y != transform.globalChunkPosition.Y || blockPosition.chunkPosition.Z != transform.globalChunkPosition.Z) {
				targetChunk = chunkHandler.GetChunk(blockPosition.chunkPosition.X, blockPosition.chunkPosition.Y, blockPosition.chunkPosition.Z, false);
				if (!targetChunk.IsGenerated()) {
					continue;
				}
			} else {
				targetChunk = currentChunk;
			}

			ushort block = targetChunk.GetBlock(blockPosition.blockPosition.X, blockPosition.blockPosition.Y, blockPosition.blockPosition.Z);

			if (block != 0) {
				Vector3 min2 = new Vector3(blockPosition.blockPosition.X + (blockPosition.chunkPosition.X * chunkSize), blockPosition.blockPosition.Y + (blockPosition.chunkPosition.Y * chunkSize), blockPosition.blockPosition.Z + (blockPosition.chunkPosition.Z * chunkSize));
				Vector3 max2 = min2 + new Vector3(1.0f);

				const float epsilon = 1e-5f;
				bool isColliding =
					(min1.X < max2.X - epsilon && max1.X > min2.X + epsilon) &&
					(min1.Y < max2.Y - epsilon && max1.Y > min2.Y + epsilon) &&
					(min1.Z < max2.Z - epsilon && max1.Z > min2.Z + epsilon);

				if (isColliding) {
					float collision = min1[axis] - min2[axis];
					if (collision < 0) {
						transform.position[axis] = min2[axis] - data.physicsBoundingBox.Corner2()[axis];
					} else {
						transform.position[axis] = max2[axis] - data.physicsBoundingBox.Corner1()[axis];
					}
					transform.velocity[axis] = 0;
					return collision;
				}
			}
		}
		return 0.0f;
	}

	private static int PositiveModulo(float value, int modulator) {
		int newValue = (int)(Math.Floor(value));
		int result = newValue % modulator;
		return result < 0 ? result + modulator : result;
	}

	private static int FloorDiv(float value, int divisor) {
		int newValue = (int)(Math.Floor(value));
		int result = newValue / divisor;
		if (value < 0 && newValue % divisor != 0) {
			result -= 1;
		}
		return result;
	}
}
