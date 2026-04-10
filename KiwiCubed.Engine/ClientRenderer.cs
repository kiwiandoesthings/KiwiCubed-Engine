namespace KiwiCubed.Engine;

using ArchEntity = Arch.Core.Entity;
using ArchWorld = Arch.Core.World;
using ImGuiNET;
using KiwiCubed.Api;
using Silk.NET.OpenGL;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Api.IPlayer;
using static KiwiCubed.Api.Util;

public static class ClientRenderer {
	static Dictionary<IntVector3, ValueTuple<RenderBuffers, int>> chunkBuffers = new();
    static Texture gameAtlas = null;
    static Shader terrainShader = null;
    static Shader entityShader = null;
    static float partialTicks = 0.0f;

    public static void SetupRenderResources() {
        AssetManager assetManager = (AssetManager)MetaHandler.Get<IAssetManager>();
        gameAtlas = assetManager.GetTextureAtlas(new AssetStringID("kiwicubed", "atlas/main"));
        terrainShader = (Shader)assetManager.GetShader(new AssetStringID("kiwicubed", "shader/terrain"));
        entityShader = (Shader)assetManager.GetShader(new AssetStringID("kiwicubed", "shader/entity"));
    }

    public static void RenderWorld() {
		World world = (World)MetaHandler.Get<ISingleplayerHandler>().GetWorld();
		RenderImGui(world);
		RenderWorldChunks(world);
		RenderWorldEntities(world);
	}

	public static void UpdateBuffers() {
		World world = (World)MetaHandler.Get<ISingleplayerHandler>().GetWorld();
        ChunkHandler chunkHandler = (ChunkHandler)world.GetChunkHandler();
        lock (chunkHandler.GetChunkMutex()) {
            foreach (KeyValuePair<IntVector3, IChunk> chunkPair in chunkHandler.GetChunks()) {
                Chunk chunk = (Chunk)chunkPair.Value;
                if (!chunkBuffers.ContainsKey(chunkPair.Key)) {
                    AllocateChunkData(chunkPair.Key);
                }
                if (chunk.IsDirty() && !chunk.IsMeshing()) {
                    ValueTuple<List<float>, List<ushort>> meshData = chunk.LiftMeshData();
                    UpdateChunkData(chunkPair.Key, meshData.Item1, meshData.Item2);
                }
            }
        }
    }

	public unsafe static void AllocateChunkData(IntVector3 chunkPosition) {
		if (chunkBuffers.ContainsKey(chunkPosition)) {
			KERR("Tried to allocate already allocated buffers for chunk at position " + chunkPosition);
			return;
		}

        RenderBuffers renderBuffers = new RenderBuffers();
        uint stride = 5 * sizeof(float);
        renderBuffers.LinkAttribute(0, 3, VertexAttribPointerType.Float, stride, 0);
        renderBuffers.LinkAttribute(1, 2, VertexAttribPointerType.Float, stride, sizeof(float) * 3);
        chunkBuffers.Add(chunkPosition, new ValueTuple<RenderBuffers, int>(renderBuffers, 0));
    }

	public static void UpdateChunkData(IntVector3 chunkPosition, List<float> vertices, List<ushort> indices) {
		if (chunkBuffers.TryGetValue(chunkPosition, out ValueTuple<RenderBuffers, int> chunkBuffersPair)) {
			Renderer.UpdateBuffers(chunkBuffersPair.Item1, vertices, indices);
			chunkBuffers[chunkPosition] = new ValueTuple<RenderBuffers, int>(chunkBuffersPair.Item1, indices.Count);
		} else {
			KERR("Tried to update none-existent buffers for chunk at position " + chunkPosition);
		}
	}

	public static void UnloadChunkData(IntVector3 chunkPosition) {
		if (!chunkBuffers.Remove(chunkPosition)) {
			KERR("Tried to unload non-existent buffers for chunk at position " + chunkPosition);
		}
	}

