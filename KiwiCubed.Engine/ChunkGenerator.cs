namespace KiwiCubed.Engine;

using KiwiCubed.Api;

using static KiwiCubed.Api.AssetDefinitions;

public static class ChunkGenerator {
	private static AssetManager assetManager;
	private static List<BiomeModel> biomes;
	private static float[] factorWeights;

	public static void Initialize() {
		assetManager = (AssetManager)SystemsManager.Get<IAssetManager>();
		biomes = assetManager.GetAllBiomeModels();
		factorWeights = [
			0.4f, 1.0f, 1.0f
		];
	}

	public static BiomeModel GetClosestBiome(float height, float temperature, float humidity) {
		BiomeModel closestBiome = null;
		float closestDistance = float.MaxValue;
		foreach (BiomeModel biome in biomes) {
			float deltaTemperature = (biome.temperature - temperature);
			float deltaHumidity = (biome.humidity - humidity);
			float deltaHeight = (biome.height - height);
			float euclidianDistance = (factorWeights[0] * deltaHeight * deltaHeight) + (factorWeights[1] * deltaTemperature * deltaTemperature) + (factorWeights[2] * deltaHumidity * deltaHumidity);
			if (euclidianDistance < closestDistance) {
				closestDistance = euclidianDistance;
				closestBiome = biome;
			}
		}

		return closestBiome;
	}
}