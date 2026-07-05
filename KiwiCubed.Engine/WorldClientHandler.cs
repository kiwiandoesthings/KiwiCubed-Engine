namespace KiwiCubed.Engine;

using KiwiCubed.Api;

public class WorldClientHandler : IWorldClientHandler, IDisposable {
    private KLogger logger;
    private WorldClient world;
    private bool isLoaded = false;
    private bool shouldUnload = false;

    public WorldClientHandler() {
        logger = new KLogger("WorldHandler");
        MetaHandler.Register<IWorldClientHandler>(this);
    }

    public IWorldClient CreateClientWorld() {
        if (isLoaded) {
            logger.ERR("Tried to create a client world while one was already loaded");
            logger.BREAK();
        }

        logger.INFO("Creating ghost world...");

        world = new WorldClient();

        EventManager eventManager = (EventManager)MetaHandler.Get<IEventManager>();
        eventManager.SubscribeToEvent<ChunkDataPacket>((ChunkDataPacket packet) => {
            world.HandleChunkDataPacket(packet);
        });
        eventManager.SubscribeToEvent<ChunkEditPacket>((ChunkEditPacket packet) => {
            world.HandleChunkDiffPacket(packet);
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
        logger.INFO("Starting client world simulation thread...");
        world.StartTickThread();

        isLoaded = true;
    }

    public void ExitWorld() {
        if (!isLoaded) {
            logger.ERR("Tried to exit client world while one wasn't loaded");
            return;
        }

        logger.INFO("Marking client world as shutdown ready...");
        shouldUnload = true;
        world.StopTickThread();
    }

    public void Update() {
        if (shouldUnload) {
            isLoaded = false;
            shouldUnload = false;

            logger.INFO("Exiting client world...");

            world.Dispose();
            world = null;

            logger.INFO("Successfully exited client world");
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