namespace VanillaCubed.UI;

using KiwiCubed.Api;
using Silk.NET.Input;
using System.Drawing;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;

public class UIButton : IUIElement {
	private Action? triggerFunction;
	private MetaTexture image;
	private string label;
	private int frame;

	public UIButton(Vector2 position, Vector2 size, Action? triggerFunction, MetaTexture image, string label) : base(position, size) {
		this.triggerFunction = triggerFunction;
		this.image = image;
		this.label = label;
		frame = 0;
	}

	public void Trigger() {
		if (triggerFunction != null) {
			triggerFunction();
		}
	}

	public override void OnClickDown() {
		Trigger();
	}

	public override void OnEnter() {
		Trigger();
	}

	public override void Render() {
		IUI ui = parentScreen.GetUI();
		if ((GetHovered())) {
			if (ui.GetInputHandler().GetMouseButtonState(MouseButton.Left)) {
				frame = 2;
			} else {
				frame = 1;
			}
		} else if (tabSelected) {
			frame = 1;
		} else {
			frame = 0;
		}

		TextureAtlasData atlasData = image.atlasDatas[(int)frame];

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

		if (label != "") {
			Vector2 textDimensions = Renderer.MeasureText(label) * 2;
			Renderer.DrawText(label, new Vector2((position.X + size.X / 2) - (textDimensions.X / 2), (position.Y + size.Y / 2) + 24), new Vector2(2.0f), Color.FromArgb(255, 150, 150, 150));
		}
	}
}