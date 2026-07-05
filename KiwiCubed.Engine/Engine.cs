namespace KiwiCubed.Engine;

public abstract class Engine {
    public bool shouldExit;

    public abstract void StartGame();
    public abstract void ExitGame();
}