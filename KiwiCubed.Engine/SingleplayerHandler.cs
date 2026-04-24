namespace KiwiCubed.Engine;

using KiwiCubed.Api;

using static KiwiCubed.Api.KLogger;

public class SingleplayerHandler : ISingleplayerHandler, IDisposable {
	private World singleplayerWorld = null;
	private bool isLoadedIntoSingleplayerWorld = false;
	private bool shouldUnloadWorld = false;

	public SingleplayerHandler() {
        MetaHandler.Register<ISingleplayerHandler>(this);
	}

	public void CreateWorld(int horizontalSize, int verticalSize) {
		OVERRIDE_LOG_NAME("SingleplayerHandler");

		if (isLoadedIntoSingleplayerWorld) {
			KERR("Tried to create a singleplayer world while one was already loaded");
			return;
		}

		KINFO("Creating singleplayer world...");

		singleplayerWorld = new World((uint)horizontalSize, (uint)verticalSize);
		singleplayerWorld.ReadyGeneration(0);
		singleplayerWorld.GenerateNewWorld();
		CommonServerSetup();
	}

    public void LoadWorld(string worldName) {
        OVERRIDE_LOG_NAME("SingleplayerHandler");

        if (isLoadedIntoSingleplayerWorld) {
            KERR("Tried to create a singleplayer world while one was already loaded");
            return;
        }

        KINFO("Creating singleplayer world...");

        singleplayerWorld = new World(0, 0);
        singleplayerWorld.LoadWorld(worldName);
        CommonServerSetup();
    }

	public void CreateGhostWorld() {
		OVERRIDE_LOG_NAME("SingleplayerHandler");
		if (isLoadedIntoSingleplayerWorld) {
			KERR("Tried to create a singleplayer world while one was already loaded");
			return;
		}

		KINFO("Creating ghost world...");

		singleplayerWorld = new World(0, 0);
		CommonClientSetup();
    }

    public void ExitWorld() {
		OVERRIDE_LOG_NAME("SingleplayerHandler");

		if (!isLoadedIntoSingleplayerWorld) {
			KERR("Tried to exit singleplayer world while one wasn't loaded");
			return;
		}

		KINFO("Marking singleplayer world as shutdown ready...");
		shouldUnloadWorld = true;
		singleplayerWorld.StopTickThread();
	}

    public void Update() {
		OVERRIDE_LOG_NAME("SingleplayerHandler");

		if (shouldUnloadWorld) {
			isLoadedIntoSingleplayerWorld = false;
			shouldUnloadWorld = false;

			KINFO("Exiting singleplayer world...");
			
			singleplayerWorld.Dispose();
			singleplayerWorld = null;

            KINFO("Successfully exited singleplayer world");
        }

        if (isLoadedIntoSingleplayerWorld) {
			((ChunkHandler)singleplayerWorld.GetChunkHandler()).CleanChunks();
        }
    }

	public void SaveWorld() {
		singleplayerWorld.SaveWorld();
	}

	public IWorld GetWorld() {
		return (IWorld)singleplayerWorld;
	}

	public bool IsLoadedIntoWorld() {
		return isLoadedIntoSingleplayerWorld;
	}

	private void CommonServerSetup() {
		OVERRIDE_LOG_NAME("SingleplayerHandler");

		EventManager eventManager = (EventManager)MetaHandler.Get<IEventManager>();
        eventManager.SubscribeToEvent<ConnectionRequestPacket>((ConnectionRequestPacket packet) => {
            singleplayerWorld.HandleConnectionRequestPacket(packet);
        });
        eventManager.SubscribeToEvent<PlayerTransformPacket>((PlayerTransformPacket packet) => {
			singleplayerWorld.HandlePlayerTransformPacket(packet);
		});

        SuperCommonSetup();
	}

	private void CommonClientSetup() { 		
		OVERRIDE_LOG_NAME("SingleplayerHandler");

        EventManager eventManager = (EventManager)MetaHandler.Get<IEventManager>();
        eventManager.SubscribeToEvent<ChunkDataPacket>((ChunkDataPacket packet) => {
            singleplayerWorld.HandleChunkDataPacket(packet);
        });

		SuperCommonSetup();
    }

    private void SuperCommonSetup() {
		OVERRIDE_LOG_NAME("SingleplayerHandler");

        isLoadedIntoSingleplayerWorld = true;

        KINFO("Starting singleplayer world...");

        singleplayerWorld.StartTickThread();
    }

    public void Dispose() {
        MetaHandler.Deregister<ISingleplayerHandler>();
	}
};