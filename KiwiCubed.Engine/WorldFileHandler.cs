namespace KiwiCubed.Engine;

using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;
using KiwiCubed.Api;
using System.Diagnostics;
using System.Text;

using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.Utils;

public class WorldFileHandler {
    private byte worldFormatVersion = 1;

    private WorldServer world;
    private ChunkHandler chunkHandler;
    private KLogger logger;

    public WorldFileHandler(WorldServer world) {
        this.world = world;
        chunkHandler = (ChunkHandler)world.GetChunkHandler();
        logger = new KLogger("WorldFileHandler");
    }

    public void SaveWorld(string worldName) {
        logger.INFO("Saving world...");
        Stopwatch stopwatch = Stopwatch.StartNew();
        
        string saveFolder = Path.Combine(topSaveFolder, "Saves");
        
        if (!Directory.Exists(saveFolder)) {
            Directory.CreateDirectory(saveFolder);
        }
        
        logger.INFO("Writing world file...");
        string worldSaveFilename = Path.Combine(saveFolder, "world_" + worldName + ".kcl");
        FileStream filestream = new FileStream(worldSaveFilename, FileMode.Create, FileAccess.Write);
        byte[] trueHeader = Encoding.ASCII.GetBytes("KCENGINE");
        filestream.Write(trueHeader, 0, trueHeader.Length);
        filestream.WriteByte(worldFormatVersion);
        filestream.Write(BitConverter.GetBytes(world.GetSeed()));
        filestream.Close();
        
        Dictionary<IntVector3, List<Chunk>> chunkRegions = [];
        lock (chunkHandler.GetChunkMutex()) {
            foreach (IChunk chunk in chunkHandler.GetChunks().Values) {
                IntVector3 regionPosition = new IntVector3(chunk.chunkX >> 4, chunk.chunkY >> 4, chunk.chunkZ >> 4);
                if (chunkRegions.TryGetValue(regionPosition, out List<Chunk> value)) {
                    value.Add((Chunk)chunk);
                } else {
                    chunkRegions.Add(regionPosition, [(Chunk)chunk]);
                }
            }
        }
        
        logger.INFO("Writing region files...");
        foreach (IntVector3 regionPosition in chunkRegions.Keys) {
            chunkHandler.SaveChunksOfRegion(chunkRegions[regionPosition], out byte[] regionChunkDatas);
        
            string regionFilename = "region_" + regionPosition.X + "." + regionPosition.Y + "." + regionPosition.Z + ".kcr";
            regionFilename = Path.Combine(saveFolder, regionFilename);
            filestream = new FileStream(regionFilename, FileMode.Create, FileAccess.Write);
        
            filestream.Write(regionChunkDatas, 0, regionChunkDatas.Length);
            filestream.Close();
            logger.INFO(" * Finished collecting and writing data for region " + regionPosition);
        }
        
        double totalTime = stopwatch.Elapsed.TotalMilliseconds;
        logger.INFO("Took " + totalTime.ToString("F2") + "ms to create and write world save");
    }

    public bool LoadWorld(string worldName, out int seed) {
        Stopwatch stopwatch = Stopwatch.StartNew();
        logger.INFO("Loading world...");
        
        string saveFolder = Path.Combine(topSaveFolder, "Saves");
        
        string worldSaveFilename = Path.Combine(saveFolder, "world_" + worldName + ".kcl");
        
        if (!File.Exists(worldSaveFilename)) {
            logger.ERR("Tried to load world from file \"" + worldSaveFilename + "\" that does not exist");
            logger.BREAK();
        }
        
        FileStream filestream = new FileStream(worldSaveFilename, FileMode.Open, FileAccess.Read);
        byte[] headerBytes = new byte[8];
        filestream.ReadExactly(headerBytes);
        string header = Encoding.ASCII.GetString(headerBytes);
        if (header != "KCENGINE") {
            logger.ERR("Tried to load world with invalid header \"" + header + "\" when it should have matched \"KCENGINE\"");
            logger.BREAK();
        }
        
        byte formatVersion = (byte)filestream.ReadByte();
        if (formatVersion != worldFormatVersion) {
            logger.ERR("Tried to load world with unsupported format version {" + formatVersion + "}, latest format version is {" + worldFormatVersion + "}");
            logger.BREAK();
        }
        
        byte[] worldSeedBytes = new byte[4];
        filestream.ReadExactly(worldSeedBytes);
        int worldSeed = BitConverter.ToInt32(worldSeedBytes);
        seed = worldSeed;
        
        filestream.Close();
        
        foreach (string filepath in Directory.GetFiles(saveFolder, "*.kcr")) {
            filestream = new FileStream(filepath, FileMode.Open, FileAccess.Read);
            byte[] chunkDatas = new byte[filestream.Length];
            filestream.ReadExactly(chunkDatas);
            int chunkOffset = 0;
            while (chunkOffset < chunkDatas.Length) {
                int chunkX = ReadIntFromBuffer(chunkDatas, ref chunkOffset);
                int chunkY = ReadIntFromBuffer(chunkDatas, ref chunkOffset);
                int chunkZ = ReadIntFromBuffer(chunkDatas, ref chunkOffset);
                int totalBlocks = ReadIntFromBuffer(chunkDatas, ref chunkOffset);

                ushort[] rawBlockData = new ushort[chunkVolume];
                List<ushort> blockPalette = [];
                ushort[] blockIndices = new ushort[chunkVolume];
                Chunk chunk = (Chunk)chunkHandler.AddChunk(chunkX, chunkY, chunkZ);

                Buffer.BlockCopy(chunkDatas, chunkOffset, rawBlockData, 0, rawBlockData.Length * 2);
                for (int iterator = 0; iterator < chunkVolume; iterator++) {
                    int paletteLocation = blockPalette.IndexOf(rawBlockData[iterator]);
                    if (paletteLocation == -1) {
                        paletteLocation = blockPalette.Count;
                        blockPalette.Add(rawBlockData[iterator]);
                    }
                    blockIndices[iterator] = (ushort)paletteLocation;
                }
                chunkOffset += chunkVolume * 2;
        
                chunk.LoadChunkData(blockPalette.ToArray(), blockIndices);
            }
            filestream.Close();
        }
        
        double totalTime = stopwatch.Elapsed.TotalMilliseconds;
        logger.INFO("Took " + totalTime.ToString("F2") + "ms to load world from file");
        
        return true;
    }
}