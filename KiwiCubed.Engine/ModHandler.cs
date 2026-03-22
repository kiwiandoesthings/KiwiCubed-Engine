namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using StbImageSharp;
using System.Collections.Frozen;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.KLogger;

public class ModHandler {
	private JsonSerializerOptions options;
	
	private List<ValueTuple<string, string>> validModFolders;
	private List<IMod> loadedMods;

	private AssetManager assetManager;
	private AtlasBuilder atlasBuilder;
	private Dictionary<AssetStringID, ImageResult> textureDatas;

	public ModHandler() {
		OVERRIDE_LOG_NAME("Mod Handler");

		KINFO("Loading mod assets...");
		Stopwatch stopwatch = Stopwatch.StartNew();

		validModFolders = new();
		loadedMods = new();

		assetManager = (AssetManager)SystemsManager.Get<IAssetManager>();
		atlasBuilder = new AtlasBuilder();
		textureDatas = new();

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
			string[] modMetadatas = Directory.GetFiles(modFolder, "mod.json", SearchOption.TopDirectoryOnly);
			if (modMetadatas.Length != 1) {
				if (modMetadatas.Length == 0) {
					KERR("Could not find \"mod.json\" file in folder \"" + modFolder + "\", skipping");
					continue;
				} else {
					KERR("Why the fuck are there " + modMetadatas.Length + " mod.jsons in " + modFolder + " broski");
					continue;
				}
			}

			ModMetadataJSON modMetadata = PathReadJSON<ModMetadataJSON>(modMetadatas[0]);
			string modNamespace = modMetadata.modNamespace;
			validModFolders.Add(new ValueTuple<string, string>(modNamespace, modFolder));
			KINFO("Mod detected with title \"" + modMetadata.title + "\" and version \"" + modMetadata.version + "\" using namespace \"" + modNamespace + "\"");

			string[] resourceFolders = Directory.GetDirectories(Path.Combine(modFolder, "Resources"));
			foreach (string resourceFolder in resourceFolders) {
				if (Path.GetFileName(resourceFolder) == "Textures") {
					string[] textureFiles = Directory.GetFiles(resourceFolder, "*.png", SearchOption.AllDirectories);
					foreach (string texture in textureFiles) {
						ImageResult textureData = Texture.GetRawTexture(texture);
						AssetStringID textureStringID = new AssetStringID(modNamespace, "texture/" + Path.GetFileName(texture));
						textureDatas.Add(textureStringID, textureData);
						atlasBuilder.AddTexture(textureData.Width, textureData.Height, textureStringID);
					}
				}
			}

			FrozenDictionary<AssetStringID, TextureAtlasData> atlasDatas = atlasBuilder.PackTextures();
			List<ValueTuple<TextureAtlasData, ImageResult>> textures = new();
			foreach (KeyValuePair<AssetStringID, TextureAtlasData> atlasData in atlasDatas) {
				AssetStringID textureStringID = new AssetStringID(atlasData.Key.modName, Path.GetFileNameWithoutExtension(atlasData.Key.assetName));
				assetManager.RegisterTextureAtlasData(textureStringID.Prefix("texture"), atlasData.Value);
				textureDatas.TryGetValue(atlasData.Key, out ImageResult textureData);
				textures.Add(new ValueTuple<TextureAtlasData, ImageResult>(atlasData.Value, textureData));
			}
			Texture gameAtlas = atlasBuilder.CreateAtlas(textures);
			assetManager.RegisterTextureAtlas(new AssetStringID("kiwicubed", "atlas/main"), gameAtlas);
		}

		KINFO("Took " + stopwatch.Elapsed.TotalMilliseconds + "ms to load mod assets");
		KINFO("Successfully loaded mod assets");
	}

	public bool LoadMods() {
		OVERRIDE_LOG_NAME("Mod Handler");

		KINFO("Initializing mods...");
		Stopwatch stopwatch = Stopwatch.StartNew();

		bool success = true;
		foreach (ValueTuple<string, string> modFolder in validModFolders) {
			string scriptFolder = Path.Combine(modFolder.Item2, "Scripts");
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

					if (mod.Initialize()) {
						KINFO("Successfully initialized mod with namespace \"" + modFolder.Item1 + "\"");
					} else {
						KINFO("Failed to initialize mod with namespace \"" + modFolder.Item1 + "\"");
						success = false;
					}
				}
			}
		}

		KINFO("Took " + stopwatch.Elapsed.TotalMilliseconds + "ms to initialize mods");
		KINFO((success ? "Successfully" : "Failed to") + " initialize mods");

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
