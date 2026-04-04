namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using Silk.NET.Input;
using Silk.NET.Windowing;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Block;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.IInventory;
using static KiwiCubed.Api.Util;

public class Player : Entity, IPlayer, IDisposable {
	public override AssetStringID entityStringID { get; } = new AssetStringID("kiwicubed", "player");

	public float FOV { get; set; } = 80.0f;

	private PlayerData playerData = new PlayerData();

	private float pitch = 0.0f;
	private float yaw = -90.0f;
	private float roll = 45.0f;
	private Vector2 oldMousePosition = Vector2.Zero;
    private Camera camera = new Camera();
	private bool lastMouseFocus = false;

	private InputHandler inputHandler;
	private ChunkHandler chunkHandler;
	private VirtualWindow virtualWindow;
	private Shader terrainShader;
	private Shader entityShader;

	public Player(ulong AUID, Vector3 position, Vector3 orientation, World world) : base(AUID, position, orientation) {
		SetGameMode(GameMode.SURVIVAL);
		playerData.cameraOffset = new Vector3(0.0f, 1.62f, 0.0f);
		entityTransform.position = position;
		entityTransform.orientation = orientation;

		inputHandler = (InputHandler)SystemsManager.Get<IInputHandler>();
        chunkHandler = (ChunkHandler)world.GetChunkHandler();
        virtualWindow = (VirtualWindow)SystemsManager.Get<IVirtualWindow>();
		terrainShader = (Shader)SystemsManager.Get<IAssetManager>().GetShader(new AssetStringID("kiwicubed", "shader/terrain"));
		entityShader = (Shader)SystemsManager.Get<IAssetManager>().GetShader(new AssetStringID("kiwicubed", "shader/entity"));

		entityStats.health = 20.0f;
		entityStats.armor = 0;

		entityData.physicsBoundingBox.Resize(new Vector3(-0.3f, 0.0f, -0.3f), new Vector3(0.3f, 1.8f, 0.3f));
		entityData.name = playerUsername;

		List<AssetStringID> slotStringIDs = new();
		for (int slot = 0; slot < 27; slot++) {
			slotStringIDs.Add(new AssetStringID("kiwicubed", "inventory_slot_" + (slot < 9 ? "0" + slot : slot)));
		}
		entityData.inventory = new Inventory(slotStringIDs);

		inputHandler.RegisterMouseButtonCallback(MouseButton.Left, MouseButtonCallback, true);
		inputHandler.RegisterMouseButtonCallback(MouseButton.Right, MouseButtonCallback, true);
		inputHandler.RegisterKeyCallback(Key.F4, (Key key) => {
			if (playerData.gameMode == GameMode.CREATIVE) {
				SetGameMode(GameMode.SURVIVAL);
			} else {
				SetGameMode(GameMode.CREATIVE);
			}
		}, true);
        inputHandler.RegisterKeyCallback(Key.F3, (Key key) => {
            if (entityData.applyCollision) {
				entityData.applyCollision = false;
			} else {
				entityData.applyCollision = true;
            }
        }, true);
        inputHandler.RegisterKeyCallback(Key.F2, (Key key) => {
            if (entityData.applyGravity) {
                entityData.applyGravity = false;
            } else {
                entityData.applyGravity = true;
            }
        }, true);
		inputHandler.RegisterKeyCallback(Key.G, (Key key) => {
			SingleplayerHandler.SaveWorld();
		}, true);
    }

	public override void Update(IChunkHandler chunkHandler) {
		QueryMouseInputs();
		QueryKeyboardInputs(chunkHandler);
		base.Update(chunkHandler);
		if (playerData.gameMode == GameMode.CREATIVE) {
			entityData.isGrounded = false;
		}
		camera.Update(entityTransform.position + playerData.cameraOffset, entityTransform.orientation, FOV, virtualWindow.GetSize());
		camera.SetUniforms(terrainShader);
		camera.SetUniforms(entityShader);
	}

