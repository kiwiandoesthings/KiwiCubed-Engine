namespace KiwiCubed.Engine;

using KiwiCubed.Api;

using static KiwiCubed.Api.Utils;

public class PlayerTracker {
    private KLogger logger;
    private Dictionary<ulong, HashSet<ulong>> playersInRangeOfEntity;
    private Dictionary<ulong, HashSet<IntVector3>> chunksInRangeOfPlayer;

    public PlayerTracker() {
        logger = new KLogger("PlayerTracker");
        playersInRangeOfEntity = [];
        chunksInRangeOfPlayer = [];
    }

    public void AddTrackedEntity(ulong entityAUID) {
        if (playersInRangeOfEntity.TryGetValue(entityAUID, out HashSet<ulong>? players)) {
            logger.ERR("Tried to add an entity with AUID {" + entityAUID + "} to be tracked twice");
            logger.BREAK();
        }
        playersInRangeOfEntity.Add(entityAUID, []);
    }

    public void RemoveTrackedEntity(ulong entityAUID) {
        if (!playersInRangeOfEntity.TryGetValue(entityAUID, out HashSet<ulong>? players)) {
            logger.ERR("Tried to remove an entity with AUID {" + entityAUID + "} from being tracked that was not being tracked yet");
            logger.BREAK();
        }

        playersInRangeOfEntity.Remove(entityAUID);
    }

    public void AddPlayerToEntity(ulong entityAUID, ulong playerAUID) {
        if (!playersInRangeOfEntity.TryGetValue(entityAUID, out HashSet<ulong>? players)) {
            logger.ERR("Tried to add a player with AUID {" + entityAUID + "} to track entity with AUID {" + entityAUID + "} when the entity was not being tracked");
            logger.BREAK();
        }
        if (players.Contains(playerAUID)) {
            //logger.ERR("Tried to a player with AUID {" + entityAUID + "} to track entity with AUID {" + entityAUID + "} twice");
            //logger.BREAK();
        }

        players.Add(playerAUID);
    }

    public void RemovePlayerFromEntity(ulong entityAUID, ulong playerAUID) {
        if (!playersInRangeOfEntity.TryGetValue(entityAUID, out HashSet<ulong>? players)) {
            logger.ERR("Tried to remove a player with AUID {" + entityAUID + "} from tracking entity with AUID {" + entityAUID + "} when the entity was not being tracked");
            logger.BREAK();
        }
        if (!players.Contains(playerAUID)) {
            //logger.ERR("Tried to a player with AUID {" + entityAUID + "} to track entity with AUID {" + entityAUID + "} when that player was not tracking the entity yet");
            //logger.BREAK();
        }

        players.Remove(playerAUID);
    }

    public HashSet<ulong> GetPlayersInRangeOfEntity(ulong entityAUID) {
        if (!playersInRangeOfEntity.TryGetValue(entityAUID, out HashSet<ulong>? players)) {
            logger.ERR("Tried to query the players tracking entity with AUID {" + entityAUID + "} that did not exist");
            logger.BREAK();
        }

        return players;
    }

    public bool IsEntityTrackedByPlayer(ulong entityAUID, ulong playerAUID) {
        if (!playersInRangeOfEntity.TryGetValue(entityAUID, out HashSet<ulong>? players)) {
            logger.ERR("Tried to query the players tracking entity with AUID {" + entityAUID + "} that did not exist");
            logger.BREAK();
        }

        return players.Contains(playerAUID);
    }

    public void RegisterPlayer(ulong playerAUID) {
        if (chunksInRangeOfPlayer.TryGetValue(playerAUID, out HashSet<IntVector3>? chunks)) {
            logger.ERR("Tried to register a player with AUID {" + playerAUID + "} to be tracked twice");
            logger.BREAK();
        }

        chunksInRangeOfPlayer.Add(playerAUID, []);
    }

    public void DeregisterPlayer(ulong playerAUID) {
        if (!chunksInRangeOfPlayer.TryGetValue(playerAUID, out HashSet<IntVector3>? chunks)) {
            logger.ERR("Tried to deregister a player with AUID {" + playerAUID + "} from being tracked that wasn't being tracked yet");
            logger.BREAK();
        }

        chunksInRangeOfPlayer.Remove(playerAUID);
    }

    public void AddChunkToPlayer(ulong playerAUID, IntVector3 chunkPosition) {
        if (!chunksInRangeOfPlayer.TryGetValue(playerAUID, out HashSet<IntVector3>? chunks)) {
            logger.ERR("Tried to add a chunk to a player with AUID {" + playerAUID + "} that did not exist");
            logger.BREAK();
        }
        if (chunks.Contains(chunkPosition)) {
            logger.ERR("Tried to add a chunk to a player with AUID {" + playerAUID + "} twice");
            logger.BREAK();
        }

        chunks.Add(chunkPosition);
    }

    public void RemoveChunkFromPlayer(ulong playerAUID, IntVector3 chunkPosition) {
        if (!chunksInRangeOfPlayer.TryGetValue(playerAUID, out HashSet<IntVector3>? chunks)) {
            logger.ERR("Tried to remove a chunk to a player with AUID {" + playerAUID + "} that did not exist");
            logger.BREAK();
        }
        if (!chunks.Contains(chunkPosition)) {
            logger.ERR("Tried to remove a chunk to a player with AUID {" + playerAUID + "} that was not added");
            logger.BREAK();
        }

        chunks.Remove(chunkPosition);
    }

    public bool DoesPlayerHaveChunk(ulong playerAUID, IntVector3 chunkPosition) {
        if (chunksInRangeOfPlayer.TryGetValue(playerAUID, out HashSet<IntVector3>? chunks)) {
            return chunks.Contains(chunkPosition);
        } else {
            logger.ERR("Tried to query a chunk with a player with AUID {" + playerAUID + "} that did not exist");
            logger.BREAK();
        }

        return false;
    }

    public HashSet<IntVector3> GetPlayerChunks(ulong playerAUID) {
        if (chunksInRangeOfPlayer.TryGetValue(playerAUID, out HashSet<IntVector3>? chunks)) {
            return chunks;
        } else {
            logger.ERR("Tried to query a chunk with a player with AUID {" + playerAUID + "} that did not exist");
            logger.BREAK();
        }

        return null;
    }
}
