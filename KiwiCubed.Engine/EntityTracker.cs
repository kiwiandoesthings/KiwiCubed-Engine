namespace KiwiCubed.Engine;

using static KiwiCubed.Api.KLogger;

public class EntityTracker {
    private Dictionary<ulong, HashSet<ulong>> playersInRangeOfEntity;

    public EntityTracker() {
        playersInRangeOfEntity = [];
    }

    public void AddTrackedEntity(ulong entityAUID) {
        OVERRIDE_LOG_NAME("EntityTracker");

        if (playersInRangeOfEntity.ContainsKey(entityAUID)) {
            KERR("Tried to add an entity with AUID {" + entityAUID + "} to be tracked twice");
            KBREAK();
        }
        playersInRangeOfEntity.Add(entityAUID, []);
    }

    public void RemoveTrackedEntity(ulong entityAUID) {
        OVERRIDE_LOG_NAME("EntityTracker");

        if (!playersInRangeOfEntity.ContainsKey(entityAUID)) {
            KERR("Tried to remove an entity with AUID {" + entityAUID + "} from being tracked that was not being tracked yet");
            KBREAK();
        }

        playersInRangeOfEntity.Remove(entityAUID);
    }

    public void AddPlayerToEntity(ulong entityAUID, ulong playerAUID) {
        OVERRIDE_LOG_NAME("EntityTracker");

        if (!playersInRangeOfEntity.ContainsKey(entityAUID)) {
            KERR("Tried to add a player with AUID {" + entityAUID + "} to track entity with AUID {" + entityAUID + "} when the entity was not being tracked");
            KBREAK();
        }
        if (playersInRangeOfEntity[entityAUID].Contains(playerAUID)) {
            //KERR("Tried to a player with AUID {" + entityAUID + "} to track entity with AUID {" + entityAUID + "} twice");
            //KBREAK();
        }

        playersInRangeOfEntity[entityAUID].Add(playerAUID);
    }

    public void RemovePlayerFromEntity(ulong entityAUID, ulong playerAUID) {
        OVERRIDE_LOG_NAME("EntityTracker");

        if (!playersInRangeOfEntity.ContainsKey(entityAUID)) {
            KERR("Tried to remove a player with AUID {" + entityAUID + "} from tracking entity with AUID {" + entityAUID + "} when the entity was not being tracked");
            KBREAK();
        }
        if (!playersInRangeOfEntity[entityAUID].Contains(playerAUID)) {
            //KERR("Tried to a player with AUID {" + entityAUID + "} to track entity with AUID {" + entityAUID + "} when that player was not tracking the entity yet");
            //KBREAK();
        }

        playersInRangeOfEntity[entityAUID].Remove(playerAUID);
    }

    public HashSet<ulong> GetPlayersInRangeOfEntity(ulong entityAUID) {
        OVERRIDE_LOG_NAME("EntityTracker");

        if (!playersInRangeOfEntity.ContainsKey(entityAUID)) {
            KERR("Tried to query the players tracking entity with AUID {" + entityAUID + "} that did not exist");
            KBREAK();
        }

        return playersInRangeOfEntity[entityAUID];
    }

    public bool IsEntityTrackedByPlayer(ulong entityAUID, ulong playerAUID) {
        OVERRIDE_LOG_NAME("EntityTracker");

        if (!playersInRangeOfEntity.ContainsKey(entityAUID)) {
            KERR("Tried to query the players tracking entity with AUID {" + entityAUID + "} that did not exist");
            KBREAK();
        }

        return playersInRangeOfEntity[entityAUID].Contains(playerAUID);
    }
}