	public void QueryKeyboardInputs(IChunkHandler chunkHandler) {
        Vector3 movementVector = Vector3.Zero;
        Vector3 forward = entityTransform.orientation;
        forward.Y = 0;
        forward = Vector3.Normalize(forward);
        Vector3 upDirection = new Vector3(0.0f, 1.0f, 0.0f);
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, upDirection));
        Vector3 up = Vector3.Normalize(upDirection);
		float speed = 0.0f;
		bool shouldJump = false;

		if (playerData.gameMode == GameMode.CREATIVE) {
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

			speed = entityData.flySpeed;
			if (inputHandler.GetKeyState(Key.ControlLeft)) {
				speed *= entityData.flySprintModifier;
			}
			movementVector *= speed;

			entityTransform.velocity += movementVector;

			float friction = entityData.flyFriction;
			entityTransform.velocity.X *= friction;
			entityTransform.velocity.Y *= friction;
			entityTransform.velocity.Z *= friction;
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
			if (inputHandler.GetKeyState(Key.Space) && entityData.isGrounded) {
				shouldJump = true;
				entityData.isJumping = true;
			}

			speed = entityData.isGrounded ? entityData.walkSpeed : entityData.airSpeed;
			if (inputHandler.GetKeyState(Key.ControlLeft)) {
				speed *= entityData.walkSprintModifier;
			}

			movementVector *= speed;

			if (shouldJump) {
				movementVector.Y = entityData.jumpHeight;
			}

			entityTransform.velocity += movementVector;

			float friction = entityData.isGrounded ? entityData.groundFriction : entityData.airFrictionHorizontal;
			entityTransform.velocity.X *= friction;
			entityTransform.velocity.Z *= friction;
			entityTransform.velocity.Y *= (float)Math.Pow(entityData.airFrictionVertical, Globals.deltaTime * 20);
		}
	}

	public void QueryMouseInputs() {
		if (!virtualWindow.GetFocused()) {
			lastMouseFocus = false;
			return;
		}

		// Does some absolute magic to rotate the camera correctly
		Vector2 windowSize = virtualWindow.GetSize();
		Vector2 mousePosition = inputHandler.GetMousePosition();

		if (!(mousePosition.X == oldMousePosition.X && mousePosition.Y == oldMousePosition.Y) && lastMouseFocus) {
			// Get the amount to rotate for the frame
			float sensitivity = 100.0f;
			float rotationY = sensitivity * (float)(mousePosition.X - ((float)(windowSize.X) / 2)) / windowSize.X;
			float rotationX = sensitivity * (float)(mousePosition.Y - ((float)(windowSize.Y) / 2)) / windowSize.Y;

			yaw += rotationY;
			pitch += rotationX;

			// Clamp pitch to prevent the camera from flipping out
			if (pitch > 89.9f)
				pitch = 89.9f;
			if (pitch < -89.9f)
				pitch = -89.9f;

			// wha..? (learnopengl.com)
			Vector3 facing = Vector3.Zero;
			Vector3 orientationRadians = Vector3.DegreesToRadians(new Vector3(pitch, yaw, roll));
			facing.X = (float)(Math.Cos(orientationRadians.Y) * Math.Cos(orientationRadians.X));
			facing.Y = (float)Math.Sin(-orientationRadians.X);
			facing.Z = (float)(Math.Sin(orientationRadians.Y) * Math.Cos(orientationRadians.X));
			entityTransform.orientation = Vector3.Normalize(facing);
		}

		// We don't want anyone to be able to move the mouse off the screen, that would be very very very bad and horrible and would make the game absolutely unplayable
		if (virtualWindow.GetFocused()) {
			inputHandler.SetMousePosition(new Vector2(windowSize.X / 2.0f, windowSize.Y / 2.0f));
			lastMouseFocus = true;
		}

		oldMousePosition = mousePosition;
	}

	private void MouseButtonCallback(MouseButton button) {
		VirtualWindow globalWindow = (VirtualWindow)SystemsManager.Get<IVirtualWindow>();
		if (!globalWindow.GetFocused()) {
			return;
		}

		BlockRayHit rayHit = Physics.RaycastWorld(entityTransform.position + playerData.cameraOffset, entityTransform.orientation, 500, (IChunkHandler)chunkHandler);
		IntVector3 blockPosition = rayHit.blockHitPosition.blockPosition;
		IntVector3 chunkPosition = rayHit.blockHitPosition.chunkPosition;
		if (!rayHit.hit) {
			return;
		}
		if (button == MouseButton.Left) {
			ushort block = ((Chunk)chunkHandler.GetChunk(chunkPosition.X, chunkPosition.Y, chunkPosition.Z, false)).GetBlockPaletteIndex(blockPosition.X, blockPosition.Y, blockPosition.Z);
			//BlockType blockType = BlockManager::GetInstance().GetBlockType(block.blockID);
			//EventWorldPlayerBlock blockEvent = EventWorldPlayerBlock(BLOCK_MINED, protectedEntityData.AUID, chunkPosition.x, chunkPosition.y, chunkPosition.z, blockPosition.x, blockPosition.y, blockPosition.z, blockType->blockStringID, AssetStringID{
			//	"kiwicubed", "block/air"});
			//EventData eventData = EventData{
			//	EVENT_WORLD_PLAYER_BLOCK, &blockEvent, sizeof(blockEvent)}
			//;
			//EventManager::GetInstance().TriggerEvent(EVENT_WORLD_PLAYER_BLOCK, eventData);
			Block miningBlock = chunkHandler.GetBlock(rayHit.blockHitPosition);
			AssetStringID blockStringID = miningBlock.GetStringID();
			AssetStringID itemStringID = new AssetStringID(blockStringID.modName, "item/" + blockStringID.assetName + "_block");
			entityData.inventory.AddItem(new InventorySlot(itemStringID, 1));
			chunkHandler.RemoveBlock(rayHit.blockHitPosition);
		} else if (button == MouseButton.Right) {
			FullBlockPosition newFullPosition = rayHit.blockHitPosition;
			if (rayHit.faceHitIndex == FaceDirection.INTERIOR) {
				return;
			}
			newFullPosition.AddBlockPosition(BlockFace.GetModifier(rayHit.faceHitIndex));
			IntVector3 newChunkPosition = newFullPosition.chunkPosition;
			bool emptyBlock = ((Chunk)chunkHandler.GetChunk(newChunkPosition, false)).GetBlock(newFullPosition.blockPosition).IsAir();
			bool collidesEntity = Physics.CollideBlock(this, newFullPosition, false);
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

	public void SetGameMode(GameMode newGameMode) {
		playerData.gameMode = newGameMode;
		if (playerData.gameMode == GameMode.CREATIVE) {
			entityData.applyGravity = false;
			entityData.applyCollision = false;
		} else {
			entityData.applyGravity = true;
			entityData.applyCollision = true;
		}
	}

	public PlayerData GetPlayerData() {
		return playerData;
	}

	public void Dispose() {
		// Need a controls wrapper around inputhandler wrapper to make this easier and not have InputHandler be a static instance
		//inputHandler.DeregisterCallback()

        camera = null;
        inputHandler = null;
        chunkHandler = null;
        virtualWindow = null;
        terrainShader = null;
	}

	public struct PlayerData {
		public GameMode gameMode;
		public Vector3 cameraOffset;
	};

	public enum GameMode : byte {
		SURVIVAL,
		CREATIVE
	};
}