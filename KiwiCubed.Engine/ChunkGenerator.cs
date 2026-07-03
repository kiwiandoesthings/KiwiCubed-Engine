namespace KiwiCubed.Engine;

using KiwiCubed.Api;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.KLogger;

public static class ChunkGenerator {
	private static AssetManager assetManager;
	private static BiomeModel[] biomes;
	private static BiomeData[] biomeDatas;

    public static void Initialize() {
		OVERRIDE_LOG_NAME("ChunkGenerator");

		assetManager = (AssetManager)MetaHandler.Get<IAssetManager>();
		biomes = assetManager.GetAllBiomeModels().ToArray();
		biomeDatas = new BiomeData[biomes.Length];

		for (int iterator = 0; iterator < biomes.Length; iterator++) {
			BiomeModel biome = biomes[iterator];
			biomeDatas[iterator] = new BiomeData(biome.temperature, biome.humidity, biome.height);
		}

		KINFO("Successfully initialized Chunk Generator");
	}

	public static BiomeModel GetClosestBiome(float temperature, float humidity, float height) {
		int closestIndex = 0;
		float closestDistance = float.MaxValue;

		//Console.WriteLine(temperature + " " + humidity + " " + height);
		for (int iterator = 0; iterator < biomes.Length; iterator++) {
			ref readonly BiomeData biome = ref biomeDatas[iterator];
			float euclidianDistance = (biome.temperature - temperature) * (biome.temperature - temperature) + (biome.humidity - humidity) * (biome.humidity - humidity) + (biome.height - height) * (biome.height - height);
			if (euclidianDistance < closestDistance) {
				closestIndex = iterator;
				closestDistance = euclidianDistance;
			}
		}

		return biomes[closestIndex];
	}

	private readonly struct BiomeData {
		public readonly float temperature;
        public readonly float humidity;
        public readonly float height;

		public BiomeData(float temperature, float humidity, float height) {
			this.temperature = temperature;
			this.humidity = humidity;
			this.height = height;
		}
    }
}