namespace KiwiCubed.Engine;

using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;
using Arch.Core;
using KiwiCubed.Api;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.IPlayer;
using static KiwiCubed.Api.Utils;

public abstract class World : IWorld {
    protected int worldSeed = 0;

    protected NetworkHandler networkHandler = null;
    protected EventManager eventManager = null;
    protected AssetManager assetManager = null;
    protected ChunkHandler chunkHandler = null;
    protected EntityManager entityManager = null;
    protected ArchWorld archWorld = null;
    protected KLogger logger = null;

    protected Thread tickThread;
    protected volatile bool tickShouldRun = false;
    protected int targetTps = 20;
    protected float systemTicksPerTick = 0.0f;
    protected ulong sessionTicks = 0;
    protected ulong totalTicks = 0;
    protected float realTps = 0.0f;
    protected long lastTickTime = 0;
    protected double tickDelta = 0.0d;
    protected float partialTicks = 0.0f;

    protected HashSet<IntVector3> chunkUnloadingQueue;
    protected HashSet<IntVector3> safeChunks;
    protected int horizontalSimulationRadius = 4;
    protected int verticalSimulationRadius = 4;
    protected string currentCommandString = "";

    public World() {
        networkHandler = MetaHandler.Get<NetworkHandler>();
        eventManager = (EventManager)MetaHandler.Get<IEventManager>();
        assetManager = (AssetManager)MetaHandler.Get<IAssetManager>();
        chunkHandler = new ChunkHandler();
        entityManager = new EntityManager();
        archWorld = entityManager.GetArchWorld();
        logger = new KLogger("World");
        systemTicksPerTick = (float)Stopwatch.Frequency / targetTps;
        chunkUnloadingQueue = [];
        safeChunks = [];

        Chunk.SetupChunks(chunkHandler);

        eventManager.TriggerEvent(new WorldLoadEvent(this));
    }

    public void SendChunk(Chunk chunk, int clientID) {
        ChunkDataPacket chunkPacket = new ChunkDataPacket(chunk.chunkX, chunk.chunkY, chunk.chunkZ, chunk.GetBlockPalette(), chunk.GetPaletteIndices());
        networkHandler.QueuePacketTo(chunkPacket, PacketType.CHUNK_DATA, clientID);
    }

    public void UpdatePartialTicks() {
        long currentTime = Stopwatch.GetTimestamp();
        partialTicks = (float)((currentTime - lastTickTime) / systemTicksPerTick);
        partialTicks = Math.Clamp(partialTicks, 0.0f, 1.0f);
    }

    public void CalculateChunkNeeds(int horizontalRadius, int verticalRadius, ulong[] playerAUIDs) {
        int horizontalSafeRadius = horizontalRadius + 2;
        int verticalSafeRadus = verticalRadius + 2;
        for (int iterator = 0; iterator < playerAUIDs.Length; iterator++) {
            ArchEntity player = entityManager.GetEntity(playerAUIDs[iterator]);
            EntityTransformComponent playerTransform = archWorld.Get<EntityTransformComponent>(player);
            EntityPlayerComponent playerComponent = archWorld.Get<EntityPlayerComponent>(player);
            IntVector3 playerChunkPosition = playerTransform.globalChunkPosition;
            for (int chunkX = playerChunkPosition.X - horizontalRadius; chunkX <= playerChunkPosition.X + horizontalRadius; ++chunkX) {
                for (int chunkY = playerChunkPosition.Y - verticalRadius; chunkY <= playerChunkPosition.Y + verticalRadius; ++chunkY) {
                    for (int chunkZ = playerChunkPosition.Z - horizontalRadius; chunkZ <= playerChunkPosition.Z + horizontalRadius; ++chunkZ) {
                        IntVector3 chunkPosition = new IntVector3(chunkX, chunkY, chunkZ);
                        bool chunkExists = chunkHandler.GetChunkExists(chunkX, chunkY, chunkZ);
                        Chunk chunk = (Chunk)chunkHandler.GetChunk(chunkX, chunkY, chunkZ, false);
                        if (chunk.IsAwaitingDestruction()) {
                            continue;
                        }

                        HandleChunkNeeds(chunkPosition, chunkExists, chunk, playerAUIDs[iterator]);
					}
                }
            }
            for (int chunkX = playerChunkPosition.X - horizontalSafeRadius; chunkX <= playerChunkPosition.X + horizontalSafeRadius; ++chunkX) {
                for (int chunkY = playerChunkPosition.Y - verticalSafeRadus; chunkY <= playerChunkPosition.Y + verticalSafeRadus; ++chunkY) {
                    for (int chunkZ = playerChunkPosition.Z - horizontalSafeRadius; chunkZ <= playerChunkPosition.Z + horizontalSafeRadius; ++chunkZ) {
                        safeChunks.Add(new IntVector3(chunkX, chunkY, chunkZ));
                    }
                }
            }
        }

        lock (chunkHandler.GetChunkMutex()) {
            foreach (KeyValuePair<IntVector3, IChunk> chunkPair in chunkHandler.GetChunks()) {
                Chunk chunk = (Chunk)chunkPair.Value;
                if (!chunk.IsAwaitingDestruction() && !safeChunks.Contains(chunkPair.Key)) {
                    chunkUnloadingQueue.Add(chunkPair.Key);
                }
            }
        }

        safeChunks.Clear();
    }

