namespace KiwiCubed.Api;

using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;

public interface IEntityManager {
	public ulong SpawnEntity(AssetStringID entityType, Vector3 position = default, Vector3 orientation = default);
	public Entity GetEntity(ulong entityAUID);
}