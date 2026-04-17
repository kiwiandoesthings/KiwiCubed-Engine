namespace KiwiCubed.Engine;

using KiwiCubed.Api;

using static KiwiCubed.Api.KLogger;

public class EventManager : IEventManager {
	private Dictionary<Type, List<object>> eventsToCallbacks;

	public EventManager() {
		eventsToCallbacks = new();

		MetaHandler.Register<IEventManager>(this);
	}

	public void RegisterEvent(Type eventType) {
        OVERRIDE_LOG_NAME("EventManager");

        if (eventsToCallbacks.ContainsKey(eventType)) {
			KERR("Tried to register an event with type \"" + eventType + "\" twice");
			return;
		}

		KINFO("Successfully registered event with type \"" + eventType + "\"");
		eventsToCallbacks.Add(eventType, new List<object>());
	}

	public void DeregisterEvent(Type eventType) {
		OVERRIDE_LOG_NAME("EventManager");

		if (eventsToCallbacks.ContainsKey(eventType)) {
			KINFO("Deregistered event with type \"" + eventType + "\" with " + eventsToCallbacks[eventType].Count + " different subscribers");
			eventsToCallbacks.Remove(eventType);
		} else {
			KERR("Tried to deregister event with type \"" + eventType + "\" that wasn't registered");
		}
	}

	public void SubscribeToEvent<T>(EventCallback<T> callback) where T : struct {
        OVERRIDE_LOG_NAME("EventManager");

        Type eventType = typeof(T);

		if (eventsToCallbacks.ContainsKey(eventType)) {
			eventsToCallbacks[eventType].Add(callback);
		} else {
			KERR("Tried to subscribe to an event with type \"" + eventType + "\" that didn't exist");
		}
	}

	public void TriggerEvent<T>(T eventData) where T : struct {
        OVERRIDE_LOG_NAME("EventManager");

        Type eventType = typeof(T);
		if (eventsToCallbacks.ContainsKey(eventType)) {
			foreach (object callback in eventsToCallbacks[eventType]) {
				((EventCallback<T>)callback)(eventData);
			}
		} else {
			KERR("Tried to trigger event with type \"" + eventType + "\" that didn't exist");
		}
	}
}