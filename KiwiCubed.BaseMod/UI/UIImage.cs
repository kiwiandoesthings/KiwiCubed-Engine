namespace VanillaCubed.UI;

using KiwiCubed.Api;
using Silk.NET.Input;
using System.Drawing;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;

public class UIImage : IUIElement {
	private MetaTexture image;
	private int frameIndex;

	public UIImage(Vector2 position, Vector2 size, MetaTexture image, int frameIndex = 0) : base(position, size) {
		this.image = image;
		this.frameIndex = frameIndex;
	}

	public override void Render() {
		Render(parentScreen.GetUI(), position, size, image, frameIndex);
	}

	public static void Render(IUI ui, Vector2 position, Vector2 size, MetaTexture image, int frameIndex) {
		TextureAtlasData atlasData = image.atlasDatas[frameIndex];

		ITexture uiAtlas = ui.GetUIAtlas();

		uiAtlas.SetActive();
		uiAtlas.Bind();

		float[] vertices = [
		    // Positions      // Texture Coordinates
		    0.0f, 0.0f, atlasData.xPosition, atlasData.yPosition,
			1.0f, 0.0f, atlasData.xPosition + atlasData.xSize, atlasData.yPosition,
			1.0f, 1.0f, atlasData.xPosition + atlasData.xSize, atlasData.yPosition + atlasData.ySize,
			0.0f, 1.0f, atlasData.xPosition, atlasData.yPosition + atlasData.ySize
		];

		ushort[] indices = [
			0, 1, 2,
			2, 3, 0,
		];

		ui.GetUIShader().Bind();

		IRenderBuffers renderBuffers = ui.GetRenderBuffers();

		Renderer.UpdateBuffers(renderBuffers, vertices, indices);

		Matrix4x4 modelMatrix = Matrix4x4.CreateScale(new Vector3(size.X, size.Y, 1.0f)) * Matrix4x4.CreateTranslation(new Vector3(position.X, position.Y, 0.0f));
		Matrix4x4 projection = Matrix4x4.CreateOrthographicOffCenter(0, ui.GetGlobalWindow().GetWidth(), ui.GetGlobalWindow().GetHeight(), 0, -1.0f, 1.0f);
		ui.GetUIShader().SetMatrix4("modelMatrix", modelMatrix);
		ui.GetUIShader().SetMatrix4("projectionMatrix", projection);

		Renderer.DrawElements(renderBuffers, indices.Length);
	}
}