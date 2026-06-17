namespace KiwiCubed.Engine;

using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;
using Arch.Core;
using KiwiCubed.Api;
using System.Collections.Generic;
using System.Diagnostics;

using static Chunk;
using static FastNoiseLite;
using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.Util;
using System.Numerics;

public class WorldServer : World {
    private EntityTracker entityTracker = null;
    private WorldFileHandler worldFileHandler = null;
    private GenerationNoises noises;

    private HashSet<IntVector3> chunkGenerationQueue;

    public WorldServer(uint horizontalSize, uint verticalSize) : base(horizontalSize, verticalSize) {
        entityTracker = entityManager.GetEntityTracker();
        worldFileHandler = new WorldFileHandler(this);
        chunkGenerationQueue = [];
    }

    public void ReadyGeneration(int seed) {
        OVERRIDE_LOG_NAME("World Creation");

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
        List<Chunk> chunksToIterate = [];
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

        OnWorldGenerated(chunksToIterate);

        uint totalChunks = horizontalSize * horizontalSize * verticalSize;
        double totalTime = stopwatch.Elapsed.TotalMilliseconds;
        double averageTime = totalTime / totalChunks;
        double chunksPerSecond = 1000.0f / averageTime;
        KINFO("Took " + totalTime.ToString("F2") + "ms to generate world with size of {" + horizontalSize + "x" + verticalSize + "x" + horizontalSize + "} for {" + totalChunks + "} total chunks, taking roughly " + averageTime.ToString("F1") + "ms per chunks for roughly " + chunksPerSecond.ToString("F0") + " chunks generated per second");
    }

    protected override void HandleChunkNeeds(IntVector3 chunkPosition, bool chunkExists, Chunk chunk) {
        if (!chunkExists || (chunkExists && !chunk.IsGenerated())) {
            chunkGenerationQueue.Add(chunkPosition);
        }
    }

    protected override void ProcessTick() {
        OVERRIDE_LOG_NAME("Tick Thread");

        CalculateChunkNeeds(horizontalGenerationDistance, verticalGenerationDistance + 4);

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

        QueryDescription query = new QueryDescription();
        entityManager.GetArchWorld().Query(in query, (ref EntityTransformComponent transformComponent, ref EntityIdentifierComponent identifierComponent) => {
            HashSet<ulong> playersInRange = entityTracker.GetPlayersInRangeOfEntity(identifierComponent.entityAUID);

            EntityUpdatesPacket entityUpdatesPacket = new EntityUpdatesPacket();
            entityUpdatesPacket.entityAUID = identifierComponent.entityAUID;
            entityUpdatesPacket.entityTransform = transformComponent.AsSimpleTransform();
            foreach (ulong playerAUID in playersInRange) {
                Console.WriteLine("sending entity packet " + entityUpdatesPacket.entityAUID + " to " + playerAUID);
                networkHandler.QueuePacket<EntityUpdatesPacket>(entityUpdatesPacket, PacketType.ENTITY_UPDATES, players[playerAUID]);
            }
        });

        // temporary. chunks will track what entities they have soon and this will be optimized by only querying said chunks
        foreach (KeyValuePair<ulong, int> playerPair in players) {
            ArchEntity player = entityManager.GetEntity(playerPair.Key);
            ulong playerAUID = archWorld.Get<EntityIdentifierComponent>(player).entityAUID;
            Vector3 playerPosition = archWorld.Get<EntityTransformComponent>(player).position;

            entityManager.GetArchWorld().Query(in query, (ref EntityTransformComponent transformComponent, ref EntityIdentifierComponent identifierComponent) => {
                ulong entityAUID = identifierComponent.entityAUID;
                if (entityAUID == playerAUID) {
                    return;
                }

                float distance = Vector3.DistanceSquared(playerPosition, transformComponent.position);
                //if (distance > 25 * 25) {
                //    entityTracker.RemovePlayerFromEntity(entityAUID, playerAUID);
                //} else {
                    if (!entityTracker.IsEntityTrackedByPlayer(entityAUID, playerAUID)) {
                        NewEntityPacket newEntitiesPacket = new NewEntityPacket(entityManager.GetEntity(entityAUID), assetManager.GetEntityType(identifierComponent.entityTypeStringID), transformComponent.AsSimpleTransform(), entityAUID);
                        networkHandler.QueuePacket<NewEntityPacket>(newEntitiesPacket, PacketType.NEW_ENTITIES, playerPair.Value);
                    }
                    entityTracker.AddPlayerToEntity(entityAUID, playerAUID);
                //}
            });
        }

        eventManager.TriggerEvent<WorldTickEvent>(new WorldTickEvent(totalTicks));
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

    public ref GenerationNoises GetNoises() {
        return ref noises;
    }
}