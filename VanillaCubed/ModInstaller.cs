namespace VanillaCubed;

using KiwiCubed.Api;
using NativeFileDialogSharp;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

using static KiwiCubed.Api.Globals;

public class ModInstaller {
    private ILogger logger;
    private string[] acceptedModFileExtensions = [".zip", ".kcm"];


    public ModInstaller() {
        NativeLibrary.SetDllImportResolver(typeof(Dialog).Assembly, NfdImportResolver);

        logger = ILogger.CreateLogger("ModInstaller");
    }

    private IntPtr NfdImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath) {
        if (libraryName == "nfd") {
            string modDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            string nativeDllPath = Path.Combine(modDirectory, "runtimes", "win-x64", "native", "nfd.dll");

            if (File.Exists(nativeDllPath)) {
                return NativeLibrary.Load(nativeDllPath);
            }
        }

        return IntPtr.Zero;
    }

    public IReadOnlyList<string>? SelectZippedMods() {
        DialogResult result = Dialog.FileOpenMultiple("zip,kcm", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

        return result.Paths;
    }

    public bool InstallZippedMods(IReadOnlyList<string> modFiles) {
        string installPath = Path.Combine(topSaveFolder, "Mods");

        foreach (string modFile in modFiles) {
            if (!File.Exists(modFile)) {
                logger.ERR("Mod file at path \"" + modFile + "\" does not exist");
            }

            if (!acceptedModFileExtensions.Contains(Path.GetExtension(modFile))) {
                logger.ERR("Mod file at path \"" + modFile + "\" does not have a valid file extension");
            }

            try {
                ZipFile.ExtractToDirectory(modFile, installPath, true);
            } catch (Exception exception) {
                logger.ERR("Mod install of file \"" + modFile + "\" failed with error: " + exception.Message);
            }
        }

        return true;
    }
}