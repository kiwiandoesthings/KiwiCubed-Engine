namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using Silk.NET.OpenGL;
using StbImageSharp;
using System.Collections.Frozen;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Globals;

public class ModHandler {
	private readonly KLogger logger;
	private readonly JsonSerializerOptions options;
	private readonly List<ValueTuple<string, string>> validModFolders;
	private readonly List<ModMetadataJSON> modMetadatas;
	private readonly List<ModBase> loadedMods;
	private readonly AssetManager assetManager;
	private readonly AtlasBuilder atlasBuilder;
	private readonly Dictionary<AssetStringID, ImageResult> textureDatas;

    private record ModMetadataJSON(string title, string version, string[] authors, string description, string modNamespace, string builtForEngineVersion);
    private record ModelMetadataJSON(string? modelTexture);
	private record BlockModelJSON(string model, Dictionary<string, string> textures);

    public ModHandler() {
		Stopwatch stopwatch = Stopwatch.StartNew();

		logger = new KLogger("ModHandler");

		validModFolders = [];
		modMetadatas = [];
		loadedMods = [];

		assetManager = (AssetManager)MetaHandler.Get<IAssetManager>();
		atlasBuilder = new AtlasBuilder();
		textureDatas = [];

		logger.INFO("Loading mod assets...");

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
			string[] modFiles = Directory.GetFiles(modFolder, "mod.json", SearchOption.TopDirectoryOnly);
			if (modFiles.Length != 1) {
				if (modFiles.Length == 0) {
					logger.ERR("Could not find \"mod.json\" file in folder \"" + modFolder + "\", skipping");
					continue;
				} else {
					logger.ERR("Why the fuck are there " + modFiles.Length + " mod.jsons in " + modFolder + " broski");
					continue;
				}
			}

			ModMetadataJSON modMetadata = PathReadJSON<ModMetadataJSON>(modFiles[0]);
			modMetadatas.Add(modMetadata);
			string modNamespace = modMetadata.modNamespace;
			if (modMetadata.builtForEngineVersion != engineVersion) {
				logger.ERR("Found mod with incompatible version, built for engine version \"" + modMetadata.builtForEngineVersion + "\" when current version is \"" + engineVersion + "\"");
				continue;
			}
			validModFolders.Add(new ValueTuple<string, string>(modNamespace, modFolder));
			logger.INFO("Mod detected with title \"" + modMetadata.title + "\" and version \"" + modMetadata.version + "\" using namespace \"" + modNamespace + "\"");
		}

