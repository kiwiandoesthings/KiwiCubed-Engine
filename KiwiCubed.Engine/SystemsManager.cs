namespace KiwiCubed.Engine;

using KiwiCubed.Api;

using static KiwiCubed.Api.KLogger;

public class SystemsWrapper : ISystemsManager {
	public void Register<T>(T service) where T : class => SystemsManager.Register(service);
	public void Deregister<T>() where T : class => SystemsManager.Deregister<T>();
	public T Get<T>() where T: class => SystemsManager.Get<T>();
}

public class SystemsManager {
    private static readonly Dictionary<Type, object> services = new();

    public static void Register<T>(T service) where T: class {
		OVERRIDE_LOG_NAME("Systems Manager");

		Type type = typeof(T);
        if (services.ContainsKey(type)) {
            KERR("Tried to register the same service type \"" + type + "\" twice");
            return;
        }
        if (services.TryAdd(type, service)) {
            services[type] = service;
            KINFO("Successfully registered service with type \"" + type + "\"");
        } else {
            KERR("Failed to register service with type \"" + type + "\"");
        }
    }

    public static void Deregister<T>() where T : class {
		OVERRIDE_LOG_NAME("Systems Manager");

		Type type = typeof(T);
        if (services.Remove(type)) {
            KINFO("Successfully deregistered service of type \"" + type + "\"");
        } else {
            KERR("Tried to remove service of type \"" + type + "\" that wasn't registered");
        }
    }

    public static T Get<T>() where T: class {
        OVERRIDE_LOG_NAME("Systems Manager");

        Type type = typeof(T);
        if (services.TryGetValue(type, out Object service)) {
            return (T)service;
        }

        KERR("Tried to get service of type \"" +  type + "\" that didn't exist");
        return default;
    }
}