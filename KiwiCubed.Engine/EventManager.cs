namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using System.Buffers;

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

    // Returns an action to desubscribe the subscribed callback
    public Action SubscribeToEvent<T>(EventCallback<T> callback) where T : struct {
        OVERRIDE_LOG_NAME("EventManager");

        Type eventType = typeof(T);

		if (eventsToCallbacks.ContainsKey(eventType)) {
			eventsToCallbacks[eventType].Add(callback);
		} else {
			KERR("Tried to subscribe to an event with type \"" + eventType + "\" that didn't exist");
			KBREAK();
		}

		return () => {
			eventsToCallbacks[eventType].Remove(callback);
		};
	}

    public void SubscribeForNEvents<T>(int numberOfEvents, EventCallback<T> callback, Action? onUnsubscribe = null) where T : struct {
        if (numberOfEvents <= 0) {
			onUnsubscribe?.Invoke();
        }

        int totalEventTriggers = 0;
		Action unsubscribe = null;
        unsubscribe = SubscribeToEvent<T>((T eventData) => {
			callback(eventData);
			totalEventTriggers++;
			if (totalEventTriggers == numberOfEvents) {
				unsubscribe();
                onUnsubscribe?.Invoke();
			}
		});
    }

    public void TriggerEvent<T>(T eventData) where T : struct {
        OVERRIDE_LOG_NAME("EventManager");

        Type eventType = typeof(T);
        if (eventsToCallbacks.TryGetValue(eventType, out List<object>? callbacks)) {
            object[] buffer = ArrayPool<object>.Shared.Rent(callbacks.Count);
            callbacks.CopyTo(buffer, 0);

            for (int i = 0; i < callbacks.Count; i++) {
                ((EventCallback<T>)buffer[i])(eventData);
            }

            ArrayPool<object>.Shared.Return(buffer, true);
        } else {
            KERR("Tried to trigger event with type \"" + eventType + "\" that didn't exist");
        }
    }
}