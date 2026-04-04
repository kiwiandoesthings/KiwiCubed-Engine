namespace KiwiCubed.Api;

using static KiwiCubed.Api.Util;

public delegate void EventCallback<T>(T data) where T : struct;

public interface IEventManager {
	public void RegisterEvent(Type eventType);
	public void DeregisterEvent(Type eventType);
	public void SubscribeToEvent<T>(EventCallback<T> callback) where T : struct;
	public void TriggerEvent<T>(T eventData) where T : struct;
}

public struct WorldLoadEvent { }
public struct WorldExitEvent { }

public struct PlayerBlockInteractionEvent {
	public BlockInteractionType interactionType;
	public IPlayer player;
	public FullBlockPosition blockPosition;
}
public struct EntityBlockInteractionEvent {
	public BlockInteractionType interactionType;
	public IPlayer player;
	public FullBlockPosition blockPosition;
}

public enum BlockInteractionType : int {
	GENERAL_INTERACTION,
	BLOCK_MINED,
	BLOCK_PLACED,
	BLOCK_REPLACED,
}