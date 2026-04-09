namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using StbImageSharp;
using System.Collections.Frozen;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Globals;
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

		assetManager = (AssetManager)MetaHandler.Get<IAssetManager>();
		atlasBuilder = new AtlasBuilder();
		textureDatas = new();

		string modsPath = Path.Combine(topSaveFolder, "Mods");

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
			if (modMetadata.builtForEngineVersion != engineVersion) {
				KERR("Found mod with incompatible version, built for engine version \"" + modMetadata.builtForEngineVersion + "\" when current version is \"" + engineVersion + "\"");
				continue;
			}
			validModFolders.Add(new ValueTuple<string, string>(modNamespace, modFolder));
			KINFO("Mod detected with title \"" + modMetadata.title + "\" and version \"" + modMetadata.version + "\" using namespace \"" + modNamespace + "\"");
		}

		KINFO("Took " + stopwatch.Elapsed.TotalMilliseconds + "ms to load mod assets");
		KINFO("Successfully loaded mod assets");
	}

	public bool LoadModAssets() {
		OVERRIDE_LOG_NAME("Mod Handler");

		KINFO("Loading mod assets into AssetManager...");
		foreach (ValueTuple<string, string> modMetadata in validModFolders) {
			string modNamespace = modMetadata.Item1;
			string modFolder = modMetadata.Item2;
			string[] resourceFolders = Directory.GetDirectories(Path.Combine(modFolder, "Resources"));
			foreach (string resourceFolder in resourceFolders) {
				if (Path.GetFileName(resourceFolder) == "Textures") {
					string[] textureFiles = Directory.GetFiles(resourceFolder, "*.png", SearchOption.AllDirectories);
					foreach (string textureFile in textureFiles) {
						ImageResult textureData = Texture.GetRawTexture(textureFile);
						AssetStringID textureStringID = new AssetStringID(modNamespace, "texture/" + Path.GetFileName(textureFile));
						textureDatas.Add(textureStringID, textureData);
						atlasBuilder.AddTexture(textureData.Width, textureData.Height, textureStringID);
					}
				} else if (Path.GetFileName(resourceFolder) == "Models") {
					string[] modelFiles = Directory.GetFiles(resourceFolder, "*.json", SearchOption.AllDirectories);
					foreach (string modelFile in modelFiles) {
						ModelJSON model = PathReadJSON<ModelJSON>(modelFile);
						List<float> vertices = new();
						foreach (float[] subVertices in model.vertices) {
							vertices.AddRange(subVertices);
						}
						GeneralMesh mesh = new GeneralMesh(vertices, new List<ushort>(model.indices), model.is3D);
						AssetStringID modelStringID = new AssetStringID(modNamespace, "model/" + Path.GetFileNameWithoutExtension(modelFile));
						assetManager.RegisterMesh(modelStringID, mesh);
					}
				} else if (Path.GetFileName(resourceFolder) == "Shaders") {
					string[] vertexFiles = Directory.GetFiles(resourceFolder, "*_Vertex.vert", SearchOption.TopDirectoryOnly).Order().ToArray();
					string[] fragmentFiles = Directory.GetFiles(resourceFolder, "*_Fragment.frag", SearchOption.TopDirectoryOnly).Order().ToArray();

					if (vertexFiles.Length != fragmentFiles.Length) {
						KERR("Found mismatched amount of vertex shaders to fragment shaders, with {" + vertexFiles.Length + "} vertex and {" + fragmentFiles.Length + "} fragment");
						KBREAK();
					}

					for (int iterator = 0; iterator < vertexFiles.Length; iterator++) {
						Shader shader = new Shader(vertexFiles[iterator], fragmentFiles[iterator]);
						string shaderName = Path.GetFileNameWithoutExtension(vertexFiles[iterator]).ToLower();
						shaderName = shaderName.Substring(0, shaderName.IndexOf("_"));
						AssetStringID shaderStringID = new AssetStringID(modNamespace, "shader/" + shaderName);
						assetManager.RegisterShader(shaderStringID, shader);
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

		return true;
	}

	public bool LoadModScripts() {
		OVERRIDE_LOG_NAME("Mod Handler");

		KINFO("Loading mod scripts...");

		KINFO("Setting up mod callbacks...");
		EventManager eventManager = (EventManager)MetaHandler.Get<IEventManager>();
		eventManager.RegisterEvent(typeof(WorldLoadEvent));
		eventManager.RegisterEvent(typeof(WorldExitEvent));
		eventManager.RegisterEvent(typeof(WorldTickEvent));
		eventManager.RegisterEvent(typeof(PlayerBlockInteractionEvent));
		eventManager.RegisterEvent(typeof(EntityBlockInteractionEvent));

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

					bool indivisualSuccess = false;
					GameType gameType = MetaHandler.GetGameType();
                    if (gameType == GameType.SERVER) {
						indivisualSuccess = mod.InitializeServer();
					} else {
						indivisualSuccess = mod.InitializeClient();
					}

					if (indivisualSuccess) {
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

		return success;
	}

	public void UnloadMods() {
		OVERRIDE_LOG_NAME("Mod Handler");
		
		KINFO("Unloading mods...");
		Stopwatch stopwatch = Stopwatch.StartNew();
		
		foreach (IMod mod in loadedMods) {
			if (isServerOrClient) {
				mod.UnloadServer();
			} else {
				mod.UnloadClient();
			}
		}
		loadedMods.Clear();
		
		KINFO("Took " + stopwatch.Elapsed.TotalMilliseconds + "ms to unload mods");
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

	private struct ModelJSON {
		public List<float[]> vertices;
		public ushort[] indices;
		[JsonPropertyName("is3D")]
		public bool is3D;
	}
}
