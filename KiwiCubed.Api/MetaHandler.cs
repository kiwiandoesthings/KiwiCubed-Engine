namespace KiwiCubed.Api;

public static class Meta {
    public static IMetaHandler metaHandler;

    public static void Initialize(IMetaHandler implementation) => metaHandler = implementation;

    public static void Register<T>(T service) where T : class => metaHandler.Register(service);
    public static void Deregister<T>() where T : class => metaHandler.Deregister<T>();
    public static T Get<T>() where T : class => metaHandler.GetT<T>();
    public static GameType GetGameType() => metaHandler.GetGameType();
}

public interface IMetaHandler {
    //public void CloseGame();
    public void Register<T>(T service) where T : class;
    public void Deregister<T>() where T : class;
    public T GetT<T>() where T : class;
    public GameType GetGameType();
}

public enum GameType {
    CLIENT,
    SERVER
}