		logger.INFO("Took " + stopwatch.Elapsed.TotalMilliseconds + "ms to successfully scan available mods");
	}

	public bool LoadModAssets() {
		Stopwatch stopwatch = Stopwatch.StartNew();
		logger.INFO("Discovering mod assets...");

		List<ValueTuple<AssetStringID, string>> pendingEntityModels = [];
		List<ValueTuple<AssetStringID, string>> pendingBlockModels = [];
        List<ValueTuple<AssetStringID, string[], ShaderType[]>> pendingShaders = [];

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
					string[] entityModels = Directory.GetFiles(Path.Combine(resourceFolder, "Entities"), "*.obj");
                    string[] blockModels = Directory.GetFiles(Path.Combine(resourceFolder, "Blocks"), "*.obj");
                    foreach (string modelFile in entityModels) {
						string rawModelName = Path.ChangeExtension(modelFile, null);
                        string metadataFile = rawModelName + ".json";
						if (!File.Exists(metadataFile)) {
							logger.ERR("Tried to load model \"" + rawModelName + "\" without corresponding .json metadata from mod with namespace \"" + modNamespace + "\"");
							logger.BREAK();
						}
                        AssetStringID modelStringID = new AssetStringID(modNamespace, "model/" + Path.GetFileNameWithoutExtension(modelFile));
                        pendingEntityModels.Add(new ValueTuple<AssetStringID, string>(modelStringID, rawModelName));
                    }
					foreach (string modelFile in blockModels) {
						AssetStringID modelStringID = new AssetStringID(modNamespace, "model/" + Path.GetFileNameWithoutExtension(modelFile));
						pendingBlockModels.Add(new ValueTuple<AssetStringID, string>(modelStringID, modelFile));
					}
				} else if (Path.GetFileName(resourceFolder) == "Shaders") {
					IEnumerable<IGrouping<string, string>> groupedShaders = Directory.EnumerateFiles(resourceFolder).GroupBy(Path.GetFileNameWithoutExtension);

                    foreach (IGrouping<string, string> shaderGroup in groupedShaders) {
						AssetStringID shaderGroupStringID = new AssetStringID(modNamespace, "shader/" + shaderGroup.Key.ToLower());
						string[] shaderPaths = [.. shaderGroup];
                        ShaderType[] shaderTypes = new ShaderType[shaderPaths.Length];

						for (int iterator = 0; iterator < shaderPaths.Length; iterator++) {
							string fileExtension = Path.GetExtension(shaderPaths[iterator]);
							
							switch (fileExtension) {
								case ".vert":
                                    shaderTypes[iterator] = ShaderType.VertexShader;
                                    break;
								case ".frag":
                                    shaderTypes[iterator] = ShaderType.FragmentShader;
                                    break;
								case ".geom":
                                    shaderTypes[iterator] = ShaderType.GeometryShader;
                                    break;
								default:
									logger.WARN("Found shader with extension \"" + fileExtension + "\" which is either incorrect or not yet supported from mod with namespace \"" + modNamespace + "\"");
									logger.BREAK();
									break;
                            }
						}
						pendingShaders.Add(new ValueTuple<AssetStringID, string[], ShaderType[]>(shaderGroupStringID, shaderPaths, shaderTypes));
                    }
                }
			}
        }

        logger.INFO("Processing and loading mod assets...");

        FrozenDictionary<AssetStringID, TextureAtlasData> atlasDatas = atlasBuilder.PackTextures();
        List<ValueTuple<TextureAtlasData, ImageResult>> textures = [];
        foreach (KeyValuePair<AssetStringID, TextureAtlasData> atlasData in atlasDatas) {
            AssetStringID textureStringID = new AssetStringID(atlasData.Key.modName, Path.GetFileNameWithoutExtension(atlasData.Key.assetName));
            assetManager.RegisterTextureAtlasData(textureStringID.Prefix("texture"), atlasData.Value);
            textureDatas.TryGetValue(atlasData.Key, out ImageResult textureData);
            textures.Add(new ValueTuple<TextureAtlasData, ImageResult>(atlasData.Value, textureData));
        }
        Texture gameAtlas = atlasBuilder.CreateAtlas(textures);
        assetManager.RegisterTextureAtlas(new AssetStringID("kiwicubed", "atlas/main"), gameAtlas);

        foreach (ValueTuple<AssetStringID, string> modelPair in pendingEntityModels) {
			string modelFilePath = modelPair.Item2 + ".obj";
			string modelMetadataPath = modelPair.Item2 + ".json";
			ModelMetadataJSON metadata = PathReadJSON<ModelMetadataJSON>(modelMetadataPath);
            GeneralMesh<Vertex3D> mesh = ModelParser.ParseModel(modelFilePath);
			if (metadata.modelTexture != null) {
				TextureAtlasData atlasData = assetManager.GetTextureAtlasData(AssetStringID.FromString(metadata.modelTexture));
				mesh.UpdateTextureCoordinates(atlasData);
			}
            assetManager.RegisterMesh(modelPair.Item1, mesh);
        }

		foreach (ValueTuple<AssetStringID, string> modelPair in pendingBlockModels) {
			BlockModelJSON blockModel = PathReadJSON<BlockModelJSON>(modelPair.Item2);
			assetManager.RegisterBlockModel()
		}

        foreach (ValueTuple<AssetStringID, string[], ShaderType[]> shaderTuple in pendingShaders) {
            Shader shader = new Shader(shaderTuple.Item1, shaderTuple.Item2, shaderTuple.Item3);
            assetManager.RegisterShader(shaderTuple.Item1, shader);
        }

        logger.INFO("Took " + stopwatch.ElapsedMilliseconds + "ms to successfully load mod assets");

		return true;
	}

	public void LoadModScripts() {
		logger.INFO("Setting up mod callbacks...");
		EventManager eventManager = (EventManager)MetaHandler.Get<IEventManager>();
		eventManager.RegisterEvent(typeof(WorldLoadEvent));
		eventManager.RegisterEvent(typeof(WorldExitEvent));
		eventManager.RegisterEvent(typeof(WorldTickEvent));
		eventManager.RegisterEvent(typeof(ServerSetInventoryEvent));
		eventManager.RegisterEvent(typeof(ServerChangedInventoryEvent));
		eventManager.RegisterEvent(typeof(PlayerBlockInteractionEvent));
		eventManager.RegisterEvent(typeof(EntityBlockInteractionEvent));

		logger.INFO("Loading and initializing {" + validModFolders.Count + "} mod scripts...");
		Stopwatch stopwatch = Stopwatch.StartNew();

		foreach (ValueTuple<string, string> modFolder in validModFolders) {
			string scriptFolder = Path.Combine(modFolder.Item2, "Scripts");
			if (!Directory.Exists(scriptFolder)) {
				continue;
			}
			string[] scriptFiles = Directory.GetFiles(scriptFolder, "*.dll");
			foreach (string scriptFile in scriptFiles) {
				Assembly assembly = Assembly.LoadFrom(scriptFile);

				IEnumerable<Type> modTypes = assembly.GetTypes().Where(type => typeof(ModBase).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);
				foreach (Type modType in modTypes) {
					ModBase mod = (ModBase)Activator.CreateInstance(modType);
					mod.logger = new KLogger(modFolder.Item1);
					loadedMods.Add(mod);

					bool success = false;
					GameType gameType = MetaHandler.GetGameType();
                    if (gameType == GameType.SERVER) {
						success = mod.InitializeServer();
					} else {
						success = mod.InitializeClient();
					}

					if (success) {
						logger.INFO("Successfully initialized mod with namespace \"" + modFolder.Item1 + "\"");
					} else {
						logger.INFO("Failed to initialize mod with namespace \"" + modFolder.Item1 + "\"");
						if (!disableCrashOnError) { 
							logger.BREAK();
						}
					}
				}
			}
		}

		logger.INFO("Took " + stopwatch.Elapsed.TotalMilliseconds + "ms to initialize mod scripts");
	}

	public void UnloadMods() {
		logger.INFO("Unloading mods...");
		Stopwatch stopwatch = Stopwatch.StartNew();
		
		foreach (ModBase mod in loadedMods) {
			if (MetaHandler.GetGameType() == GameType.SERVER) {
				mod.UnloadServer();
			} else {
				mod.UnloadClient();
			}
		}
		loadedMods.Clear();
		
		logger.INFO("Took " + stopwatch.Elapsed.TotalMilliseconds + "ms to unload mods");
    }

	public T PathReadJSON<T>(string filePath) {
		try {
			string file = File.ReadAllText(filePath);
			T jsonData = JsonSerializer.Deserialize<T>(file, options);
			return jsonData;
		} catch (JsonException exception) {
			logger.ERR("Failed to parse JSON with error \"" + exception.Message + "\". JSON filepath below:");
			logger.ERR(filePath);
			return default;
		} catch (Exception exception) {
			logger.ERR("Failed to parse JSON with error \"" + exception.Message + "\". JSON filepath below:");
			logger.ERR(filePath);
			return default;
		}
	}

	public T StringReadJSON<T>(string file) {
		try {
			T jsonData = JsonSerializer.Deserialize<T>(file, options);
			return jsonData;
		} catch (JsonException exception) {
			logger.ERR("Failed to parse JSON with error \"" + exception.Message + "\". Raw JSON below:");
			logger.ERR(file);
			return default;
		}
	}
}
