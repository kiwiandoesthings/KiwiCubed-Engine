namespace KiwiCubed.Api;

public static class Systems {
	private static ISystemsManager systemsManager;

	public static void Initialize(ISystemsManager implementation) => systemsManager = implementation;

	public static void Register<T>(T service) where T : class => systemsManager.Register<T>(service);
	public static void Deregister<T>() where T : class => systemsManager.Deregister<T>();
	public static T Get<T>() where T: class => systemsManager.Get<T>();
}

public interface ISystemsManager {
	public void Register<T>(T service)where T : class;
	public void Deregister<T>() where T : class;
	public T Get<T>() where T: class;
}
