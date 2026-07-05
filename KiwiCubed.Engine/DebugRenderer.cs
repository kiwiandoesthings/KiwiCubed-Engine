namespace KiwiCubed.Engine;

using Silk.NET.OpenGL;
using System.Runtime.InteropServices;

using static KiwiCubed.Api.Utils;

public static class DebugRenderer {
    public static RenderBuffers chunkDebugBuffers;

    public static void InitializeRenderBuffers() {
        chunkDebugBuffers = new RenderBuffers();
        chunkDebugBuffers.BindArrayObject();
        chunkDebugBuffers.BindVertexBuffer();
        unsafe {
            chunkDebugBuffers.LinkIntAttribute(0, 3, GLEnum.Int, (uint)sizeof(ChunkDebugData), 0);
            chunkDebugBuffers.LinkIntAttribute(1, 1, GLEnum.Int, (uint)sizeof(ChunkDebugData), 12);
        }
    }

    public static unsafe void UpdateChunkDebugBuffers(ChunkDebugData[] chunkDebugData) {
        fixed (void* data = chunkDebugData) {
            chunkDebugBuffers.UpdateVertexBufferData(chunkDebugData.Length * sizeof(ChunkDebugData), data);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ChunkDebugData {
        public IntVector3 chunkPosition;
        public int chunkGenerationState;

        public ChunkDebugData (IntVector3 chunkPosition, int chunkGenerationState) {
            this.chunkPosition = chunkPosition;
            this.chunkGenerationState = chunkGenerationState;
        }
    }
}