    protected virtual void HandleChunkNeeds(IntVector3 chunkPosition, bool chunkExists, Chunk chunk, ulong playerAUID) { }

    public void GetTickInfo(out float realTps, out int targetTps, out ulong totalTicks, out long lastTickTime, out float partialTicks, out double tickDelta) {
        realTps = this.realTps;
        targetTps = this.targetTps;
        totalTicks = this.totalTicks;
        lastTickTime = this.lastTickTime;
        partialTicks = this.partialTicks;
        tickDelta = this.tickDelta;
    }

    protected abstract void ProcessTick();

    protected void ApplyEntityPhysics() {
        QueryDescription query = new QueryDescription().WithAll<EntityPhysicalComponent>().WithNone<EntityPlayerClientComponent>();
        archWorld.Query(in query, (ArchEntity entity, ref EntityTransformComponent transformComponent, ref EntityPhysicalComponent physicalComponent) => {
            Physics.ApplyPhysics(chunkHandler, ref transformComponent, ref physicalComponent, targetTps, tickDelta);
        });
        query = new QueryDescription();
        archWorld.Query(in query, (ref EntityTransformComponent transformComponent) => {
            IntVector3 newGlobalChunkPosition = new IntVector3(FloorDiv(transformComponent.position, 32));
            if (newGlobalChunkPosition != transformComponent.globalChunkPosition) {
                transformComponent.crossedChunkBoundary = true;
            }
            transformComponent.globalChunkPosition = newGlobalChunkPosition;
            transformComponent.localChunkPosition = new IntVector3(PositiveModulo(transformComponent.position, 32));
        });

        // Handle entities that have crossed chunk boundaries
        query = new QueryDescription().WithAll<EntityPlayerComponent>();
        archWorld.Query(in query, (ref EntityTransformComponent transformComponent, ref EntityIdentifierComponent identifierComponent) => {
;            if (transformComponent.crossedChunkBoundary) {
                RecalculateChunksForPlayer(identifierComponent.entityAUID);
             }
        });
        query = new QueryDescription();
        archWorld.Query(in query, (ref EntityTransformComponent transformComponent) => {
            if (transformComponent.crossedChunkBoundary) {
                transformComponent.currentChunk = chunkHandler.GetChunk(transformComponent.globalChunkPosition, false);
                transformComponent.crossedChunkBoundary = false;
            }
        });
    }

    protected virtual void RecalculateChunksForPlayer(ulong playerAUID) { }

    public void TickLoop() {
        long startTimestamp = Stopwatch.GetTimestamp();
        long lastTickBlockTimestamp = startTimestamp;
        float frequency = Stopwatch.Frequency;
        while (tickShouldRun) {
            sessionTicks++;
            totalTicks++;

            if (sessionTicks % (ulong)targetTps == 0) {
                realTps = targetTps / ((lastTickTime - lastTickBlockTimestamp) / frequency);
                lastTickBlockTimestamp = lastTickTime;
                //logger.INFO("Running at: " + realTps.ToString("F2") + " TPS");
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
        if (tickShouldRun) {
            logger.ERR("Tried to start tick thread while it was already running");
        }

        tickShouldRun = true;
        tickThread = new Thread(TickLoop) {
            IsBackground = true,
            Priority = ThreadPriority.Highest,
            Name = "KiwiCubed_TickThread"
        };
        tickThread.Start();

        logger.INFO("Successfully started tick thread");
    }

    public void StopTickThread() {
        if (!tickShouldRun) {
            logger.ERR("Tried to stop tick thread while it was already stopped");
        }
        tickShouldRun = false;
        tickThread.Join();
        logger.INFO("Successfully stopped tick thread");
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

    public int GetTargetTps() {
        return targetTps;
    }

    public ulong GetSessionTicks() {
        return sessionTicks;
    }

    public ulong GetTotalTicks() {
        return totalTicks;
    }

    public void CommonDispose() {
        if (tickShouldRun) {
            logger.ERR("Tried to dispose world while the tick thread was still running, performing emergency stop");
            StopTickThread();
        }

        archWorld = null;

        logger.INFO("Cleaning up chunks...");
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