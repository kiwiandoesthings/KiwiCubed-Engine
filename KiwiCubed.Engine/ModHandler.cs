namespace KiwiCubed;

using KiwiCubed.Api;
using System.IO;
using System.Reflection;
using System.Text.Json;

using static KiwiCubed.Api.KLogger;

public class ModHandler {
	private JsonSerializerOptions options;
	private List<string> validModFolders;
	private List<IMod> loadedMods;

	public ModHandler() {
		OVERRIDE_LOG_NAME("ModHandler");

		validModFolders = new();
		loadedMods = new();

		string modsPath;
		string potentialPath1 = Path.Combine("Mods");
		string potentialPath2 = Path.Combine("..", "..", "..", "Mods");
		if (Directory.Exists(potentialPath1)) {
			modsPath = potentialPath1;
		} else if (Directory.Exists(potentialPath2)) {
			modsPath = potentialPath2;
		} else {
			KERR("Could not find Mods folder in same directory as executable or 3 directories up");
			return;
		}

		options = new JsonSerializerOptions {
			PropertyNameCaseInsensitive = false,
			AllowDuplicateProperties = false,
			AllowTrailingCommas = false,
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
			IncludeFields = true
		};

		string[] modFolders = Directory.GetDirectories(modsPath);
		foreach (string modFolder in modFolders) {
			string[] files = Directory.GetFiles(modFolder, "mod.json", SearchOption.TopDirectoryOnly);
			if (files.Length != 1) {
				if (files.Length == 0) {
					KERR("Could not find \"mod.json\" file in folder \"" + modFolder + "\", skipping");
					continue;
				} else {
					KERR("Why the fuck are there " + files.Length + " mod.jsons in " + modFolder + " broski");
					continue;
				}
			}

			validModFolders.Add(modFolder);
			ModMetadataJSON modMetadata = PathReadJSON<ModMetadataJSON>(files[0]);
			KINFO("Mod detected with title \"" + modMetadata.title + "\" and version \"" + modMetadata.version + "\" using namespace \"" + modMetadata.modNamespace + "\"");
		}
	}

	public bool LoadMods() {
		OVERRIDE_LOG_NAME("Mod Loading");

		foreach (string modFolder in validModFolders) {
			string scriptFolder = Path.Combine(modFolder, "Scripts");
			if (!Directory.Exists(scriptFolder)) {
				continue;
			}
			string[] scriptFiles = Directory.GetFiles(scriptFolder, "*.dll");
			foreach (string scriptFile in scriptFiles) {
				Assembly assembly = Assembly.LoadFrom(scriptFile);

				IEnumerable<Type> modTypes = assembly.GetTypes().Where(type => typeof(IMod).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);
				foreach (Type modType in modTypes) {
					IMod mod = (IMod)Activator.CreateInstance(modType);
					loadedMods.Add(mod);

					mod.Initialize();
					KINFO("Successfully initialized mod");
				}
			}
		}

		return true;
	}

	public T PathReadJSON<T>(string filePath) {
		OVERRIDE_LOG_NAME("ModHandler");
		try {
			string file = File.ReadAllText(filePath);
			T jsonData = JsonSerializer.Deserialize<T>(file, options);
			return jsonData;
		} catch (JsonException exception) {
			KERR("Failed to parse JSON, with error \"" + exception.Message + "\". JSON filepath below:");
			KERR(filePath);
			return default(T);
		}
	}

	public T StringReadJSON<T>(string file) {
		OVERRIDE_LOG_NAME("ModHandler");
		try {
			T jsonData = JsonSerializer.Deserialize<T>(file, options);
			return jsonData;
		} catch (JsonException exception) {
			KERR("Failed to parse JSON, with error \"" + exception.Message + "\". Raw JSON below:");
			KERR(file);
			return default(T);
		}
	}

	private struct ModMetadataJSON {
		public string title;
		public string version;
		public List<string> authors;
		public string description;
		public string modNamespace;
		public string builtForEngineVersion;
	}

	private struct BlockTextureFileJSON {
		public string textureName;
		public List<BlockTextureJSON> textures;
	}

	private struct BlockTextureJSON {
		public string id;
		public int xPosition;
		public int yPosition;
		public int width;
		public int height;
	}
}
