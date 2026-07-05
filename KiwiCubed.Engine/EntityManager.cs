namespace KiwiCubed.Engine;

using ArchWorld = Arch.Core.World;
using ArchEntity = Arch.Core.Entity;
using Arch.Core;
using KiwiCubed.Api;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Utils;

public class EntityManager : IEntityManager, IDisposable {
	private KLogger logger;
	private ArchWorld worldEntities;
	private Dictionary<AssetStringID, List<ArchEntity>> entitiesByType;
	private Dictionary<ulong, ArchEntity> entitiesByAUID;
	private PlayerTracker entityTracker;

	public EntityManager() {
		logger = new KLogger("EntityManager");
        worldEntities = ArchWorld.Create();
		entitiesByType = [];
		entitiesByAUID = [];
		entityTracker = new PlayerTracker();
    }

    public ArchEntity SpawnEntity(EntityType entityType, SimpleTransform entityTransform) {
        ulong entityAUID = MakeRandomAUID();

        return SpawnEntity(entityAUID, entityType, entityTransform.position, entityTransform.orientation);
    }

    public ArchEntity SpawnEntity(EntityType entityType, Vector3 entityPosition = default, Quaternion entityOrientation = default) {
		ulong entityAUID = MakeRandomAUID();

		return SpawnEntity(entityAUID, entityType, entityPosition, entityOrientation);
	}

    public ArchEntity SpawnEntity(ulong entityAUID, EntityType entityType, Vector3 entityPosition = default, Quaternion entityOrientation = default) {
		if (entitiesByAUID.ContainsKey(entityAUID)) {
			logger.ERR("Tried to spawn an entity with AUID {" + entityAUID + "} twice");
			logger.BREAK();
		}

        ArchEntity entity = CreateEntity(entityAUID, entityType, entityPosition, entityOrientation);

        if (Meta.GetGameType() == GameType.SERVER) {
            entityTracker.AddTrackedEntity(entityAUID);
        }

        return entity;
    }

    private ArchEntity CreateEntity(ulong AUID, EntityType entityType, Vector3 position = default, Quaternion orientation = default) {
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

		entitiesByAUID[AUID] = entity;

		worldEntities.Set<EntityIdentifierComponent>(entity, new EntityIdentifierComponent(AUID, entityType.stringID));
        worldEntities.Set<EntityTransformComponent>(entity, new EntityTransformComponent(position, orientation));

		logger.INFO("Spawned entity with AUID {" + AUID + "} and type " + entityType.stringID + " at " + position);

		return entity;
	}

	public void KillEntity(ulong entityAUID) {
        if (entitiesByAUID.TryGetValue(entityAUID, out ArchEntity entity)) {
			AssetStringID entityTypeStringID = worldEntities.Get<EntityIdentifierComponent>(entity).entityTypeStringID;
            entitiesByAUID.Remove(entityAUID);
			entitiesByType[entityTypeStringID].Remove(entity);

			if (Meta.GetGameType() == GameType.SERVER) {
				entityTracker.RemoveTrackedEntity(entityAUID);
			}
		} else {
			logger.ERR("Tried to kill entity with GUID {" + entityAUID + "} that didn't exist");
			logger.BREAK();
		}
	}

	public void ForEachEntity(Action<ArchEntity> action) {
		QueryDescription query = new QueryDescription();
		worldEntities.Query(in query, (entity) => action(entity));
	}

	public ArchEntity GetEntity(ulong entityGuid) {
        if (entitiesByAUID.TryGetValue(entityGuid, out ArchEntity entity)) {
			return entity;
		}

		logger.ERR("Tried to get entity with GUID {" + entityGuid + "} that didn't exist");
		logger.BREAK();
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

	public PlayerTracker GetEntityTracker() {
		return entityTracker;
	}

	public void Dispose() {
		worldEntities.Dispose();
		worldEntities = null;
		entitiesByType = null;
		entitiesByAUID = null;
		entityTracker = null;
	}
}
