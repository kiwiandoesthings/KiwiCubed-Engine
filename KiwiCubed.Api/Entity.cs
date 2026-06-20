namespace KiwiCubed.Api;

using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;
using Arch.Core;
using LiteNetLib.Utils;
using Silk.NET.OpenGL;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Utils;

public delegate void ArchEntitySerializer(NetDataWriter writer, ArchEntity entity);
public delegate void ArchEntityDeserializer(NetDataReader reader, ArchEntity entity);
public struct ArchEntityNetworkFunctions {
    public ArchEntitySerializer serializer = (writer, entity) => { };
    public ArchEntityDeserializer deserializer = (reader, entity) => { };

	public ArchEntityNetworkFunctions() { }

    public ArchEntityNetworkFunctions(ArchEntitySerializer serializer, ArchEntityDeserializer deserializer) {
        this.serializer = serializer;
        this.deserializer = deserializer;
    }
}

public class EntityType {
	public AssetStringID stringID;
	public ComponentType[] components;
	public EntitySetup setupFunction;
	public ArchEntityNetworkFunctions networkFunctions;

    public EntityType(AssetStringID entityStringID, ComponentType[] entityComponents, EntitySetup entitySetupFunction) {
        stringID = entityStringID;
        components = entityComponents;
        setupFunction = entitySetupFunction;
		networkFunctions = new ArchEntityNetworkFunctions();
    }

	public EntityType(AssetStringID entityStringID, ComponentType[] entityComponents, EntitySetup entitySetupFunction, ArchEntityNetworkFunctions entityNetworkFunctions) {
		stringID = entityStringID;
		components = entityComponents;
		setupFunction = entitySetupFunction;
		networkFunctions = entityNetworkFunctions;
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
    public Quaternion orientation = Quaternion.Identity;
    public Vector3 upDirection = Vector3.Zero;
    public Vector3 velocity = Vector3.Zero;

    public IntVector3 globalChunkPosition = IntVector3.Zero;
    public IntVector3 localChunkPosition = IntVector3.Zero;

    public EntityTransformComponent(Vector3 position, Quaternion orientation) {
        this.position = position;
        this.orientation = orientation;
    }

	public SimpleTransform AsSimpleTransform() {
		return new SimpleTransform(position, orientation);
	}
}

public struct EntityRenderableComponent {
	public bool renderBuffersSetup { get; private set; }
	public bool renderBuffersDirty { get; set; }

	public bool visible;
	public IRenderBuffers renderBuffers;
	public GeneralMesh mesh;

	public Vector3 renderScale = Vector3.One;
	public Vector3 positionOffset = Vector3.Zero;
	public Quaternion orientationOffset = Quaternion.Identity;

	public Vector3 oldPosition = Vector3.Zero;
	public Quaternion oldOrientation = Quaternion.Identity;
	public Vector3 oldPositionOffset = Vector3.Zero;
	public Quaternion oldOrientationOffset = Quaternion.Identity;

	public EntityRenderableComponent(bool isVisible, GeneralMesh entityMesh) {
		renderBuffersSetup = false;
		renderBuffersDirty = false;
		visible = isVisible;
		mesh = entityMesh;
	}

    public void SetupRenderBuffers() {
        if (!renderBuffersSetup) {
            renderBuffers = Renderer.CreateRenderBuffers();

            uint stride = 5 * sizeof(float);
            renderBuffers.LinkAttribute(0, 3, VertexAttribPointerType.Float, stride, 0);
            renderBuffers.LinkAttribute(1, 2, VertexAttribPointerType.Float, stride, (sizeof(float) * 3));
            Renderer.UpdateBuffers(renderBuffers, mesh.vertices, mesh.indices);

            renderBuffersSetup = true;
            renderBuffersDirty = false;
        } else if (renderBuffersDirty) {
            Renderer.UpdateBuffers(renderBuffers, mesh.vertices, mesh.indices);

            renderBuffersDirty = false;
        }
    }

    public static Vector3 GetInterpolatedVector(Vector3 oldValues, Vector3 newValues, float partialTicks) {
		return oldValues + (newValues - oldValues) * partialTicks;
	}
}

public struct EntityPhysicalComponent {
	public bool applyGravity = true;
	public bool applyCollision = true;

	public float terminalVelocity = 100.0f;
	public float gravity = 10.0f;

	public BoundingBox physicsBoundingBox = new BoundingBox(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f));
	public BoundingBox interactionBoundingBox = new BoundingBox(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f));

	public float groundFriction = 0.5f;
	public float airFrictionHorizontal = 0.9f;
	public float airFrictionVertical = 0.98f;
	public float flyFriction = 0.2f;

	public float walkSpeed = 2.0f;
	public float airSpeed = 0.5f;
	public float flySpeed = 7.0f;
	public float jumpHeight = 4.2f;

	public float flySprintModifier = 2.0f;
	public float walkSprintModifier = 1.5f;
	public float jumpSprintModifier = 1.15f;

	public bool isGrounded = false;
	public bool shouldJump = false;
	public bool isJumping = false;
	public bool isFlying = false;

	public EntityPhysicalComponent() { }
}