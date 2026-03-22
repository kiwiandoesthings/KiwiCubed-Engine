namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.KLogger;

public class EntityManager : IEntityManager {
	private IAssetManager assetManager;

	private SivVector<Entity> entities;
	private Dictionary<AssetStringID, List<Entity>> entityTypesToEntities;


	public EntityManager() {
		assetManager = Systems.Get<IAssetManager>();
		
		entities = new SivVector<Entity>();
		entityTypesToEntities = new();

		SystemsManager.Register<EntityManager>(this);
	}

	public ulong SpawnEntity(AssetStringID entityType, Vector3 position = default, Vector3 orientation = default) {
		Type defaultEntity = assetManager.GetEntityType(entityType);

		int entityAUID = entities.GetNextId();
		object[] constructorArguments = { entityAUID, position, orientation };
		Entity entity = (Entity)Activator.CreateInstance(defaultEntity, constructorArguments);

		List<AssetStringID> inventorySlots = entity.GetInventorySlotIDs();
		entity.Setup(new Inventory(inventorySlots));
		entities.Add(entity);

		return (ulong)entityAUID;
	}

	public Entity GetEntity(ulong entityAUID) {
		return entities.Get((int)entityAUID);
	}

	public List<Entity> GetEntities() {
		List<Entity> allEntities = new();
		foreach (KeyValuePair<AssetStringID, List<Entity>> entitiesPair in entityTypesToEntities) {
			allEntities.AddRange(entitiesPair.Value);
		}
		return allEntities;
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
