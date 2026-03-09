namespace KiwiCubed;

using KiwiCubed.Api;
using System.Numerics;
using Silk.NET.Input;
using Silk.NET.Windowing;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Block;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.Util;

using static Physics;

public class Player : Entity {
	public override AssetStringID entityStringID { get; } = new AssetStringID("kiwicubed", "player");

	private float pitch = 0.0f;
	private float yaw = -90.0f;
	private float roll = 0.0f;

	private Vector2 oldMousePosition = Vector2.Zero;

	private Vector3 cameraOffset;
    private Camera camera = new Camera();

	private ChunkHandler chunkHandler;
	private VirtualWindow virtualWindow = SystemsManager.Get<VirtualWindow>();

	public Player(ulong AUID, Vector3 position, Vector3 orientation) : base(AUID, position, orientation) {
		entityTransform.position = position;
		entityTransform.orientation = orientation;
		cameraOffset = new Vector3(0.0f, 1.62f, 0.0f);

		chunkHandler = SystemsManager.Get<ChunkHandler>();

		entityStats.health = 20.0f;
		entityStats.armor = 0;

		entityData.name = "Player";

		entityData.physicsBoundingBox.Resize(new Vector3(-0.3f, 0.0f, -0.3f), new Vector3(0.3f, 1.8f, 0.3f));

		//std::vector<AssetStringID> slotStringIDs;
		//slotStringIDs.reserve(27);
		//for (int slot = 0; slot < 27; slot++) {
		//	slotStringIDs.push_back(AssetStringID{
		//		"kiwicubed", "inventory_slot_" + fmt::format("{:02}", slot)});
		//	}
		//
		//	entityData.inventory = Inventory(slotStringIDs);
		//}

		InputHandler inputHandler = SystemsManager.Get<InputHandler>();
		inputHandler.RegisterMouseButtonCallback(MouseButton.Left, MouseButtonCallback, true);
		inputHandler.RegisterMouseButtonCallback(MouseButton.Right, MouseButtonCallback, true);
	}

    public void Update(Shader shader) {
        camera.Update(entityTransform.position + cameraOffset, entityTransform.orientation, 80.0f);
        camera.SetUniforms(shader);

        Vector3 movementVector = Vector3.Zero;
        Vector3 forward = entityTransform.orientation;
        forward.Y = 0;
        forward = Vector3.Normalize(forward);
        Vector3 upDirection = new Vector3(0.0f, 1.0f, 0.0f);
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, upDirection));
        Vector3 up = Vector3.Normalize(upDirection);

		InputHandler inputHandler = SystemsManager.Get<InputHandler>();
		if (inputHandler.GetKeyState(Silk.NET.Input.Key.W)) {
			movementVector += forward;
		}
		if (inputHandler.GetKeyState(Silk.NET.Input.Key.A)) {
			movementVector += -right;
		}
		if (inputHandler.GetKeyState(Silk.NET.Input.Key.S)) {
			movementVector += -forward;
		}
		if (inputHandler.GetKeyState(Silk.NET.Input.Key.D)) {
			movementVector += right;
		}
		if (inputHandler.GetKeyState(Silk.NET.Input.Key.Space)) {
			movementVector += up;
		}
		if (inputHandler.GetKeyState(Silk.NET.Input.Key.ShiftLeft)) {
			movementVector += -up;
		}

		if (movementVector.Length() > 0.0f) {
			movementVector = (Vector3.Normalize(movementVector)) * 0.5f;
		}

		entityTransform.position += movementVector;

		QueryMouseInputs();
	}

	public void QueryMouseInputs() {
		IWindow window = SystemsManager.Get<VirtualWindow>().GetWindow();
		InputHandler inputHandler = SystemsManager.Get<InputHandler>();

		// Does some absolute magic to rotate the camera correctly
		Vector2 windowSize = (Vector2)window.GetFullSize();
		Vector2 mousePosition = inputHandler.GetMousePosition();

		if (!(mousePosition.X == oldMousePosition.X && mousePosition.Y == oldMousePosition.Y)) {
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
		}

		oldMousePosition = mousePosition;
	}

	public void MouseButtonCallback(MouseButton button) {
		BlockRayHit rayHit = RaycastWorld(entityTransform.position + cameraOffset, entityTransform.orientation, 500, chunkHandler);
		IntVector3 blockPosition = rayHit.blockHitPosition.blockPosition;
		IntVector3 chunkPosition = rayHit.blockHitPosition.chunkPosition;
		if (!rayHit.hit) {
			return;
		}
		if (button == MouseButton.Left) {
			ushort block = chunkHandler.GetChunk(chunkPosition.X, chunkPosition.Y, chunkPosition.Z, false).GetBlock(blockPosition.X, blockPosition.Y, blockPosition.Z);
			//BlockType blockType = BlockManager::GetInstance().GetBlockType(block.blockID);
			//EventWorldPlayerBlock blockEvent = EventWorldPlayerBlock(BLOCK_MINED, protectedEntityData.AUID, chunkPosition.x, chunkPosition.y, chunkPosition.z, blockPosition.x, blockPosition.y, blockPosition.z, blockType->blockStringID, AssetStringID{
			//	"kiwicubed", "block/air"});
			//EventData eventData = EventData{
			//	EVENT_WORLD_PLAYER_BLOCK, &blockEvent, sizeof(blockEvent)}
			//;
			//EventManager::GetInstance().TriggerEvent(EVENT_WORLD_PLAYER_BLOCK, eventData);
			chunkHandler.RemoveBlock(rayHit.blockHitPosition);
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
		} else if (button == MouseButton.Right) {
			FullBlockPosition newFullPosition = rayHit.blockHitPosition;
			if (rayHit.faceHitIndex == FaceDirection.INTERIOR) {
				return;
			}
			newFullPosition.AddBlockPosition(BlockFace.GetModifier(rayHit.faceHitIndex));
			IntVector3 newChunkPosition = newFullPosition.chunkPosition;

			chunkHandler.AddBlock(newFullPosition, 1);
			chunkHandler.RemeshChunk(newChunkPosition.X, newChunkPosition.Y, newChunkPosition.Z, false);
		}
	}
}