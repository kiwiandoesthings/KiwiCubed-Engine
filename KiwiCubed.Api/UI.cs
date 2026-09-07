using System.Numerics;

namespace KiwiCubed.Api;

using static KiwiCubed.Api.AssetDefinitions;

public interface IUI {
	public void AddScreen(UIScreenDefinition screenDefinition);
	public void SetCurrentScreen(AssetStringID screenName);
	public void MoveScreenBack();
	public void ArrangeElements();
	public AssetStringID GetCurrentScreenName();
	public void DisableUI();
	public bool IsDisabled();
	public IInputHandler GetInputHandler();
	public IShader GetUIShader();
	public ITexture GetUIAtlas();
	public IVirtualWindow GetGlobalWindow();
	public IRenderBuffers GetRenderBuffers();
}

public interface IUIScreen {
	public IUI GetUI();
}

public struct UIScreenDefinition {
	public readonly AssetStringID screenName;
	public readonly Action<List<UIElement>> screenConstructor;
	public readonly Action<List<UIElement>>? screenDestructor;

	public UIScreenDefinition(AssetStringID screenName, Action<List<UIElement>> screenConstructor, Action<List<UIElement>>? screenDestructor = null) {
        this.screenName = screenName;
        this.screenConstructor = screenConstructor;
        this.screenDestructor = screenDestructor;
    }
}

// UI is really messy right now because it's a complicated system and I'm not sure how to handle everything yet
// Stuff like tab selection should be handled by the screen solely and stuff and its just kinda all over the place right now
public abstract class UIElement {
	public static ILogger logger = ILogger.CreateLogger("UI");

	public Vector2 position;
	public Vector2 size;
	protected IUIScreen parentScreen;
	protected List<UIElement> children;
	protected bool visible;
	protected bool tabSelected;
	protected bool hoverSelected;

	public UIElement(Vector2 size) {
		this.size = size;
		children = [];
		visible = true;
	}

	public void AddElementToScreen(IUIScreen uiScreen) {
		parentScreen = uiScreen;

		foreach (UIElement child in children) {
			child.AddElementToScreen(parentScreen);
		}
    }

	public void AddChildElement(UIElement child) {
		if (children.Contains(child)) {
			logger.WARN("Tried to add multiple of the same child element to the same parent. Elements are not reusable");
			return;
		}

		children.Add(child);
	}

	public void RecalculateElement(Vector2 position, Vector2 size) {
		this.position = position;
		this.size = size;

		ArrangeChildren();
	}

	public virtual void ArrangeChildren() {	}

	public virtual void Render() { }

    public virtual void OnClickUp() { }

    public virtual void OnClickDown() {	}

	public virtual void OnEnter() { }

	public bool GetVisible() {
		return visible;
	}

	public void SetVisible(bool visible) {
		this.visible = visible;
	}

	public IUIScreen GetParentScreen() {
		return parentScreen;
	}

	public List<UIElement> GetChildren() {
		return children;
	}

	public bool GetSelected() {
		return tabSelected || hoverSelected;
	}

	public void SetSelected(bool selected) {
		tabSelected = selected;
	}

	public bool GetHovered() {
		if (!visible) {
            return false;
        }

		IVirtualWindow globalWindow = parentScreen.GetUI().GetGlobalWindow();
		IInputHandler inputHandler = parentScreen.GetUI().GetInputHandler();

		Vector2 mousePosition = inputHandler.GetMousePosition();
		int windowHeight = (int)globalWindow.GetWidth();

		return mousePosition.X >= position.X && mousePosition.Y >= position.Y && mousePosition.X <= position.X + size.X && mousePosition.Y <= position.Y + size.Y;
	}
}