	private static void RenderImGui(World world) {
        ChunkHandler chunkHandler = (ChunkHandler)world.GetChunkHandler();
        ArchWorld archWorld = world.GetEntityManager().GetArchWorld();
        ArchEntity player = world.GetPlayers()[0];
        if (ImGui.CollapsingHeader("Player Info")) {
            EntityTransform playerTransform = archWorld.Get<EntityTransform>(player);
            EntityPhysicalComponent physicalComponent = archWorld.Get<EntityPhysicalComponent>(player);
            EntityPlayerComponent playerComponent = archWorld.Get<EntityPlayerComponent>(player);
            //ImGui.Text("Player name: " + playerComponent.name);
            //ImGui.Text("Player AUID: " + player.GetProtectedEntityData().AUID);
            ImGui.Text("Player gamemode: " + playerComponent.gameMode);
            //ImGui.Text("Player health: " + player.GetEntityStats().health);
            ImGui.Text("Player position: " + playerTransform.position);
            ImGui.Text("Player orientation: " + playerTransform.orientation);
            ImGui.Text("Player velocity: " + playerTransform.velocity);
            ImGui.Text("Player grounded: " + physicalComponent.isGrounded);
            ImGui.Text("Player jumping: " + physicalComponent.isJumping);
            ImGui.Text("Global chunk position: " + playerTransform.globalChunkPosition);
            ImGui.Text("Local chunk position: " + playerTransform.localChunkPosition);
            //ImGui.Text("Current chunk info: " + ((Chunk)chunkHandler.GetChunk(playerTransform.globalChunkPosition, false)).GetImGuiText());
        }

        if (ImGui.CollapsingHeader("World Info")) {
            world.GetTickInfo(out float realTps, out int targetTps, out ulong totalTicks, out long lastTickTime, out float partialTicks);
            ClientRenderer.partialTicks = partialTicks;
            ImGui.Text("TPS: " + realTps.ToString("F2"));
            ImGui.Text("Target TPS: " + targetTps);
            ImGui.Text("Total ticks: " + totalTicks);
            ImGui.Text("Last tick time: " + lastTickTime);
            ImGui.Text("Partial ticks: " + partialTicks.ToString("F2"));
            ImGui.Text("Total chunks: " + chunkHandler.GetChunks().Count);
        
        	if (ImGui.CollapsingHeader("Chunks")) {
                lock (chunkHandler.GetChunkMutex()) {
                    foreach (KeyValuePair<IntVector3, IChunk> chunkPair in chunkHandler.GetChunks()) {
                        Chunk chunk = (Chunk)chunkPair.Value;
                        ImGui.Text(chunk.GetImGuiText());
                    }
                }
            }
        }
    }

    private static void RenderWorldChunks(World world) {
        gameAtlas.Bind();
        terrainShader.Bind();

        foreach (KeyValuePair<IntVector3, ValueTuple<RenderBuffers, int>> chunkBuffersPair in chunkBuffers) {
			Renderer.DrawElements(chunkBuffersPair.Value.Item1, chunkBuffersPair.Value.Item2);
		}
	}

	private static void RenderWorldEntities(World world) {
        entityShader.Bind();

        //gl.Disable(EnableCap.CullFace);
        //QueryDescription query = new QueryDescription().WithAll<EntityRenderableComponent>();
        //archWorld.Query(in query, (ref EntityRenderableComponent renderableComponent, ref EntityTransform transformComponent) => {
        //	if (renderableComponent.visible) {
        //		Vector3 interpolatedPosition = renderableComponent.oldPosition + (transformComponent.position - renderableComponent.oldPosition) * partialTicks;
        //		Vector3 interpolatedOrientation = renderableComponent.oldOrientation + (transformComponent.orientation - renderableComponent.oldOrientation) * partialTicks;
        //		Vector3 interpolatedPositionOffset = renderableComponent.oldPositionOffset + (renderableComponent.positionOffset - renderableComponent.oldPositionOffset) * partialTicks;
        //		Vector3 interpolatedOrientationOffset = renderableComponent.oldOrientationOffset + (renderableComponent.orientationOffset - renderableComponent.oldOrientationOffset) * partialTicks;
        //        
        //        Vector3 renderPosition = interpolatedPosition + interpolatedPositionOffset;
        //        Vector3 renderOrientation = interpolatedOrientation + interpolatedOrientationOffset;
        //
        //		Matrix4x4 modelMatrix = Matrix4x4.CreateRotationX(renderOrientation.X) * Matrix4x4.CreateRotationY(renderOrientation.Y) * Matrix4x4.CreateRotationZ(renderOrientation.Z) * Matrix4x4.CreateScale(renderableComponent.renderScale) * Matrix4x4.CreateTranslation(renderPosition);
        //		entityShader.SetMatrix4("modelMatrix", modelMatrix);
        //		Renderer.DrawElements((RenderBuffers)renderableComponent.renderBuffers, renderableComponent.mesh.indices.Count);
        //	}
        //});
        //gl.Enable(EnableCap.CullFace);
    }
}