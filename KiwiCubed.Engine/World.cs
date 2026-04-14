namespace KiwiCubed.Engine;

using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;
using Arch.Core;
using KiwiCubed.Api;
using System.Diagnostics;
using System.Numerics;
using System.Threading;

using static FastNoiseLite;
using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.IPlayer;
using static KiwiCubed.Api.Util;
using static KiwiCubed.Engine.Chunk;
using LiteNetLib.Utils;

public class World : IWorld, IDisposable {
    private int worldSeed = 0;

    private uint horizontalSize = 0;
    private uint verticalSize = 0;

    private NetworkHandler networkHandler = null;
    private EventManager eventManager = null;
    private AssetManager assetManager = null;
    private ChunkHandler chunkHandler = null;
    private EntityManager entityManager = null;
    private WorldFileHandler worldFileHandler = null;
    private GenerationNoises noises;
    private ArchWorld archWorld = null;
    private Dictionary<ArchEntity, int> players = null;

    private Thread tickThread;
    private volatile bool tickShouldRun = false;
    private int targetTps = 20;
    private float systemTicksPerTick = 0.0f;
    private ulong sessionTicks = 0;
    private ulong totalTicks = 0;
    private float realTps = 0.0f;
    private long lastTickTime = 0;
    private float partialTicks = 0.0f;

    private HashSet<IntVector3> chunkGenerationQueue;
    private HashSet<IntVector3> chunkMeshingQueue;
    private HashSet<IntVector3> chunkUnloadingQueue;
    private uint horizontalGenerationDistance = 8;
    private uint verticalGenerationDistance = 4;
    private string currentCommandString = "";

    public World(uint horizontalSize, uint verticalSize) {
        this.horizontalSize = horizontalSize;
        this.verticalSize = verticalSize;
        networkHandler = MetaHandler.Get<NetworkHandler>();
        eventManager = (EventManager)MetaHandler.Get<IEventManager>();
        assetManager = (AssetManager)MetaHandler.Get<IAssetManager>();
        chunkHandler = new ChunkHandler(this);
        entityManager = new EntityManager();
        worldFileHandler = new WorldFileHandler(this);
		archWorld = entityManager.GetArchWorld();
        players = new();
		systemTicksPerTick = (float)Stopwatch.Frequency / targetTps;
        chunkGenerationQueue = new();
        chunkMeshingQueue = new();
        chunkUnloadingQueue = new();

        Chunk.SetupChunks(chunkHandler);

        int horizontalBound = -(int)horizontalSize / 2;
        int verticalBound = (int)verticalSize - 2;
        for (int chunkX = horizontalBound; chunkX < -horizontalBound; chunkX++) {
            for (int chunkY = -2; chunkY < verticalBound; chunkY++) {
                for (int chunkZ = horizontalBound; chunkZ < -horizontalBound; chunkZ++) {
                    chunkHandler.AddChunk(chunkX, chunkY, chunkZ);
                }
            }
        }

        if (MetaHandler.GetGameType() == GameType.CLIENT) {
            ArchEntity player = SetupNewPlayer(0);
            ClientPlayer.Setup(this, archWorld, player);
        }

		eventManager.TriggerEvent<WorldLoadEvent>(new WorldLoadEvent(this));
	}

