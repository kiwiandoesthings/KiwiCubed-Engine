namespace KiwiCubed.Engine;

using System.Diagnostics;
using System.Numerics;
using System.Threading;
using ImGuiNET;
using KiwiCubed.Api;
using Silk.NET.OpenGL;

using static FastNoiseLite;
using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.Util;
using static KiwiCubed.Engine.Chunk;

public class World : IDisposable {
    private uint horizontalSize = 0;
    private uint verticalSize = 0;

    private GL gl = null;
    private AssetManager assetManager = null;
    private ChunkHandler chunkHandler = null;
    private EntityManager entityManager = null;
    private GenerationNoises noises;
    private Player player;

    private Thread tickThread;
    private volatile bool tickShouldRun = false;
    private int targetTps = 20;
    private float realTps = 0.0f;
    private int currentTicks = 0;
    private ulong totalTicks = 0;

    private List<IntVector3> chunkGenerationQueue;
    private List<IntVector3> chunkMeshingQueue;
    private List<IntVector3> chunkUnloadingQueue;

    public World(uint horizontalSize, uint verticalSize) {
        this.horizontalSize = horizontalSize;
        this.verticalSize = verticalSize;
        gl = SystemsManager.Get<GL>();
        assetManager = (AssetManager)SystemsManager.Get<IAssetManager>();
        chunkHandler = new ChunkHandler(this);
        entityManager = new EntityManager();
        chunkGenerationQueue = new();
        chunkMeshingQueue = new();
        chunkUnloadingQueue = new();

        for (int chunkX = -(int)horizontalSize / 2; chunkX < horizontalSize / 2; chunkX++) {
            for (int chunkY = -2; chunkY < verticalSize - 2; chunkY++) {
                for (int chunkZ = -(int)horizontalSize / 2; chunkZ < horizontalSize / 2; chunkZ++) {
                    chunkHandler.AddChunk(chunkX, chunkY, chunkZ);
                }
            }
        }

        int baseSeed = (int)Environment.TickCount64;
		FastNoiseLite terrainNoise = new FastNoiseLite();
        terrainNoise.SetSeed(baseSeed);
        terrainNoise.SetNoiseType(NoiseType.OpenSimplex2);
        terrainNoise.SetFractalType(FractalType.FBm);
        terrainNoise.SetFractalOctaves(1);
        terrainNoise.SetFractalLacunarity(2.0f);
        terrainNoise.SetFractalGain(0.5f);
        terrainNoise.SetFractalWeightedStrength(5.0f);
        FastNoiseLite shapingNoise = new FastNoiseLite();
        shapingNoise.SetSeed(baseSeed + 1);
        shapingNoise.SetNoiseType(NoiseType.OpenSimplex2);
        shapingNoise.SetFrequency(0.0005f);
        FastNoiseLite temperatureNoise = new FastNoiseLite();
        temperatureNoise.SetSeed(baseSeed + 2);
        temperatureNoise.SetNoiseType(NoiseType.OpenSimplex2S);
        temperatureNoise.SetFrequency(0.002f);
		FastNoiseLite humidityNoise = new FastNoiseLite();
        humidityNoise.SetSeed(baseSeed + 3);
		humidityNoise.SetNoiseType(NoiseType.OpenSimplex2S);
		humidityNoise.SetFrequency(0.002f);

		noises = new GenerationNoises(terrainNoise, shapingNoise, temperatureNoise, humidityNoise);

        player = new Player(0UL, new Vector3(0, 30, 0), new Vector3(1, 0, 0));
    }

    public void GenerateWorld() {
        OVERRIDE_LOG_NAME("World Generation");

        KINFO("Generating world...");
        Stopwatch stopwatch = Stopwatch.StartNew();

		ChunkGenerator.Initialize();

		int horizontalBound = -(int)horizontalSize / 2;
        int verticalBound = (int)verticalSize - 2;
		Chunk defaultChunk = (Chunk)chunkHandler.GetDefaultChunk();
		for (int chunkX = horizontalBound; chunkX < -horizontalBound; chunkX++) {
            for (int chunkY = -2; chunkY < verticalBound; chunkY++) {
                for (int chunkZ = horizontalBound; chunkZ < -horizontalBound; chunkZ++) {
                    Chunk currentChunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY, chunkZ, false);
					GenerateChunk(chunkX, chunkY, chunkZ, currentChunk, false, defaultChunk);
                }
            }
        }

