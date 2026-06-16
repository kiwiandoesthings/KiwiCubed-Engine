namespace KiwiCubed.Engine;

using ArchWorld = Arch.Core.World;
using ArchEntity = Arch.Core.Entity;
using Arch.Core;
using KiwiCubed.Api;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.Util;

public class EntityManager : IEntityManager, IDisposable {
	private ArchWorld worldEntities;
	private Dictionary<AssetStringID, List<ArchEntity>> entitiesByType;
	private Dictionary<ulong, ArchEntity> entitiesByAUIDs;
	private EntityTracker entityTracker;

	public EntityManager() {
		worldEntities = ArchWorld.Create();
		entitiesByType = [];
		entitiesByAUIDs = [];
		entityTracker = new EntityTracker();
    }

	public ArchEntity SpawnEntity(EntityType entityType, Vector3 position = default, Quaternion orientation = default) {
		ulong AUID = MakeRandomAUID();
		ArchEntity entity = CreateEntity(AUID, entityType, position, orientation);
		
		if (Meta.GetGameType() == GameType.SERVER) {
			entityTracker.AddTrackedEntity(AUID);
		}
		
		return entity;
	}

	// this is a stupid debug overload that will be removed asap, only because servers dont send clients their AUID yet
    public ArchEntity SpawnEntity(string playerName, EntityType entityType, Vector3 position = default, Quaternion orientation = default) {
        ulong AUID = MakeAUID(playerName);
        ArchEntity entity = CreateEntity(AUID, entityType, position, orientation);

        if (Meta.GetGameType() == GameType.SERVER) {
            entityTracker.AddTrackedEntity(AUID);
        }

        return entity;
    }

    private ArchEntity CreateEntity(ulong AUID, EntityType entityType, Vector3 position = default, Quaternion orientation = default) {
		OVERRIDE_LOG_NAME("EntityManager");

		ComponentType[] components = new ComponentType[entityType.components.Length + 2];
		entityType.components.CopyTo(components, 0);
		components[^1] = typeof(EntityIdentifierComponent);
		components[^2] = typeof(EntityTransformComponent);

		ArchEntity entity = worldEntities.Create(components);
		entityType.setupFunction(worldEntities, entity);
		if (entitiesByType.TryGetValue(entityType.stringID, out List<ArchEntity> entitiesOfType)) {
			entitiesOfType.Add(entity);
		} else {
			entitiesByType[entityType.stringID] = [entity];
		}

		entitiesByAUIDs[AUID] = entity;

		worldEntities.Set<EntityIdentifierComponent>(entity, new EntityIdentifierComponent(AUID, entityType.stringID));
        worldEntities.Set<EntityTransformComponent>(entity, new EntityTransformComponent(position, orientation));

		KINFO("Spawned entity with AUID {" + AUID + "} and type " + entityType.stringID + " at " + position);

		return entity;
	}

	public void KillEntity(ulong entityAUID) {
		if (entitiesByAUIDs.TryGetValue(entityAUID, out ArchEntity entity)) {
			AssetStringID entityTypeStringID = worldEntities.Get<EntityIdentifierComponent>(entity).entityTypeStringID;
            entitiesByAUIDs.Remove(entityAUID);
			entitiesByType[entityTypeStringID].Remove(entity);

			if (Meta.GetGameType() == GameType.SERVER) {
				entityTracker.RemoveTrackedEntity(entityAUID);
			}
		} else {
			KERR("Tried to kill entity with GUID {" + entityAUID + "} that didn't exist");
			KBREAK();
		}
	}

	public void ForEachEntity(Action<ArchEntity> action) {
		QueryDescription query = new QueryDescription();
		worldEntities.Query(in query, (entity) => action(entity));
	}

	public ArchEntity GetEntity(ulong entityGuid) {
		if (entitiesByAUIDs.TryGetValue(entityGuid, out ArchEntity entity)) {
			return entity;
		}

		KERR("Tried to get entity with GUID {" + entityGuid + "} that didn't exist");
		KBREAK();
		return default;
    }

	public List<ArchEntity> GetEntitiesOfType(AssetStringID entityTypeStringID) {
		if (entitiesByType.TryGetValue(entityTypeStringID, out List<ArchEntity> entitiesOfType)) {
			return entitiesOfType;
		}
		return [];
	}

	public ArchWorld GetArchWorld() {
		return worldEntities;
	}

	public EntityTracker GetEntityTracker() {
		return entityTracker;
	}

	public void Dispose() {
		// TODO: Fill out
	}
}
