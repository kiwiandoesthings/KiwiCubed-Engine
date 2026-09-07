namespace KiwiCubed.Api;

using ArchEntity = Arch.Core.Entity;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.IInventory;
using static KiwiCubed.Api.IPlayer;
using static KiwiCubed.Api.Utils;

public delegate void EventCallback<T>(T data) where T : struct;

public interface IEventManager {
	public void RegisterEvent(Type eventType);
	public void DeregisterEvent(Type eventType);
	public Action SubscribeToEvent<T>(EventCallback<T> callback) where T : struct;
	public void TriggerEvent<T>(T eventData) where T : struct;
}

public readonly struct WorldLoadEvent {
	public readonly IWorld world;

	public WorldLoadEvent(IWorld world) {
		this.world = world;
	}
}
public readonly struct WorldExitEvent { }
public readonly struct WorldTickEvent {
	public readonly ulong totalTicks;

	public WorldTickEvent(ulong totalTicks) {
		this.totalTicks = totalTicks;
	}
}

public readonly struct PlayerBlockInteractionEvent {
	public readonly BlockEventType interactionType;
	public readonly ArchEntity player;
	public readonly FullBlockPosition blockPosition;
	public readonly AssetStringID blockStringID;

	public PlayerBlockInteractionEvent(BlockEventType interactionType, ArchEntity player, FullBlockPosition blockPosition, AssetStringID blockStringID) {
		this.interactionType = interactionType;
		this.player = player;
		this.blockPosition = blockPosition;
		this.blockStringID = blockStringID;
	}
}

public readonly struct EntityBlockInteractionEvent {
	public readonly BlockInteractionType interactionType;
	public readonly ArchEntity entity;
	public readonly FullBlockPosition blockPosition;
	public readonly AssetStringID blockStringID;

	public EntityBlockInteractionEvent(BlockInteractionType interactionType, ArchEntity entity, FullBlockPosition blockPosition, AssetStringID blockStringID) {
		this.interactionType = interactionType;
		this.entity = entity;
		this.blockPosition = blockPosition;
		this.blockStringID = blockStringID;
	}
}

public readonly struct ClientInventoryChangeEvent {
	public readonly ValueTuple<ItemStack, ItemStack>[] inventoryDeltas;

	public ClientInventoryChangeEvent(ValueTuple<ItemStack, ItemStack>[] inventoryDeltas) {
		this.inventoryDeltas = inventoryDeltas;
	}
}

public enum BlockEventType : byte {
	BLOCK_MINED,
	BLOCK_PLACED,
	BLOCK_REPLACED,
	BLOCK_INTERACTED
}