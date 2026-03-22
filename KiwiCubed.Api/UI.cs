using System.Numerics;

namespace KiwiCubed.Api;

using static KiwiCubed.Api.AssetDefinitions;

public interface IUI {
	public void AddScreen(AssetStringID screenName);
	public void SetCurrentScreen(AssetStringID screenName);
	public void MoveScreenBack();
	public void AddElementToScreen(AssetStringID screenName, IUIElement uiElement);
	public void AddCustomDrawCommandToScreen(AssetStringID screenName, Action<IUIScreen> drawCommand);
	public AssetStringID GetCurrentScreenName();
	public void DisableUI();
	public bool IsDisabled();
	public IInputHandler GetInputHandler();
	public IShader GetUIShader();
	public ITexture GetUIAtlas();
	public IVirtualWindow GetGlobalWindow();
	public IRenderBuffer GetRenderBuffer();
}

public interface IUIScreen {
	public IUI GetUI();
}

public abstract class IUIElement {
	public Vector2 position;
	public Vector2 size;
	protected IUIScreen parentScreen;
	protected bool visible;
	protected bool tabSelected;
	protected bool hoverSelected;

	public IUIElement(Vector2 position, Vector2 size) {
		this.position = position;
		this.size = size;
		visible = true;
	}

	public void AddElementToScreen(IUIScreen uiScreen) {
		parentScreen = uiScreen;
	}

	public virtual void Render() { }

	public virtual void OnClick() {	}

	public virtual void OnEnter() { }

	public virtual void OnHover() { 
	}

	public bool GetVisible() {
		return visible;
	}

	public void SetVisible(bool visible) {
		this.visible = visible;
	}

	public Vector2 GetPosition() {
		return position;
	}

	public Vector2 GetSize() {
		return size;
	}

	public bool GetSelected() {
		return tabSelected || hoverSelected;
	}

	public void SetSelected(bool selected) {
		tabSelected = selected;
	}

	public bool GetHovered() {
		IVirtualWindow globalWindow = parentScreen.GetUI().GetGlobalWindow();
		IInputHandler inputHandler = parentScreen.GetUI().GetInputHandler();

		Vector2 mousePosition = inputHandler.GetMousePosition();
		int windowHeight = (int)globalWindow.GetWidth();

		return mousePosition.X >= position.X && mousePosition.Y >= position.Y && mousePosition.X <= position.X + size.X && mousePosition.Y <= position.Y + size.Y;
	}
}