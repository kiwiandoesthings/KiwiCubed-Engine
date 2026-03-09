namespace KiwiCubed.Api;

using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Util;

public struct EntityStats {
	public float health = 0.0f;
	public uint armor = 0;

	public EntityStats() {
	}
}

public struct EntityData {
	public object? currentChunk = null;

	public float terminalVelocity = 100.0f; // Needs to be moved to entity data registration like with models+textures

	public BoundingBox physicsBoundingBox = new BoundingBox(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f)); // prolly also needs moved (below too)
	public BoundingBox interactionBoundingBox = new BoundingBox(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f));

	public string name = "";

	//Inventory inventory;

	public float walkSpeed = 1.0f; // same as above, this struct should be for temp modifiers not base entityType stats
	public float jumpHeight = 1.25f;

	public float walkModifier = 1.0f;
	public float jumpModifier = 1.0f;

	public bool applyGravity = true;
	public bool applyCollision = true;

	public bool isGrounded = false;
	public bool isJumping = false;
	public bool isFlying = false;

	public bool isPlayer = false; //should also have better solution for this

	public EntityData() {
	}
}

public struct EntityRenderData {
	public IntVector3 oldOldPosition = IntVector3.Zero;
	public IntVector3 oldOrientation = IntVector3.Zero;
	public IntVector3 oldUpDirection = IntVector3.Zero;
	public IntVector3 oldVelocity;

	public IntVector3 positionOffset = IntVector3.Zero;
	public IntVector3 orientationOffset = IntVector3.Zero;
	public IntVector3 oldPositionOffset = IntVector3.Zero;
	public IntVector3 oldOrientationOffset = IntVector3.Zero;

	public EntityRenderData() {
	}
}

public struct ProtectedEntityData {
	public ulong AUID = 0UL;

	public ProtectedEntityData() {
	}
}
public struct EntityTransform {
	public Vector3 position = Vector3.Zero;
	public Vector3 orientation = Vector3.Zero;
	public Vector3 upDirection = Vector3.Zero;
	public Vector3 velocity = Vector3.Zero;

	public IntVector3 globalChunkPosition = IntVector3.Zero;
	public IntVector3 localChunkPosition = IntVector3.Zero;

	public EntityTransform() {
	}
}

public abstract class Entity {
	protected EntityStats entityStats;
	protected EntityData entityData;
	protected EntityRenderData entityRenderData;
	protected ProtectedEntityData protectedEntityData;
	protected EntityTransform entityTransform;

	public virtual AssetStringID entityStringID { get; } = new AssetStringID("kiwicubed", "invalid");

	public Entity(ulong AUID, Vector3 position, Vector3 orientation = default) {
		entityTransform.position = position;
		entityTransform.orientation = orientation;

		protectedEntityData.AUID = AUID;
	}

	public EntityData GetEntityData() {
		return entityData;
	}

	public EntityTransform GetEntityTransform() {
		return entityTransform;
	}

	public void SetEntityData(EntityData entityData) {
		this.entityData = entityData;
	}

	public void SetEntityTransform(EntityTransform entityTransform) {
		this.entityTransform = entityTransform;
	}
}
