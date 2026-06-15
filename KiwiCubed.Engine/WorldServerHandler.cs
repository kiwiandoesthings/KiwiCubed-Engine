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

    public void CreateWorld(int horizontalSize, int verticalSize) {
        OVERRIDE_LOG_NAME("ServerWorldHandler");

        if (isLoaded) {
            KERR("Tried to create a server world while one was already loaded");
            return;
        }

        KINFO("Creating server world...");

        world = new WorldServer((uint)horizontalSize, (uint)verticalSize);
        world.ReadyGeneration(0);
        world.GenerateNewWorld();
        CommonSetup();
    }

    public void LoadWorld(string worldName) {
        OVERRIDE_LOG_NAME("ServerWorldHandler");

        if (isLoaded) {
            KERR("Tried to load a server world while one was already loaded");
            return;
        }

        KINFO("Loading server world...");

        world = new WorldServer(0, 0);
        world.LoadWorld(worldName);
        CommonSetup();
    }

    public void ExitWorld() {
        OVERRIDE_LOG_NAME("ServerWorldHandler");

        if (!isLoaded) {
            KERR("Tried to exit server world while one wasn't loaded");
            return;
        }

        KINFO("Marking server world as shutdown ready...");
        shouldUnload = true;
        world.StopTickThread();
    }

    public void Update() {
        OVERRIDE_LOG_NAME("ServerWorldHandler");

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

    public IWorld GetWorld() {
        return (IWorld)world;
    }

    public bool IsLoadedIntoWorld() {
        return isLoaded;
    }

    private void CommonSetup() {
        OVERRIDE_LOG_NAME("ServerWorldHandler");

        if (!Meta.Get<NetworkHandler>().StartServer("0.0.0.0", (int)defaultPort)) {
            KERR("Failed to start network interface for server");
            KBREAK();
        }

        EventManager eventManager = (EventManager)MetaHandler.Get<IEventManager>();
        eventManager.SubscribeToEvent<ConnectionRequestPacket>((ConnectionRequestPacket packet) => {
            world.HandleConnectionRequestPacket(packet);
        });
        eventManager.SubscribeToEvent<PlayerTransformPacket>((PlayerTransformPacket packet) => {
            world.HandlePlayerTransformPacket(packet);
        });

        isLoaded = true;
        KINFO("Starting server world tick thread...");
        world.StartTickThread();
    }

    public void Dispose() {
        MetaHandler.Deregister<IWorldServerHandler>();
    }
}