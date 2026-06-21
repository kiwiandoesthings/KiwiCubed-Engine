namespace KiwiCubed.Api;

using ArchEntity = Arch.Core.Entity;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.IPlayer;
using static KiwiCubed.Api.Utils;

public delegate void EventCallback<T>(T data) where T : struct;

public interface IEventManager {
	public void RegisterEvent(Type eventType);
	public void DeregisterEvent(Type eventType);
	public Action SubscribeToEvent<T>(EventCallback<T> callback) where T : struct;
	public void TriggerEvent<T>(T eventData) where T : struct;
}

public struct WorldLoadEvent {
	public IWorld world;

	public WorldLoadEvent(IWorld world) {
		this.world = world;
	}
}
public struct WorldExitEvent { }
public struct WorldTickEvent {
	public ulong totalTicks;

	public WorldTickEvent(ulong totalTicks) {
		this.totalTicks = totalTicks;
	}
}

public struct PlayerBlockInteractionEvent {
	public BlockEventType interactionType;
	public ArchEntity player;
	public FullBlockPosition blockPosition;
	public AssetStringID blockStringID;

	public PlayerBlockInteractionEvent(BlockEventType interactionType, ArchEntity player, FullBlockPosition blockPosition, AssetStringID blockStringID) {
		this.interactionType = interactionType;
		this.player = player;
		this.blockPosition = blockPosition;
		this.blockStringID = blockStringID;
	}
}

public struct EntityBlockInteractionEvent {
	public BlockInteractionType interactionType;
	public ArchEntity entity;
	public FullBlockPosition blockPosition;
	public AssetStringID blockStringID;

	public EntityBlockInteractionEvent(BlockInteractionType interactionType, ArchEntity entity, FullBlockPosition blockPosition, AssetStringID blockStringID) {
		this.interactionType = interactionType;
		this.entity = entity;
		this.blockPosition = blockPosition;
		this.blockStringID = blockStringID;
	}
}

public enum BlockEventType : byte {
	BLOCK_MINED,
	BLOCK_PLACED,
	BLOCK_REPLACED,
	BLOCK_INTERACTED
}