    public void ReadyGeneration(int seed) {
        OVERRIDE_LOG_NAME("World Generation");

        worldSeed = seed;
        worldSeed = 0;

        // todo: i mega need splines on these noises
        // only way to get noise maps that dont affect more area than wanted it seems
        FastNoiseLite terrainNoise = new FastNoiseLite();
        terrainNoise.SetSeed(worldSeed);
        terrainNoise.SetNoiseType(NoiseType.OpenSimplex2);
        terrainNoise.SetFractalType(FractalType.FBm);
        terrainNoise.SetFrequency(0.01f);
        terrainNoise.SetFractalOctaves(4);
        terrainNoise.SetFractalLacunarity(2.0f);
        terrainNoise.SetFractalGain(0.5f);
        terrainNoise.SetFractalWeightedStrength(5.0f);
        FastNoiseLite heightNoise = new FastNoiseLite();
        heightNoise.SetSeed(worldSeed + 1);
        heightNoise.SetNoiseType(NoiseType.OpenSimplex2);
        heightNoise.SetFrequency(0.004f);
        heightNoise.SetFractalType(FractalType.FBm);
        heightNoise.SetFractalOctaves(1);
        FastNoiseLite weirdNoise = new FastNoiseLite();
        weirdNoise.SetSeed(worldSeed + 2);
        weirdNoise.SetNoiseType(NoiseType.OpenSimplex2);
        weirdNoise.SetFrequency(0.0001f);
        FastNoiseLite temperatureNoise = new FastNoiseLite();
        temperatureNoise.SetSeed(worldSeed + 3);
        temperatureNoise.SetNoiseType(NoiseType.OpenSimplex2S);
        temperatureNoise.SetFrequency(0.002f);
        FastNoiseLite humidityNoise = new FastNoiseLite();
        humidityNoise.SetSeed(worldSeed + 4);
        humidityNoise.SetNoiseType(NoiseType.OpenSimplex2S);
        humidityNoise.SetFrequency(0.002f);

        noises = new GenerationNoises(terrainNoise, heightNoise, weirdNoise, temperatureNoise, humidityNoise);

        ChunkGenerator.Initialize();

        KINFO("Prepared world for generation with seed {" + worldSeed + "}");
    }

    public void GenerateNewWorld() {
        OVERRIDE_LOG_NAME("World Generation");

        KINFO("Generating world...");
        Stopwatch stopwatch = Stopwatch.StartNew();

        int horizontalBound = -(int)horizontalSize / 2;
        int verticalBound = (int)verticalSize - 2;
		Chunk defaultChunk = (Chunk)chunkHandler.GetDefaultChunk();
        List<Chunk> chunksToIterate = new();
		for (int chunkX = horizontalBound; chunkX < -horizontalBound; chunkX++) {
            for (int chunkY = -2; chunkY < verticalBound; chunkY++) {
                for (int chunkZ = horizontalBound; chunkZ < -horizontalBound; chunkZ++) {
                    Chunk currentChunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY, chunkZ, false);
					chunksToIterate.Add(currentChunk);
                }
            }
        }

        foreach (Chunk chunk in chunksToIterate) {
            chunk.GenerateBlocks(this);
        }
        if (Meta.GetGameType() == GameType.CLIENT) {
            foreach (Chunk chunk in chunksToIterate) {
                if (chunk.IsNeededForMeshing()) {
                    chunk.GenerateMesh(false);
                }
            }
        }

