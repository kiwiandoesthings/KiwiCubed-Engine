namespace KiwiCubed.Api;

using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;
using Arch.Core;
using Silk.NET.OpenGL;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Physics;
using static KiwiCubed.Api.Util;

public struct EntityStats {
	public float health = 0.0f;
	public uint armor = 0;

	public EntityStats() {
	}
}

public struct EntityData {
	public object? currentChunk = null;

	public float terminalVelocity = 1000.0f; // Needs to be moved to entity data registration like with models+textures
	public float gravity = 0.08f;

	public BoundingBox physicsBoundingBox = new BoundingBox(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f)); // prolly also needs moved (below too)
	public BoundingBox interactionBoundingBox = new BoundingBox(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f));

	public string name = "";

	public IInventory inventory;

	public float groundFriction = 0.5f;
	public float airFrictionHorizontal = 0.7f;
	public float airFrictionVertical = 0.98f;
	public float flyFriction = 0.5f;

	public float walkSpeed = 5.0f; // same as above, this struct should be for temp modifiers not base entityType stats
	public float airSpeed = 2.0f;
	public float flySpeed = 100.0f;
	public float jumpHeight = 10.0f;

	public float flySprintModifier = 2.0f;
	public float walkSprintModifier = 1.5f;
	public float jumpSprintModifier = 1.15f;

	public bool applyGravity = true;
	public bool applyCollision = true;

	public bool isGrounded = false;
	public bool isJumping = false;
	public bool isFlying = false;

	public EntityData() { }
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
	public readonly ulong AUID = 0UL;

	public ProtectedEntityData(ulong AUID) {
		this.AUID = AUID;
	}
}
public struct EntityTransform {
	public IChunk? currentChunk;

	public Vector3 position = Vector3.Zero;
	public Vector3 orientation = Vector3.Zero;
	public Vector3 upDirection = Vector3.Zero;
	public Vector3 velocity = Vector3.Zero;

	public IntVector3 globalChunkPosition = IntVector3.Zero;
	public IntVector3 localChunkPosition = IntVector3.Zero;

	public EntityTransform(Vector3 position, Vector3 orientation) {
		this.position = position;
		this.orientation = orientation;
	}
}

public abstract class Entity {
	protected EntityStats entityStats = new EntityStats();
	protected EntityData entityData = new EntityData();
	protected EntityRenderData entityRenderData = new EntityRenderData();
	protected ProtectedEntityData protectedEntityData;
	protected EntityTransform entityTransform = new EntityTransform();

	public virtual AssetStringID entityStringID { get; } = new AssetStringID("kiwicubed", "invalid");

	public Entity(ulong AUID, Vector3 position, Vector3 orientation = default) {
		entityTransform.position = position;
		entityTransform.orientation = orientation;

		protectedEntityData = new ProtectedEntityData(AUID);
	}

	public void Setup(IInventory inventory) {
		entityData.inventory = inventory;
	}

	public virtual void Update(IChunkHandler chunkHandler) {
		EntityPhysicalComponent TEMPORARY = new EntityPhysicalComponent();
		if (ApplyPhysics(chunkHandler, ref entityTransform, ref TEMPORARY)) {
			entityData.isGrounded = true;
			entityData.isJumping = false;
		}

		entityTransform.globalChunkPosition = new IntVector3(FloorDiv(entityTransform.position, 32));
		entityTransform.localChunkPosition = new IntVector3(PositiveModulo(entityTransform.position, 32));
	}

	public virtual void Render() { }

	public ref EntityStats GetEntityStats() {
		return ref entityStats;
	}

	public ref EntityData GetEntityData() {
		return ref entityData;
	}

	public ProtectedEntityData GetProtectedEntityData() {
		return protectedEntityData;
	}

	public ref EntityTransform GetEntityTransform() {
		return ref entityTransform;
	}

	public void SetEntityData(EntityData entityData) {
		this.entityData = entityData;
	}

	public void SetEntityTransform(EntityTransform entityTransform) {
		this.entityTransform = entityTransform;
	}

	public virtual List<AssetStringID> GetInventorySlotIDs() {
		return new List<AssetStringID>();
	}
}

public class EntityType {
	public AssetStringID stringID;
	public ComponentType[] components;
	public EntitySetup setupFunction;

	public EntityType(AssetStringID entityStringID, ComponentType[] entityComponents, EntitySetup entitySetupFunction) {
		stringID = entityStringID;
		components = entityComponents;
		setupFunction = entitySetupFunction;
	}
}

public delegate void EntitySetup(ArchWorld world, ArchEntity entity);

public readonly struct EntityIdentifierComponent {
	public readonly Guid entityGuid;
	public readonly AssetStringID entityTypeStringID;

	public EntityIdentifierComponent(Guid entityGuid, AssetStringID entityTypeStringID) {
		this.entityGuid = entityGuid;
		this.entityTypeStringID = entityTypeStringID;
	}
}

public struct EntityTransformComponent {
	public Vector3 position;
	public Vector3 orientation;

	public EntityTransformComponent(Vector3 position, Vector3 orientation) {
		this.position = position;
		this.orientation = orientation;
	}

	public EntityTransformComponent() {
		position = Vector3.Zero;
		orientation = Vector3.Zero;
	}
}

public struct EntityRenderableComponent {
	public bool visible;
	public IRenderBuffers renderBuffers;
	public GeneralMesh mesh;

	public Vector3 renderScale = Vector3.One;
	public Vector3 positionOffset = Vector3.Zero;
	public Vector3 orientationOffset = Vector3.Zero;

	public Vector3 oldPosition = Vector3.Zero;
	public Vector3 oldOrientation = Vector3.Zero;
	public Vector3 oldPositionOffset = Vector3.Zero;
	public Vector3 oldOrientationOffset = Vector3.Zero;

	public EntityRenderableComponent(bool isVisible, GeneralMesh entityMesh) {
		visible = isVisible;
		renderBuffers = Renderer.CreateRenderBuffers();
		mesh = entityMesh;

		uint stride = 5 * sizeof(float);
		renderBuffers.LinkAttribute(0, 3, VertexAttribPointerType.Float, stride, 0);
		renderBuffers.LinkAttribute(1, 2, VertexAttribPointerType.Float, stride, (sizeof(float) * 3));
		Renderer.UpdateBuffers(renderBuffers, mesh.vertices, mesh.indices);
	}

	public Vector3 GetInterpolatedVector(Vector3 oldValues, Vector3 newValues, float partialTicks) {
		return oldValues + (newValues - oldValues) * partialTicks;
	}
}

public struct EntityPhysicalComponent {
	public bool applyGravity = true;
	public bool applyCollision = true;

	public float terminalVelocity = 100.0f;
	public float gravity = 0.08f;

	public BoundingBox physicsBoundingBox = new BoundingBox(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f));
	public BoundingBox interactionBoundingBox = new BoundingBox(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f));

	public float groundFriction = 0.5f;
	public float airFrictionHorizontal = 0.7f;
	public float airFrictionVertical = 0.98f;
	public float flyFriction = 0.2f;

	public float walkSpeed = 0.1f;
	public float airSpeed = 0.05f;
	public float flySpeed = 0.5f;
	public float jumpHeight = 0.42f;

	public float flySprintModifier = 2.0f;
	public float walkSprintModifier = 1.5f;
	public float jumpSprintModifier = 1.15f;

	public bool isGrounded = false;
	public bool shouldJump = false;
	public bool isJumping = false;
	public bool isFlying = false;

	public EntityPhysicalComponent() { }
}