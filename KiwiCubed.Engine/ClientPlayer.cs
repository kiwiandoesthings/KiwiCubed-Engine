namespace KiwiCubed.Engine;

using Arch.Core;
using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;
using KiwiCubed.Api;
using Silk.NET.Input;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Block;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.IPlayer;
using static KiwiCubed.Api.Util;

public class ClientPlayer : IDisposable {
	private static ArchWorld archWorld;
	private static ArchEntity player;

	private static InputHandler inputHandler;
	private static ChunkHandler chunkHandler;
	private static VirtualWindow virtualWindow;
	private static AssetManager assetManager;
	private static Shader terrainShader;
	private static Shader entityShader;

	//public Player(ulong ulong, Vector3 position, Vector3 orientation, World world) : base(ulong, position, orientation) {
	//	SetGameMode(GameMode.SURVIVAL);
	//	playerData.cameraOffset = new Vector3(0.0f, 1.62f, 0.0f);
	//	entityTransform.position = position;
	//	entityTransform.orientation = orientation;
	//
	//	inputHandler = (InputHandler)MetaHandler.Get<IInputHandler>();
    //    chunkHandler = (ChunkHandler)world.GetChunkHandler();
    //    virtualWindow = (VirtualWindow)MetaHandler.Get<IVirtualWindow>();
	//	terrainShader = (Shader)MetaHandler.Get<IAssetManager>().GetShader(new AssetStringID("kiwicubed", "shader/terrain"));
	//	entityShader = (Shader)MetaHandler.Get<IAssetManager>().GetShader(new AssetStringID("kiwicubed", "shader/entity"));
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
		ClientPlayer.archWorld = archWorld;
		ClientPlayer.player = player;

		inputHandler = (InputHandler)MetaHandler.Get<IInputHandler>();
		chunkHandler = (ChunkHandler)world.GetChunkHandler();
		virtualWindow = (VirtualWindow)MetaHandler.Get<IVirtualWindow>();
		assetManager = (AssetManager)MetaHandler.Get<IAssetManager>();
        terrainShader = (Shader)MetaHandler.Get<IAssetManager>().GetShader(new AssetStringID("kiwicubed", "shader/terrain"));
		entityShader = (Shader)MetaHandler.Get<IAssetManager>().GetShader(new AssetStringID("kiwicubed", "shader/entity"));

