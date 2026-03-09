namespace KiwiCubed.Api;

using Silk.NET.OpenGL;

public class Globals {
    public static string engineVersion = "0.06pre-alpha";
    public static int glVersionMajor = 4;
    public static int glVersionMinor = 3;

    public static int chunkSize = 32;
    public static int chunkArea = chunkSize * chunkSize;
    public static int chunkVolume = chunkSize * chunkSize * chunkSize;
}