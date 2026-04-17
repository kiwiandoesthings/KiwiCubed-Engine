namespace KiwiCubed.Api;

using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;
using Arch.Core;
using Silk.NET.OpenGL;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Util;

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
	public readonly ulong entityAUID;
	public readonly AssetStringID entityTypeStringID;

	public EntityIdentifierComponent(ulong entityAUID, AssetStringID entityTypeStringID) {
		this.entityAUID = entityAUID;
		this.entityTypeStringID = entityTypeStringID;
	}
}

public struct EntityTransformComponent {
    public IChunk? currentChunk;

    public Vector3 position = Vector3.Zero;
    public Vector3 orientation = Vector3.Zero;
    public Vector3 upDirection = Vector3.Zero;
    public Vector3 velocity = Vector3.Zero;

    public IntVector3 globalChunkPosition = IntVector3.Zero;
    public IntVector3 localChunkPosition = IntVector3.Zero;

    public EntityTransformComponent(Vector3 position, Vector3 orientation) {
        this.position = position;
        this.orientation = orientation;
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