namespace KiwiCubed;

using System.Numerics;
using System.Threading;

using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.Util;
using static FastNoiseLite;

public class World {
    private ChunkHandler chunkHandler = null;
    private FastNoiseLite noise = null;
    private Player player;

    private uint horizontalSize = 0;
    private uint verticalSize = 0;

    public World(uint horizontalSize, uint verticalSize) {
        chunkHandler = new ChunkHandler(this);
        noise = new FastNoiseLite();
        this.horizontalSize = horizontalSize;
        this.verticalSize = verticalSize;

        for (int chunkX = 0; chunkX < horizontalSize; chunkX++) {
            for (int chunkY = 0; chunkY < verticalSize; chunkY++) {
                for (int chunkZ = 0; chunkZ < horizontalSize; chunkZ++) {
                    chunkHandler.AddChunk(chunkX, chunkY, chunkZ);
                }
            }
        }

        noise.SetSeed((int)Environment.TickCount64);
        noise.SetNoiseType(NoiseType.OpenSimplex2);
        noise.SetFractalType(FractalType.FBm);
        noise.SetFractalOctaves(5);
        noise.SetFractalLacunarity(2.0f);
        noise.SetFractalGain(0.5f);
        noise.SetFractalWeightedStrength(5.0f);

        player = new Player(0UL, new Vector3(-32, 0, 0), new Vector3(1, 0, 0));
    }

    public void GenerateWorld() {
        OVERRIDE_LOG_NAME("World Generation");
        KINFO("Generating world...");

        Chunk defaultChunk = chunkHandler.GetDefaultChunk();
		for (int chunkX = 0; chunkX < horizontalSize; chunkX++) {
            for (int chunkY = 0; chunkY < verticalSize; chunkY++) {
                for (int chunkZ = 0; chunkZ < horizontalSize; chunkZ++) {
                    Chunk currentChunk = chunkHandler.GetChunk(chunkX, chunkY, chunkZ, false);
					GenerateChunk(chunkX, chunkY, chunkZ, currentChunk, false, defaultChunk);
                }
            }
        }

        SystemsManager.Get<VirtualWindow>().SetFocused(true);
    }

    private void GenerateChunk(int chunkX, int chunkY, int chunkZ, Chunk chunk, bool updateCallerChunk, Chunk callerChunk) {
        if (!chunk.IsGenerated()) {
            chunk.GenerateBlocks(this, callerChunk, false, false);
        }

		Chunk positiveXChunk = chunkHandler.GetChunk(chunkX + 1, chunkY, chunkZ, true);     // Positive X
		Chunk negativeXChunk = chunkHandler.GetChunk(chunkX - 1, chunkY, chunkZ, true);     // Negative X
		Chunk positiveYChunk = chunkHandler.GetChunk(chunkX, chunkY + 1, chunkZ, true);     // Positive Y
		Chunk negativeYChunk = chunkHandler.GetChunk(chunkX, chunkY - 1, chunkZ, true);     // Negative Y
		Chunk positiveZChunk = chunkHandler.GetChunk(chunkX, chunkY, chunkZ + 1, true);     // Positive Z
		Chunk negativeZChunk = chunkHandler.GetChunk(chunkX, chunkY, chunkZ - 1, true);     // Negative Z

		if (positiveXChunk.IsGenerated() && negativeXChunk.IsGenerated() && positiveYChunk.IsGenerated() && negativeYChunk.IsGenerated() && positiveZChunk.IsGenerated() && negativeZChunk.IsGenerated() && !chunk.IsMeshed()) {
			chunk.GenerateMesh(false);
		} else if (!updateCallerChunk) {
			if (!positiveXChunk.IsGenerated()) {
				GenerateChunk(chunkX + 1, chunkY, chunkZ, positiveXChunk, true, chunk);
			}

			if (!negativeXChunk.IsGenerated()) {
				GenerateChunk(chunkX - 1, chunkY, chunkZ, negativeXChunk, true, chunk);
			}

			if (!positiveYChunk.IsGenerated()) {
				GenerateChunk(chunkX, chunkY + 1, chunkZ, positiveYChunk, true, chunk);
			}

			if (!negativeYChunk.IsGenerated()) {
				GenerateChunk(chunkX, chunkY - 1, chunkZ, negativeYChunk, true, chunk);
			}

			if (!positiveZChunk.IsGenerated()) {
				GenerateChunk(chunkX, chunkY, chunkZ + 1, positiveZChunk, true, chunk);
			}

			if (!negativeZChunk.IsGenerated()) {
				GenerateChunk(chunkX, chunkY, chunkZ - 1, negativeZChunk, true, chunk);
			}
		}

		if (updateCallerChunk) {
			GenerateChunk(callerChunk.chunkX, callerChunk.chunkY, callerChunk.chunkZ, callerChunk, false, chunk);
		}
	}

    public void Render() {
        foreach (KeyValuePair<IntVector3, Chunk> chunkPair in chunkHandler.GetChunks()) {
            chunkPair.Value.Render();
        }
    }

    public void Update(Shader shader) {
        player.Update(shader);
    }

    public FastNoiseLite GetNoise() {
        return noise;
    }
}