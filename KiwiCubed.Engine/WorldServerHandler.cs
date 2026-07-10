namespace KiwiCubed.Engine;

using KiwiCubed.Api;

using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.Utils;

public class WorldServerHandler : IWorldServerHandler, IDisposable {
    private KLogger logger;
    private WorldServer world = null;
    private bool isLoaded = false;
    private bool shouldUnload = false;

    public WorldServerHandler() {
        logger = new KLogger("WorldHandler");
        MetaHandler.Register<IWorldServerHandler>(this);
    }

    public IWorldServer CreateWorld(int seed) {
        if (isLoaded) {
            logger.ERR("Tried to create a server world while one was already loaded");
            logger.BREAK();
        }

        logger.INFO("Creating server world...");

        world = new WorldServer();
        world.ReadyGeneration(seed);
        world.GenerateSpawnArea(8, 8, IntVector3.Zero);
        CommonSetup();

        return world;
    }

    public IWorldServer LoadWorld(string worldName) {
        if (isLoaded) {
            logger.ERR("Tried to load a server world while one was already loaded");
            logger.BREAK();
        }

        logger.INFO("Loading server world...");

        world = new WorldServer();
        world.LoadWorld(worldName);
        world.ReadyGeneration(world.GetSeed());
        CommonSetup();

        return world;
    }

    public void ExitWorld() {
        if (!isLoaded) {
            logger.ERR("Tried to exit server world while one wasn't loaded");
            return;
        }

        logger.INFO("Marking server world as shutdown ready...");
        shouldUnload = true;
        world.StopTickThread();
    }

    public void Update() {
        if (shouldUnload) {
            isLoaded = false;
            shouldUnload = false;

            logger.INFO("Exiting server world...");

            world.Dispose();
            world = null;

            logger.INFO("Successfully exited server world");
        }

        if (isLoaded) {
            ((ChunkHandler)world.GetChunkHandler()).CleanChunks();
        }
    }

    public void SaveWorld() {
        world.SaveWorld();
    }

    public IWorldServer GetWorld() {
        return world;
    }

    public bool IsLoadedIntoWorld() {
        return isLoaded;
    }

    private void CommonSetup() {
        if (!Meta.Get<NetworkHandler>().StartServer("10.0.0.76", (int)defaultPort)) {
            logger.ERR("Failed to start network interface for server");
            logger.BREAK();
        }

        EventManager eventManager = (EventManager)MetaHandler.Get<IEventManager>();
        eventManager.SubscribeToEvent((PeerDisconnectedEvent packet) => {
            world.QueuePlayerDisconnect(world.GetPlayerAUID(packet.clientPeerID));
        });
        eventManager.SubscribeToEvent((ConnectionRequestPacket packet) => {
            world.HandleConnectionRequestPacket(packet);
        });
        eventManager.SubscribeToEvent((DataReadyPacket packet) => {
            world.HandleDataReadyPacket(packet);
        });
        eventManager.SubscribeToEvent((PlayerTransformPacket packet) => {
            world.HandlePlayerTransformPacket(packet);
        });
        eventManager.SubscribeToEvent((BlockInteractPacket packet) => {
            world.HandleBlockInteractPacket(packet);
        });
        eventManager.SubscribeToEvent((EntityInteractPacket packet) => {
            world.HandleEntityInteractPacket(packet);
        });
        eventManager.SubscribeToEvent((IntegratedServerControlPacket packet) => {
            world.HandleIntegratedControlPacket(packet);
        });

        isLoaded = true;
        logger.INFO("Starting server world tick thread...");
        world.StartTickThread();
    }

    public void Dispose() {
        MetaHandler.Deregister<IWorldServerHandler>();
    }
}