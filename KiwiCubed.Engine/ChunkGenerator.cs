namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using System.Buffers;
using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Utils;

public class ChunkGenerator {
	private readonly int seed;
	private readonly AssetManager assetManager;
	private readonly BiomeModel[] biomes;
	private readonly BiomeData[] biomeDatas;

    public ChunkGenerator(int seed) {
		this.seed = seed;

		assetManager = (AssetManager)MetaHandler.Get<IAssetManager>();
		biomes = [.. assetManager.GetAllBiomeModels()];
		biomeDatas = new BiomeData[biomes.Length];

		for (int iterator = 0; iterator < biomes.Length; iterator++) {
			BiomeModel biome = biomes[iterator];
			biomeDatas[iterator] = new BiomeData(biome.temperature, biome.humidity, biome.height);
		}

		KLogger.shared.INFO("Successfully initialized ChunkGenerator with " + biomes.Length + " biomes.");
    }

	public BiomeModel GetClosestBiome(float temperature, float humidity, float height) {
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

    public bool GenerateChunk(Chunk chunk) {
		return chunk.GenerateBlocks(this, seed);
    }

	public int GetSeed() {
		return seed;
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