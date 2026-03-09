using KiwiCubed;

class SingleplayerHandler {
	private World singleplayerWorld = null;
	//private DebugRenderer debugRenderer = null;
	private bool isLoadedIntoSingleplayerWorld = false;
	private bool shouldUnloadWorld = false;

	static void Delete() {}

	//void Setup(DebugRenderer debugRenderer) {
	//	this.debugRenderer = debugRenderer;
	//}

	void StartSingleplayerWorld() {
		//OVERRIDE_LOG_NAME("Singleplayer Handler");
		if (singleplayerWorld != null) {
			//KCRITICAL("Tried to generate already generated world, aborting");
			//psnip_trap();
		}

		//singleplayerWorld = new World(5, 2, this);
		isLoadedIntoSingleplayerWorld = true;
		//singleplayerWorld.GenerateWorld();
		//singleplayerWorld.Setup();
		isLoadedIntoSingleplayerWorld = true;
		//debugRenderer.SetupBuffers(singleplayerWorld.GetChunkDebugVisualizationVertices(), singleplayerWorld.GetChunkDebugVisualizationIndices(), singleplayerWorld.GetChunkOrigins());

		//if (UI::GetInstance().GetCurrentScreenName() == "ui/main_menu") {
		//	UI::GetInstance().DisableUI();
		//}

		//Physics::Initialize();

		//EventManager & eventManager = EventManager::GetInstance();
		//eventManager.RegisterFunctionToEvent(EVENT_WORLD_PLAYER_MOVE, [=](const EventData&eventData) {
		//	const EventData moveEventCopy = eventData;
		//	const EventWorldPlayerMove* moveEvent = moveEventCopy.GetDataStruct<EventWorldPlayerMove>();
		//	glm::ivec3 oldChunkPosition = glm::ivec3(moveEvent->oldPlayerX / chunkSize, moveEvent->oldPlayerY / chunkSize, moveEvent->oldPlayerZ / chunkSize);
		//	glm::ivec3 newChunkPosition = glm::ivec3(moveEvent->newPlayerX / chunkSize, moveEvent->newPlayerY / chunkSize, moveEvent->newPlayerZ / chunkSize);
		//	if (oldChunkPosition == newChunkPosition) {
		//		return;
		//	}
		//
		//	singleplayerWorld->QueueTickTask([this, moveEventCopy]()-> void {
		//		singleplayerWorld->RecalculateChunksToLoad(moveEventCopy);
		//	});
		//});

		//singleplayerWorld->StartTickThread();
	}

	void EndSingleplayerWorld() {
		shouldUnloadWorld = true;
	}

	void Update() {
		//OVERRIDE_LOG_NAME("Singleplayer Handler");
		if (shouldUnloadWorld) {
			//EventManager & eventManager = EventManager::GetInstance();
			//singleplayerWorld.Delete();
			isLoadedIntoSingleplayerWorld = false;
			//UI & ui = UI::GetInstance();
			//ui.SetCurrentScreen("ui/main_menu");
			shouldUnloadWorld = false;
			//KINFO("Exiting singleplayer world");
		}
	}

	World GetWorld() {
		return singleplayerWorld;
	}

	bool IsLoadedIntoSingleplayerWorld() {
		return isLoadedIntoSingleplayerWorld;
	}
};