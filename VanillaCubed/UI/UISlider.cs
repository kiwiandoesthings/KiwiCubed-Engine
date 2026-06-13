namespace VanillaCubed.UI;

using KiwiCubed.Api;
using Silk.NET.Input;
using System.Drawing;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;

public class UISlider : IUIElement {
	private MetaTexture texture;
	private string label;
	private Func<float> getValue;
	private Action<float> setValue;
	private int lowerBound;
	private int upperBound;
	private float clickStartX;
	private float clickStartValue;
	private IInputHandler inputHandler;

	public UISlider(Vector2 position, Vector2 size, MetaTexture texture, string label, Func<float> getValue, Action<float> setValue, int lowerBound, int upperBound) : base(position, size) {
		this.texture = texture;
		this.label = label + ": ";
		this.getValue = getValue;
		this.setValue = setValue;
		this.lowerBound = lowerBound;
		this.upperBound = upperBound;
		clickStartX = -1;
		clickStartValue = 0;
		inputHandler = Meta.Get<IInputHandler>();
	}

	public override void Render() {
		int frame = 1;
		IUI ui = parentScreen.GetUI();
		if ((GetHovered())) {
			if (inputHandler.GetMouseButtonState(MouseButton.Left)) {
				frame = 2;
			}
		}

		float boundWidth = upperBound - lowerBound;
		float modPerPixel = boundWidth / (size.X - 32.0f);

		if (clickStartX != -1) {
			int currentMouseX = (int)inputHandler.GetMousePosition().X;
			float newValue = clickStartValue + (currentMouseX - clickStartX) * modPerPixel;
			if (newValue < lowerBound) {
				newValue = lowerBound;
			} else if (newValue > upperBound) {
				newValue = upperBound;
			}
			setValue(newValue);
		}

		int value = (int)getValue();

		UIImage.Render(parentScreen.GetUI(), position, size, texture, 0);

		float currentOffset = (value - lowerBound) / boundWidth * (size.X - 32.0f);
		UIImage.Render(ui, new Vector2(position.X + currentOffset, position.Y), new Vector2(32, 128), texture, frame);

		Vector2 textDimensions = Renderer.MeasureText(label + value.ToString()) * 2;
		Renderer.DrawText(label + value.ToString(), new Vector2((position.X + size.X / 2) - (textDimensions.X / 2), (position.Y + size.Y / 2) + 24), new Vector2(2.0f), Color.FromArgb(255, 150, 150, 150));
	}

	public override void OnClickDown() {
		clickStartX = (int)inputHandler.GetMousePosition().X;
		clickStartValue = getValue();
	}

	public override void OnClickUp() {
		clickStartX = -1;
	}
}