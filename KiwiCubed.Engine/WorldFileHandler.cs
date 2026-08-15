namespace KiwiCubed.Engine;

using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;
using KiwiCubed.Api;
using LevelDB;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.Utils;

public class WorldFileHandler : IDisposable {
    public static readonly byte regionSize = 16;
    public static readonly byte regionShift = (byte)Math.Log2(regionSize);
    public static readonly int totalChunksInRegion = regionSize * regionSize * regionSize;
    public static readonly int chunkDataSize = chunkVolume * 2;
    private readonly string saveFolder;
    private readonly string worldSaveFilename;
    private readonly JsonSerializerOptions jsonOptions;

    private byte worldFormatVersion = 2;

    private readonly DB database;
    private readonly KLogger logger;
    private readonly WorldServer world;
    private readonly ChunkHandler chunkHandler;


    public WorldFileHandler(WorldServer world, string worldName) {
        saveFolder = Path.Combine(topSaveFolder, "Saves", worldName);
        worldSaveFilename = Path.Combine(saveFolder, worldName + ".json");
        jsonOptions = new JsonSerializerOptions {
            AllowDuplicateProperties = false,
            AllowTrailingCommas = false,
            WriteIndented = true
        };
        Options databaseOptions = new Options {
            CreateIfMissing = true
        };
        database = new DB(databaseOptions, saveFolder);
        logger = new KLogger("WorldFileHandler");
        this.world = world;
        chunkHandler = (ChunkHandler)world.GetChunkHandler();
    }

    public void SaveWorld() {
        logger.INFO("Saving world...");
        Stopwatch stopwatch = Stopwatch.StartNew();

        if (!Directory.Exists(saveFolder)) {
            Directory.CreateDirectory(saveFolder);
        }

        logger.INFO("Writing world file...");

        WorldMetadata metadata = new WorldMetadata {
            worldFormatVersion = worldFormatVersion,
            seed = world.GetSeed()
        };

        string json = JsonSerializer.Serialize(metadata, jsonOptions);
        File.WriteAllText(worldSaveFilename, json);

        double totalTime = stopwatch.Elapsed.TotalMilliseconds;
        logger.INFO("Took " + totalTime.ToString("F2") + "ms to create and write world save");
    }

    public bool LoadWorld(out int seed) {
        Stopwatch stopwatch = Stopwatch.StartNew();
        logger.INFO("Loading world...");

        if (!File.Exists(worldSaveFilename)) {
            logger.ERR("Tried to load world from file \"" + worldSaveFilename + "\" that does not exist");
            logger.BREAK();
        }

        string json = File.ReadAllText(worldSaveFilename);
        WorldMetadata? metadata = JsonSerializer.Deserialize<WorldMetadata>(json, jsonOptions);

        if (metadata == null) {
            logger.ERR("Found malformed world file at \"" + worldSaveFilename + "\"");
            logger.BREAK();
        }

        seed = metadata.Value.seed;

        double totalTime = stopwatch.Elapsed.TotalMilliseconds;
        logger.INFO("Took " + totalTime.ToString("F2") + "ms to load world from file");

        return true;
    }

    public void SaveChunk(Chunk chunk) {
        byte[] serializedChunkData = GetChunkData(chunk);
        SaveSerializedChunk(chunk.GetPosition(), serializedChunkData);
    }

    public Chunk? LoadChunk(IntVector3 chunkPosition) {
        byte[] serializedChunkData = LoadSerializedChunk(chunkPosition);
        if (serializedChunkData == null) {
            return null;
        }

        Chunk chunk = (Chunk)chunkHandler.AddChunk(chunkPosition);

        ushort[] rawBlockData = serializedChunkData.AsUshortArray();
        ushort[] blockIndices = new ushort[chunkVolume];
        List<ushort> blockPalette = [];
        Dictionary<ushort, ushort> paletteMap = [];
        ushort totalBlocks = 0;

        blockPalette.Add(0);
        paletteMap.Add(0, 0);

        for (int iterator = 0; iterator < chunkVolume; iterator++) {
            ushort currentBlock = rawBlockData[iterator];
            if (currentBlock != 0) {
                totalBlocks++;
            }
            if (!paletteMap.TryGetValue(currentBlock, out ushort paletteIndex)) {
                paletteIndex = (ushort)blockPalette.Count;
                blockPalette.Add(currentBlock);
                paletteMap.Add(currentBlock, paletteIndex);
            }
            blockIndices[iterator] = paletteIndex;
        }

        chunk.LoadChunkData(blockPalette.ToArray(), blockIndices, totalBlocks);

        return chunk;
    }

    private void SaveSerializedChunk(IntVector3 chunkPosition, byte[] chunkData) {
        byte[] key = GetChunkKey(chunkPosition);
        database.Put(key, chunkData);
    }

    private byte[]? LoadSerializedChunk(IntVector3 chunkPosition) {
        byte[] key = GetChunkKey(chunkPosition);

        return database.Get(key);
    }

    private byte[] GetChunkData(Chunk chunk) {
        ushort[] palette = chunk.GetBlockPalette();
        ushort[] indices = chunk.GetPaletteIndices();

        ushort[] rawBlockData = new ushort[chunkVolume];
        int totalBlocks = 0;

        for (int iterator = 0; iterator < chunkVolume; iterator++) {
            ushort blockID = palette[indices[iterator]];
            rawBlockData[iterator] = blockID;
            if (blockID != 0) {
                totalBlocks++;
            }
        }

        return rawBlockData.AsByteArray();
    }

    private byte[] GetChunkKey(IntVector3 chunkPosition) {
        byte[] key = new byte[12];
        Buffer.BlockCopy(BitConverter.GetBytes(chunkPosition.X), 0, key, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(chunkPosition.Y), 0, key, 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(chunkPosition.Z), 0, key, 8, 4);

        return key;
    }

    public void Dispose() {
        database.Close();
        database.Dispose();

        GC.SuppressFinalize(this);
    }

    private struct WorldMetadata {
        public int worldFormatVersion { get; set; }
        public int seed { get; set; }
    }
}