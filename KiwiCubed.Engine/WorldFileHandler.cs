namespace KiwiCubed.Engine;

using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;
using KiwiCubed.Api;
using System.Diagnostics;
using System.Numerics;
using System.Text;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.IPlayer;
using static KiwiCubed.Api.Util;

public class WorldFileHandler {
    private byte worldFormatVersion = 0;

    private World world;
    private ChunkHandler chunkHandler;
    private AssetManager assetManager;

    public WorldFileHandler(World world) {
        this.world = world;
        chunkHandler = (ChunkHandler)world.GetChunkHandler();
        assetManager = (AssetManager)SystemsManager.Get<IAssetManager>();
    }

    public void SaveWorld(string worldName) {
        OVERRIDE_LOG_NAME("World Saving");

        KINFO("Saving world...");
        Stopwatch stopwatch = Stopwatch.StartNew();

        string saveFolder = Path.Combine(topSaveFolder, "Saves");

        if (!Directory.Exists(saveFolder)) {
            Directory.CreateDirectory(saveFolder);
        }

        KINFO("Writing world file...");
        string worldSaveFilename = Path.Combine(saveFolder, "world_" + worldName + ".kcl");
        FileStream filestream = new FileStream(worldSaveFilename, FileMode.Create, FileAccess.Write);
        byte[] trueHeader = Encoding.ASCII.GetBytes("KCENGINE");
        filestream.Write(trueHeader, 0, trueHeader.Length);
        filestream.WriteByte(worldFormatVersion);
        filestream.Write(BitConverter.GetBytes(world.GetSeed()));
        filestream.Close();

        Dictionary<IntVector3, List<Chunk>> chunkRegions = new();
        lock (chunkHandler.GetChunkMutex()) {
            foreach (IChunk chunk in chunkHandler.GetChunks().Values) {
                IntVector3 regionPosition = new IntVector3(chunk.chunkX >> 4, chunk.chunkY >> 4, chunk.chunkZ >> 4);
                if (chunkRegions.ContainsKey(regionPosition)) {
                    chunkRegions[regionPosition].Add((Chunk)chunk);
                } else {
                    chunkRegions.Add(regionPosition, new List<Chunk>() { (Chunk)chunk });
                }
            }
        }

        KINFO("Writing player file...");
        //string playerSaveFilename = Path.Combine(saveFolder, "player_" + world.GetPlayer().GetEntityData().name + ".kcp");
        //filestream = new FileStream(playerSaveFilename, FileMode.Create, FileAccess.Write);
        //ArchEntity player = world.GetPlayer();
        //PlayerData playerData = player.GetPlayerData();
        //EntityData data = player.GetEntityData();
        //EntityTransform transform = player.GetEntityTransform();
        //filestream.Write(BitConverter.GetBytes(transform.position.X));
        //filestream.Write(BitConverter.GetBytes(transform.position.Y));
        //filestream.Write(BitConverter.GetBytes(transform.position.Z));
        //filestream.Write(BitConverter.GetBytes(transform.orientation.X));
        //filestream.Write(BitConverter.GetBytes(transform.orientation.Y));
        //filestream.Write(BitConverter.GetBytes(transform.orientation.Z));
        //filestream.Write(BitConverter.GetBytes((int)playerData.gameMode));
        //filestream.Close();

        KINFO("Writing region files...");
        Dictionary<IntVector3, ValueTuple<byte[], byte[]>> regionDatas = new();
        foreach (IntVector3 regionPosition in chunkRegions.Keys) {
            chunkHandler.SaveChunksOfRegion(chunkRegions[regionPosition], out byte[] regionHeader, out byte[] regionChunkDatas);
            KINFO(" * Finished collecting data for region " + regionPosition);

            regionDatas.Add(regionPosition, new ValueTuple<byte[], byte[]>(regionHeader, regionChunkDatas));

            string regionFilename = "region_" + regionPosition.X + "." + regionPosition.Y + "." + regionPosition.Z + ".kcr";
            regionFilename = Path.Combine(saveFolder, regionFilename);
            filestream = new FileStream(regionFilename, FileMode.Create, FileAccess.Write);

            filestream.Write(regionHeader, 0, regionHeader.Length);
            filestream.Write(regionChunkDatas, 0, regionChunkDatas.Length);
            filestream.Close();
        }

        double totalTime = stopwatch.Elapsed.TotalMilliseconds;
        KINFO("Took " + totalTime.ToString("F2") + "ms to create and write world save");
    }

