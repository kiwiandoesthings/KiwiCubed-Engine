namespace KiwiCubed.Engine;

using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;
using KiwiCubed.Api;
using System.Collections.Generic;
using System.Linq;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.Util;

public class WorldClient : World {
    public WorldClient() : base(0, 0) {
        ArchEntity player = SetupNewPlayer(0, playerUsername);
        ClientPlayer.Setup(this, archWorld, player);
    }

    protected override void OnWorldGenerated(List<Chunk> chunksToIterate) {
        foreach (Chunk chunk in chunksToIterate) {
            if (chunk.IsNeededForMeshing()) {
                chunk.GenerateMesh(false);
            }
        }
    }

    protected override void HandleChunkNeeds(IntVector3 chunkPosition, bool chunkExists, Chunk chunk) {
        if (chunkExists && chunk.IsMeshable() && !chunkMeshingQueue.Contains(chunkPosition) && !chunk.IsMeshing()) {
            chunkMeshingQueue.Add(chunkPosition);
        }
    }

    protected override void ProcessTick() {
        OVERRIDE_LOG_NAME("Tick Thread");

        CalculateChunkNeeds(horizontalGenerationDistance, verticalGenerationDistance);

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

        ArchEntity clientPlayer = ClientPlayer.GetPlayer();
        EntityIdentifierComponent identifierComponent = archWorld.Get<EntityIdentifierComponent>(clientPlayer);
        EntityTransformComponent transformComponent = archWorld.Get<EntityTransformComponent>(clientPlayer);
        EntityPhysicalComponent physicalComponent = archWorld.Get<EntityPhysicalComponent>(clientPlayer);

        PlayerTransformPacket transformPacket = new PlayerTransformPacket(identifierComponent.entityAUID, sessionTicks, transformComponent.position, transformComponent.orientation, physicalComponent.isGrounded);
        networkHandler.QueuePacket(transformPacket, PacketType.PLAYER_TRANSFORM);
    }
}