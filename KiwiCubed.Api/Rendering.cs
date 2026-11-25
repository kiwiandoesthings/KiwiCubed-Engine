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

	public static void UpdateBuffers<T1, T2>(IRenderBuffers renderBuffers, ReadOnlySpan<T1> vertices, ReadOnlySpan<T2> indices) where T1: unmanaged where T2: unmanaged => renderer!.UpdateBuffers(renderBuffers, vertices, indices);
	public static void UpdateBuffers<T>(IRenderBuffers renderBuffers, GeneralMesh<T> mesh) where T: unmanaged => renderer!.UpdateBuffers(renderBuffers, mesh.vertices, mesh.indices);
	public static void DrawElements(IRenderBuffers renderBuffers, int indicesCount) => renderer!.DrawElements(renderBuffers, indicesCount);
	public static void RenderText(string text, Vector2 position, Vector2 scale, Color color) => textRenderer!.RenderText(text, position, scale, color);
    public static Vector2 MeasureText(string text) => textRenderer!.MeasureText(text);

	public static IRenderBuffers CreateRenderBuffers() => renderer!.CreateRenderBuffers();
	public static ICamera CreateCamera() => renderer!.CreateCamera();

    public static GeneralMesh<T> UpdateTextureCoordinates<T>(this GeneralMesh<T> mesh, TextureAtlasData atlasData) where T : unmanaged, ITexturedVertex<T> {
        T[] newVertices = new T[mesh.vertices.Length];
        for (int iterator = 0; iterator < mesh.vertices.Length; iterator++) {
            newVertices[iterator] = mesh.vertices[iterator].UpdateTextureCoordinates(atlasData);
        }

        return new GeneralMesh<T>(newVertices, mesh.indices);
    }
}

public interface IRenderer {
	public void UpdateBuffers<T1, T2>(IRenderBuffers renderBuffers, ReadOnlySpan<T1> vertices, ReadOnlySpan<T2> indices) where T1: unmanaged where T2: unmanaged;
	public void DrawElements(IRenderBuffers renderBuffers, int indicesCount);
    public IRenderBuffers CreateRenderBuffers();
	public ICamera CreateCamera();
}

public interface ITextRenderer {
	public void RenderText(string text, Vector2 position, Vector2 scale, Color color);
	public Vector2 MeasureText(string text);
}

public readonly struct GeneralMesh<T> where T : unmanaged {
    public readonly T[] vertices;
    public readonly ushort[] indices;

    public GeneralMesh(IList<T> vertices, IList<ushort> indices) {
        this.vertices = [.. vertices];
        this.indices = [.. indices];
    }

    public GeneralMesh() {
        vertices = [];
        indices = [];
    }
}

public interface ITexturedVertex<T> where T: unmanaged {
    T UpdateTextureCoordinates(TextureAtlasData atlasData);
}

public readonly struct Vertex3D : ITexturedVertex<Vertex3D> {
	public readonly float x;
	public readonly float y;
	public readonly float z;
	public readonly float u;
	public readonly float v;

	public Vertex3D(float x, float y, float z, float u, float v) {
		this.x = x;
		this.y = y;
		this.z = z;
		this.u = u;
		this.v = v;
	}

    public Vertex3D UpdateTextureCoordinates(TextureAtlasData atlasData) {
        float newU = atlasData.xPosition + (u * atlasData.xSize);
        float newV = atlasData.yPosition + (v * atlasData.ySize);
        return new Vertex3D(x, y, z, newU, newV);
    }
}

public readonly struct Vertex2D : ITexturedVertex<Vertex2D> {
	public readonly float x;
	public readonly float y;
	public readonly float u;
	public readonly float v;

    public Vertex2D(float x, float y, float u, float v) {
        this.x = x;
        this.y = y;
        this.u = u;
        this.v = v;
    }

    public Vertex2D UpdateTextureCoordinates(TextureAtlasData atlasData) {
        float newU = atlasData.xPosition + (u * atlasData.xSize);
        float newV = atlasData.yPosition + (v * atlasData.ySize);
        return new Vertex2D(x, y, newU, newV);
    }
}

public readonly struct VertexTerrain : ITexturedVertex<VertexTerrain> {
    public readonly float x;
    public readonly float y;
    public readonly float z;
    public readonly float u;
    public readonly float v;
    public readonly byte ao;

    public VertexTerrain(float x, float y, float z, float u, float v, byte ao) {
        this.x = x;
        this.y = y;
        this.z = z;
        this.u = u;
        this.v = v;
        this.ao = ao;
    }

    public VertexTerrain UpdateTextureCoordinates(TextureAtlasData atlasData) {
        float newU = atlasData.xPosition + (u * atlasData.xSize);
        float newV = atlasData.yPosition + (v * atlasData.ySize);
        return new VertexTerrain(x, y, z, newU, newV, ao);
    }
}