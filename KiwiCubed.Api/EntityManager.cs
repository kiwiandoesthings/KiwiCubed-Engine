namespace KiwiCubed.Api;

using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;

public interface IEntityManager {
	public ArchEntity SpawnEntity(EntityType entityType, Vector3 position = default, Quaternion orientation = default);
	public void ForEachEntity(Action<ArchEntity> action);
	public List<ArchEntity> GetEntitiesOfType(AssetStringID entityTypeStringID);
	public ArchWorld GetArchWorld();
}