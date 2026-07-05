namespace KiwiCubed.Engine;

using CommunityToolkit.HighPerformance.Buffers;
using KiwiCubed.Api;
using Silk.NET.OpenGL;
using StbImageSharp;
using System.Collections.Frozen;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Globals;

public class ModHandler {
	private JsonSerializerOptions options;
	
	private List<ValueTuple<string, string>> validModFolders;
	private List<ModMetadataJSON> modMetadatas;
	private List<ModBase> loadedMods;

	private KLogger logger;
	private AssetManager assetManager;
	private AtlasBuilder atlasBuilder;
	private Dictionary<AssetStringID, ImageResult> textureDatas;

	public ModHandler() {
		Stopwatch stopwatch = Stopwatch.StartNew();

		validModFolders = [];
		modMetadatas = [];
		loadedMods = [];

		logger = new KLogger("ModHandler");
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

		logger.INFO("Took " + stopwatch.Elapsed.TotalMilliseconds + "ms to scan available mods");
		logger.INFO("Successfully scanned for available mods");
	}

	public bool LoadModAssets() {
		Stopwatch stopwatch = Stopwatch.StartNew();
		logger.INFO("Discovering mod assets...");

        List<ValueTuple<AssetStringID, string>> pendingJsonModels = [];
        List<ValueTuple<AssetStringID, string>> pendingObjModels = [];
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
					string[] jsonModelFiles = Directory.GetFiles(resourceFolder, "*.json", SearchOption.AllDirectories);
					foreach (string modelFile in jsonModelFiles) {
						AssetStringID modelStringID = new AssetStringID(modNamespace, "model/" + Path.GetFileNameWithoutExtension(modelFile));
						pendingJsonModels.Add(new ValueTuple<AssetStringID, string>(modelStringID, modelFile));
					}
					string[] objModelFiles = Directory.GetFiles(resourceFolder, "*.obj", SearchOption.AllDirectories);
					foreach (string modelFile in objModelFiles) {
                        AssetStringID modelStringID = new AssetStringID(modNamespace, "model/" + Path.GetFileNameWithoutExtension(modelFile));
                        pendingObjModels.Add(new ValueTuple<AssetStringID, string>(modelStringID, modelFile));
                    }
				} else if (Path.GetFileName(resourceFolder) == "Shaders") {
					IEnumerable<IGrouping<string, string>> groupedShaders = Directory.EnumerateFiles(resourceFolder).GroupBy(Path.GetFileNameWithoutExtension);

                    foreach (IGrouping<string, string> shaderGroup in groupedShaders) {
						AssetStringID shaderGroupStringID = new AssetStringID(modNamespace, "shader/" + shaderGroup.Key.ToLower());
						string[] shaderPaths = shaderGroup.ToArray();
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
									logger.WARN("Found shader with extension \"" + fileExtension + "\" which is either incorrect or not yet supported");
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

        foreach (ValueTuple<AssetStringID, string> modelPair in pendingJsonModels) {
            ModelJSON model = PathReadJSON<ModelJSON>(modelPair.Item2);
            List<float> vertices = [];
            foreach (float[] subVertices in model.vertices) {
                vertices.AddRange(subVertices);
            }
            GeneralMesh mesh = new GeneralMesh(vertices, new List<ushort>(model.indices));

            assetManager.RegisterMesh(modelPair.Item1, mesh);
        }

        foreach (ValueTuple<AssetStringID, string> modelPair in pendingObjModels) {
            GeneralMesh mesh = ModelParser.ParseModel(modelPair.Item2);
            TextureAtlasData atlasData = assetManager.GetTextureAtlasData(modelPair.Item1.Prefix("texture"));
            mesh.UpdateTextureCoordinates(atlasData);
            assetManager.RegisterMesh(modelPair.Item1, mesh);
        }

        foreach (ValueTuple<AssetStringID, string[], ShaderType[]> shaderTuple in pendingShaders) {
            Shader shader = new Shader(shaderTuple.Item1, shaderTuple.Item2, shaderTuple.Item3);
            assetManager.RegisterShader(shaderTuple.Item1, shader);
        }

        logger.INFO("Took " + stopwatch.ElapsedMilliseconds + "ms to load mod assets");
		logger.INFO("Successfully loaded mod assets");

		return true;
	}

	public bool LoadModScripts() {
		logger.INFO("Setting up mod callbacks...");
		EventManager eventManager = (EventManager)MetaHandler.Get<IEventManager>();
		eventManager.RegisterEvent(typeof(WorldLoadEvent));
		eventManager.RegisterEvent(typeof(WorldExitEvent));
		eventManager.RegisterEvent(typeof(WorldTickEvent));
		eventManager.RegisterEvent(typeof(PlayerBlockInteractionEvent));
		eventManager.RegisterEvent(typeof(EntityBlockInteractionEvent));

		logger.INFO("Loading and initializing {" + validModFolders.Count + "} mods worth of scripts...");
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

				IEnumerable<Type> modTypes = assembly.GetTypes().Where(type => typeof(ModBase).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);
				foreach (Type modType in modTypes) {
					ModBase mod = (ModBase)Activator.CreateInstance(modType);
					mod.logger = new KLogger(modFolder.Item1);
					loadedMods.Add(mod);

					bool indivisualSuccess = false;
					GameType gameType = MetaHandler.GetGameType();
                    if (gameType == GameType.SERVER) {
						indivisualSuccess = mod.InitializeServer();
					} else {
						indivisualSuccess = mod.InitializeClient();
					}

					if (indivisualSuccess) {
						logger.INFO("Successfully initialized mod with namespace \"" + modFolder.Item1 + "\"");
					} else {
						logger.INFO("Failed to initialize mod with namespace \"" + modFolder.Item1 + "\"");
						success = false;
					}
				}
			}
		}

		logger.INFO("Took " + stopwatch.Elapsed.TotalMilliseconds + "ms to initialize mods");
		logger.INFO((success ? "Successfully" : "Failed to") + " initialize mods");

		if (!success && disableCrashOnError) {
			return true;
		}

		return success;
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
			logger.ERR("Failed to parse JSON, with error \"" + exception.Message + "\". JSON filepath below:");
			logger.ERR(filePath);

			return default;
		}
	}

	public T StringReadJSON<T>(string file) {
		try {
			T jsonData = JsonSerializer.Deserialize<T>(file, options);
			return jsonData;
		} catch (JsonException exception) {
			logger.ERR("Failed to parse JSON, with error \"" + exception.Message + "\". Raw JSON below:");
			logger.ERR(file);

			return default;
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
	}
}
