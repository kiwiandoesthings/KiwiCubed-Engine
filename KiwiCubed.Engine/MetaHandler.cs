namespace KiwiCubed.Engine;

using KiwiCubed.Api;

public class MetaHandler : IMetaHandler {
    private VirtualWindow globalWindow = (VirtualWindow)SystemsManager.Get<IVirtualWindow>();

    public MetaHandler() {
        SystemsManager.Register<IMetaHandler>(this);
    }

    public void CloseGame() {
        globalWindow.GetWindow().Close();
    }
}