namespace KiwiCubed.Engine;

using System.Numerics;
using Arch.Core;
using KiwiCubed.Api;
using Silk.NET.Input;
using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Block;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.IInventory;
using static KiwiCubed.Api.Util;
using static KiwiCubed.Engine.Player;
using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;

public class Player : IDisposable {
	private static ArchWorld archWorld;
	private static ArchEntity player;

	private static InputHandler inputHandler;
	private static ChunkHandler chunkHandler;
	private static VirtualWindow virtualWindow;
	private static Shader terrainShader;
	private static Shader entityShader;

	//public Player(ulong AUID, Vector3 position, Vector3 orientation, World world) : base(AUID, position, orientation) {
	//	SetGameMode(GameMode.SURVIVAL);
	//	playerData.cameraOffset = new Vector3(0.0f, 1.62f, 0.0f);
	//	entityTransform.position = position;
	//	entityTransform.orientation = orientation;
	//
	//	inputHandler = (InputHandler)SystemsManager.Get<IInputHandler>();
    //    chunkHandler = (ChunkHandler)world.GetChunkHandler();
    //    virtualWindow = (VirtualWindow)SystemsManager.Get<IVirtualWindow>();
	//	terrainShader = (Shader)SystemsManager.Get<IAssetManager>().GetShader(new AssetStringID("kiwicubed", "shader/terrain"));
	//	entityShader = (Shader)SystemsManager.Get<IAssetManager>().GetShader(new AssetStringID("kiwicubed", "shader/entity"));
	//
	//	entityStats.health = 20.0f;
	//	entityStats.armor = 0;
	//
	//	entityData.physicsBoundingBox.Resize(new Vector3(-0.3f, 0.0f, -0.3f), new Vector3(0.3f, 1.8f, 0.3f));
	//	entityData.name = playerUsername;
	//
	//	List<AssetStringID> slotStringIDs = new();
	//	for (int slot = 0; slot < 27; slot++) {
	//		slotStringIDs.Add(new AssetStringID("kiwicubed", "inventory_slot_" + (slot < 9 ? "0" + slot : slot)));
	//	}
	//	entityData.inventory = new Inventory(slotStringIDs);
	//
	//	inputHandler.RegisterMouseButtonCallback(MouseButton.Left, MouseButtonCallback, true);
	//	inputHandler.RegisterMouseButtonCallback(MouseButton.Right, MouseButtonCallback, true);
	//	inputHandler.RegisterKeyCallback(Key.F4, (Key key) => {
	//		if (playerData.gameMode == GameMode.CREATIVE) {
	//			SetGameMode(GameMode.SURVIVAL);
	//		} else {
	//			SetGameMode(GameMode.CREATIVE);
	//		}
	//	}, true);
    //    inputHandler.RegisterKeyCallback(Key.F3, (Key key) => {
    //        if (entityData.applyCollision) {
	//			entityData.applyCollision = false;
	//		} else {
	//			entityData.applyCollision = true;
    //        }
    //    }, true);
    //    inputHandler.RegisterKeyCallback(Key.F2, (Key key) => {
    //        if (entityData.applyGravity) {
    //            entityData.applyGravity = false;
    //        } else {
    //            entityData.applyGravity = true;
    //        }
    //    }, true);
	//	inputHandler.RegisterKeyCallback(Key.G, (Key key) => {
	//		SingleplayerHandler.SaveWorld();
	//	}, true);
    //}

	public static void Setup(World world, ArchWorld archWorld, ArchEntity player) {
		Player.archWorld = archWorld;
		Player.player = player;

		inputHandler = (InputHandler)SystemsManager.Get<IInputHandler>();
		chunkHandler = (ChunkHandler)world.GetChunkHandler();
		virtualWindow = (VirtualWindow)SystemsManager.Get<IVirtualWindow>();
		terrainShader = (Shader)SystemsManager.Get<IAssetManager>().GetShader(new AssetStringID("kiwicubed", "shader/terrain"));
		entityShader = (Shader)SystemsManager.Get<IAssetManager>().GetShader(new AssetStringID("kiwicubed", "shader/entity"));

		inputHandler.RegisterMouseButtonCallback(MouseButton.Left, MouseButtonCallback, true);
		inputHandler.RegisterMouseButtonCallback(MouseButton.Right, MouseButtonCallback, true);
		inputHandler.RegisterKeyCallback(Key.F4, (Key key) => {
			EntityPlayerComponent playerComponent = archWorld.Get<EntityPlayerComponent>(player);
			if (playerComponent.playerData.gameMode == GameMode.CREATIVE) {
				SetGameMode(GameMode.SURVIVAL);
			} else {
				SetGameMode(GameMode.CREATIVE);
			}
		}, true);
		inputHandler.RegisterKeyCallback(Key.F3, (Key key) => {
			ref EntityPhysicalComponent physicalComponent = ref archWorld.Get<EntityPhysicalComponent>(player);
			physicalComponent.applyCollision = !physicalComponent.applyCollision;
		}, true);
		inputHandler.RegisterKeyCallback(Key.F2, (Key key) => {
			ref EntityPhysicalComponent physicalComponent = ref archWorld.Get<EntityPhysicalComponent>(player);
			physicalComponent.applyGravity = !physicalComponent.applyGravity;
		}, true);
		inputHandler.RegisterKeyCallback(Key.G, (Key key) => {
			SystemsManager.Get<ISingleplayerHandler>().SaveWorld();
		}, true);
	}

