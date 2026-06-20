namespace KiwiCubed.Engine;

using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.Utils;

public class PlayerTracker {
    private Dictionary<ulong, HashSet<ulong>> playersInRangeOfEntity;
    private Dictionary<ulong, HashSet<IntVector3>> chunksInRangeOfPlayer;

    public PlayerTracker() {
        playersInRangeOfEntity = [];
        chunksInRangeOfPlayer = [];
    }

    public void AddTrackedEntity(ulong entityAUID) {
        OVERRIDE_LOG_NAME("PlayerTracker");

        if (playersInRangeOfEntity.TryGetValue(entityAUID, out HashSet<ulong>? players)) {
            KERR("Tried to add an entity with AUID {" + entityAUID + "} to be tracked twice");
            KBREAK();
        }
        playersInRangeOfEntity.Add(entityAUID, []);
    }

    public void RemoveTrackedEntity(ulong entityAUID) {
        OVERRIDE_LOG_NAME("PlayerTracker");

        if (!playersInRangeOfEntity.TryGetValue(entityAUID, out HashSet<ulong>? players)) {
            KERR("Tried to remove an entity with AUID {" + entityAUID + "} from being tracked that was not being tracked yet");
            KBREAK();
        }

        playersInRangeOfEntity.Remove(entityAUID);
    }

    public void AddPlayerToEntity(ulong entityAUID, ulong playerAUID) {
        OVERRIDE_LOG_NAME("PlayerTracker");

        if (!playersInRangeOfEntity.TryGetValue(entityAUID, out HashSet<ulong>? players)) {
            KERR("Tried to add a player with AUID {" + entityAUID + "} to track entity with AUID {" + entityAUID + "} when the entity was not being tracked");
            KBREAK();
        }
        if (players.Contains(playerAUID)) {
            //KERR("Tried to a player with AUID {" + entityAUID + "} to track entity with AUID {" + entityAUID + "} twice");
            //KBREAK();
        }

        players.Add(playerAUID);
    }

    public void RemovePlayerFromEntity(ulong entityAUID, ulong playerAUID) {
        OVERRIDE_LOG_NAME("PlayerTracker");

        if (!playersInRangeOfEntity.TryGetValue(entityAUID, out HashSet<ulong>? players)) {
            KERR("Tried to remove a player with AUID {" + entityAUID + "} from tracking entity with AUID {" + entityAUID + "} when the entity was not being tracked");
            KBREAK();
        }
        if (!players.Contains(playerAUID)) {
            //KERR("Tried to a player with AUID {" + entityAUID + "} to track entity with AUID {" + entityAUID + "} when that player was not tracking the entity yet");
            //KBREAK();
        }

        players.Remove(playerAUID);
    }

    public HashSet<ulong> GetPlayersInRangeOfEntity(ulong entityAUID) {
        OVERRIDE_LOG_NAME("PlayerTracker");

        if (!playersInRangeOfEntity.TryGetValue(entityAUID, out HashSet<ulong>? players)) {
            KERR("Tried to query the players tracking entity with AUID {" + entityAUID + "} that did not exist");
            KBREAK();
        }

        return players;
    }

    public bool IsEntityTrackedByPlayer(ulong entityAUID, ulong playerAUID) {
        OVERRIDE_LOG_NAME("PlayerTracker");

        if (!playersInRangeOfEntity.TryGetValue(entityAUID, out HashSet<ulong>? players)) {
            KERR("Tried to query the players tracking entity with AUID {" + entityAUID + "} that did not exist");
            KBREAK();
        }

        return players.Contains(playerAUID);
    }

    public void RegisterPlayer(ulong playerAUID) {
        OVERRIDE_LOG_NAME("PlayerTracker");

        if (chunksInRangeOfPlayer.TryGetValue(playerAUID, out HashSet<IntVector3>? chunks)) {
            KERR("Tried to register a player with AUID {" + playerAUID + "} to be tracked twice");
            KBREAK();
        }

        chunksInRangeOfPlayer.Add(playerAUID, []);
    }

    public void DeregisterPlayer(ulong playerAUID) {
        OVERRIDE_LOG_NAME("PlayerTracker");

        if (!chunksInRangeOfPlayer.TryGetValue(playerAUID, out HashSet<IntVector3>? chunks)) {
            KERR("Tried to deregister a player with AUID {" + playerAUID + "} from being tracked that wasn't being tracked yet");
            KBREAK();
        }

        chunksInRangeOfPlayer.Remove(playerAUID);
    }

    public void AddChunkToPlayer(ulong playerAUID, IntVector3 chunkPosition) {
        OVERRIDE_LOG_NAME("PlayerTracker");

        if (!chunksInRangeOfPlayer.TryGetValue(playerAUID, out HashSet<IntVector3>? chunks)) {
            KERR("Tried to add a chunk to a player with AUID {" + playerAUID + "} that did not exist");
            KBREAK();
        }
        if (chunks.Contains(chunkPosition)) {
            KERR("Tried to add a chunk to a player with AUID {" + playerAUID + "} twice");
            KBREAK();
        }

        chunks.Add(chunkPosition);
    }

    public void RemoveChunkFromPlayer(ulong playerAUID, IntVector3 chunkPosition) {
        OVERRIDE_LOG_NAME("PlayerTracker");

        if (!chunksInRangeOfPlayer.TryGetValue(playerAUID, out HashSet<IntVector3>? chunks)) {
            KERR("Tried to remove a chunk to a player with AUID {" + playerAUID + "} that did not exist");
            KBREAK();
        }
        if (!chunks.Contains(chunkPosition)) {
            KERR("Tried to remove a chunk to a player with AUID {" + playerAUID + "} that was not added");
            KBREAK();
        }

        chunks.Remove(chunkPosition);
    }
}
