namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using System.Buffers;

public class EventManager : IEventManager {
	private KLogger logger;
	private Dictionary<Type, List<object>> eventsToCallbacks;

	public EventManager() {
		logger = new KLogger("EventManager");
		eventsToCallbacks = [];

		MetaHandler.Register<IEventManager>(this);
	}

	public void RegisterEvent(Type eventType) {
        if (eventsToCallbacks.ContainsKey(eventType)) {
			logger.ERR("Tried to register an event with type \"" + eventType + "\" twice");
			return;
		}

		logger.INFO("Successfully registered event with type \"" + eventType + "\"");
		eventsToCallbacks.Add(eventType, []);
	}

	public void DeregisterEvent(Type eventType) {
		if (eventsToCallbacks.TryGetValue(eventType, out List<object>? callbacks)) {
            logger.INFO("Deregistered event with type \"" + eventType + "\" with " + eventsToCallbacks[eventType].Count + " different subscribers");
            eventsToCallbacks.Remove(eventType);
        } else {
			logger.ERR("Tried to deregister event with type \"" + eventType + "\" that wasn't registered");
		}
	}

    // Returns an action to desubscribe the subscribed callback
    public Action SubscribeToEvent<T>(EventCallback<T> callback) where T : struct {
        Type eventType = typeof(T);

        if (eventsToCallbacks.TryGetValue(eventType, out List<object>? callbacks)) {
            callbacks.Add(callback);
		} else {
			logger.ERR("Tried to subscribe to an event with type \"" + eventType + "\" that didn't exist");
			logger.BREAK();
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
        Type eventType = typeof(T);
        if (eventsToCallbacks.TryGetValue(eventType, out List<object>? callbacks)) {
            object[] buffer = ArrayPool<object>.Shared.Rent(callbacks.Count);
            callbacks.CopyTo(buffer, 0);

            for (int i = 0; i < callbacks.Count; i++) {
                ((EventCallback<T>)buffer[i])(eventData);
            }

            ArrayPool<object>.Shared.Return(buffer, true);
        } else {
            logger.ERR("Tried to trigger event with type \"" + eventType + "\" that didn't exist");
        }
    }
}