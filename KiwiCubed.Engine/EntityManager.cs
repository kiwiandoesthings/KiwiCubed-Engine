namespace KiwiCubed.Engine;

using ArchWorld = Arch.Core.World;
using ArchEntity = Arch.Core.Entity;
using KiwiCubed.Api;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.KLogger;
using Arch.Core;

public class EntityManager : IEntityManager {
	private IAssetManager assetManager;

	private ArchWorld worldEntities;

	public EntityManager() {
		assetManager = Systems.Get<IAssetManager>();

		worldEntities = ArchWorld.Create();
	}

	public ArchEntity SpawnEntity(EntityType entityType, Vector3 position = default, Vector3 orientation = default) {
		ArchEntity entity = worldEntities.Create(entityType.components, typeof(EntityTransform));
		entityType.setupFunction(worldEntities, entity);

		return entity;
	}

	//public Entity GetEntity(ulong entityAUID) {
	//	return entities.Get((int)entityAUID);
	//}

	public List<ArchEntity> GetEntities() {
		return worldEntities.GetEntities();
	}

	public List<Entity> GetEntitiesOfType(AssetStringID entityType) {
		if (entityTypesToEntities.TryGetValue(entityType, out List<Entity> entitiesOfType)) {
			return entitiesOfType;
		}
		return new List<Entity>();
	}

	public void ForEachEntity(Action<Entity> action) {
		entities.ForEach(action);
	}

	public void ForEachEntityOfType(Action<Entity> action, AssetStringID entityType) {
		GetEntitiesOfType(entityType).ForEach(action);
	}
}
