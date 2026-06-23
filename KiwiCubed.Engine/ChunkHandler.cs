namespace KiwiCubed.Engine;

using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;
using KiwiCubed.Api;
using System;

using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.Utils;

public class ChunkHandler : IChunkHandler, IDisposable {
	private World world;
	private ArchWorld archWorld;
	private Dictionary<IntVector3, IChunk> chunks;
	private List<IntVector3> chunksToUnload;
	private object chunkMutex;
	private IChunk defaultChunk;

	public ChunkHandler(World world) {
		this.world = world;
		archWorld = Meta.Get<IAssetManager>().GetArchWorld();
		chunks = new();
		chunksToUnload = new();
		chunkMutex = new object();
		defaultChunk = (IChunk)(new Chunk(0, 0, 0, this));
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
			((Chunk)chunks[chunkPosition]).MakeReal();
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
		OVERRIDE_LOG_NAME("ChunkHandler");

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
			KWARN("Tried to use ChunkHandler.AddBlock with an air block, use ChunkHandler.RemoveBlock instead, returning");
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

	public void ClearChunks() { 
		OVERRIDE_LOG_NAME("ChunkHandler");

		lock (chunkMutex) {
            foreach (KeyValuePair<IntVector3, IChunk> chunkPair in chunks) {
                ((Chunk)chunkPair.Value).Dispose();
                chunks.Remove(chunkPair.Key);
            }
        }

		KINFO("Successfully cleared all chunks");
    }

	public void SaveChunksOfRegion(List<Chunk> chunksInRegion, out byte[] worldHeader, out byte[] chunkDatas) {
		//OVERRIDE_LOG_NAME("ChunkHandler");
		//
		//Stopwatch stopwatch = new Stopwatch();
		//
		//// Create world header
		////   Collect all blocks used
		//List<Block> globalBlockPalette = new();
		//lock (chunkMutex) {
		//	foreach (Chunk chunk in chunksInRegion) {
		//		List<ushort> globalBlockIndices = ((Chunk)chunk).SaveChunkData(ref globalBlockPalette);
		//	}
		//}
		////   Get the strings from every block
		//List<string> blockStrings = new();
		//foreach (Block block in globalBlockPalette) {
		//	blockStrings.Add(block.GetStringID().CanonicalName());
		//}
		////   Turn every string into bytes
		//int totalSize = 4;
		//List<byte[]> stringDatas = new();
		//foreach (string blockString in blockStrings) {
		//	byte[] stringData = System.Text.Encoding.UTF8.GetBytes(blockString);
		//    stringDatas.Add(stringData);
		//	totalSize += 4 + stringData.Length;
		//}
		////   Write the header size
		//worldHeader = new byte[totalSize];
		//int headerOffset = 0;
		//WriteIntToBuffer(worldHeader, ref headerOffset, blockStrings.Count);
		////   Write every string and prefix with it's length
		//foreach (byte[] stringData in stringDatas) {
		//    WriteIntToBuffer(worldHeader, ref headerOffset, stringData.Length);
		//	Buffer.BlockCopy(stringData, 0, worldHeader, headerOffset, stringData.Length);
		//    headerOffset += stringData.Length;
		//}
		//
		//// Create chunk data
		//lock (chunkMutex) {
		//	//   Collect global palette indices
		//	List<ushort[]> globalPaletteIndices = new();
		//	foreach (Chunk chunk in chunksInRegion) {
		//        if (((Chunk)chunk).IsEmpty()) {
		//            continue;
		//        }
		//        List<Block> chunkPalette = ((Chunk)chunk).GetBlockPalette();
		//		ushort[] paletteIndices = ((Chunk)chunk).GetPaletteIndices();
		//		ushort[] remappedIndices = new ushort[paletteIndices.Length];
		//		for (int iterator = 0; iterator < paletteIndices.Length; iterator++) {
		//			remappedIndices[iterator] = (ushort)globalBlockPalette.IndexOf(chunkPalette[paletteIndices[iterator]]);
		//        }
		//		globalPaletteIndices.Add(remappedIndices);
		//    }
		//	// Pack chunk positions and indices into bytes
		//    int totalChunkDataSize = 0;
		//    foreach (ushort[] indices in globalPaletteIndices) {
		//        totalChunkDataSize += (3 * 4) + 4 + (indices.Length * 2);
		//    }
		//    chunkDatas = new byte[totalChunkDataSize];
		//    int index = 0;
		//	int chunkDataOffset = 0;
		//	foreach (Chunk chunk in chunksInRegion) {
		//        if (((Chunk)chunk).IsEmpty()) {
		//            continue;
		//        }
		//        WriteIntToBuffer(chunkDatas, ref chunkDataOffset, chunk.chunkX);
		//		WriteIntToBuffer(chunkDatas, ref chunkDataOffset, chunk.chunkY);
		//		WriteIntToBuffer(chunkDatas, ref chunkDataOffset, chunk.chunkZ);
		//        WriteIntToBuffer(chunkDatas, ref chunkDataOffset, ((Chunk)chunk).GetTotalBlocks());
		//		ushort[] paletteIndices = globalPaletteIndices[index];
		//		Buffer.BlockCopy(paletteIndices, 0, chunkDatas, chunkDataOffset, paletteIndices.Length * 2);
		//		chunkDataOffset += paletteIndices.Length * 2;
		//        index++;
		//    }
		//}
		worldHeader = [];
		chunkDatas = [];
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
        world = null;
		chunksToUnload = null;
		chunkMutex = null;
		defaultChunk = null;
	}
}
