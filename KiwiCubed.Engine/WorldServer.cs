namespace KiwiCubed.Engine;

using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;
using Arch.Core;
using KiwiCubed.Api;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.IPlayer;
using static KiwiCubed.Api.Utils;

public class WorldServer : World, IWorldServer, IDisposable {
    private PlayerTracker playerTracker = null;
    private WorldFileHandler worldFileHandler = null;
    private ConcurrentDictionary<ulong, int> players = null;
    private Dictionary<int, string> connectingPlayers = null;
    private ConcurrentStack<ulong> playersToDisconnect = null;
    private Dictionary<ulong, List<IntVector3>> chunksToSend = null;

    private HashSet<IntVector3> chunkGenerationQueue;

    public WorldServer(uint horizontalSize, uint verticalSize) : base(horizontalSize, verticalSize) {
        playerTracker = entityManager.GetEntityTracker();
        worldFileHandler = new WorldFileHandler(this);
        players = [];
        connectingPlayers = [];
        playersToDisconnect = [];
        chunkGenerationQueue = [];
        chunksToSend = [];
    }

    public void ReadyGeneration(int seed) {
        worldSeed = seed;
        ChunkGenerator.Initialize();

        logger.INFO("Prepared world for generation with seed {" + worldSeed + "}");
    }

    public ArchEntity SetupNewPlayer(int clientID, string playerName) {
        EntityType playerType = assetManager.GetEntityType(new AssetStringID("kiwicubed", "player"));
        ulong playerAUID = MakeAUID(playerName);
        ArchEntity player = entityManager.SpawnEntity(playerAUID, playerType, new Vector3(0, 81, 0), Quaternion.CreateFromYawPitchRoll(0.0f, 0.5f, 0.0f));
        ref EntityPlayerComponent playerComponent = ref archWorld.Get<EntityPlayerComponent>(player);
        ref EntityPhysicalComponent physicalComponent = ref archWorld.Get<EntityPhysicalComponent>(player);        

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

                    if (!chunk.IsGenerated() || !chunk.IsMeshed() || chunk.IsEmpty() || chunk.IsFull()) {
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
        } else {
            logger.WARN("Could not find suitable position to spawn player");
        }

        NewEntityPacket newEntityPacket = new NewEntityPacket(player, assetManager.GetEntityType(new AssetStringID("kiwicubed", "player")), new SimpleTransform(position), playerAUID);
        networkHandler.QueuePacketTo(newEntityPacket, PacketType.NEW_ENTITY, clientID);

        return player;
    }

    public void QueuePlayerDisconnect(ulong playerAUID) {
        playersToDisconnect.Push(playerAUID);
    }

    protected override void HandleChunkNeeds(IntVector3 chunkPosition, bool chunkExists, Chunk chunk, ulong playerAUID) {
        if (!chunkExists || (chunkExists && !chunk.IsGenerated())) {
            chunkGenerationQueue.Add(chunkPosition);
        } else if (chunkExists) {
            if (!playerTracker.DoesPlayerHaveChunk(playerAUID, chunkPosition)) {
                if (!chunksToSend[playerAUID].Contains(chunkPosition)) {
                    chunksToSend[playerAUID].Add(chunkPosition);
                }
                playerTracker.AddChunkToPlayer(playerAUID, chunkPosition);
            }
        }
    }

