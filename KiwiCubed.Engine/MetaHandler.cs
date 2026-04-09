namespace KiwiCubed.Engine;

using KiwiCubed.Api;

public class MetaHandlerWrapper : IMetaHandler {
    public void Register<T>(T service) where T : class => MetaHandler.Register(service);
    public void Deregister<T>() where T : class => MetaHandler.Deregister<T>();
    public T GetT<T>() where T : class => MetaHandler.Get<T>();
    public GameType GetGameType() => MetaHandler.GetGameType();
}

public static class MetaHandler {
    private static readonly ThreadLocal<SystemsManager> threadSystems = new ThreadLocal<SystemsManager>(() => new SystemsManager());
    private static readonly ThreadLocal<GameType> threadType = new ThreadLocal<GameType>(() => new GameType());

    public static void Register<T>(T service) where T : class {
        threadSystems.Value.Register(service);
    }

    public static void Deregister<T>() where T : class {
        threadSystems.Value.Deregister<T>();
    }

    public static T Get<T>() where T : class {
        return threadSystems.Value.Get<T>();
    }

    public static void SetGameType(GameType type) {
        threadType.Value = type;
    }

    public static GameType GetGameType() {
        return threadType.Value;
    }

    //private static VirtualWindow globalWindow = (VirtualWindow)MetaHandler.Get<IVirtualWindow>();
    //
    //public static void CloseGame() {
    //    globalWindow.GetWindow().Close();
    //}
}