namespace KiwiCubed.Engine;

using ArchWorld = Arch.Core.World;
using ArchEntity = Arch.Core.Entity;
using KiwiCubed.Api;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.KLogger;
using Arch.Core;

public class EntityManager : IEntityManager, IDisposable {
	private ArchWorld worldEntities;
	private Dictionary<AssetStringID, List<ArchEntity>> entitiesByType;

	public EntityManager() {
		worldEntities = ArchWorld.Create();
		entitiesByType = new();
	}

	public ArchEntity SpawnEntity(EntityType entityType, Vector3 position = default, Vector3 orientation = default) {
		ComponentType[] components = new ComponentType[entityType.components.Length + 1];
		entityType.components.CopyTo(components, 0);
		components[^1] = typeof(EntityTransform);

		ArchEntity entity = worldEntities.Create(components);
		entityType.setupFunction(worldEntities, entity);
		if (entitiesByType.TryGetValue(entityType.stringID, out List<ArchEntity> entitiesOfType)) {
			entitiesOfType.Add(entity);
		} else {
			entitiesByType[entityType.stringID] = new List<ArchEntity>() { entity };
		}

		worldEntities.Set<EntityTransform>(entity, new EntityTransform(position, orientation));

		return entity;
	}

	public void ForEachEntity(Action<ArchEntity> action) {
		QueryDescription query = new QueryDescription();
		worldEntities.Query(in query, (entity) => action(entity));
	}

	public List<ArchEntity> GetEntitiesOfType(AssetStringID entityTypeStringID) {
		if (entitiesByType.TryGetValue(entityTypeStringID, out List<ArchEntity> entitiesOfType)) {
			return entitiesOfType;
		}
		return new List<ArchEntity>();
	}

	public ArchWorld GetArchWorld() {
		return worldEntities;
	}

	public void Dispose() {
		// TODO: Fill out
	}
}
