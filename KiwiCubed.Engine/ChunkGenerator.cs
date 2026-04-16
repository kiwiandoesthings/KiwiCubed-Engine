namespace KiwiCubed.Engine;

using KiwiCubed.Api;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.KLogger;

public static class ChunkGenerator {
	private static AssetManager assetManager;
	private static BiomeModel[] biomes;
	private static float[] temperatures;
	private static float[] humidities;
	private static float[] heights;
	private static float[] factorWeights;

	public static void Initialize() {
		OVERRIDE_LOG_NAME("ChunkGenerator");

		assetManager = (AssetManager)MetaHandler.Get<IAssetManager>();
		biomes = assetManager.GetAllBiomeModels().ToArray();
		temperatures = new float[biomes.Length];
		humidities = new float[biomes.Length];
		heights = new float[biomes.Length];

		for (int iterator = 0; iterator < biomes.Length; iterator++) {
			BiomeModel biome = biomes[iterator];
			temperatures[iterator] = biome.temperature;
			humidities[iterator] = biome.humidity;
			heights[iterator] = biome.height;
		}

		factorWeights = [
			0.4f, 1.0f, 1.0f
		];

		KINFO("Successfully initialized Chunk Generator");
	}

	public static BiomeModel GetClosestBiome(float height, float temperature, float humidity) {
		int closestIndex = 0;
		float closestDistance = 0.0f;
		for (int iterator = 0; iterator < biomes.Length; iterator++) {
			float deltaTemperature = (temperatures[iterator] - temperature);
			float deltaHumidity = (humidities[iterator] - humidity);
			float deltaHeight = (heights[iterator] - height);
			float euclidianDistance = (factorWeights[0] * deltaHeight * deltaHeight) + (factorWeights[1] * deltaTemperature * deltaTemperature) + (factorWeights[2] * deltaHumidity * deltaHumidity);
			if (euclidianDistance < closestDistance || iterator == 0) {
				closestIndex = iterator;
				closestDistance = euclidianDistance;
			}
		}

		return biomes[closestIndex];
	}
}