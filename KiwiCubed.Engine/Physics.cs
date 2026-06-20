namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using System.Numerics;

using static KiwiCubed.Api.Block;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.Utils;

public class PhysicsWrapper : IPhysics {
	public void ApplyPhysics(IChunkHandler virtualChunkHandler, ref EntityTransformComponent transform, ref EntityPhysicalComponent physicalComponent, double delta) => PhysicsSystem.ApplyPhysics(virtualChunkHandler, ref transform, ref physicalComponent, delta);
	public BlockRayHit RaycastWorld(Vector3 origin, Vector3 direction, int maxDistance, IChunkHandler chunkHandler) => PhysicsSystem.RaycastWorld(origin, direction, maxDistance, chunkHandler);
	public bool GetGrounded(ref EntityTransformComponent transform, ref EntityPhysicalComponent physicalComponent, IChunkHandler virtualChunkHandler) => PhysicsSystem.GetGrounded(ref transform, ref physicalComponent, virtualChunkHandler);
	public bool CollideBlock(ref EntityTransformComponent transform, ref EntityPhysicalComponent physicalComponent, FullBlockPosition fullBlockPosition, bool resolveCollision) => PhysicsSystem.CollideBlock(ref transform, ref physicalComponent, fullBlockPosition, resolveCollision);
}

public class PhysicsSystem {
	private static double epsilon = 1e-5f;

	public static void ApplyPhysics(IChunkHandler virtualChunkHandler, ref EntityTransformComponent transform, ref EntityPhysicalComponent physicalComponent, double deltaTime) {
		float delta = (float)deltaTime;
		ChunkHandler chunkHandler = (ChunkHandler)virtualChunkHandler;

		// need player handling for gamemode specific frictions? yes
		//float friction = physicalComponent.flyFriction;
		//transform.velocity.X *= friction;
		//transform.velocity.Y *= friction;
		//transform.velocity.Z *= friction;
		
		if (physicalComponent.applyGravity) {
			ApplyGravity(ref transform, ref physicalComponent, delta);
		}

        float baseHorizontalFriction = physicalComponent.isGrounded ? physicalComponent.groundFriction : physicalComponent.airFrictionHorizontal;
        float recalculatedHorizontalFriction = MathF.Pow(baseHorizontalFriction, delta * World.targetTps);
		float recalculatedVerticalFriction = MathF.Pow(physicalComponent.airFrictionVertical, delta * World.targetTps);

        transform.velocity.X *= recalculatedHorizontalFriction;
        transform.velocity.Z *= recalculatedHorizontalFriction;
		transform.velocity.Y *= recalculatedVerticalFriction;

        if (physicalComponent.shouldJump) {
			transform.velocity.Y = physicalComponent.jumpHeight;
			physicalComponent.shouldJump = false;
			physicalComponent.isJumping = true;
		}

		if (physicalComponent.applyCollision) {
			ApplyTerrainCollision(ref transform, ref physicalComponent, chunkHandler, delta);
            if (physicalComponent.isGrounded) {
                physicalComponent.isJumping = false;
            }
        } else {
			transform.position.X += transform.velocity.X * delta;
			transform.position.Y += transform.velocity.Y * delta;
			transform.position.Z += transform.velocity.Z * delta;
		}

		ClipVelocity(ref transform, ref physicalComponent, 0);
		ClipVelocity(ref transform, ref physicalComponent, 1);
		ClipVelocity(ref transform, ref physicalComponent, 2);
	}

