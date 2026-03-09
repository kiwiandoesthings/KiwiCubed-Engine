namespace KiwiCubed;

using System;

using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.Util;

public class ChunkHandler : IDisposable {
	private readonly World world = null;
	private Dictionary<IntVector3, Chunk> chunks = new();
	private readonly Chunk defaultChunk = null;
	public ChunkHandler(World world) {
		this.world = world;
		defaultChunk = new Chunk(0, 0, 0, this);

		SystemsManager.Register<ChunkHandler>(this);
	}

	public Chunk AddChunk(int chunkX, int chunkY, int chunkZ) {
		OVERRIDE_LOG_NAME("ChunkHandler");
		IntVector3 chunkPosition = new IntVector3(chunkX, chunkY, chunkZ);
		if (!chunks.ContainsKey(chunkPosition)) {
			chunks.Add(chunkPosition, new Chunk(chunkX, chunkY, chunkZ, this));
			chunks[chunkPosition].SetupRenderComponents();
			return chunks[chunkPosition];
		} else {
			KERR("Tried to add chunk in the same place twice at {" + chunkX + ", " + chunkY + ", " + chunkZ + "}");
			return null;
		}
	}

	public bool RemeshChunk(int chunkX, int chunkY, int chunkZ, bool updateNeighbors) {
		Chunk chunk = GetChunk(chunkX, chunkY, chunkZ, false);
		if (!chunk.IsGenerated()) {
			return false;
		}

		chunk.GenerateMesh(true);

		if (updateNeighbors) {
			GetChunk(chunkX + 1, chunkY, chunkZ, false).GenerateMesh(true);
			GetChunk(chunkX - 1, chunkY, chunkZ, false).GenerateMesh(true);
			GetChunk(chunkX, chunkY + 1, chunkZ, false).GenerateMesh(true);
			GetChunk(chunkX, chunkY - 1, chunkZ, false).GenerateMesh(true);
			GetChunk(chunkX, chunkY, chunkZ + 1, false).GenerateMesh(true);
			GetChunk(chunkX, chunkY, chunkZ - 1, false).GenerateMesh(true);
		}

		return true;
	}

	public Chunk GetChunk(int chunkX, int chunkY, int chunkZ, bool addIfNotFound) {
		OVERRIDE_LOG_NAME("ChunkHandler");
		IntVector3 chunkPosition = new IntVector3(chunkX, chunkY, chunkZ);
		if (chunks.TryGetValue(chunkPosition, out Chunk chunk)) {
			return chunk;
		} else {
			if (addIfNotFound) {
				return AddChunk(chunkX, chunkY, chunkZ);
			}
			//KERR("Tried to get chunk at position {" + chunkX + ", " + chunkY + ", " + chunkZ + "} that didn't exist");
			return defaultChunk;
		}
	}

	public Chunk GetChunk(IntVector3 chunkPosition, bool addIfNotFound) {
		OVERRIDE_LOG_NAME("ChunkHandler");
		if (chunks.TryGetValue(chunkPosition, out Chunk chunk)) {
			return chunk;
		} else {
			if (addIfNotFound) {
				return AddChunk(chunkPosition.X, chunkPosition.Y, chunkPosition.Z);
			}
			//KERR("Tried to get chunk at position " + chunkPosition + " that didn't exist");
			return defaultChunk;
		}
	}

	public bool AddBlock(FullBlockPosition fullPosition, ushort newBlockID) {
		if (newBlockID == 0) {
			KWARN("Tried to use ChunkHandler.AddBlock with a newBlockID of 0, use ChunkHandler.RemoveBlock instead, returning");
			return false;
		}
		Chunk chunk = GetChunk(fullPosition.chunkPosition, false);
		if (chunk == null) {
			return false;
		}
		if (chunk.SetBlock(fullPosition.blockPosition, newBlockID)) {
			return true;
		}

		return false;
	}

	public bool RemoveBlock(FullBlockPosition fullPosition) {
		Chunk chunk = GetChunk(fullPosition.chunkPosition, false);
		if (chunk == null) {
			return false;
		}
		if (chunk.SetBlock(fullPosition.blockPosition, 0)) {
			return true;
		}

		return false;
	}

	public Dictionary<IntVector3, Chunk> GetChunks() {
		return chunks;
	}

	public Chunk GetDefaultChunk() {
		return defaultChunk;
	}

	public void Dispose() {
		SystemsManager.Deregister<ChunkHandler>();
	}
}
