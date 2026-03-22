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

	public static void UpdateBuffers(IRenderBuffer renderBuffer, List<float> vertices, List<ushort> indices) => renderer.UpdateBuffers(renderBuffer, vertices, indices);
	public static void DrawElements(IRenderBuffer renderBuffer, int indicesCount) => renderer.DrawElements(renderBuffer, indicesCount);
	public static void DrawText(string text, Vector2 position, Vector2 scale, Color color) => textRenderer.RenderText(text, position, scale, color);
	public static Vector2 MeasureText(string text) => textRenderer.MeasureText(text);
}

public interface IRenderer {
	public void UpdateBuffers(IRenderBuffer renderBuffer, List<float> vertices, List<ushort> indices);
	public void DrawElements(IRenderBuffer renderBuffer, int indicesCount);
}

public interface ITextRenderer {
	public void RenderText(string text, Vector2 position, Vector2 scale, Color color);
	public Vector2 MeasureText(string text);
}