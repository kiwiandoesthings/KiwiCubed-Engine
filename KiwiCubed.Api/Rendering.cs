namespace KiwiCubed.Api;

using System.Drawing;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;

public static class Renderer {
	private static IRenderer? renderer;
	private static ITextRenderer? textRenderer;

	public static void Initialize(IRenderer rendererImplementation, ITextRenderer textRendererImplementation) {
		renderer = rendererImplementation;
		textRenderer = textRendererImplementation;
	}

	public static void UpdateBuffers(IRenderBuffers renderBuffers, float[] vertices, ushort[] indices) => renderer!.UpdateBuffers(renderBuffers, vertices, indices);
	public static void UpdateBuffers(IRenderBuffers renderBuffers, GeneralMesh mesh) => renderer!.UpdateBuffers(renderBuffers, mesh.vertices, mesh.indices);
	public static void DrawElements(IRenderBuffers renderBuffers, int indicesCount) => renderer!.DrawElements(renderBuffers, indicesCount);
	public static void DrawText(string text, Vector2 position, Vector2 scale, Color color) => textRenderer!.RenderText(text, position, scale, color);
	public static void EnqueueRenderTask(Action renderTask) => renderer!.EnqueueRenderTask(renderTask);
    public static Vector2 MeasureText(string text) => textRenderer!.MeasureText(text);

	public static IRenderBuffers CreateRenderBuffers() => renderer!.CreateRenderBuffers();
	public static ICamera CreateCamera() => renderer!.CreateCamera();
}

public interface IRenderer {
	public void UpdateBuffers(IRenderBuffers renderBuffers, float[] vertices, ushort[] indices);
	public void DrawElements(IRenderBuffers renderBuffers, int indicesCount);
	public void EnqueueRenderTask(Action renderTask);
    public IRenderBuffers CreateRenderBuffers();
	public ICamera CreateCamera();
}

public interface ITextRenderer {
	public void RenderText(string text, Vector2 position, Vector2 scale, Color color);
	public Vector2 MeasureText(string text);
}

public readonly struct GeneralMesh {
	public readonly float[] vertices;
	public readonly ushort[] indices;

	public GeneralMesh(List<float> vertices, List<ushort> indices) : this(vertices.ToArray(), indices.ToArray()) { }

	public GeneralMesh(float[] vertices, ushort[] indices) {
		if (vertices.Length % 5 != 0) {
			Logger.ERR("Tried to create a GeneralMesh with {" + vertices.Length + "} vertices which is not a multiple of  5 (3 position and 2 texture coordinates), aborting");
            return;
		}

		this.vertices = vertices;
		this.indices = indices;
	}

	public GeneralMesh() {
		vertices = Array.Empty<float>();
		indices = Array.Empty<ushort>();
	}

    public void UpdateTextureCoordinates(TextureAtlasData atlasData) {
        for (int iterator = 0; iterator < vertices.Length; iterator += 5) {
            vertices[iterator + 3] = atlasData.xPosition + (vertices[iterator + 3] * atlasData.xSize);
            vertices[iterator + 4] = atlasData.yPosition + (vertices[iterator + 4] * atlasData.ySize);
        }
    }

    public void UpdateTextureCooordinates(float[] coordinates) {
		if (coordinates.Length != (vertices.Length / 5) * 2) {
			Logger.ERR("Texture coordinates array has incorrect length, aborting");
			return;
		}

		int textureCoordsIndex = 0;
		for (int iterator = 0; iterator < vertices.Length; iterator += 5) {
			vertices[iterator + 3] = coordinates[textureCoordsIndex++];
			vertices[iterator + 4] = coordinates[textureCoordsIndex++];
		}
	}
}