		inputHandler.RegisterMouseButtonCallback(MouseButton.Left, MouseButtonCallback, true);
		inputHandler.RegisterMouseButtonCallback(MouseButton.Right, MouseButtonCallback, true);
		inputHandler.RegisterKeyCallback(Key.F4, (Key key) => {
			EntityPlayerComponent playerComponent = archWorld.Get<EntityPlayerComponent>(player);
			if (playerComponent.gameMode == GameMode.CREATIVE) {
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
		//inputHandler.RegisterKeyCallback(Key.G, (Key key) => {
		//	MetaHandler.Get<ISingleplayerHandler>().SaveWorld();
		//}, true);

		SetGameMode(GameMode.CREATIVE);

		ref EntityPhysicalComponent physicalComponent = ref archWorld.Get<EntityPhysicalComponent>(player);
		ref EntityPlayerClientComponent playerClientComponent = ref archWorld.Get<EntityPlayerClientComponent>(player);
		ref EntityRenderableComponent renderableComponent = ref archWorld.Get<EntityRenderableComponent>(player);
		physicalComponent.physicsBoundingBox.Resize(new Vector3(-0.3f, 0.0f, -0.3f), new Vector3(0.3f, 1.8f, 0.3f));
		playerClientComponent.cameraOffset = new Vector3(0.0f, 1.62f, 0.0f);
		renderableComponent.visible = false;
    }

    public static void Update(World world, double deltaTime) {
        ref EntityTransformComponent transformComponent = ref archWorld.Get<EntityTransformComponent>(player);
        ref EntityPlayerClientComponent playerClientComponent = ref archWorld.Get<EntityPlayerClientComponent>(player);
		ref EntityPhysicalComponent physicalComponent = ref archWorld.Get<EntityPhysicalComponent>(player);

        QueryMouseInputs();

        playerClientComponent.camera.Update(transformComponent.position + playerClientComponent.cameraOffset, transformComponent.orientation, playerClientComponent.FOV, virtualWindow.GetSize());
        playerClientComponent.camera.SetUniforms(terrainShader);
        playerClientComponent.camera.SetUniforms(entityShader);

        QueryKeyboardInputs();

        Physics.ApplyPhysics(chunkHandler, ref transformComponent, ref physicalComponent, deltaTime);
    }

    public static void QueryKeyboardInputs(PlayerInput[] inputs, ArchWorld archWorld, ArchEntity playerEntity) {
		ref EntityTransformComponent transform = ref archWorld.Get<EntityTransformComponent>(playerEntity);
		ref EntityPhysicalComponent physicalComponent = ref archWorld.Get<EntityPhysicalComponent>(playerEntity);
		ref EntityPlayerComponent playerComponent = ref archWorld.Get<EntityPlayerComponent>(playerEntity);

		Vector3 movementVector = Vector3.Zero;
		Vector3 forward = Vector3.Transform(new Vector3(0, 0, -1), transform.orientation);
        forward.Y = 0;
        forward = Vector3.Normalize(forward);
		Vector3 upDirection = Vector3.Transform(Vector3.UnitY, transform.orientation);
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, upDirection));
        Vector3 up = Vector3.Normalize(upDirection);
		float speed = 0.0f;

		if (playerComponent.gameMode == GameMode.CREATIVE) {
			if (inputs.Contains(PlayerInput.MoveForward)) {
				movementVector += forward;
			}
			if (inputs.Contains(PlayerInput.MoveLeft)) {
				movementVector += -right;
			}
			if (inputs.Contains(PlayerInput.MoveBackward)) {
				movementVector += -forward;
			}
			if (inputs.Contains(PlayerInput.MoveRight)) {
				movementVector += right;
			}
			if (inputs.Contains(PlayerInput.MoveUp)) {
				movementVector += up;
			}
			if (inputs.Contains(PlayerInput.MoveDown)) {
				movementVector += -up;
			}

			speed = physicalComponent.flySpeed;
			if (inputs.Contains(PlayerInput.Sprint)) {
				speed *= physicalComponent.flySprintModifier;
			}
			movementVector *= speed;

			transform.velocity += movementVector;
		} else {
			if (inputs.Contains(PlayerInput.MoveForward)) {
				movementVector += forward;
			}
			if (inputs.Contains(PlayerInput.MoveLeft)) {
				movementVector += -right;
			}
			if (inputs.Contains(PlayerInput.MoveBackward)) {
				movementVector += -forward;
			}
			if (inputs.Contains(PlayerInput.MoveRight)) {
				movementVector += right;
			}
			if (inputs.Contains(PlayerInput.MoveUp) && physicalComponent.isGrounded) {
				physicalComponent.shouldJump = true;
			}

			speed = physicalComponent.isGrounded ? physicalComponent.walkSpeed : physicalComponent.airSpeed;
			if (inputs.Contains(PlayerInput.Sprint)) {
				speed *= physicalComponent.walkSprintModifier;
			}

			movementVector *= speed;
			transform.velocity += movementVector;
		}
	}
	
	// TODO: Should probably think about a cleaner/more extensible way to do this
	public static void QueryKeyboardInputs() {
		List<PlayerInput> inputs = new();
        if (inputHandler.GetKeyState(Key.W)) {
			inputs.Add(PlayerInput.MoveForward);
        }
        if (inputHandler.GetKeyState(Key.A)) {
            inputs.Add(PlayerInput.MoveLeft);
        }
        if (inputHandler.GetKeyState(Key.S)) {
			inputs.Add(PlayerInput.MoveBackward);
        }
        if (inputHandler.GetKeyState(Key.D)) {
			inputs.Add(PlayerInput.MoveRight);
        }
        if (inputHandler.GetKeyState(Key.Space)) {
			inputs.Add(PlayerInput.MoveUp);
        }
        if (inputHandler.GetKeyState(Key.ShiftLeft)) {
			inputs.Add(PlayerInput.MoveDown);
        }
        QueryKeyboardInputs(inputs.ToArray(), archWorld, player);
    }

