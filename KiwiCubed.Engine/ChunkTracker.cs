namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using static KiwiCubed.Api.Utils;

public class ChunkTracker {
    private KLogger logger;
    private Dictionary<IntVector3, ushort> chunkReferences;

    public ChunkTracker() {
        logger = new KLogger("ChunkTracker");
        chunkReferences = [];
    }

    public void AddChunkReferences(IntVector3 chunkPosition, int referenceCount = 0) {
        ref ushort references = ref CollectionsMarshal.GetValueRefOrAddDefault(chunkReferences, chunkPosition, out bool exists);
        references += (ushort)referenceCount;
    }

    public void RemoveChunkReferences(IntVector3 chunkPosition, int referenceCount = 0) {
        ref ushort references = ref CollectionsMarshal.GetValueRefOrNullRef(chunkReferences, chunkPosition);
        if (!Unsafe.IsNullRef(ref references)) {
            if (referenceCount > references) {
                logger.ERR("Tried to remove {" + referenceCount + "} references from a chunk at " + chunkPosition + " that only had {" + references + "} references");
                logger.BREAK();
            } else {
                references -= (ushort)referenceCount;
            }
            if (references == 0) {
                chunkReferences.Remove(chunkPosition);
            }
        } else {
            logger.ERR("Tried to remove {" + referenceCount + "} references from a chunk at " + chunkPosition + " that didn't have any references");
        }
    }
}