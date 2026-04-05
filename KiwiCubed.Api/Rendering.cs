namespace KiwiCubed.Api;

using System.Drawing;
using System.Numerics;

public static class Renderer {
	private static IRenderer? renderer;
	private static ITextRenderer? textRenderer;

	public static void Initialize(IRenderer rendererImplementation, ITextRenderer textRendererImplementation) {
		renderer = rendererImplementation;
		textRenderer = textRendererImplementation;
	}

	public static void UpdateBuffers(IRenderBuffers renderBuffers, List<float> vertices, List<ushort> indices) => renderer.UpdateBuffers(renderBuffers, vertices, indices);
	public static void UpdateBuffers(IRenderBuffers renderBuffers, GeneralMesh mesh) => renderer.UpdateBuffers(renderBuffers, mesh.vertices, mesh.indices);
	public static void DrawElements(IRenderBuffers renderBuffers, int indicesCount) => renderer.DrawElements(renderBuffers, indicesCount);
	public static void DrawText(string text, Vector2 position, Vector2 scale, Color color) => textRenderer.RenderText(text, position, scale, color);
	public static Vector2 MeasureText(string text) => textRenderer.MeasureText(text);

	public static IRenderBuffers CreateRenderBuffers() => renderer.CreateRenderBuffers();
}

public interface IRenderer {
	public void UpdateBuffers(IRenderBuffers renderBuffers, List<float> vertices, List<ushort> indices);
	public void DrawElements(IRenderBuffers renderBuffers, int indicesCount);
	public IRenderBuffers CreateRenderBuffers();
}

public interface ITextRenderer {
	public void RenderText(string text, Vector2 position, Vector2 scale, Color color);
	public Vector2 MeasureText(string text);
}

public struct GeneralMesh {
	public readonly List<float> vertices;
	public readonly List<ushort> indices;
	public readonly bool positionsAre3D;

	public GeneralMesh(List<float> vertices, List<ushort> indices, bool positionsAre3D) {
		this.positionsAre3D = positionsAre3D;
		int multipleOf = 2 + (positionsAre3D ? 3 : 2);
		if ((float)vertices.Count / multipleOf != vertices.Count / multipleOf) {
			Console.WriteLine("Tried to create a GeneralMesh with {" + vertices.Count + "} vertices which is not a multiple of " + multipleOf + " (" + (multipleOf - 2) + " position and 2 texture coordinates)");
			return;
		}

		this.vertices = vertices;
		this.indices = indices;
	}

	public GeneralMesh() {
		vertices = new();
		indices = new();
	}

	public void UpdateTextureCooordinates(List<float> coordinates) {
		int positionSize = positionsAre3D ? 3 : 2;
		int stride = positionSize + 2;

		if (coordinates.Count != (vertices.Count / stride) * 2) {
			Console.WriteLine("wrong"); // TODO: really need klogger in here
			return;
		}

		int textureCoordsIndex = 0;
		for (int iterator = 0; iterator < vertices.Count; iterator += stride) {
			vertices[iterator + positionSize] = coordinates[textureCoordsIndex++];
			vertices[iterator + positionSize + 1] = coordinates[textureCoordsIndex++];
		}
	}
}