    protected override void ProcessTick() {
        CalculateChunkNeeds(horizontalSimulationRadius, verticalSimulationRadius, players.Keys.ToArray());

        foreach (ulong playerAUID in playersToDisconnect) {
            chunksToSend.Remove(playerAUID);
            playerTracker.DeregisterPlayer(playerAUID);
            players.Remove(playerAUID, out int clientID);

            logger.INFO("Disconnected player with AUID {" + playerAUID + "}");
        }

        Parallel.ForEach(chunkGenerationQueue, chunkPosition => {
            Chunk chunk = (Chunk)chunkHandler.GetChunk(chunkPosition, true);
            chunk.GenerateBlocks(this);
        });
        chunkGenerationQueue.Clear();

        foreach (IntVector3 chunkPosition in chunkUnloadingQueue) {
            chunkHandler.RemoveChunk(chunkPosition);
            foreach (ulong playerAUID in players.Keys) {
                playerTracker.GetPlayerChunks(playerAUID).Remove(chunkPosition);
            }
        }
        chunkUnloadingQueue.Clear();

        foreach (KeyValuePair<ulong, List<IntVector3>> playerChunkPair in chunksToSend) {
            foreach (IntVector3 chunkPosition in  playerChunkPair.Value) {
                SendChunk((Chunk)chunkHandler.GetChunk(chunkPosition, false));
            }
            playerChunkPair.Value.Clear();
        }

        ApplyEntityPhysics();

        QueryDescription query = new QueryDescription();
        entityManager.GetArchWorld().Query(in query, (ref EntityTransformComponent transformComponent, ref EntityIdentifierComponent identifierComponent) => {
            HashSet<ulong> playersInRange = playerTracker.GetPlayersInRangeOfEntity(identifierComponent.entityAUID);

            EntityUpdatePacket entityUpdatesPacket = new EntityUpdatePacket();
            entityUpdatesPacket.entityAUID = identifierComponent.entityAUID;
            entityUpdatesPacket.entityTransform = transformComponent.AsSimpleTransform();
            foreach (ulong playerAUID in playersInRange) {
                networkHandler.QueuePacketTo<EntityUpdatePacket>(entityUpdatesPacket, PacketType.ENTITY_UPDATE, players[playerAUID]);
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
                if (distance > 25 * 25) {
                    //playerTracker.RemovePlayerFromEntity(entityAUID, playerAUID);
                    //if (playerTracker.IsEntityTrackedByPlayer(entityAUID, playerAUID)) {
                    //    UnloadEntityPacket unloadEntityPacket = new UnloadEntityPacket(entityAUID);
                    //    networkHandler.QueuePacketTo<UnloadEntityPacket>(unloadEntityPacket, PacketType.UNLOAD_ENTITY, playerPair.Value);
                    //    playerTracker.RemovePlayerFromEntity(entityAUID, playerAUID);
                    //}
                } else {
                    if (!playerTracker.IsEntityTrackedByPlayer(entityAUID, playerAUID)) {
                        NewEntityPacket newEntitiesPacket = new NewEntityPacket(entityManager.GetEntity(entityAUID), assetManager.GetEntityType(identifierComponent.entityTypeStringID), transformComponent.AsSimpleTransform(), entityAUID);
                        networkHandler.QueuePacketTo<NewEntityPacket>(newEntitiesPacket, PacketType.NEW_ENTITY, playerPair.Value);
                    }
                    playerTracker.AddPlayerToEntity(entityAUID, playerAUID);
                }
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

    public void HandleConnectionRequestPacket(ConnectionRequestPacket packet) {
        logger.INFO("Awaiting DataReady packet from client with ID {" + packet.clientPeerID + "}");
        connectingPlayers.Add(packet.clientPeerID, packet.playerName);
    }

    public void HandleDataReadyPacket(DataReadyPacket packet) {
        if (connectingPlayers.TryGetValue(packet.clientPeerID, out string playerName)) {
            ArchEntity player = SetupNewPlayer(packet.clientPeerID, playerName);

            lock (chunkHandler.GetChunkMutex()) {
                foreach (KeyValuePair<IntVector3, IChunk> chunkPair in chunkHandler.GetChunks()) {
                    SendChunk((Chunk)chunkPair.Value);
                }
            }

            EntityIdentifierComponent identifierComponent = archWorld.Get<EntityIdentifierComponent>(player);
            connectingPlayers.Remove(packet.clientPeerID);
            chunksToSend.Add(identifierComponent.entityAUID, []);
            playerTracker.RegisterPlayer(identifierComponent.entityAUID);
            players.TryAdd(identifierComponent.entityAUID, packet.clientPeerID);
        } else {
            logger.ERR("Got DataReadyPacket from client with ID {" + packet.clientPeerID + "} that was not being awaited for by server");
            logger.BREAK();
        }
    }

    public void HandlePlayerTransformPacket(PlayerTransformPacket packet) {
        ArchEntity player = entityManager.GetEntity(packet.AUID);
        ref EntityTransformComponent transformComponent = ref archWorld.Get<EntityTransformComponent>(player);
        transformComponent.position = packet.position;
        transformComponent.orientation = packet.orientation;
    }

    public void HandleBlockInteractPacket(BlockInteractPacket packet) {
        if (packet.interactionType == BlockInteractionType.PLACE_BLOCK || packet.interactionType == BlockInteractionType.REPLACE_BLOCK) {
            if (!assetManager.IsValidBlockDefinition(packet.heldItem)) {
                logger.WARN("Received BlockInteractPacket with a held item string ID " + packet.heldItem + " that was not a valid block definition string ID");
                return;
            }
        }

        switch (packet.interactionType) {
            case BlockInteractionType.START_MINE:
                chunkHandler.RemoveBlock(packet.interactedBlockPosition);

                ChunkEditPacket chunkEditPacket = new ChunkEditPacket(packet.interactedBlockPosition, MetaHandler.Get<IAssetManager>().airStringID);
                networkHandler.QueuePacketToAll(chunkEditPacket, PacketType.CHUNK_EDIT, null, packet.clientPeerID);

                break;
            case BlockInteractionType.PLACE_BLOCK:
                ushort blockID = assetManager.GetBlockDefinitionRawID(packet.heldItem);
                chunkHandler.AddBlock(packet.interactedBlockPosition, blockID);
                break;
        }
    }

    public void HandleEntityInteractPacket(EntityInteractPacket packet) {

    }

    public ulong GetPlayerAUID(int clientID) {
        if (players.TryGetKeyByValue(clientID, out ulong playerAUID)) {
            return playerAUID;
        } else {
            logger.ERR("Could not find player AUID for client ID {" + clientID + "}");
            logger.BREAK();

            return 0;
        }
    }

    public void Dispose() {
        playerTracker = null;
        worldFileHandler = null;
        players = null;
        connectingPlayers = null;
        playersToDisconnect = null;
        chunksToSend = null;

        CommonDispose();
    }
}