namespace KiwiCubed.Engine;

using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;
using Arch.Core;
using KiwiCubed.Api;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.IPlayer;
using static KiwiCubed.Api.Util;

public abstract class World : IWorld, IDisposable {
    protected int worldSeed = 0;

    protected uint horizontalSize = 0;
    protected uint verticalSize = 0;

    protected NetworkHandler networkHandler = null;
    protected EventManager eventManager = null;
    protected AssetManager assetManager = null;
    protected ChunkHandler chunkHandler = null;
    protected EntityManager entityManager = null;
    protected ArchWorld archWorld = null;
    protected ConcurrentDictionary<ulong, int> players = null;

    protected Thread tickThread;
    protected volatile bool tickShouldRun = false;
    public static int targetTps { get; private set; } = 20;
    protected float systemTicksPerTick = 0.0f;
    protected ulong sessionTicks = 0;
    protected ulong totalTicks = 0;
    protected float realTps = 0.0f;
    protected long lastTickTime = 0;
    protected double tickDelta = 0.0d;
    protected float partialTicks = 0.0f;

    protected HashSet<IntVector3> chunkMeshingQueue;
    protected HashSet<IntVector3> chunkUnloadingQueue;
    protected uint horizontalGenerationDistance = 8;
    protected uint verticalGenerationDistance = 4;
    protected string currentCommandString = "";

    public World(uint horizontalSize, uint verticalSize) {
        this.horizontalSize = horizontalSize;
        this.verticalSize = verticalSize;
        networkHandler = MetaHandler.Get<NetworkHandler>();
        eventManager = (EventManager)MetaHandler.Get<IEventManager>();
        assetManager = (AssetManager)MetaHandler.Get<IAssetManager>();
        chunkHandler = new ChunkHandler(this);
        entityManager = new EntityManager();
        archWorld = entityManager.GetArchWorld();
        players = new();
        systemTicksPerTick = (float)Stopwatch.Frequency / targetTps;
        chunkMeshingQueue = [];
        chunkUnloadingQueue = [];

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

        eventManager.TriggerEvent<WorldLoadEvent>(new WorldLoadEvent(this));
    }

    protected virtual void OnWorldGenerated(List<Chunk> chunksToIterate) { }

    public ArchEntity SetupNewPlayer(int clientID, string playerName) {
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

        return player;
    }

    public void HandleChunkDataPacket(ChunkDataPacket packet) {
        ((Chunk)chunkHandler.GetChunk(packet.X, packet.Y, packet.Z, true)).LoadChunkData(packet.blockPalette, packet.blockIndices);
    }

    public void HandleNewEntitiesPacket(NewEntityPacket packet) {
        ArchEntity newEntity = entityManager.SpawnEntity(packet.newEntityAUID, packet.newEntityType, packet.newEntityTransform.position, packet.newEntityTransform.orientation);
        ArchEntityDeserializer deserializer = packet.newEntityType.networkFunctions.deserializer;
        deserializer(packet.reader, newEntity);
    }

    public void HandleEntityUpdatesPacket(EntityUpdatesPacket packet) {
        ArchEntity entity = entityManager.GetEntity(packet.entityAUID);

        ref EntityTransformComponent transformComponent = ref archWorld.Get<EntityTransformComponent>(entity);
        ref EntityRenderableComponent renderableComponent = ref archWorld.Get<EntityRenderableComponent>(entity);
        renderableComponent.oldPosition = transformComponent.position;
        renderableComponent.oldOrientation = transformComponent.orientation;

        transformComponent.position = packet.entityTransform.position;
        transformComponent.orientation = packet.entityTransform.orientation;
    }

    public void HandleConnectionRequestPacket(ConnectionRequestPacket packet) {
        SetupNewPlayer(packet.clientPeerID, packet.playerName);

        lock (chunkHandler.GetChunkMutex()) {
            foreach (KeyValuePair<IntVector3, IChunk> chunkPair in chunkHandler.GetChunks()) {
                SendChunk((Chunk)chunkPair.Value);
            }
        }
    }

    public void HandlePlayerTransformPacket(PlayerTransformPacket packet) {
        ArchEntity player = entityManager.GetEntity(packet.AUID);
        ref EntityTransformComponent transformComponent = ref archWorld.Get<EntityTransformComponent>(player);
        transformComponent.position = packet.position;
        transformComponent.orientation = packet.orientation;
    }

    public void SendChunk(Chunk chunk) {
        if (chunk.IsEmpty()) {
            return;
        }

        ChunkDataPacket chunkPacket = new ChunkDataPacket(chunk.chunkX, chunk.chunkY, chunk.chunkZ, chunk.GetBlockPalette(), chunk.GetPaletteIndices());
        networkHandler.QueuePacket(chunkPacket, PacketType.CHUNK_DATA);
    }

    public void LoadPlayer(Vector3 position, Vector3 orientation, GameMode gameMode) {
    //    player = new Player(0UL, position, orientation, this);
    }

    public void RemovePlayer(ulong player) {
        if (!players.TryRemove(player, out int clientID)) {
            KERR("Tried to remove a player that the world wasn't aware of");
        }
    }

    public void UpdatePartialTicks() {
        long currentTime = Stopwatch.GetTimestamp();
        partialTicks = (float)((currentTime - lastTickTime) / systemTicksPerTick);
        partialTicks = Math.Clamp(partialTicks, 0.0f, 1.0f);
        //Console.WriteLine(partialTicks + " " + sessionTicks);
    }

