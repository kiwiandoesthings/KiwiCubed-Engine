namespace KiwiCubed.Api;

using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;

public interface IPlayer {
    public float FOV { get; set; }

    public ref EntityTransformComponent GetEntityTransform();
    public void SetEntityTransform(EntityTransformComponent entityTransform);
    public List<AssetStringID> GetInventorySlotIDs();

    public enum GameMode : byte {
        SURVIVAL,
        CREATIVE
    };

    public enum BlockInteractionType : byte {
        START_MINE,
        STOP_MINE,
        PLACE_BLOCK,
        REPLACE_BLOCk,
        INTERACT,
    }

    public struct EntityPlayerComponent {
        public GameMode gameMode = GameMode.CREATIVE;

        public EntityPlayerComponent() { }
    }

    public struct EntityPlayerClientComponent {
        public ICamera camera;
        public Vector3 cameraOffset;

        public float FOV = 80.0f;
        public float pitch = 0.0f;
        public float yaw = -90.0f;
        public float roll = 0.0f;
        public Vector2 oldMousePosition = Vector2.Zero;
        public bool lastMouseFocus = false;

        public EntityPlayerClientComponent() {
            camera = Renderer.CreateCamera();
        }
    }
}