        uint totalChunks = horizontalSize * horizontalSize * verticalSize;
        double totalTime = stopwatch.Elapsed.TotalMilliseconds;
        double averageTime = totalTime / totalChunks;
        double chunksPerSecond = 1000.0f / averageTime;
        KINFO("Took " + totalTime.ToString("F2") + "ms to generate world with size of {" + horizontalSize + "x" + verticalSize + "x" + horizontalSize + "} for {" + totalChunks + "} total chunks, taking roughly " + averageTime.ToString("F1") + "ms per chunks for roughly " + chunksPerSecond.ToString("F0") + " chunks generated per second");
    }

    public ArchEntity SetupNewPlayer(int clientID) {
        EntityType playerType = assetManager.GetEntityType(new AssetStringID("kiwicubed", "player"));
        ArchEntity player = entityManager.SpawnEntity(playerType, new Vector3(0, 100, 0), new Vector3(1, 0, 0));
        players.Add(player, clientID);

        int minHorizontal = -(int)horizontalSize / 2;
        int maxHorizontal = (int)horizontalSize / 2;
    
        int maxVertical = (int)verticalSize - 1;
        int minVertical = -2;
    
        bool foundPosition = false;
        Vector3 position = Vector3.Zero;
        BoundingBox playerBoundingBox = archWorld.Get<EntityPhysicalComponent>(player).physicsBoundingBox;
        float xOffset = 1.0f - (playerBoundingBox.GetWidth() / 2);
        float yOffset = playerBoundingBox.GetHeight();
        float zOffset = 1.0f - (playerBoundingBox.GetLength() / 2);
    
        for (int chunkX = minHorizontal; chunkX <= maxHorizontal && !foundPosition; chunkX++) {
            for (int chunkZ = minHorizontal; chunkZ <= maxHorizontal && !foundPosition; chunkZ++) {
                for (int chunkY = maxVertical; chunkY >= minVertical && !foundPosition; chunkY--) {
                    Chunk chunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY, chunkZ, false);
    
                    if (!chunk.IsGenerated() || !chunk.IsMeshed() || chunk.IsEmpty()) {
                        continue;
                    }
    
                    for (int x = 0; x < chunkSize && !foundPosition; ++x) {
                        for (int z = 0; z < chunkSize && !foundPosition; ++z) {
                            int level = chunk.GetHeightmapLevelAt(x, z);
                            if (level != -2 && level != chunkSize && level != -1) {
                                position = new Vector3((chunk.chunkX * chunkSize) + x + xOffset, (chunk.chunkY * chunkSize) + level + yOffset - 1, (chunk.chunkZ * chunkSize) + z + zOffset);
                                foundPosition = true;
                            }
                        }
                    }
                }
            }
        }

        ref EntityTransform playerTransform = ref archWorld.Get<EntityTransform>(player);
        playerTransform.position = position;
    
        if (!foundPosition) {
            KWARN("Could not find suitable position to spawn player");
        }

        return player;
    }

    public void ReceivePlayer(int clientID) {
        SetupNewPlayer(clientID);

        lock (chunkHandler.GetChunkMutex()) {
            foreach (KeyValuePair<IntVector3, IChunk> chunkPair in chunkHandler.GetChunks()) {
                SendChunk((Chunk)chunkPair.Value);
            }
        }
    }

    public void SendChunk(Chunk chunk) {
        if (chunk.IsEmpty()) {
            return;
        }

		ChunkPacket chunkPacket = new ChunkPacket(chunk.chunkX, chunk.chunkY, chunk.chunkZ, chunk.GetBlockPalette(), chunk.GetPaletteIndices());
		NetDataWriter writer = new NetDataWriter();
		chunkPacket.Serialize(writer);
		networkHandler.QueuePacket(writer, (int)PacketType.CHUNK_DATA);
	}

    public void ReceiveChunk(int chunkX, int chunkY, int chunkZ, ushort[] blockPalette, ushort[] blockIndices) {
        ((Chunk)chunkHandler.GetChunk(chunkX, chunkY, chunkZ, true)).LoadChunkData(blockPalette, blockIndices);
    }
    
    public void LoadPlayer(Vector3 position, Vector3 orientation, GameMode gameMode) {
    //    player = new Player(0UL, position, orientation, this);
    }

    public void RemovePlayer(ArchEntity player) {
        if (!players.Remove(player)) {
            KERR("Tried to remove a player that the world wasn't aware of");
        }
    }

    public void Update() {
		long currentTime = Stopwatch.GetTimestamp();
		partialTicks = (float)((currentTime - lastTickTime) / systemTicksPerTick);
		partialTicks = Math.Clamp(partialTicks, 0.0f, 1.0f);

		chunkHandler.CleanChunks();
    }

    public void RecalculateChunkNeeds(uint horizontalRadius, uint verticalRadius) {
        List<Chunk> safeChunks = new();
		uint unloadingDistanceHorizontal = horizontalRadius + 2;
		uint unloadingDistanceVertical = verticalRadius + 2;
		foreach (KeyValuePair<ArchEntity, int> playerPair in players) {
			EntityTransform playerTransform = archWorld.Get<EntityTransform>(players.First().Key);
            EntityPlayerComponent playerComponent = archWorld.Get<EntityPlayerComponent>(players.First().Key);
			IntVector3 playerChunkPosition = playerTransform.globalChunkPosition;
			for (int chunkX = playerChunkPosition.X - (int)horizontalRadius; chunkX < playerChunkPosition.X + horizontalRadius; ++chunkX) {
                for (int chunkY = playerChunkPosition.Y - (int)verticalRadius; chunkY < playerChunkPosition.Y + verticalRadius; ++chunkY) {
                    for (int chunkZ = playerChunkPosition.Z - (int)horizontalRadius; chunkZ < playerChunkPosition.Z + horizontalRadius; ++chunkZ) {
                        IntVector3 chunkPosition = new IntVector3(chunkX, chunkY, chunkZ);
                        bool chunkExists = chunkHandler.GetChunkExists(chunkX, chunkY, chunkZ);
                        Chunk chunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY, chunkZ, false);
                        if (chunk.IsAwaitingDestruction()) {
                            continue;
						}

						IntVector3 distance = (playerTransform.globalChunkPosition - chunkPosition).Abs();
                        if (distance.X > unloadingDistanceHorizontal || distance.Y > unloadingDistanceVertical || distance.Z > unloadingDistanceHorizontal) {
                            continue;
                        } else {
                            if (!safeChunks.Contains(chunk)) {
                                safeChunks.Add(chunk);
							}
						}

						if (MetaHandler.GetGameType() == GameType.CLIENT) {
                            if (chunkExists && chunk.IsMeshable() && !chunkMeshingQueue.Contains(chunkPosition) && !chunk.IsMeshing()) {
                                chunkMeshingQueue.Add(chunkPosition);
                            }
                        } else {
                            if (!chunkExists || (chunkExists && !chunk.IsGenerated())) {
                                chunkGenerationQueue.Add(chunkPosition);
							}
						}
                    }
                }
            }
		}

        lock (chunkHandler.GetChunkMutex()) {
            foreach (KeyValuePair<IntVector3, IChunk> chunkPair in chunkHandler.GetChunks()) {
                Chunk chunk = (Chunk)chunkPair.Value;
                if (!chunk.IsAwaitingDestruction() && !safeChunks.Contains(chunk)) {
                    IntVector3 chunkPosition = new IntVector3(chunk.chunkX, chunk.chunkY, chunk.chunkZ);
                    chunkUnloadingQueue.Add(chunkPosition);
                }
            }
        }
	}

    private void GetConsoleInput() {
		while (Console.KeyAvailable) {
			ConsoleKeyInfo keyInfo = Console.ReadKey(true);

            if (keyInfo.Key == ConsoleKey.Enter) {
                ExecuteConsoleCommand();
                currentCommandString = "";
            } else {
                if (keyInfo.Key == ConsoleKey.Backspace && currentCommandString.Length > 0) {
                    currentCommandString = currentCommandString.Substring(0, currentCommandString.Length - 1);
                } else if (!char.IsControl(keyInfo.KeyChar)) {
                    currentCommandString += keyInfo.KeyChar;
                }
            }
		}
	}

    private void ExecuteConsoleCommand() {
        OVERRIDE_LOG_NAME("Console Command");

		string[] parts = currentCommandString.ToLower().Split(" ");
		switch (parts[0]) {
			case "tickqueue":
                if (parts.Length < 2) {
                    KWARN("Subcommand not found. Use \"help " + parts[0] + "\" to see a list of valid subcommands");
                    break;
				}
				if (parts[1] == "generate") {
					KINFO("Generation queue info");
					KINFO(" * Size: " + chunkGenerationQueue.Count);
				} else if (parts[1] == "unload") {
					KINFO("Unloading queue info");
					KINFO(" * Size: " + chunkUnloadingQueue.Count);
				} else {
					KWARN("Subcommand \"" + parts[1] + "\" not a valid argument for command \"" + parts[0] + "\", use \"help " + parts[0] + "\" to see a list of valid subcommands");
				}
				break;
			case "players":
				KINFO("Players info: ");
				KINFO(" * Count: " + players.Count);
				break;
            case "worldinfo":
                KINFO("World info: ");
                KINFO(" * Seed: " + worldSeed);
                KINFO(" * Total chunks: " + chunkHandler.GetChunks().Count);
                break;
            default:
				KWARN("Command \"" + parts[0] + "\" not recognized. Use \"help\" to see a list of valid commands");
				break;
		}
	}

    public void GetTickInfo(out float realTps, out int targetTps, out ulong totalTicks, out long lastTickTime, out float partialTicks) {
        realTps = this.realTps;
        targetTps = this.targetTps;
        totalTicks = this.totalTicks;
        lastTickTime = this.lastTickTime / Stopwatch.Frequency;
        partialTicks = this.partialTicks;
    }

	private void ServerTick() {
        OVERRIDE_LOG_NAME("Tick Thread");

        RecalculateChunkNeeds(horizontalGenerationDistance, verticalGenerationDistance + 4);

		GetConsoleInput();

		Parallel.ForEach(chunkGenerationQueue, chunkPosition => {
            Chunk chunk = (Chunk)chunkHandler.GetChunk(chunkPosition, true);
            chunk.GenerateBlocks(this);
            SendChunk(chunk);
        });
        chunkGenerationQueue.Clear();

		foreach (IntVector3 chunkPosition in chunkUnloadingQueue) {
            if (!chunkHandler.GetChunkExists(chunkPosition)) {
				continue;
			}
            chunkHandler.RemoveChunk(chunkPosition);
        }
        chunkUnloadingQueue.Clear();

        ApplyEntityPhysics();

		eventManager.TriggerEvent<WorldTickEvent>(new WorldTickEvent(totalTicks));
    }

    private void ClientTick() {
        OVERRIDE_LOG_NAME("Tick Thread");

		RecalculateChunkNeeds(horizontalGenerationDistance, verticalGenerationDistance);

		GetConsoleInput();

        Parallel.ForEach(chunkMeshingQueue, chunkPosition => {
            Chunk chunk = (Chunk)chunkHandler.GetChunk(chunkPosition, true);
            chunk.GenerateMesh(false);
        });
        chunkMeshingQueue.Clear();

		foreach (IntVector3 chunkPosition in chunkUnloadingQueue) {
			if (!chunkHandler.GetChunkExists(chunkPosition)) {
				continue;
			}
			chunkHandler.RemoveChunk(chunkPosition);
		}
		chunkUnloadingQueue.Clear();

		QueryDescription query = new QueryDescription().WithAll<EntityTransform, EntityRenderableComponent>();
        archWorld.Query(in query, (ref EntityTransform transformComponent, ref EntityRenderableComponent renderableComponent) => {
            renderableComponent.oldPosition = transformComponent.position;
            renderableComponent.oldOrientation = transformComponent.orientation;
            renderableComponent.oldPositionOffset = renderableComponent.positionOffset;
            renderableComponent.oldOrientationOffset = renderableComponent.orientationOffset;
        });
        ApplyEntityPhysics();
    }

    private void ApplyEntityPhysics() {
        QueryDescription query = new QueryDescription().WithAll<EntityPhysicalComponent>();
        archWorld.Query(in query, (ArchEntity entity, ref EntityTransform transformComponent, ref EntityPhysicalComponent physicalComponent) => {
            Physics.ApplyPhysics(chunkHandler, ref transformComponent, ref physicalComponent);
        });
        query = new QueryDescription();
        archWorld.Query(in query, (ref EntityTransform transformComponent) => {
            IChunk currentChunk = chunkHandler.GetChunk(transformComponent.globalChunkPosition, false);
            if (currentChunk.IsReal()) {
                transformComponent.currentChunk = currentChunk;
            } else {
                transformComponent.currentChunk = null;
            }

            transformComponent.globalChunkPosition = new IntVector3(FloorDiv(transformComponent.position, 32));
            transformComponent.localChunkPosition = new IntVector3(PositiveModulo(transformComponent.position, 32));
        });
    }

	public void TickLoop() {
        OVERRIDE_LOG_NAME("Tick Thread");

		long startTimestamp = Stopwatch.GetTimestamp();
        long lastTickBlockTimestamp = startTimestamp;
		float frequency = (float)Stopwatch.Frequency;
		while (tickShouldRun) {
			sessionTicks++;
			totalTicks++;
			lastTickTime = Stopwatch.GetTimestamp();

            if (sessionTicks % (ulong)targetTps == 0) {
                realTps = targetTps / ((lastTickTime - lastTickBlockTimestamp) / frequency);
                lastTickBlockTimestamp = lastTickTime;
                KINFO("Running at: " + realTps.ToString("F2") + " TPS");
            }

            networkHandler.PollEvents();
            if (MetaHandler.GetGameType() == GameType.SERVER) {
                ServerTick();
            } else {
                ClientTick();
            }
            MetaHandler.Get<NetworkHandler>().FlushPackets();

            long nextTickTarget = startTimestamp + (long)(sessionTicks * systemTicksPerTick);
			while (Stopwatch.GetTimestamp() < nextTickTarget) {
                if ((nextTickTarget - Stopwatch.GetTimestamp()) > (frequency / 1000) * 15) {
                	Thread.Sleep(1);
                } else {
                	Thread.SpinWait(5);
                }
			}
		}
	}

	public void StartTickThread() {
        OVERRIDE_LOG_NAME("Tick Thread");
        if (tickShouldRun) {
            KERR("Tried to start tick thread while it was already running");
        }

        tickShouldRun = true;
        tickThread = new Thread(TickLoop) {
            IsBackground = true,
            Priority = ThreadPriority.Highest
        };
        tickThread.Name = "KiwiCubed_TickThread";
        tickThread.Start();

		KINFO("Successfully started tick thread");
    }

    public void StopTickThread() {
		OVERRIDE_LOG_NAME("Tick Thread");
		if (!tickShouldRun) {
			KERR("Tried to stop tick thread while it was already stopped");
		}
        tickShouldRun = false;
        tickThread.Join();
        KINFO("Successfully stopped tick thread");
	}

    public void SaveWorld() {
        worldFileHandler.SaveWorld("worldname");
    }

    public bool LoadWorld(string worldName) {
        ReadyGeneration(0); // need to use actual world seed
        bool returnCode = worldFileHandler.LoadWorld("worldname");
        //player = new Player(0, Vector3.Zero, Vector3.Zero, this); //  Actually load  player stuff

		eventManager.TriggerEvent<WorldLoadEvent>(new WorldLoadEvent());

		return returnCode;
    }

    public int GetSeed() {
        return worldSeed;
    }

    public List<ArchEntity> GetPlayers() {
        return players.Keys.ToList();
    }

    public IChunkHandler GetChunkHandler() {
        return chunkHandler;
    }

    public IEntityManager GetEntityManager() {
        return entityManager; 
    }

    public ref GenerationNoises GetNoises() {
        return ref noises;
    }

    public void Dispose() {
        OVERRIDE_LOG_NAME("World");

        if (tickShouldRun) {
            KERR("Tried to dispose world while the tick thread was still running, performing emergency stop");
            StopTickThread();
        }

        archWorld = null;
        players = null;

        KINFO("Cleaning up chunks...");
        lock (chunkHandler.GetChunkMutex()) {
            foreach (IChunk chunk in chunkHandler.GetChunks().Values) {
                ((Chunk)chunk).Dispose();
            }
        }

        chunkHandler.Dispose();
        chunkHandler = null;
        entityManager.Dispose();
        entityManager = null;
    }
}