    public void CalculateChunkNeeds(uint horizontalRadius, uint verticalRadius) {
        List<Chunk> safeChunks = [];
        uint unloadingDistanceHorizontal = horizontalRadius + 2;
        uint unloadingDistanceVertical = verticalRadius + 2;
        foreach (KeyValuePair<ulong, int> playerPair in players) {
            ArchEntity player = entityManager.GetEntity(playerPair.Key);
            EntityTransformComponent playerTransform = archWorld.Get<EntityTransformComponent>(player);
            EntityPlayerComponent playerComponent = archWorld.Get<EntityPlayerComponent>(player);
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

                        HandleChunkNeeds(chunkPosition, chunkExists, chunk);
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

    protected abstract void HandleChunkNeeds(IntVector3 chunkPosition, bool chunkExists, Chunk chunk);

    //protected void GetConsoleInput() {
    //    while (Console.KeyAvailable) {
    //        ConsoleKeyInfo keyInfo = Console.ReadKey(true);
    //
    //        if (keyInfo.Key == ConsoleKey.Enter) {
    //            ExecuteConsoleCommand();
    //            currentCommandString = "";
    //        } else {
    //            if (keyInfo.Key == ConsoleKey.Backspace && currentCommandString.Length > 0) {
    //                currentCommandString = currentCommandString[..^1];
    //            } else if (!char.IsControl(keyInfo.KeyChar)) {
    //                currentCommandString += keyInfo.KeyChar;
    //            }
    //        }
    //    }
    //}

    // will be replaced by a much better and much more dynamic system. dunno why i put so much effort into a simple debug bit i knew id replace
    //protected virtual void ExecuteConsoleCommand() {
    //    OVERRIDE_LOG_NAME("Console Command");
    //
    //    string[] parts = currentCommandString.ToLower().Split(" ");
    //    switch (parts[0]) {
    //        case "tickqueue":
    //            if (parts.Length < 2) {
    //                KWARN("Subcommand not found. Use \"help " + parts[0] + "\" to see a list of valid subcommands");
    //                break;
    //            }
    //            if (parts[1] == "generate") {
    //                KINFO("Generation queue info");
    //                //KINFO(" * Size: " + chunkGenerationQueue.Count);
    //            } else if (parts[1] == "unload") {
    //                KINFO("Unloading queue info");
    //                KINFO(" * Size: " + chunkUnloadingQueue.Count);
    //            } else {
    //                KWARN("Subcommand \"" + parts[1] + "\" not a valid argument for command \"" + parts[0] + "\", use \"help " + parts[0] + "\" to see a list of valid subcommands");
    //            }
    //            break;
    //        case "players":
    //            KINFO("Players info: ");
    //            KINFO(" * Count: " + players.Count);
    //            break;
    //        case "worldinfo":
    //            KINFO("World info: ");
    //            KINFO(" * Seed: " + worldSeed);
    //            KINFO(" * Total chunks: " + chunkHandler.GetChunks().Count);
    //            break;
    //        default:
    //            KWARN("Command \"" + parts[0] + "\" not recognized. Use \"help\" to see a list of valid commands");
    //            break;
    //    }
    //}

    public void GetTickInfo(out float realTps, out int targetTps, out ulong totalTicks, out long lastTickTime, out float partialTicks, out double tickDelta) {
        realTps = this.realTps;
        targetTps = World.targetTps;
        totalTicks = this.totalTicks;
        lastTickTime = this.lastTickTime;
        partialTicks = this.partialTicks;
        tickDelta = this.tickDelta;
    }

    protected abstract void ProcessTick();

    protected void ApplyEntityPhysics() {
        QueryDescription query = new QueryDescription().WithAll<EntityPhysicalComponent>().WithNone<EntityPlayerClientComponent>();
        archWorld.Query(in query, (ArchEntity entity, ref EntityTransformComponent transformComponent, ref EntityPhysicalComponent physicalComponent) => {
            Physics.ApplyPhysics(chunkHandler, ref transformComponent, ref physicalComponent, tickDelta);
        });
        query = new QueryDescription();
        archWorld.Query(in query, (ref EntityTransformComponent transformComponent) => {
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

            if (sessionTicks % (ulong)targetTps == 0) {
                realTps = targetTps / ((lastTickTime - lastTickBlockTimestamp) / frequency);
                lastTickBlockTimestamp = lastTickTime;
                //KINFO("Running at: " + realTps.ToString("F2") + " TPS");
            }

            networkHandler.PollEvents();

            tickDelta = (double)(Stopwatch.GetTimestamp() - lastTickTime) / Stopwatch.Frequency;

            ProcessTick();

            lastTickTime = Stopwatch.GetTimestamp();

            networkHandler.FlushPackets();

            long nextTickTarget = startTimestamp + (long)(sessionTicks * systemTicksPerTick);
            while (Stopwatch.GetTimestamp() < nextTickTarget) {
                if ((nextTickTarget - Stopwatch.GetTimestamp()) > (frequency / 1000) * 15) {
                    Thread.Sleep(0);
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

    public int GetSeed() {
        return worldSeed;
    }

    public List<ArchEntity> GetPlayers() {
        return entityManager.GetEntitiesOfType(new AssetStringID("kiwicubed", "player"));
    }

    public IChunkHandler GetChunkHandler() {
        return chunkHandler;
    }

    public IEntityManager GetEntityManager() {
        return entityManager;
    }

    public ulong GetSessionTicks() {
        return sessionTicks;
    }

    public virtual void Dispose() {
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