	public static void Update(float partialTicks) {
		EntityTransform transform = archWorld.Get<EntityTransform>(player);
		EntityRenderableComponent renderableComponent = archWorld.Get<EntityRenderableComponent>(player);
		EntityPlayerComponent playerComponent = archWorld.Get<EntityPlayerComponent>(player);

		Vector3 interpolatedPosition = renderableComponent.GetInterpolatedVector(renderableComponent.oldPosition, transform.position, partialTicks);
		playerComponent.camera.Update(interpolatedPosition + playerComponent.playerData.cameraOffset, transform.orientation, playerComponent.FOV, virtualWindow.GetSize());
		playerComponent.camera.SetUniforms(terrainShader);
		playerComponent.camera.SetUniforms(entityShader);

		QueryMouseInputs();
		QueryKeyboardInputs();
	}

	public static void QueryKeyboardInputs() {
		ref EntityTransform transform = ref archWorld.Get<EntityTransform>(player);
		ref EntityPhysicalComponent physicalComponent = ref archWorld.Get<EntityPhysicalComponent>(player);
		ref EntityPlayerComponent playerComponent = ref archWorld.Get<EntityPlayerComponent>(player);

		Vector3 movementVector = Vector3.Zero;
        Vector3 forward = transform.orientation;
        forward.Y = 0;
        forward = Vector3.Normalize(forward);
        Vector3 upDirection = new Vector3(0.0f, 1.0f, 0.0f);
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, upDirection));
        Vector3 up = Vector3.Normalize(upDirection);
		float speed = 0.0f;
		bool shouldJump = false;

		if (playerComponent.playerData.gameMode == GameMode.CREATIVE) {
			if (inputHandler.GetKeyState(Key.W)) {
				movementVector += forward;
			}
			if (inputHandler.GetKeyState(Key.A)) {
				movementVector += -right;
			}
			if (inputHandler.GetKeyState(Key.S)) {
				movementVector += -forward;
			}
			if (inputHandler.GetKeyState(Key.D)) {
				movementVector += right;
			}
			if (inputHandler.GetKeyState(Key.Space)) {
				movementVector += up;
			}
			if (inputHandler.GetKeyState(Key.ShiftLeft)) {
				movementVector += -up;
			}

			speed = physicalComponent.flySpeed;
			if (inputHandler.GetKeyState(Key.ControlLeft)) {
				speed *= physicalComponent.flySprintModifier;
			}
			movementVector *= speed;

			transform.velocity += movementVector;

			float friction = physicalComponent.flyFriction;
			transform.velocity.X *= friction;
			transform.velocity.Y *= friction;
			transform.velocity.Z *= friction;

		} else {
			if (inputHandler.GetKeyState(Key.W)) {
				movementVector += forward;
			}
			if (inputHandler.GetKeyState(Key.A)) {
				movementVector += -right;
			}
			if (inputHandler.GetKeyState(Key.S)) {
				movementVector += -forward;
			}
			if (inputHandler.GetKeyState(Key.D)) {
				movementVector += right;
			}
			if (inputHandler.GetKeyState(Key.Space) && physicalComponent.isGrounded) {
				shouldJump = true;
				physicalComponent.isJumping = true;
			}

			speed = physicalComponent.isGrounded ? physicalComponent.walkSpeed : physicalComponent.airSpeed;
			if (inputHandler.GetKeyState(Key.ControlLeft)) {
				speed *= physicalComponent.walkSprintModifier;
			}

			movementVector *= speed;

			if (shouldJump) {
				movementVector.Y = physicalComponent.jumpHeight;
			}

			transform.velocity += movementVector;

			float friction = physicalComponent.isGrounded ? physicalComponent.groundFriction : physicalComponent.airFrictionHorizontal;
			transform.velocity.X *= friction;
			transform.velocity.Z *= friction;
			transform.velocity.Y *= (float)Math.Pow(physicalComponent.airFrictionVertical, Globals.deltaTime * 20);
		}
	}

	public static void QueryMouseInputs() {
		ref EntityTransform transform = ref archWorld.Get<EntityTransform>(player);
		ref EntityPlayerComponent playerComponent = ref archWorld.Get<EntityPlayerComponent>(player);

		if (!virtualWindow.GetFocused()) {
			playerComponent.lastMouseFocus = false;
			return;
		}

		// Does some absolute magic to rotate the camera correctly
		Vector2 windowSize = virtualWindow.GetSize();
		Vector2 mousePosition = inputHandler.GetMousePosition();

		if (!(mousePosition.X == playerComponent.oldMousePosition.X && mousePosition.Y == playerComponent.oldMousePosition.Y) && playerComponent.lastMouseFocus) {
			// Get the amount to rotate for the frame
			float sensitivity = 100.0f;
			float rotationY = sensitivity * (float)(mousePosition.X - ((float)(windowSize.X) / 2)) / windowSize.X;
			float rotationX = sensitivity * (float)(mousePosition.Y - ((float)(windowSize.Y) / 2)) / windowSize.Y;

			playerComponent.yaw += rotationY;
			playerComponent.pitch += rotationX;

			// Clamp pitch to prevent the camera from flipping out
			if (playerComponent.pitch > 89.9f) {
				playerComponent.pitch = 89.9f;
			} else if (playerComponent.pitch < -89.9f) {
				playerComponent.pitch = -89.9f;
			}

			// wha..? (learnopengl.com)
			Vector3 facing = Vector3.Zero;
			Vector3 orientationRadians = Vector3.DegreesToRadians(new Vector3(playerComponent.pitch, playerComponent.yaw, playerComponent.roll));
			facing.X = (float)(Math.Cos(orientationRadians.Y) * Math.Cos(orientationRadians.X));
			facing.Y = (float)Math.Sin(-orientationRadians.X);
			facing.Z = (float)(Math.Sin(orientationRadians.Y) * Math.Cos(orientationRadians.X));
			transform.orientation = Vector3.Normalize(facing);
		}

		// We don't want anyone to be able to move the mouse off the screen, that would be very very very bad and horrible and would make the game absolutely unplayable
		if (virtualWindow.GetFocused()) {
			inputHandler.SetMousePosition(new Vector2(windowSize.X / 2.0f, windowSize.Y / 2.0f));
			playerComponent.lastMouseFocus = true;
		}

		playerComponent.oldMousePosition = mousePosition;
	}

	private static void MouseButtonCallback(MouseButton button) {
		EntityTransform transform = archWorld.Get<EntityTransform>(player);
		EntityPhysicalComponent physicalComponent = archWorld.Get<EntityPhysicalComponent>(player);
		EntityPlayerComponent playerComponent = archWorld.Get<EntityPlayerComponent>(player);

		VirtualWindow globalWindow = (VirtualWindow)SystemsManager.Get<IVirtualWindow>();
		if (!globalWindow.GetFocused()) {
			return;
		}

		BlockRayHit rayHit = Physics.RaycastWorld(transform.position + playerComponent.playerData.cameraOffset, transform.orientation, 500, (IChunkHandler)chunkHandler);
		IntVector3 blockPosition = rayHit.blockHitPosition.blockPosition;
		IntVector3 chunkPosition = rayHit.blockHitPosition.chunkPosition;
		if (!rayHit.hit) {
			return;
		}
		if (button == MouseButton.Left) {
			Block miningBlock = chunkHandler.GetBlock(rayHit.blockHitPosition);
			AssetStringID blockStringID = miningBlock.GetStringID();
			PlayerBlockInteractionEvent eventData = new PlayerBlockInteractionEvent(BlockInteractionType.BLOCK_MINED, player, rayHit.blockHitPosition, miningBlock.GetStringID());
			SystemsManager.Get<IEventManager>().TriggerEvent<PlayerBlockInteractionEvent>(eventData);
			chunkHandler.RemoveBlock(rayHit.blockHitPosition);
		} else if (button == MouseButton.Right) {
			FullBlockPosition newFullPosition = rayHit.blockHitPosition;
			if (rayHit.faceHitIndex == FaceDirection.INTERIOR) {
				return;
			}
			newFullPosition.AddBlockPosition(BlockFace.GetModifier(rayHit.faceHitIndex));
			IntVector3 newChunkPosition = newFullPosition.chunkPosition;
			bool emptyBlock = ((Chunk)chunkHandler.GetChunk(newChunkPosition, false)).GetBlock(newFullPosition.blockPosition).IsAir();
			bool collidesEntity = Physics.CollideBlock(ref transform, ref physicalComponent, newFullPosition, false);
			if (emptyBlock && !collidesEntity) {
				chunkHandler.AddBlock(newFullPosition, AssetManager.airBlock);
				chunkHandler.RemeshChunk(newChunkPosition.X, newChunkPosition.Y, newChunkPosition.Z, false);
			}
		}

		if (blockPosition.X == 0 || blockPosition.X == chunkSize - 1 || blockPosition.Y == 0 || blockPosition.Y == chunkSize - 1 || blockPosition.Z == 0 || blockPosition.Z == chunkSize - 1) {
			chunkHandler.RemeshChunk(chunkPosition.X, chunkPosition.Y, chunkPosition.Z, false);
			if (blockPosition.X == 0 || blockPosition.X == chunkSize - 1) {
				chunkHandler.RemeshChunk(chunkPosition.X - 1, chunkPosition.Y, chunkPosition.Z, false);
			}
			if (blockPosition.Y == 0 || blockPosition.Y == chunkSize - 1) {
				chunkHandler.RemeshChunk(chunkPosition.X, chunkPosition.Y - 1, chunkPosition.Z, false);
			}
			if (blockPosition.Z == 0 || blockPosition.Z == chunkSize - 1) {
				chunkHandler.RemeshChunk(chunkPosition.X, chunkPosition.Y, chunkPosition.Z - 1, false);
			}
			if (blockPosition.X == chunkSize - 1) {
				chunkHandler.RemeshChunk(chunkPosition.X + 1, chunkPosition.Y, chunkPosition.Z, false);
			}
			if (blockPosition.Y == chunkSize - 1) {
				chunkHandler.RemeshChunk(chunkPosition.X, chunkPosition.Y + 1, chunkPosition.Z, false);
			}
			if (blockPosition.Z == chunkSize - 1) {
				chunkHandler.RemeshChunk(chunkPosition.X, chunkPosition.Y, chunkPosition.Z + 1, false);
			}
		} else {
			chunkHandler.RemeshChunk(chunkPosition.X, chunkPosition.Y, chunkPosition.Z, false);
		}
	}

	public static void SetGameMode(GameMode newGameMode) {
		ref EntityPhysicalComponent physicalComponent = ref archWorld.Get<EntityPhysicalComponent>(player);
		ref EntityPlayerComponent playerComponent = ref archWorld.Get<EntityPlayerComponent>(player);

		playerComponent.playerData.gameMode = newGameMode;
		if (newGameMode == GameMode.CREATIVE) {
			physicalComponent.applyGravity = false;
			physicalComponent.applyCollision = false;
		} else {
			physicalComponent.applyGravity = true;
			physicalComponent.applyCollision = true;
		}
	}
	
	public void Dispose() {
		// Need a controls wrapper around inputhandler wrapper to make this easier and not have InputHandler be a static instance
		//inputHandler.DeregisterCallback()

		// TODO: needs to clean up ArchEntity player
	
        inputHandler = null;
        chunkHandler = null;
        virtualWindow = null;
        terrainShader = null;
	}

	public struct PlayerData {
		public GameMode gameMode = GameMode.CREATIVE;
		public Vector3 cameraOffset = new Vector3(0.0f, 1.8f, 0.0f);

		public PlayerData() { }
	};

	public enum GameMode : byte {
		SURVIVAL,
		CREATIVE
	};
}

public struct EntityPlayerComponent {
	public float FOV = 80.0f;

	public PlayerData playerData = new PlayerData();
	 
	public float pitch = 0.0f;
	public float yaw = -90.0f;
	public float roll = 0.0f;
	public Vector2 oldMousePosition = Vector2.Zero;
	public Camera camera = new Camera();
	public bool lastMouseFocus = false;

	public EntityPlayerComponent() {
		
	}
}