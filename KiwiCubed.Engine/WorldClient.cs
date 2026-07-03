namespace KiwiCubed.Engine;

using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;
using KiwiCubed.Api;

using static KiwiCubed.Api.Block;
using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.Utils;

public class WorldClient : World, IWorldClient, IDisposable {
    protected HashSet<IntVector3> chunkMeshingQueue;
    protected object meshingQueueLock = new object();

    public WorldClient() : base(0, 0) { 
        chunkMeshingQueue = [];
    }

    protected override void HandleChunkNeeds(IntVector3 chunkPosition, bool chunkExists, Chunk chunk, ulong playerAUID) {
        if (chunkExists && chunk.IsMeshable() && !chunkMeshingQueue.Contains(chunkPosition) && !chunk.IsMeshing()) {
            lock (meshingQueueLock) {
                chunkMeshingQueue.Add(chunkPosition);
            }
        }
    }

    protected override void ProcessTick() {
        OVERRIDE_LOG_NAME("Tick Thread");

        CalculateChunkNeeds(horizontalSimulationRadius, verticalSimulationRadius, [ClientPlayer.GetPlayerAUID()]);

        lock (meshingQueueLock) {
            Parallel.ForEach(chunkMeshingQueue, chunkPosition => {
                Chunk chunk = (Chunk)chunkHandler.GetChunk(chunkPosition, true);
                chunk.GenerateMesh(true);
            });
            chunkMeshingQueue.Clear();
        }

        foreach (IntVector3 chunkPosition in chunkUnloadingQueue) {
            chunkHandler.RemoveChunk(chunkPosition);
        }
        chunkUnloadingQueue.Clear();

        ArchEntity clientPlayer = ClientPlayer.GetPlayer();
        EntityIdentifierComponent identifierComponent = archWorld.Get<EntityIdentifierComponent>(clientPlayer);
        EntityTransformComponent transformComponent = archWorld.Get<EntityTransformComponent>(clientPlayer);
        EntityPhysicalComponent physicalComponent = archWorld.Get<EntityPhysicalComponent>(clientPlayer);

        PlayerTransformPacket transformPacket = new PlayerTransformPacket(identifierComponent.entityAUID, sessionTicks, transformComponent.position, transformComponent.orientation, physicalComponent.isGrounded);
        networkHandler.QueuePacketToAll(transformPacket, PacketType.PLAYER_TRANSFORM);
    }

    public void HandleChunkDataPacket(ChunkDataPacket packet) {
        ((Chunk)chunkHandler.GetChunk(packet.X, packet.Y, packet.Z, true)).LoadChunkData(packet.blockPalette, packet.blockIndices);
        for (int iterator = 1; iterator < BlockFace.faceModifiers.Length; iterator++) {
            IntVector3 adjacentChunkPosition = new IntVector3(packet.X + BlockFace.faceModifiers[iterator].X, packet.Y + BlockFace.faceModifiers[iterator].Y, packet.Z + BlockFace.faceModifiers[iterator].Z);
            Chunk adjacentChunk = (Chunk)chunkHandler.GetChunk(adjacentChunkPosition, false);
            if (adjacentChunk.IsReal() && adjacentChunk.WasNeighborEmptyAtMesh(BlockFace.GetOpposite((FaceDirection)iterator))) {
                lock (meshingQueueLock) {
                    chunkMeshingQueue.Add(adjacentChunkPosition);
                }
            }
        }
    }

    public void HandleChunkDiffPacket(ChunkEditPacket packet) {
        ushort blockID = assetManager.GetBlockDefinitionRawID(packet.newBlockStringID);
        chunkHandler.SetBlock(packet.editedBlockPosition, blockID);

        chunkHandler.MeshModifiedChunk(packet.editedBlockPosition);
    }

    public void HandleNewEntitiesPacket(NewEntityPacket packet) { }

    public void HandleUnloadEntityPacket(UnloadEntityPacket packet) {
        entityManager.KillEntity(packet.entityAUID);
    }

    public void HandleEntityUpdatesPacket(EntityUpdatePacket packet) {
        ArchEntity entity = entityManager.GetEntity(packet.entityAUID);

        ref EntityTransformComponent transformComponent = ref archWorld.Get<EntityTransformComponent>(entity);
        ref EntityRenderableComponent renderableComponent = ref archWorld.Get<EntityRenderableComponent>(entity);
        renderableComponent.oldPosition = transformComponent.position;
        renderableComponent.oldOrientation = transformComponent.orientation;

        transformComponent.position = packet.entityTransform.position;
        transformComponent.orientation = packet.entityTransform.orientation;
    }

    public ArchEntity GetClientPlayer() {
        return ClientPlayer.GetPlayer();
    }

    public void Dispose() {
        CommonDispose();
    }
}