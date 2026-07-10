namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using static ClientServerInterface;
using static KiwiCubed.Api.Globals;

public class WorldClientHandler : IWorldClientHandler, IDisposable {
    private KLogger logger;
    private WorldClient world;
    private bool isLoaded = false;
    private bool shouldUnload = false;
    private bool isExiting = false;

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
        eventManager.SubscribeToEvent((ChunkDataPacket packet) => {
            world.HandleChunkDataPacket(packet);
        });
        eventManager.SubscribeToEvent((ChunkEditPacket packet) => {
            world.HandleChunkDiffPacket(packet);
        });
        eventManager.SubscribeToEvent((NewEntityPacket packet) => {
            world.HandleNewEntitiesPacket(packet);
        });
        eventManager.SubscribeToEvent((UnloadEntityPacket packet) => {
            world.HandleUnloadEntityPacket(packet);
        });
        eventManager.SubscribeToEvent((EntityUpdatePacket packet) => {
            world.HandleEntityUpdatesPacket(packet);
        });
        eventManager.SubscribeToEvent((DisconnectPacket packet) => {
            isExiting = true;
            world.HandleDisconnectPacket(packet);
            if (!isIntegratedGame) {
                ExitWorld();
            }
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

        isExiting = true;

        if (isIntegratedGame) {
            logger.INFO("Shutting down integrated server...");
            NetworkHandler networkHandler = Meta.Get<NetworkHandler>();
            networkHandler.QueuePacketToAll(new IntegratedServerControlPacket(IntegratedServerCommand.STOP), PacketType.INTEGRATED_CONTROL);
            networkHandler.FlushPackets();
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
            isExiting = false;

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
        return isLoaded && !isExiting;
    }

    public void Dispose() {
        MetaHandler.Deregister<IWorldClientHandler>();
    }
}