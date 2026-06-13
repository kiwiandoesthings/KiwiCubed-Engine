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
	public static Vector2 MeasureText(string text) => textRenderer!.MeasureText(text);

	public static IRenderBuffers CreateRenderBuffers() => renderer!.CreateRenderBuffers();
	public static ICamera CreateCamera() => renderer!.CreateCamera();
}

public interface IRenderer {
	public void UpdateBuffers(IRenderBuffers renderBuffers, float[] vertices, ushort[] indices);
	public void DrawElements(IRenderBuffers renderBuffers, int indicesCount);
	public IRenderBuffers CreateRenderBuffers();
	public ICamera CreateCamera();
}

public interface ITextRenderer {
	public void RenderText(string text, Vector2 position, Vector2 scale, Color color);
	public Vector2 MeasureText(string text);
}

public struct GeneralMesh {
	public readonly float[] vertices;
	public readonly ushort[] indices;
	public readonly bool positionsAre3D;

	public GeneralMesh(List<float> vertices, List<ushort> indices, bool positionsAre3D) {
		this.positionsAre3D = positionsAre3D;
		int multipleOf = 2 + (positionsAre3D ? 3 : 2);
		if ((float)vertices.Count / multipleOf != vertices.Count / multipleOf) {
			Console.WriteLine("Tried to create a GeneralMesh with {" + vertices.Count + "} vertices which is not a multiple of " + multipleOf + " (" + (multipleOf - 2) + " position and 2 texture coordinates)");
			return;
		}

		this.vertices = vertices.ToArray();
		this.indices = indices.ToArray();
	}

	public GeneralMesh(float[] vertices, ushort[] indices, bool positionsAre3D) {
		this.positionsAre3D = positionsAre3D;
		int multipleOf = 2 + (positionsAre3D ? 3 : 2);
		if ((float)vertices.Length / multipleOf != vertices.Length / multipleOf) {
			Console.WriteLine("Tried to create a GeneralMesh with {" + vertices.Length + "} vertices which is not a multiple of " + multipleOf + " (" + (multipleOf - 2) + " position and 2 texture coordinates)");
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
        int positionSize = positionsAre3D ? 3 : 2;
        int stride = positionSize + 2;

        for (int iterator = 0; iterator < vertices.Length; iterator += stride) {
            vertices[iterator + positionSize] = atlasData.xPosition + (vertices[iterator + positionSize] * atlasData.xSize);
            vertices[iterator + positionSize + 1] = atlasData.yPosition + (1.0f - vertices[iterator + positionSize + 1] * atlasData.ySize);
        }
    }

	public void UpdateTextureCooordinates(float[] coordinates) {
		int positionSize = positionsAre3D ? 3 : 2;
		int stride = positionSize + 2;

		if (coordinates.Length != (vertices.Length / stride) * 2) {
			Console.WriteLine("wrong"); // TODO: really need klogger in here
			return;
		}

		int textureCoordsIndex = 0;
		for (int iterator = 0; iterator < vertices.Length; iterator += stride) {
			vertices[iterator + positionSize] = coordinates[textureCoordsIndex++];
			vertices[iterator + positionSize + 1] = coordinates[textureCoordsIndex++];
		}
	}
}