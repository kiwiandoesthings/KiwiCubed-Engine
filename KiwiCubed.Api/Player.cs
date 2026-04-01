namespace KiwiCubed.Api;

using static KiwiCubed.Api.AssetDefinitions;

public interface IPlayer {
    public float FOV { get; set; }

    public ref EntityStats GetEntityStats();
    public ref EntityData GetEntityData();
    public ProtectedEntityData GetProtectedEntityData();
    public ref EntityTransform GetEntityTransform();
    public void SetEntityData(EntityData entityData);
    public void SetEntityTransform(EntityTransform entityTransform);
    public List<AssetStringID> GetInventorySlotIDs();
}