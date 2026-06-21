namespace KiwiCubed.Engine;

using KiwiCubed.Api;

using static KiwiCubed.Api.KLogger;

public class SystemsManager {
    private readonly Dictionary<Type, object> services = [];

    public void Register<T>(T service) where T: class {
		OVERRIDE_LOG_NAME("SystemsManager");

		Type type = typeof(T);
        if (services.ContainsKey(type)) {
            KERR("Tried to register the same service type \"" + type + "\" twice");
            return;
        }

        services.Add(type, service);

        KINFO("Successfully registered service with type \"" + type + "\"");
    }

    public void Deregister<T>() where T : class {
		OVERRIDE_LOG_NAME("SystemsManager");

		Type type = typeof(T);
        if (services.Remove(type)) {
            KINFO("Successfully deregistered service of type \"" + type + "\"");
        } else {
            KERR("Tried to remove service of type \"" + type + "\" that wasn't registered");
            KBREAK();
        }
    }

    public T Get<T>() where T: class {
        OVERRIDE_LOG_NAME("SystemsManager");

        Type type = typeof(T);
        if (services.TryGetValue(type, out Object service)) {
            return (T)service;
        }

        KERR("Tried to get service of type \"" +  type + "\" that didn't exist");
        KBREAK();
        return default;
    }
}