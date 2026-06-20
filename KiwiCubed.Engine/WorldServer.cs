namespace KiwiCubed.Engine;

using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;
using Arch.Core;
using KiwiCubed.Api;
using System.Collections.Generic;
using System.Collections.Concurrent;

using static Chunk;
using static FastNoiseLite;
using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.IPlayer;
using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.Utils;
using System.Numerics;

public class WorldServer : World, IWorldServer, IDisposable {
    private PlayerTracker playerTracker = null;
    private WorldFileHandler worldFileHandler = null;
    private ConcurrentDictionary<ulong, int> players = null;
    private Dictionary<int, string> connectingPlayers = null;
    private GenerationNoises noises;

    private HashSet<IntVector3> chunkGenerationQueue;

    public WorldServer(uint horizontalSize, uint verticalSize) : base(horizontalSize, verticalSize) {
        playerTracker = entityManager.GetEntityTracker();
        worldFileHandler = new WorldFileHandler(this);
        players = [];
        connectingPlayers = [];
        chunkGenerationQueue = [];
    }

    public void ReadyGeneration(int seed) {
        OVERRIDE_LOG_NAME("World Creation");

        worldSeed = seed;

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

    public ArchEntity SetupNewPlayer(int clientID, string playerName, int clientPeerID) {
        EntityType playerType = assetManager.GetEntityType(new AssetStringID("kiwicubed", "player"));
        ulong playerAUID = MakeAUID(playerName);
        ArchEntity player = entityManager.SpawnEntity(playerAUID, playerType, new Vector3(0, 81, 0), Quaternion.CreateFromYawPitchRoll(0.0f, 0.5f, 0.0f));
        ref EntityPlayerComponent playerComponent = ref archWorld.Get<EntityPlayerComponent>(player);
        ref EntityPhysicalComponent physicalComponent = ref archWorld.Get<EntityPhysicalComponent>(player);
        EntityIdentifierComponent identifierComponent = archWorld.Get<EntityIdentifierComponent>(player);
        players.TryAdd(identifierComponent.entityAUID, clientID);

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

        if (foundPosition) {
            ref EntityTransformComponent playerTransform = ref archWorld.Get<EntityTransformComponent>(player);
            playerTransform.position = position;
        }

        if (!foundPosition) {
            KWARN("Could not find suitable position to spawn player");
        }

        NewEntityPacket newEntityPacket = new NewEntityPacket(player, assetManager.GetEntityType(new AssetStringID("kiwicubed", "player")), new SimpleTransform(position), playerAUID);
        networkHandler.QueuePacket(newEntityPacket, PacketType.NEW_ENTITY, clientPeerID);

        return player;
    }

    protected override void HandleChunkNeeds(IntVector3 chunkPosition, bool chunkExists, Chunk chunk) {
        if (!chunkExists || (chunkExists && !chunk.IsGenerated())) {
            chunkGenerationQueue.Add(chunkPosition);
        }
    }

    protected override void ProcessTick() {
        OVERRIDE_LOG_NAME("Tick Thread");

        CalculateChunkNeeds(horizontalGenerationDistance, verticalGenerationDistance + 4, players.Keys.ToArray());

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
            HashSet<ulong> playersInRange = playerTracker.GetPlayersInRangeOfEntity(identifierComponent.entityAUID);

            EntityUpdatesPacket entityUpdatesPacket = new EntityUpdatesPacket();
            entityUpdatesPacket.entityAUID = identifierComponent.entityAUID;
            entityUpdatesPacket.entityTransform = transformComponent.AsSimpleTransform();
            foreach (ulong playerAUID in playersInRange) {
                networkHandler.QueuePacket<EntityUpdatesPacket>(entityUpdatesPacket, PacketType.ENTITY_UPDATES, players[playerAUID]);
            }
        });

        // temporary. chunks will track what entities they have soon and this will be optimized by only querying said chunks
        foreach (KeyValuePair<ulong, int> playerPair in players) {
            ArchEntity player = entityManager.GetEntity(playerPair.Key);
            ulong playerAUID = archWorld.Get<EntityIdentifierComponent>(player).entityAUID;
            EntityTransformComponent trans = archWorld.Get<EntityTransformComponent>(player);
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
                    if (!playerTracker.IsEntityTrackedByPlayer(entityAUID, playerAUID)) {
                        NewEntityPacket newEntitiesPacket = new NewEntityPacket(entityManager.GetEntity(entityAUID), assetManager.GetEntityType(identifierComponent.entityTypeStringID), transformComponent.AsSimpleTransform(), entityAUID);
                        networkHandler.QueuePacket<NewEntityPacket>(newEntitiesPacket, PacketType.NEW_ENTITY, playerPair.Value);
                    }
                    playerTracker.AddPlayerToEntity(entityAUID, playerAUID);
                //}
            });

            //Console.WriteLine(chunkHandler.GetChunkExists(trans.globalChunkPosition));
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

    public void HandleConnectionRequestPacket(ConnectionRequestPacket packet) {
        OVERRIDE_LOG_NAME("World");

        KINFO("Awaiting DataReady packet from client with ID {" + packet.clientPeerID + "}");
        connectingPlayers.Add(packet.clientPeerID, packet.playerName);
    }

    public void HandleDataReadyPacket(DataReadyPacket packet) {
        OVERRIDE_LOG_NAME("World");

        if (connectingPlayers.TryGetValue(packet.clientPeerID, out string playerName)) {
            SetupNewPlayer(packet.clientPeerID, playerName, packet.clientPeerID);

            lock (chunkHandler.GetChunkMutex()) {
                foreach (KeyValuePair<IntVector3, IChunk> chunkPair in chunkHandler.GetChunks()) {
                    SendChunk((Chunk)chunkPair.Value);
                }
            }

            connectingPlayers.Remove(packet.clientPeerID);
        } else {
            KERR("Got DataReadyPacket from client with ID {" + packet.clientPeerID + "} that was not being awaited for by server");
            KBREAK();
        }
    }

    public void HandlePlayerTransformPacket(PlayerTransformPacket packet) {
        ArchEntity player = entityManager.GetEntity(packet.AUID);
        ref EntityTransformComponent transformComponent = ref archWorld.Get<EntityTransformComponent>(player);
        transformComponent.position = packet.position;
        transformComponent.orientation = packet.orientation;
    }

    public ref GenerationNoises GetNoises() {
        return ref noises;
    }

    public void Dispose() {
        players = null;

        CommonDispose();
    }
}