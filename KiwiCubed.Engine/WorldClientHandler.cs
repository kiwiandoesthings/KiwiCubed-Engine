namespace KiwiCubed.Engine;

using KiwiCubed.Api;

using static KiwiCubed.Api.KLogger;

public class WorldClientHandler : IWorldClientHandler, IDisposable {
    private WorldClient world = null;
    private bool isLoaded = false;
    private bool shouldUnload = false;

    public WorldClientHandler() {
        MetaHandler.Register<IWorldClientHandler>(this);
    }

    public void CreateClientWorld() {
        OVERRIDE_LOG_NAME("ClientWorldHandler");
        if (isLoaded) {
            KERR("Tried to create a client world while one was already loaded");
            return;
        }

        KINFO("Creating ghost world...");

        world = new WorldClient();

        EventManager eventManager = (EventManager)MetaHandler.Get<IEventManager>();
        eventManager.SubscribeToEvent<ChunkDataPacket>((ChunkDataPacket packet) => {
            world.HandleChunkDataPacket(packet);
        });
        eventManager.SubscribeToEvent<NewEntitiesPacket>((NewEntitiesPacket packet) => {
            world.HandleNewEntitiesPacket(packet);
        });
        eventManager.SubscribeToEvent<EntityUpdatesPacket>((EntityUpdatesPacket packet) => {
            world.HandleEntityUpdatesPacket(packet);
        });

        isLoaded = true;
        KINFO("Starting client world simulation thread...");
        world.StartTickThread();
    }

    public void ExitWorld() {
        OVERRIDE_LOG_NAME("ClientWorldHandler");

        if (!isLoaded) {
            KERR("Tried to exit client world while one wasn't loaded");
            return;
        }

        KINFO("Marking client world as shutdown ready...");
        shouldUnload = true;
        world.StopTickThread();
    }

    public void Update() {
        OVERRIDE_LOG_NAME("ClientWorldHandler");

        if (shouldUnload) {
            isLoaded = false;
            shouldUnload = false;

            KINFO("Exiting client world...");

            world.Dispose();
            world = null;

            KINFO("Successfully exited client world");
        }

        if (isLoaded) {
            ((ChunkHandler)world.GetChunkHandler()).CleanChunks();
        }
    }

    public IWorld GetWorld() {
        return (IWorld)world;
    }

    public bool IsLoadedIntoWorld() {
        return isLoaded;
    }

    public void Dispose() {
        MetaHandler.Deregister<IWorldClientHandler>();
    }
}