namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using System;

using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.Utils;

public class ChunkHandler : IChunkHandler, IDisposable {
	private KLogger logger;
	private WorldFileHandler worldFileHandler;
	private Dictionary<IntVector3, IChunk> chunks;
	private List<IntVector3> chunksToUnload;
	private object chunkMutex;
	private IChunk defaultChunk;

	public ChunkHandler() {
		logger = new KLogger("ChunkHandler");
		chunks = [];
		chunksToUnload = [];
		chunkMutex = new object();
		defaultChunk = new Chunk(0, 0, 0, this);
	}

	public void SetupWorldFileHandling(WorldFileHandler worldFileHandler) {
		this.worldFileHandler = worldFileHandler;
	}

    public IChunk AddChunk(int chunkX, int chunkY, int chunkZ) {
		return AddChunk(new IntVector3(chunkX, chunkY, chunkZ));
	}

	public IChunk AddChunk(IntVector3 chunkPosition) {
		lock (chunkMutex) {
			return AddChunkUnlocked(chunkPosition);
		}
    }

	public IChunk AddChunkUnlocked(int chunkX, int chunkY, int chunkZ) {
		return AddChunkUnlocked(new IntVector3(chunkX, chunkY, chunkZ));
	}

    public IChunk AddChunkUnlocked(IntVector3 chunkPosition) {
        if (!chunks.ContainsKey(chunkPosition)) {
            chunks.Add(chunkPosition, new Chunk(chunkPosition.X, chunkPosition.Y, chunkPosition.Z, this));
            ((Chunk)chunks[chunkPosition]).MakeReal();
            return chunks[chunkPosition];
        } else {
            logger.ERR("Tried to add chunk in the same place twice at " + chunkPosition);
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
				logger.ERR("Tried to queue chunk at " + chunkPosition + " for unloading twice, returning");
				return false;
			}
			chunksToUnload.Add(chunkPosition);
			return true;
		}
	}

    public bool RemeshChunk(IntVector3 chunkPosition, bool updateNeighbors) {
		return RemeshChunk(chunkPosition.X, chunkPosition.Y, chunkPosition.Z, updateNeighbors);
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

	public bool MeshModifiedChunk(FullBlockPosition modificationPosition) {
		return MeshModifiedChunk(modificationPosition.chunkPosition.X, modificationPosition.chunkPosition.Y, modificationPosition.chunkPosition.Z, modificationPosition.blockPosition.X, modificationPosition.blockPosition.Y, modificationPosition.blockPosition.Z);
	}

	public bool MeshModifiedChunk(int chunkX, int chunkY, int chunkZ, int blockX, int blockY, int blockZ) {
		bool returnValue = RemeshChunk(chunkX, chunkY, chunkZ, false);

        if (blockX == 0) {
            RemeshChunk(chunkX - 1, chunkY, chunkZ, false);
        }
        if (blockY == 0) {
            RemeshChunk(chunkX, chunkY - 1, chunkZ, false);
        }
        if (blockZ == 0) {
            RemeshChunk(chunkX, chunkY, chunkZ - 1, false);
        }
        if (blockX == chunkEdge) {
            RemeshChunk(chunkX + 1, chunkY, chunkZ, false);
        }
        if (blockY == chunkEdge) {
            RemeshChunk(chunkX, chunkY + 1, chunkZ, false);
        }
        if (blockZ == chunkEdge) {
            RemeshChunk(chunkX, chunkY, chunkZ + 1, false);
        }

		return returnValue;
    }

	public IChunk GetChunk(int chunkX, int chunkY, int chunkZ, bool addIfNotFound) {
		lock (chunkMutex) {
			return GetChunkUnlocked(new IntVector3(chunkX, chunkY, chunkZ), addIfNotFound);
		}
	}

	public IChunk GetChunk(IntVector3 chunkPosition, bool addIfNotFound) {
		lock (chunkMutex) {
			return GetChunkUnlocked(chunkPosition, addIfNotFound);
		}
	}

	public IChunk GetChunkUnlocked(IntVector3 chunkPosition, bool addIfNotFound) {
		if (chunks.TryGetValue(chunkPosition, out IChunk chunk)) {
			return chunk;
		} else {
			if (worldFileHandler != null) {
				Chunk loadedChunk = worldFileHandler.LoadChunk(chunkPosition);
                if (loadedChunk != null) {
					return loadedChunk;
                }
            }

			if (addIfNotFound) {
				return AddChunk(chunkPosition.X, chunkPosition.Y, chunkPosition.Z);
			}
			//logger.ERR("Tried to get chunk at position " + chunkPosition + " that didn't exist");
			return defaultChunk;
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

	public ushort GetBlock(FullBlockPosition fullPosition) {
		return ((Chunk)GetChunk(fullPosition.chunkPosition, false)).GetBlock(fullPosition.blockPosition);
	}

	public bool SetBlock(FullBlockPosition fullPosition, ushort newBlock) {
        IChunk chunk = GetChunk(fullPosition.chunkPosition, false);
        if (chunk == null) {
            return false;
        }

		return chunk.SetBlock(fullPosition.blockPosition, newBlock);
    }

	public bool AddBlock(FullBlockPosition fullPosition, ushort newBlock) {
		if (newBlock == 0) {
			logger.WARN("Tried to use ChunkHandler.AddBlock with an air block, use ChunkHandler.RemoveBlock instead, returning");
			return false;
		}
		IChunk chunk = GetChunk(fullPosition.chunkPosition, false);
		if (chunk == null) {
			return false;
		}

		return chunk.SetBlock(fullPosition.blockPosition, newBlock);
	}

	public bool RemoveBlock(FullBlockPosition fullPosition) {
		IChunk chunk = GetChunk(fullPosition.chunkPosition, false);
		if (chunk == null) {
			return false;
		}

		return chunk.SetBlock(fullPosition.blockPosition, 0);
	}

	public void CleanChunks() {
		lock (chunkMutex) {
			foreach (IntVector3 chunkPosition in chunksToUnload) {
				if (chunks.TryGetValue(chunkPosition, out IChunk chunk)) {
					if (worldFileHandler != null) {
						worldFileHandler.SaveChunk((Chunk)chunk);
					}

					((Chunk)chunk).Dispose();
					chunks.Remove(chunkPosition);
				} else {
					logger.ERR("Tried to unload chunk at " + chunkPosition + " that didn't exist");
				}
			}

			chunksToUnload.Clear();
		}
	}

	public void ClearChunks() {
		lock (chunkMutex) {
            foreach (KeyValuePair<IntVector3, IChunk> chunkPair in chunks) {
                ((Chunk)chunkPair.Value).Dispose();
                chunks.Remove(chunkPair.Key);
            }
        }

		logger.INFO("Successfully cleared all chunks");
    }

	public static void ForChunkInRange(Func<int, int, int, bool> chunkAction, IntVector3 startPosition, IntVector3 endPosition) {
        for (int chunkX = startPosition.X; chunkX <= endPosition.X; chunkX++) {
			for (int chunkY = startPosition.Y; chunkY <= endPosition.Y; ++chunkY) {
				for (int chunkZ = startPosition.Z; chunkZ <= endPosition.Z; ++chunkZ) {
					if (chunkAction(chunkX, chunkY, chunkZ)) {
						return;
					}
				}
			}
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
		foreach (KeyValuePair<IntVector3, IChunk> chunkPair in chunks) {
			((Chunk)chunkPair.Value).Dispose();
		}

		Chunk.DisposeAll();

		chunks = null;
		chunksToUnload = null;
		chunkMutex = null;
		defaultChunk = null;

        GC.SuppressFinalize(this);
    }
}
