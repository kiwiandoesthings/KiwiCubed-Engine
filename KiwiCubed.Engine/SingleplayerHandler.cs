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
        isLoadedIntoSingleplayerWorld = true;

		KINFO("Starting singleplayer world...");

		singleplayerWorld.StartTickThread();
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
        isLoadedIntoSingleplayerWorld = true;

        KINFO("Starting singleplayer world...");

        singleplayerWorld.StartTickThread();
    }

	public void CreateGhostWorld() {
		OVERRIDE_LOG_NAME("SingleplayerHandler");
		if (isLoadedIntoSingleplayerWorld) {
			KERR("Tried to create a singleplayer world while one was already loaded");
			return;
		}

		KINFO("Creating ghost world...");

		singleplayerWorld = new World(0, 0);
		isLoadedIntoSingleplayerWorld = true;

		KINFO("Starting ghost world...");

		singleplayerWorld.StartTickThread();
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
            singleplayerWorld.Update();
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

	public void Dispose() {
        MetaHandler.Deregister<ISingleplayerHandler>();
	}
};