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

    public IWorldClient CreateClientWorld() {
        OVERRIDE_LOG_NAME("WorldHandler");
        if (isLoaded) {
            KERR("Tried to create a client world while one was already loaded");
            KBREAK();
        }

        KINFO("Creating ghost world...");

        world = new WorldClient();

        EventManager eventManager = (EventManager)MetaHandler.Get<IEventManager>();
        eventManager.SubscribeToEvent<ChunkDataPacket>((ChunkDataPacket packet) => {
            world.HandleChunkDataPacket(packet);
        });
        eventManager.SubscribeToEvent<NewEntityPacket>((NewEntityPacket packet) => {
            world.HandleNewEntitiesPacket(packet);
        });
        eventManager.SubscribeToEvent<UnloadEntityPacket>((UnloadEntityPacket packet) => {
            world.HandleUnloadEntityPacket(packet);
        });
        eventManager.SubscribeToEvent<EntityUpdatePacket>((EntityUpdatePacket packet) => {
            world.HandleEntityUpdatesPacket(packet);
        });

        return world;
    }

    public void StartClientWorld() {
        OVERRIDE_LOG_NAME("WorldHandler");

        KINFO("Starting client world simulation thread...");
        world.StartTickThread();

        isLoaded = true;
    }

    public void ExitWorld() {
        OVERRIDE_LOG_NAME("WorldHandler");

        if (!isLoaded) {
            KERR("Tried to exit client world while one wasn't loaded");
            return;
        }

        KINFO("Marking client world as shutdown ready...");
        shouldUnload = true;
        world.StopTickThread();
    }

    public void Update() {
        OVERRIDE_LOG_NAME("WorldHandler");

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

    public IWorldClient GetWorld() {
        return world;
    }

    public bool IsLoadedIntoWorld() {
        return isLoaded;
    }

    public void Dispose() {
        MetaHandler.Deregister<IWorldClientHandler>();
    }
}