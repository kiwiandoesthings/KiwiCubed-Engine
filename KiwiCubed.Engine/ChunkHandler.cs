namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using System;

using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.Util;

public class ChunkHandler : IChunkHandler, IDisposable {
	private readonly World world;
	private Dictionary<IntVector3, IChunk> chunks;
	private List<IntVector3> chunksToUnload;
	private object chunkMutex;
	private readonly IChunk defaultChunk;

	public ChunkHandler(World world) {
		this.world = world;
		chunks = new();
		chunksToUnload = new();
		chunkMutex = new object();
		defaultChunk = (IChunk)(new Chunk(0, 0, 0, this));

		SystemsManager.Register<ChunkHandler>(this);
	}

	public IChunk AddChunk(int chunkX, int chunkY, int chunkZ) {
		lock (chunkMutex) {
			return AddChunkUnlocked(chunkX, chunkY, chunkZ);
		}
	}

	public IChunk AddChunkUnlocked(int chunkX, int chunkY, int chunkZ) {
		OVERRIDE_LOG_NAME("ChunkHandler");
		IntVector3 chunkPosition = new IntVector3(chunkX, chunkY, chunkZ);
		if (!chunks.ContainsKey(chunkPosition)) {
			chunks.Add(chunkPosition, (IChunk)(new Chunk(chunkX, chunkY, chunkZ, this)));
			return chunks[chunkPosition];
		} else {
			KERR("Tried to add chunk in the same place twice at " + chunkPosition);
			return null;
		}
	}

	public bool RemoveChunk(int chunkX, int chunkY, int chunkZ) {
		return RemoveChunk(new IntVector3(chunkX, chunkY, chunkZ));
	}

	public bool RemoveChunk(IntVector3 chunkPosition) {
		lock (chunkMutex) {
			((Chunk)GetChunk(chunkPosition, false)).ReadyDestroy();
			if (chunksToUnload.Contains(chunkPosition)) {
				KERR("Tried to queue chunk at " + chunkPosition + " for unloading twice, returning");
				return false;
			}
			chunksToUnload.Add(chunkPosition);
			return true;
		}
	}

	public bool RemeshChunk(int chunkX, int chunkY, int chunkZ, bool updateNeighbors) {
		Chunk chunk = (Chunk)GetChunk(chunkX, chunkY, chunkZ, false);
		if (!chunk.IsGenerated()) {
			return false;
		}

		chunk.GenerateMesh(true);

		if (updateNeighbors) {
			((Chunk)GetChunk(chunkX + 1, chunkY, chunkZ, false)).GenerateMesh(true);
			((Chunk)GetChunk(chunkX - 1, chunkY, chunkZ, false)).GenerateMesh(true);
			((Chunk)GetChunk(chunkX, chunkY + 1, chunkZ, false)).GenerateMesh(true);
			((Chunk)GetChunk(chunkX, chunkY - 1, chunkZ, false)).GenerateMesh(true);
			((Chunk)GetChunk(chunkX, chunkY, chunkZ + 1, false)).GenerateMesh(true);
			((Chunk)GetChunk(chunkX, chunkY, chunkZ - 1, false)).GenerateMesh(true);
		}

		return true;
	}

	public IChunk GetChunk(int chunkX, int chunkY, int chunkZ, bool addIfNotFound) {
		OVERRIDE_LOG_NAME("ChunkHandler");
		IntVector3 chunkPosition = new IntVector3(chunkX, chunkY, chunkZ);
		if (chunks.TryGetValue(chunkPosition, out IChunk chunk)) {
			return chunk;
		} else {
			if (addIfNotFound) {
				return AddChunk(chunkX, chunkY, chunkZ);
			}
			//KERR("Tried to get chunk at position {" + chunkX + ", " + chunkY + ", " + chunkZ + "} that didn't exist");
			return defaultChunk;
		}
	}

	public IChunk GetChunk(IntVector3 chunkPosition, bool addIfNotFound) {
		OVERRIDE_LOG_NAME("ChunkHandler");
		lock (chunkMutex) {
			if (chunks.TryGetValue(chunkPosition, out IChunk chunk)) {
				return chunk;
			} else {
				if (addIfNotFound) {
					return AddChunk(chunkPosition.X, chunkPosition.Y, chunkPosition.Z);
				}
				//KERR("Tried to get chunk at position " + chunkPosition + " that didn't exist");
				return defaultChunk;
			}
		}
	}

	public bool GetChunkExists(int chunkX, int chunkY, int chunkZ) {
		return GetChunkExists(new IntVector3(chunkX, chunkY, chunkZ));
	}

	public bool GetChunkExists(IntVector3 chunkPosition) {
		lock (chunkMutex) {
			return chunks.ContainsKey(chunkPosition);
		}
	}

	public Block GetBlock(FullBlockPosition fullPosition) {
		return ((Chunk)GetChunk(fullPosition.chunkPosition, false)).GetBlock(fullPosition.blockPosition);
	}

	public bool AddBlock(FullBlockPosition fullPosition, ushort newBlockID) {
		if (newBlockID == 0) {
			KWARN("Tried to use ChunkHandler.AddBlock with a newBlockID of 0, use ChunkHandler.RemoveBlock instead, returning");
			return false;
		}
		IChunk chunk = GetChunk(fullPosition.chunkPosition, false);
		if (chunk == null) {
			return false;
		}
		if (((Chunk)chunk).SetBlock(fullPosition.blockPosition, newBlockID)) {
			return true;
		}

		return false;
	}

	public bool RemoveBlock(FullBlockPosition fullPosition) {
		IChunk chunk = GetChunk(fullPosition.chunkPosition, false);
		if (chunk == null) {
			return false;
		}
		if (((Chunk)chunk).SetBlock(fullPosition.blockPosition, 0)) {
			return true;
		}

		return false;
	}

	public void CleanChunks() {
		OVERRIDE_LOG_NAME("ChunkHandler");
		lock (chunkMutex) {
			foreach (IntVector3 chunkPosition in chunksToUnload) {
				if (chunks.TryGetValue(chunkPosition, out IChunk chunk)) {
					((Chunk)chunk).Dispose();
					chunks.Remove(chunkPosition);
				} else {
					KERR("Tried to unload chunk at " + chunkPosition + " that didn't exist");
				}
			}

			chunksToUnload.Clear();
		}
	}

	public Dictionary<IntVector3, IChunk> GetChunks() {
		return chunks;
	}

	public object GetChunkMutex() {
		return chunkMutex;
	}

	public IChunk GetDefaultChunk() {
		return defaultChunk;
	}

	public void Dispose() {
		SystemsManager.Deregister<ChunkHandler>();
	}
}