	// Literally what the fuck even is this functionn I hate raycasting so much
	// TODO: stop using the out parameters and just keep the return struct
	public static BlockRayHit RaycastWorld(Vector3 origin, Vector3 direction, int maxDistance, IChunkHandler virtualChunkHandler) {
		ChunkHandler chunkHandler = (ChunkHandler)virtualChunkHandler;
		IntVector3 currentBlock = new IntVector3(
			(int)Math.Floor(origin.X),
			(int)Math.Floor(origin.Y),
			(int)Math.Floor(origin.Z)
		);
		IntVector3 currentChunk = new IntVector3((int)Math.Floor((float)currentBlock.X / chunkSize), (int)Math.Floor((float)currentBlock.Y / chunkSize), (int)Math.Floor((float)currentBlock.Z / chunkSize));

		IntVector3 stepDirection = new IntVector3(direction.X > 0 ? 1 : -1, direction.Y > 0 ? 1 : -1, direction.Z > 0 ? 1 : -1);
		int lastStepAxis = 0;

		Vector3 stepsilonize = new Vector3(Math.Abs(1.0f / direction.X), Math.Abs(1.0f / direction.Y), Math.Abs(1.0f / direction.Z));
		Vector3 distanceToBlockBoundary = new Vector3((currentBlock.X + (stepDirection.X > 0 ? 1 : 0) - origin.X) / direction.X, (currentBlock.Y + (stepDirection.Y > 0 ? 1 : 0) - origin.Y) / direction.Y, (currentBlock.Z + (stepDirection.Z > 0 ? 1 : 0) - origin.Z) / direction.Z);

		BlockRayHit rayHit = new BlockRayHit();
		rayHit.faceHitIndex = FaceDirection.INTERIOR;
		for (int i = 0; i < maxDistance; ++i) {
			IntVector3 localBlockPos = new IntVector3(PositiveModulo((float)currentBlock.X, chunkSize), PositiveModulo((float)currentBlock.Y, chunkSize), PositiveModulo((float)currentBlock.Z, chunkSize));

			Chunk chunk = (Chunk)chunkHandler.GetChunk(currentChunk.X, currentChunk.Y, currentChunk.Z, false);
			if (chunk.IsGenerated()) {
				ushort block = chunk.GetBlock(localBlockPos.X, localBlockPos.Y, localBlockPos.Z);
				if (block != 0) {
					rayHit.hit = true;
					rayHit.blockHitPosition.blockPosition = new IntVector3(PositiveModulo((float)currentBlock.X, chunkSize), PositiveModulo((float)currentBlock.Y, chunkSize), PositiveModulo((float)currentBlock.Z, chunkSize));
					rayHit.blockHitPosition.chunkPosition = currentChunk;
					if (i == 0) {
						rayHit.faceHitIndex = FaceDirection.INTERIOR;
					} else {
						if (lastStepAxis == 0) {
							rayHit.faceHitIndex = (direction.X) > 0 ? FaceDirection.LEFT : FaceDirection.RIGHT;
						} else if (lastStepAxis == 1) {
							rayHit.faceHitIndex = (direction.Y) > 0 ? FaceDirection.BOTTOM : FaceDirection.TOP;
						} else {
							rayHit.faceHitIndex = (direction.Z) > 0 ? FaceDirection.BACK : FaceDirection.FRONT;
						}
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

	public static bool GetGrounded(ref EntityTransformComponent transform, ref EntityPhysicalComponent physicalComponent, IChunkHandler virtualChunkHandler) {
		ChunkHandler chunkHandler = (ChunkHandler)virtualChunkHandler;
		Span<FullBlockPosition> collisionQueue = stackalloc FullBlockPosition[512];
		Span<FullBlockPosition> usingQueue = FillCollisionQueue(collisionQueue, ref transform, ref physicalComponent, chunkHandler);
		return CollideAxisFloat(1, ref transform, ref physicalComponent, chunkHandler, usingQueue) > 0;
	}

	public static bool CollideBlock(ref EntityTransformComponent transform, ref EntityPhysicalComponent physicalComponent, FullBlockPosition fullBlockPosition, bool resolveCollision) {
		for (int axis = 0; axis < 3; axis++) {
			Vector3 min1 = new Vector3(physicalComponent.physicsBoundingBox.Corner1().X + transform.position.X, physicalComponent.physicsBoundingBox.Corner1().Y + transform.position.Y, physicalComponent.physicsBoundingBox.Corner1().Z + transform.position.Z);
			Vector3 max1 = new Vector3(physicalComponent.physicsBoundingBox.Corner2().X + transform.position.X, physicalComponent.physicsBoundingBox.Corner2().Y + transform.position.Y, physicalComponent.physicsBoundingBox.Corner2().Z + transform.position.Z);
			Vector3 min2 = new Vector3(fullBlockPosition.blockPosition.X + (fullBlockPosition.chunkPosition.X * chunkSize), fullBlockPosition.blockPosition.Y + (fullBlockPosition.chunkPosition.Y * chunkSize), fullBlockPosition.blockPosition.Z + (fullBlockPosition.chunkPosition.Z * chunkSize));
			Vector3 max2 = min2 + new Vector3(1.0f);

			bool isColliding =
					(min1.X < max2.X - epsilon && max1.X > min2.X + epsilon) &&
					(min1.Y < max2.Y - epsilon && max1.Y > min2.Y + epsilon) &&
					(min1.Z < max2.Z - epsilon && max1.Z > min2.Z + epsilon);

			if (isColliding) {
				if (!resolveCollision) {
					return true;
				}
				float collision = min1[axis] - min2[axis];
				if (collision < 0) {
					transform.position[axis] = min2[axis] - physicalComponent.physicsBoundingBox.Corner2()[axis];
				} else {
					transform.position[axis] = max2[axis] - physicalComponent.physicsBoundingBox.Corner1()[axis];
				}
				transform.velocity[axis] = 0;
				return true; // early return as soon as 1 collision is detected makes big hitboxes simply clip through many blocks, needs to resolve ALL collisionns
			}
		}
		return false;
	}

	private static bool ApplyTerrainCollision(ref EntityTransformComponent transform, ref EntityPhysicalComponent physicalComponent, ChunkHandler chunkHandler, float delta) {
		Span<FullBlockPosition> collisionQueue = stackalloc FullBlockPosition[512];
		Span<FullBlockPosition> usingQueue = FillCollisionQueue(collisionQueue, ref transform, ref physicalComponent, chunkHandler);

		transform.position.X += transform.velocity.X * delta;
		bool xAxis = CollideAxis(0, ref transform, ref physicalComponent, chunkHandler, usingQueue);
		transform.position.Y += transform.velocity.Y * delta;
		float yAxis = CollideAxisFloat(1, ref transform, ref physicalComponent, chunkHandler, usingQueue);
		transform.position.Z += transform.velocity.Z * delta;
		bool zAxis = CollideAxis(2, ref transform, ref physicalComponent, chunkHandler, usingQueue);

		physicalComponent.isGrounded = (yAxis > 0);

		if (xAxis || yAxis != 0 || zAxis) {
			return true;
		}

		return false;
	}

	private static void ApplyGravity(ref EntityTransformComponent transform, ref EntityPhysicalComponent physicalComponent, float delta) {
		transform.velocity.Y -= physicalComponent.gravity * delta;
	}

	private static void ClipVelocity(ref EntityTransformComponent transform, ref EntityPhysicalComponent physicalComponent, int axis) {
		float axisVelocity = Math.Abs(transform.velocity[axis]);
		if (axisVelocity < 0.001f) {
			transform.velocity[axis] = 0.0f;
		} else if (axisVelocity > physicalComponent.terminalVelocity) {
			if (transform.velocity[axis] > 0) {
				transform.velocity[axis] = physicalComponent.terminalVelocity;
			} else {
				transform.velocity[axis] = -physicalComponent.terminalVelocity;
			}
		}
	}

	private static Span<FullBlockPosition> FillCollisionQueue(Span<FullBlockPosition> blockCollisionQueue, ref EntityTransformComponent transform, ref EntityPhysicalComponent physicalComponent, ChunkHandler chunkHandler) {
		blockCollisionQueue.Clear();
		int index = 0;
		Vector3 minCorner = Vector3.Min(physicalComponent.physicsBoundingBox.Corner1() + transform.position, physicalComponent.physicsBoundingBox.Corner2() + transform.position);
		Vector3 maxCorner = Vector3.Max(physicalComponent.physicsBoundingBox.Corner1() + transform.position, physicalComponent.physicsBoundingBox.Corner2() + transform.position);

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

					if (((Chunk)chunkHandler.GetChunk(chunkPosition.X, chunkPosition.Y, chunkPosition.Z, false)).GetBlock(blockPosition.X, blockPosition.Y, blockPosition.Z) != 0) {
						blockCollisionQueue[index] = new FullBlockPosition(blockPosition, chunkPosition);
						index++;
					}
				}
			}
		}

		return blockCollisionQueue.Slice(0, index);
	}

	private static bool CollideAxis(int axis, ref EntityTransformComponent transform, ref EntityPhysicalComponent physicalComponent, ChunkHandler chunkHandler, ReadOnlySpan<FullBlockPosition> collisionQueue) {
		Chunk currentChunk = (Chunk)transform.currentChunk;
		if (currentChunk == null || !currentChunk.IsGenerated()) {
			return false;
		}

		Vector3 min1 = new Vector3(physicalComponent.physicsBoundingBox.Corner1().X + transform.position.X, physicalComponent.physicsBoundingBox.Corner1().Y + transform.position.Y, physicalComponent.physicsBoundingBox.Corner1().Z + transform.position.Z);
		Vector3 max1 = new Vector3(physicalComponent.physicsBoundingBox.Corner2().X + transform.position.X, physicalComponent.physicsBoundingBox.Corner2().Y + transform.position.Y, physicalComponent.physicsBoundingBox.Corner2().Z + transform.position.Z);
		foreach (FullBlockPosition blockPosition in collisionQueue) {
			Chunk targetChunk = null;
			if (blockPosition.chunkPosition.X != transform.globalChunkPosition.X || blockPosition.chunkPosition.Y != transform.globalChunkPosition.Y || blockPosition.chunkPosition.Z != transform.globalChunkPosition.Z) {
				targetChunk = (Chunk)chunkHandler.GetChunk(blockPosition.chunkPosition.X, blockPosition.chunkPosition.Y, blockPosition.chunkPosition.Z, false);
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

				bool isColliding =
					(min1.X < max2.X - epsilon && max1.X > min2.X + epsilon) &&
					(min1.Y < max2.Y - epsilon && max1.Y > min2.Y + epsilon) &&
					(min1.Z < max2.Z - epsilon && max1.Z > min2.Z + epsilon);

				if (isColliding) {
					float collision = min1[axis] - min2[axis];
					if (collision < 0) {
						transform.position[axis] = min2[axis] - physicalComponent.physicsBoundingBox.Corner2()[axis];
					} else {
						transform.position[axis] = max2[axis] - physicalComponent.physicsBoundingBox.Corner1()[axis];
					}
					transform.velocity[axis] = 0;
					return true;
				}
			}
		}
		return false;
	}

	private static float CollideAxisFloat(int axis, ref EntityTransformComponent transform, ref EntityPhysicalComponent physicalComponent, ChunkHandler chunkHandler, ReadOnlySpan<FullBlockPosition> collisionQueue) {
		Chunk currentChunk = (Chunk)transform.currentChunk;
		if (currentChunk == null || !currentChunk.IsGenerated()) {
			return 0.0f;
		}

		Vector3 min1 = new Vector3(physicalComponent.physicsBoundingBox.Corner1().X + transform.position.X, physicalComponent.physicsBoundingBox.Corner1().Y + transform.position.Y, physicalComponent.physicsBoundingBox.Corner1().Z + transform.position.Z);
		Vector3 max1 = new Vector3(physicalComponent.physicsBoundingBox.Corner2().X + transform.position.X, physicalComponent.physicsBoundingBox.Corner2().Y + transform.position.Y, physicalComponent.physicsBoundingBox.Corner2().Z + transform.position.Z);
		foreach (FullBlockPosition blockPosition in collisionQueue) {
			Chunk targetChunk = null;
			if (blockPosition.chunkPosition.X != transform.globalChunkPosition.X || blockPosition.chunkPosition.Y != transform.globalChunkPosition.Y || blockPosition.chunkPosition.Z != transform.globalChunkPosition.Z) {
				targetChunk = (Chunk)chunkHandler.GetChunk(blockPosition.chunkPosition.X, blockPosition.chunkPosition.Y, blockPosition.chunkPosition.Z, false);
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

				bool isColliding =
					(min1.X < max2.X - epsilon && max1.X > min2.X + epsilon) &&
					(min1.Y < max2.Y - epsilon && max1.Y > min2.Y + epsilon) &&
					(min1.Z < max2.Z - epsilon && max1.Z > min2.Z + epsilon);

				if (isColliding) {
					float collision = min1[axis] - min2[axis];
					if (collision < 0) {
						transform.position[axis] = min2[axis] - physicalComponent.physicsBoundingBox.Corner2()[axis];
					} else {
						transform.position[axis] = max2[axis] - physicalComponent.physicsBoundingBox.Corner1()[axis];
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
