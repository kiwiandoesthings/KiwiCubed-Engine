namespace KiwiCubed.Engine;

using ImGuiNET;
using KiwiCubed.Api;
using Silk.NET.OpenGL;
using System.Diagnostics;
using System.Numerics;
using System.Threading;

using static FastNoiseLite;
using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.Util;
using static KiwiCubed.Engine.Chunk;
using static Player;

public class World : IDisposable {
    private int worldSeed = 0;

    private uint horizontalSize = 0;
    private uint verticalSize = 0;

    private GL gl = null;
    private AssetManager assetManager = null;
    private ChunkHandler chunkHandler = null;
    private EntityManager entityManager = null;
    private WorldFileHandler worldFileHandler = null;
    private GenerationNoises noises;
    private Player player;

    private Thread tickThread;
    private volatile bool tickShouldRun = false;
    private int targetTps = 20;
    private float realTps = 0.0f;
    private int currentTicks = 0;
    private ulong totalTicks = 0;

    private HashSet<IntVector3> chunkGenerationQueue;
    private HashSet<IntVector3> chunkMeshingQueue;
    private HashSet<IntVector3> chunkUnloadingQueue;
    private uint horizontalGenerationDistance = 8;
    private uint verticalGenerationDistance = 4;

    public World(uint horizontalSize, uint verticalSize) {
        this.horizontalSize = horizontalSize;
        this.verticalSize = verticalSize;
        gl = SystemsManager.Get<GL>();
        assetManager = (AssetManager)SystemsManager.Get<IAssetManager>();
        chunkHandler = new ChunkHandler(this);
        entityManager = new EntityManager();
        worldFileHandler = new WorldFileHandler(this);
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
    }

    public void ReadyGeneration(int seed = -1) {
        OVERRIDE_LOG_NAME("World Generation");

        worldSeed = seed;
        if (seed == -1) {
            worldSeed = Environment.TickCount;
        }

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
        foreach (Chunk chunk in chunksToIterate) {
            if (chunk.GetMeshable()) {
                chunk.GenerateMesh(false);
            }
        }

        uint totalChunks = horizontalSize * horizontalSize * verticalSize;
        double totalTime = stopwatch.Elapsed.TotalMilliseconds;
        double averageTime = totalTime / totalChunks;
        double chunksPerSecond = 1000.0f / averageTime;
        KINFO("Took " + totalTime.ToString("F2") + "ms to generate world with size of {" + horizontalSize + "x" + verticalSize + "x" + horizontalSize + "} for {" + totalChunks + "} total chunks, taking roughly " + averageTime.ToString("F1") + "ms per chunks for roughly " + chunksPerSecond.ToString("F0") + " chunks generated per second");
    }

    public void SetupNewPlayer() {
        player = new Player(0UL, Vector3.Zero, new Vector3(1, 0, 0), this);

        int minHorizontal = -(int)horizontalSize / 2;
        int maxHorizontal = (int)horizontalSize / 2;

        int maxVertical = (int)verticalSize - 1;
        int minVertical = -2;

        bool foundPosition = false;
        Vector3 position = Vector3.Zero;
        BoundingBox playerBoundingBox = player.GetEntityData().physicsBoundingBox;
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

        player.GetEntityTransform().position = position;

        if (!foundPosition) {
            KWARN("Could not find suitable position to spawn player");
        }

        SystemsManager.Get<IVirtualWindow>().SetFocused(true);
    }

    public void LoadPlayer(Vector3 position, Vector3 orientation, GameMode gameMode) {
        player = new Player(0UL, position, orientation, this);
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
            ImGui.Text("Current chunk info: " + ((Chunk)chunkHandler.GetChunk(player.GetEntityTransform().globalChunkPosition, false)).GetImGuiText());
        }

        if (ImGui.CollapsingHeader("World Info")) {
            ImGui.Text("Total chunks: " + chunkHandler.GetChunks().Count);
            ImGui.Text("Total ticks: " + totalTicks);

            if (ImGui.CollapsingHeader("Chunks")) {
                lock (chunkHandler.GetChunkMutex()) {
                    foreach (KeyValuePair<IntVector3, IChunk> chunkPair in chunkHandler.GetChunks()) {
                        Chunk chunk = (Chunk)chunkPair.Value;
                        ImGui.Text(chunk.GetImGuiText());
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

    public void RecalculateChunkNeeds(uint horizontalRadius, uint verticalRadius) {
        IntVector3 playerChunkPosition = player.GetEntityTransform().globalChunkPosition;
        for (int chunkX = playerChunkPosition.X - (int)horizontalRadius; chunkX < playerChunkPosition.X + horizontalRadius; ++chunkX) {
            for (int chunkY = playerChunkPosition.Y - (int)verticalRadius; chunkY < playerChunkPosition.Y + verticalRadius; ++chunkY) {
                for (int chunkZ = playerChunkPosition.Z - (int)horizontalRadius; chunkZ < playerChunkPosition.Z + horizontalRadius; ++chunkZ) {
                    IntVector3 chunkPosition = new IntVector3(chunkX, chunkY, chunkZ);
                    bool chunkExists = chunkHandler.GetChunkExists(chunkX, chunkY, chunkZ);
                    Chunk chunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY, chunkZ, false);
                    if (chunkGenerationQueue.Contains(chunkPosition) || chunk.IsGenerating()) {
                        continue;
                    }
					if (!chunkExists || (chunkExists && !chunk.IsGenerated())) {
                        chunkGenerationQueue.Add(chunkPosition);
						continue;
                    }
                    if (chunk.GetMeshable() && !chunkMeshingQueue.Contains(chunkPosition) && !chunk.IsMeshing()) {
                        chunkMeshingQueue.Add(chunkPosition);
                        continue;
                    }
                }
            }
        }

        uint unloadingDistanceHorizontal = horizontalRadius + 2;
        uint unloadingDistanceVertical = verticalRadius + 2;
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
        RecalculateChunkNeeds(horizontalGenerationDistance, verticalGenerationDistance);

        Parallel.ForEach(chunkGenerationQueue, chunkPosition => {
            Chunk chunk = (Chunk)chunkHandler.GetChunk(chunkPosition, true);
            chunk.GenerateBlocks(this);
        });
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

    public void SaveWorld() {
        worldFileHandler.SaveWorld("worldname");
    }

    public bool LoadWorld(string worldName) {
        return worldFileHandler.LoadWorld("worldname");
    }

    public int GetSeed() {
        return worldSeed;
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

        if (tickShouldRun) {
            KERR("Tried to dispose world while the tick thread was still running, performing emergency stop");
            StopTickThread();
        }

        player.Dispose();
        player = null;

        KINFO("Cleaning chunk GPU objects...");
        lock (chunkHandler.GetChunkMutex()) {
            foreach (IChunk chunk in chunkHandler.GetChunks().Values) {
                ((Chunk)chunk).Dispose();
            }
        }

        chunkHandler.Dispose();
        chunkHandler = null;
        entityManager = null;
    }
}