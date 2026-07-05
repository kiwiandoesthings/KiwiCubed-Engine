namespace KiwiCubed.Engine;

using KiwiCubed.Api;

public class SystemsManager {
    private readonly KLogger logger = new KLogger("SystemsManager");
    private readonly Dictionary<Type, object> services = [];

    public void Register<T>(T service) where T: class {
		Type type = typeof(T);
        if (services.ContainsKey(type)) {
            logger.ERR("Tried to register the same service type \"" + type + "\" twice");
            return;
        }

        services.Add(type, service);

        logger.INFO("Successfully registered service with type \"" + type + "\"");
    }

    public void Deregister<T>() where T : class {
		Type type = typeof(T);
        if (services.Remove(type)) {
            logger.INFO("Successfully deregistered service of type \"" + type + "\"");
        } else {
            logger.ERR("Tried to remove service of type \"" + type + "\" that wasn't registered");
            logger.BREAK();
        }
    }

    public T Get<T>() where T: class {
        Type type = typeof(T);
        if (services.TryGetValue(type, out object service)) {
            return (T)service;
        }

        logger.ERR("Tried to get service of type \"" +  type + "\" that didn't exist");
        logger.BREAK();
        return default;
    }
}