    public static void QueryMouseInputs() {
		ref EntityTransformComponent transform = ref archWorld.Get<EntityTransformComponent>(player);
		ref EntityPlayerClientComponent playerClientComponent = ref archWorld.Get<EntityPlayerClientComponent>(player);

		if (!virtualWindow.GetFocused()) {
            playerClientComponent.lastMouseFocus = false;
			return;
		}

		// Does some (no longer) absolute magic to rotate the camera correctly
		Vector2 windowSize = virtualWindow.GetSize();
		Vector2 mousePosition = inputHandler.GetMousePosition();

		if (!(mousePosition.X == playerClientComponent.oldMousePosition.X && mousePosition.Y == playerClientComponent.oldMousePosition.Y) && playerClientComponent.lastMouseFocus) {
            float sensitivity = 0.5f;
            float deltaYaw = sensitivity * (float)(mousePosition.X - (windowSize.X / 2)) / windowSize.X;
            float deltaPitch = sensitivity * (float)(mousePosition.Y - (windowSize.Y / 2)) / windowSize.Y;

            Quaternion yawChange = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -deltaYaw);
            Quaternion pitchChange = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -deltaPitch);

            transform.orientation = Quaternion.Normalize(yawChange * transform.orientation * pitchChange);
        }

		// We don't want anyone to be able to move the mouse off the screen, that would be very very very bad and horrible and would make the game absolutely unplayable
		if (virtualWindow.GetFocused()) {
			inputHandler.SetMousePosition(new Vector2(windowSize.X / 2.0f, windowSize.Y / 2.0f));
            playerClientComponent.lastMouseFocus = true;
		}

        playerClientComponent.oldMousePosition = mousePosition;
	}

	private static void MouseButtonCallback(MouseButton button) {
		EntityTransformComponent transform = archWorld.Get<EntityTransformComponent>(player);
		EntityPhysicalComponent physicalComponent = archWorld.Get<EntityPhysicalComponent>(player);
		EntityPlayerClientComponent playerClientComponent = archWorld.Get<EntityPlayerClientComponent>(player);

		VirtualWindow globalWindow = (VirtualWindow)MetaHandler.Get<IVirtualWindow>();
		if (!globalWindow.GetFocused()) {
			return;
		}

		BlockRayHit rayHit = Physics.RaycastWorld(transform.position + playerClientComponent.cameraOffset, Vector3.Transform(new Vector3(0, 0, -1), transform.orientation), 500, (IChunkHandler)chunkHandler);
		IntVector3 blockPosition = rayHit.blockHitPosition.blockPosition;
		IntVector3 chunkPosition = rayHit.blockHitPosition.chunkPosition;
		if (!rayHit.hit) {
			return;
		}
		if (button == MouseButton.Left) {
			ushort miningBlockID = chunkHandler.GetBlock(rayHit.blockHitPosition);
			BlockDefinition miningBlock = assetManager.GetBlockDefinition(miningBlockID);
			AssetStringID blockStringID = miningBlock.stringID;
			PlayerBlockInteractionEvent eventData = new PlayerBlockInteractionEvent(BlockInteractionType.BLOCK_MINED, player, rayHit.blockHitPosition, miningBlock.stringID);
            MetaHandler.Get<IEventManager>().TriggerEvent<PlayerBlockInteractionEvent>(eventData);
			chunkHandler.RemoveBlock(rayHit.blockHitPosition);
		} else if (button == MouseButton.Right) {
			FullBlockPosition newFullPosition = rayHit.blockHitPosition;
			if (rayHit.faceHitIndex == FaceDirection.INTERIOR) {
				return;
			}
			newFullPosition.AddBlockPosition(BlockFace.GetModifier(rayHit.faceHitIndex));
			IntVector3 newChunkPosition = newFullPosition.chunkPosition;
			bool emptyBlock = ((Chunk)chunkHandler.GetChunk(newChunkPosition, false)).GetBlock(newFullPosition.blockPosition) == 0;
			bool collidesEntity = Physics.CollideBlock(ref transform, ref physicalComponent, newFullPosition, false);
			if (emptyBlock && !collidesEntity) {
				chunkHandler.AddBlock(newFullPosition, 0);
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

		playerComponent.gameMode = newGameMode;
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
}

public enum PlayerInput : byte {
	MoveForward,
	MoveLeft,
	MoveBackward,
	MoveRight,
	MoveUp,
	MoveDown,
	Sprint,
	Crouch
}