		bool foundPosition = false;
		Vector3 position = player.GetEntityTransform().position;
		for (int chunkX = horizontalBound + 2; chunkX < -horizontalBound - 2 && !foundPosition; chunkX++) {
			for (int chunkZ = horizontalBound + 2; chunkZ < -horizontalBound - 2 && !foundPosition; chunkZ++) {
				for (int chunkY = verticalBound; chunkY >= -2 && !foundPosition; chunkY--) {
					Chunk chunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY, chunkZ, false);

					if (!chunk.IsGenerated() || !chunk.IsMeshed() || chunk.IsEmpty()) {
						continue;
					}

					for (int x = 0; x < chunkSize; ++x) {
						for (int z = 0; z < chunkSize; ++z) {
							int level = chunk.GetHeightmapLevelAt(x, z);
							if (level != 0) {
								position = new Vector3((chunk.chunkX * chunkSize) + x, (chunk.chunkY * chunkSize) + level + 1, (chunk.chunkZ * chunkSize) + z);
								foundPosition = true;
							}
						}
					}
				}
			}
		}

		if (!foundPosition) {
			KWARN("Could not find suitable position to spawn player");
		}

		SystemsManager.Get<IVirtualWindow>().SetFocused(true);

        uint totalChunks = horizontalSize * horizontalSize * verticalSize;
        double totalTime = stopwatch.Elapsed.TotalMilliseconds;
        double averageTime = totalTime / totalChunks;
        double chunksPerSecond = 1000.0f / averageTime;
        KINFO("Took " + totalTime.ToString("F2") + "ms to generate world with size of {" + horizontalSize + "x" + verticalSize + "x" + horizontalSize + "} for {" + totalChunks + "} total chunks, taking roughly " + averageTime.ToString("F1") + "ms per chunks for roughly " + chunksPerSecond.ToString("F0") + " chunks generated per second");
    }

    public void GenerateChunk(int chunkX, int chunkY, int chunkZ, Chunk chunk, bool updateCallerChunk, Chunk callerChunk) {
        if (!chunk.IsGenerated()) {
            chunk.GenerateBlocks(this, callerChunk, false, false);
        }

		Chunk positiveXChunk = ((Chunk)chunkHandler.GetChunk(chunkX + 1, chunkY, chunkZ, true));     // Positive X
		Chunk negativeXChunk = ((Chunk)chunkHandler.GetChunk(chunkX - 1, chunkY, chunkZ, true));     // Negative X
		Chunk positiveYChunk = ((Chunk)chunkHandler.GetChunk(chunkX, chunkY + 1, chunkZ, true));     // Positive Y
		Chunk negativeYChunk = ((Chunk)chunkHandler.GetChunk(chunkX, chunkY - 1, chunkZ, true));     // Negative Y
		Chunk positiveZChunk = ((Chunk)chunkHandler.GetChunk(chunkX, chunkY, chunkZ + 1, true));     // Positive Z
		Chunk negativeZChunk = ((Chunk)chunkHandler.GetChunk(chunkX, chunkY, chunkZ - 1, true));     // Negative Z

		if (positiveXChunk.IsGenerated() && negativeXChunk.IsGenerated() && positiveYChunk.IsGenerated() && negativeYChunk.IsGenerated() && positiveZChunk.IsGenerated() && negativeZChunk.IsGenerated() && !chunk.IsMeshed()) {
			chunk.GenerateMesh(false);
		} else if (!updateCallerChunk) {
			if (!positiveXChunk.IsGenerated()) {
				GenerateChunk(chunkX + 1, chunkY, chunkZ, positiveXChunk, true, chunk);
			}

			if (!negativeXChunk.IsGenerated()) {
				GenerateChunk(chunkX - 1, chunkY, chunkZ, negativeXChunk, true, chunk);
			}

			if (!positiveYChunk.IsGenerated()) {
				GenerateChunk(chunkX, chunkY + 1, chunkZ, positiveYChunk, true, chunk);
			}

			if (!negativeYChunk.IsGenerated()) {
				GenerateChunk(chunkX, chunkY - 1, chunkZ, negativeYChunk, true, chunk);
			}

			if (!positiveZChunk.IsGenerated()) {
				GenerateChunk(chunkX, chunkY, chunkZ + 1, positiveZChunk, true, chunk);
			}

			if (!negativeZChunk.IsGenerated()) {
				GenerateChunk(chunkX, chunkY, chunkZ - 1, negativeZChunk, true, chunk);
			}
		}

		if (updateCallerChunk) {
			GenerateChunk(callerChunk.chunkX, callerChunk.chunkY, callerChunk.chunkZ, callerChunk, false, chunk);
		}
	}

    public void Render() {
        if (ImGui.CollapsingHeader("Player Info")) {
            ImGui.Text("Player name: " + player.GetEntityData().name);
            ImGui.Text("Player AUID: " + player.GetProtectedEntityData().AUID);
            ImGui.Text("Player gamemode: " + player.GetPlayerData().gameMode);
            ImGui.Text("Player health: " + player.GetEntityStats().health);
            ImGui.Text("Player position: " + player.GetEntityTransform().position);
            ImGui.Text("Player orientation: " + player.GetEntityTransform().orientation);
            ImGui.Text("Player velocity: " + player.GetEntityTransform().velocity);
            ImGui.Text("Player grounded: " + player.GetEntityData().isGrounded);
            ImGui.Text("Player jumping: " + player.GetEntityData().isJumping);
            ImGui.Text("Global chunk position: " + player.GetEntityTransform().globalChunkPosition);
            ImGui.Text("Local chunk position: " + player.GetEntityTransform().localChunkPosition);
            ImGui.Text("Current chunk info: " + ((Chunk)chunkHandler.GetChunk(player.GetEntityTransform().globalChunkPosition, false)).GetGenerationState());
        }

        if (ImGui.CollapsingHeader("World Info")) {
            ImGui.Text("Total chunks: " + chunkHandler.GetChunks().Count);
            ImGui.Text("Total ticks: " + totalTicks);

            if (ImGui.CollapsingHeader("Chunks")) {
                lock (chunkHandler.GetChunkMutex()) {
                    foreach (KeyValuePair<IntVector3, IChunk> chunkPair in chunkHandler.GetChunks()) {
                        Chunk chunk = (Chunk)chunkPair.Value;
                        chunk.DisplayImGui();
                    }
                }
            }
        }

		Texture terrainAtlas = assetManager.GetTextureAtlas(new AssetStringID("kiwicubed", "atlas/main"));
        terrainAtlas.Bind();

		lock (chunkHandler.GetChunkMutex()) {
			foreach (KeyValuePair<IntVector3, IChunk> chunkPair in chunkHandler.GetChunks()) {
				Chunk chunk = (Chunk)chunkPair.Value;
                chunk.Render();
			}
		}

		gl.Disable(EnableCap.CullFace);
		entityManager.ForEachEntity(entity => {
            entity.Render();
        });
		gl.Enable(EnableCap.CullFace);
	}

    public void Update() {
        player.Update((IChunkHandler)chunkHandler);

        chunkHandler.CleanChunks();
    }

    public void RecalculateChunkNeeds(int horizontalRadius, int verticalRadius) {
        IntVector3 playerChunkPosition = player.GetEntityTransform().globalChunkPosition;
        for (int chunkX = playerChunkPosition.X - horizontalRadius; chunkX < playerChunkPosition.X + horizontalRadius; ++chunkX) {
            for (int chunkY = playerChunkPosition.Y - verticalRadius; chunkY < playerChunkPosition.Y + verticalRadius; ++chunkY) {
                for (int chunkZ = playerChunkPosition.Z - horizontalRadius; chunkZ < playerChunkPosition.Z + horizontalRadius; ++chunkZ) {
                    IntVector3 chunkPosition = new IntVector3(chunkX, chunkY, chunkZ);
                    bool chunkExists = chunkHandler.GetChunkExists(chunkX, chunkY, chunkZ);
                    Chunk chunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY, chunkZ, false);
					if (!chunkExists || (chunkExists && !chunk.IsGenerated())) {
                        chunkGenerationQueue.Add(chunkPosition);
						continue;
                    }
                    if (chunk.GetMeshable(chunkHandler)) {
                        chunkMeshingQueue.Add(chunkPosition);
                        continue;
                    }
                }
            }
        }

        int unloadingDistanceHorizontal = horizontalRadius + 2;
        int unloadingDistanceVertical = verticalRadius + 2;
		foreach (Chunk chunk in chunkHandler.GetChunks().Values) {
            if (chunk.IsAwaitingDestruction()) {
                continue;
            }
            IntVector3 chunkPosition = new IntVector3(chunk.chunkX, chunk.chunkY, chunk.chunkZ);
            Vector3 distance = Vector3.Abs((player.GetEntityTransform().position / Globals.chunkSize) - chunkPosition.ToVector3());
            if (distance.X > unloadingDistanceHorizontal || distance.Y > unloadingDistanceVertical || distance.Z > unloadingDistanceHorizontal) {
                chunkUnloadingQueue.Add(chunkPosition);
                chunkGenerationQueue.Remove(chunkPosition);
                chunkMeshingQueue.Remove(chunkPosition);
			}
		}
	}

    private void Tick() {
        OVERRIDE_LOG_NAME("Tick Thread");
        RecalculateChunkNeeds(8, 4);

        foreach (IntVector3 chunkPosition in chunkGenerationQueue) {
            Chunk chunk = (Chunk)chunkHandler.GetChunk(chunkPosition, true);
            Task.Run(() => { chunk.GenerateBlocks(this, (Chunk)chunkHandler.GetDefaultChunk(), false, false); });
        }
        chunkGenerationQueue.Clear();

		foreach (IntVector3 chunkPosition in chunkMeshingQueue) {
            Chunk chunk = (Chunk)chunkHandler.GetChunk(chunkPosition, false);
            chunk.GenerateMesh(false);
        }
        chunkMeshingQueue.Clear();

		foreach (IntVector3 chunkPosition in chunkUnloadingQueue) {
            if (!chunkHandler.GetChunkExists(chunkPosition)) {
				continue;
			}
            chunkHandler.RemoveChunk(chunkPosition);
        }
        chunkUnloadingQueue.Clear();
    }

    public void TickLoop() {
        Stopwatch stopwatch = Stopwatch.StartNew();
        long start = Stopwatch.GetTimestamp();
        double tps = 1.0d / targetTps;
        double tpms = tps * 1000.0d;
        ulong sessionTicks = 0;
        while (tickShouldRun) {
            sessionTicks++;
            totalTicks++;
            double nextTickTime = sessionTicks * tpms;

            Tick();

            while (stopwatch.Elapsed.TotalMilliseconds < nextTickTime) {
                double remainingTime = nextTickTime - stopwatch.Elapsed.TotalMilliseconds;
                if (remainingTime > 8.0d) {
                    Thread.Sleep(1);
                } else {
                    Thread.SpinWait(10);
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

    public Player GetPlayer() {
        return player;
    }

    public ChunkHandler GetChunkHandler() {
        return chunkHandler;
    }

    public EntityManager GetEntityManager() {
        return entityManager; 
    }

    public ref GenerationNoises GetNoises() {
        return ref noises;
    }

    public void Dispose() {
        OVERRIDE_LOG_NAME("World");

        KINFO("Cleaning chunk GPU objects...");
        Dictionary<IntVector3, IChunk> chunks = chunkHandler.GetChunks();

		foreach (IChunk chunk in chunks.Values) {
            ((Chunk)chunk).Dispose();
        }
    }
}