    public bool LoadWorld(string worldName) {
        OVERRIDE_LOG_NAME("World Loading");

        Stopwatch stopwatch = Stopwatch.StartNew();
        KINFO("Loading world...");

        string saveFolder = Path.Combine(topSaveFolder, "Saves");

        string worldSaveFilename = Path.Combine(saveFolder, "world_" + worldName + ".kcl");

        if (!File.Exists(worldSaveFilename)) {
            KERR("Tried to load world from file \"" + worldSaveFilename + "\" that does not exist");
            return false;
        }

        FileStream filestream = new FileStream(worldSaveFilename, FileMode.Open, FileAccess.Read);
        byte[] headerBytes = new byte[8];
        filestream.ReadExactly(headerBytes);
        string header = Encoding.ASCII.GetString(headerBytes);
        if (header != "KCENGINE") {
            KERR("Tried to load world with invalid header \"" + header + "\" when it should have matched \"KCENGINE\"");
            return false;
        }

        byte formatVersion = (byte)filestream.ReadByte();
        if (formatVersion != worldFormatVersion) {
            KERR("Tried to load world with unsupported format version {" + formatVersion + "}, latest format version is {" + worldFormatVersion + "}");
            return false;
        }

        byte[] worldSeedBytes = new byte[4];
        filestream.ReadExactly(worldSeedBytes);
        int worldSeed = BitConverter.ToInt32(worldSeedBytes);

        world.ReadyGeneration(worldSeed);

        filestream.Close();

        string playerSaveFilename = Path.Combine(saveFolder, "player_" + playerUsername + ".kcp");
        filestream = new FileStream(playerSaveFilename, FileMode.Open, FileAccess.Read);
        BinaryReader reader = new BinaryReader(filestream);
        float xPosition = reader.ReadSingle();
        float yPosition = reader.ReadSingle();
        float zPosition = reader.ReadSingle();
        float xLooking = reader.ReadSingle();
        float yLooking = reader.ReadSingle();
        float zLooking = reader.ReadSingle();
        Vector3 position = new Vector3(xPosition, yPosition, zPosition);
        Vector3 orientation = new Vector3(xLooking, yLooking, zLooking);
        GameMode gameMode = (GameMode)reader.ReadInt32();
        world.LoadPlayer(position, orientation, gameMode);
        filestream.Close();

        foreach (string filepath in Directory.GetFiles(saveFolder, "*.kcr")) {
            filestream = new FileStream(filepath, FileMode.Open, FileAccess.Read);

            byte[] headerSize = new byte[4];
            filestream.ReadExactly(headerSize);
            int stringOffset = 0;
            int paletteCount = ReadIntFromBuffer(headerSize, ref stringOffset);

            List<string> globalBlockPaletteStrings = new();
            for (int iterator = 0; iterator < paletteCount; iterator++) {
                filestream.ReadExactly(headerSize);
                stringOffset = 0;
                int length = ReadIntFromBuffer(headerSize, ref stringOffset);

                byte[] stringData = new byte[length];
                filestream.ReadExactly(stringData);
                globalBlockPaletteStrings.Add(Encoding.UTF8.GetString(stringData));
            }
            List<Block> globalBlockPalette = new();
            foreach (string blockString in globalBlockPaletteStrings) {
                //Block block = assetManager.GetBlock(AssetStringID.FromString(blockString));
                //globalBlockPalette.Add(block);
            }

            long remaining = filestream.Length - filestream.Position;
            byte[] chunkDatas = new byte[remaining];
            filestream.ReadExactly(chunkDatas);
            int chunkOffset = 0;
            while (chunkOffset < chunkDatas.Length) {
                int chunkX = ReadIntFromBuffer(chunkDatas, ref chunkOffset);
                int chunkY = ReadIntFromBuffer(chunkDatas, ref chunkOffset);
                int chunkZ = ReadIntFromBuffer(chunkDatas, ref chunkOffset);
                int totalBlocks = ReadIntFromBuffer(chunkDatas, ref chunkOffset);

                HashSet<Block> chunkBlocks = new();
                Chunk chunk = (Chunk)chunkHandler.AddChunk(chunkX, chunkY, chunkZ);
                List<Block> chunkPalette = new();
                ushort[] blockIndices = new ushort[chunkVolume];
                ushort[] localBlockIndices = new ushort[chunkVolume];
                System.Buffer.BlockCopy(chunkDatas, chunkOffset, blockIndices, 0, blockIndices.Length * 2);
                for (int iterator = 0; iterator < chunkVolume; iterator++) {
                    ushort blockIndex = blockIndices[iterator];
                    if (chunkPalette.Contains(globalBlockPalette[blockIndex])) {
                        localBlockIndices[iterator] = (ushort)chunkPalette.IndexOf(globalBlockPalette[blockIndex]);
                    } else {
                        chunkPalette.Add(globalBlockPalette[blockIndex]);
                        localBlockIndices[iterator] = (ushort)(chunkPalette.Count - 1);
                    }
                }
                chunkOffset += chunkVolume * 2;

                chunk.LoadChunkData(chunkPalette, localBlockIndices, totalBlocks);
            }
            filestream.Close();
        }

        double totalTime = stopwatch.Elapsed.TotalMilliseconds;
        KINFO("Took " + totalTime.ToString("F2") + "ms to load world from file");

        return true;
    }
}