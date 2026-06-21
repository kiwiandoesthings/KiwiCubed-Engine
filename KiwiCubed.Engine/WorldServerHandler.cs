namespace KiwiCubed.Engine;

using KiwiCubed.Api;

using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.KLogger;

public class WorldServerHandler : IWorldServerHandler, IDisposable {
    private WorldServer world = null;
    private bool isLoaded = false;
    private bool shouldUnload = false;

    public WorldServerHandler() {
        MetaHandler.Register<IWorldServerHandler>(this);
    }

    public IWorldServer CreateWorld(int horizontalSize, int verticalSize) {
        OVERRIDE_LOG_NAME("WorldHandler");

        if (isLoaded) {
            KERR("Tried to create a server world while one was already loaded");
            KBREAK();
        }

        KINFO("Creating server world...");

        world = new WorldServer((uint)horizontalSize, (uint)verticalSize);
        world.ReadyGeneration(0);
        CommonSetup();

        return world;
    }

    public IWorldServer LoadWorld(string worldName) {
        OVERRIDE_LOG_NAME("WorldHandler");

        if (isLoaded) {
            KERR("Tried to load a server world while one was already loaded");
            KBREAK();
        }

        KINFO("Loading server world...");

        world = new WorldServer(0, 0);
        world.LoadWorld(worldName);
        CommonSetup();

        return world;
    }

    public void ExitWorld() {
        OVERRIDE_LOG_NAME("WorldHandler");

        if (!isLoaded) {
            KERR("Tried to exit server world while one wasn't loaded");
            return;
        }

        KINFO("Marking server world as shutdown ready...");
        shouldUnload = true;
        world.StopTickThread();
    }

    public void Update() {
        OVERRIDE_LOG_NAME("WorldHandler");

        if (shouldUnload) {
            isLoaded = false;
            shouldUnload = false;

            KINFO("Exiting server world...");

            world.Dispose();
            world = null;

            KINFO("Successfully exited server world");
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
        OVERRIDE_LOG_NAME("WorldHandler");

        if (!Meta.Get<NetworkHandler>().StartServer("0.0.0.0", (int)defaultPort)) {
            KERR("Failed to start network interface for server");
            KBREAK();
        }

        EventManager eventManager = (EventManager)MetaHandler.Get<IEventManager>();
        eventManager.SubscribeToEvent<ConnectionRequestPacket>((ConnectionRequestPacket packet) => {
            world.HandleConnectionRequestPacket(packet);
        });
        eventManager.SubscribeToEvent<DataReadyPacket>((DataReadyPacket packet) => {
            world.HandleDataReadyPacket(packet);
        });
        eventManager.SubscribeToEvent<PlayerTransformPacket>((PlayerTransformPacket packet) => {
            world.HandlePlayerTransformPacket(packet);
        });
        eventManager.SubscribeToEvent<BlockInteractPacket>((BlockInteractPacket packet) => {
            world.HandleBlockInteractPacket(packet);
        });
        eventManager.SubscribeToEvent<EntityInteractPacket>((EntityInteractPacket packet) => {
            world.HandleEntityInteractPacket(packet);
        });

        isLoaded = true;
        KINFO("Starting server world tick thread...");
        world.StartTickThread();
    }

    public void Dispose() {
        MetaHandler.Deregister<IWorldServerHandler>();
    }
}