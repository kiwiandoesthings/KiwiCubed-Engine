namespace KiwiCubed.Api;

public static class Globals {
    // Game Info
    public static string engineVersion = "0.06pre-alpha";
    public static int glVersionMajor = 4;
    public static int glVersionMinor = 3;

    public static int chunkSize = 32;
    public static int chunkArea = chunkSize * chunkSize;
    public static int chunkVolume = chunkSize * chunkSize * chunkSize;

    public static double deltaTime = 0.0f;

    public static bool isDebug = false;
    public static bool forceSquareTextures = false;
    public static bool forcePowerOfTwoTextures = false;

    // System Info
    public static uint bitness = 0;

    // Meta Info
    public static string topSaveFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    public static string playerUsername = "Player";
}