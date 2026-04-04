namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using Silk.NET.Input;
using Silk.NET.OpenGL;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.KLogger;

public class UI : IUI {
	private readonly GL gl;
	private readonly InputHandler inputHandler;
	private readonly Shader uiShader;
	private readonly Texture uiAtlas;
	private readonly VirtualWindow globalWindow;

	private readonly VertexArrayObject vertexArrayObject;
	private readonly VertexBufferObject vertexBufferObject;
	private readonly IndexBufferObject indexBufferObject;

	private List<UIScreen> uiScreens;
	private Dictionary<AssetStringID, int> screenNameToIndex;
	private UIScreen? currentScreen;
	private Stack<UIScreen> stackedScreens;

	public unsafe UI(Shader uiShader, Texture uiAtlas) {
		gl = SystemsManager.Get<GL>();
		inputHandler = (InputHandler)SystemsManager.Get<IInputHandler>();
		this.uiShader = uiShader;
		this.uiAtlas = uiAtlas;
		globalWindow = (VirtualWindow)SystemsManager.Get<IVirtualWindow>();

		vertexArrayObject = new VertexArrayObject();
		vertexBufferObject = new VertexBufferObject();
		indexBufferObject = new IndexBufferObject();
		vertexArrayObject.LinkAttribute(vertexBufferObject, 0, 2, VertexAttribPointerType.Float, false, sizeof(float) * 4, (void*)0);
		vertexArrayObject.LinkAttribute(vertexBufferObject, 1, 2, VertexAttribPointerType.Float, false, sizeof(float) * 4, (void*)(sizeof(float) * 2));

		uiScreens = new();
		screenNameToIndex = new();
		stackedScreens = new();

		inputHandler.RegisterMouseButtonCallback(MouseButton.Left, (MouseButton button) => {
			if (currentScreen == null) {
				return;
			}

			List<IUIElement> elements = currentScreen.GetUIElements();
			for (int iterator = 0; iterator < elements.Count; ++iterator) {
				IUIElement uiElement = elements[iterator];
				if (uiElement.GetHovered()) {
					uiElement.OnClickDown();
				}
			}
		}, true);
        inputHandler.RegisterMouseButtonCallback(MouseButton.Left, (MouseButton button) => {
            if (currentScreen == null) {
                return;
            }

            List<IUIElement> elements = currentScreen.GetUIElements();
            for (int iterator = 0; iterator < elements.Count; ++iterator) {
                IUIElement uiElement = elements[iterator];
				uiElement.OnClickUp();
            }
        }, false);
        inputHandler.RegisterKeyCallback(Key.Tab, (Key key) => {
			int totalElements = currentScreen.GetUIElements().Count;
			int tabIndex = currentScreen.GetTabIndex();
			if (tabIndex + 2 > totalElements) {
				currentScreen.SetTabIndex(0);
			} else {
				currentScreen.SetTabIndex(tabIndex + 1);
			}
		}, true);
		inputHandler.RegisterKeyCallback(Key.Enter, (Key key) => {
			if (currentScreen.GetTabIndex() == -1) {
				return;
			}

			IUIElement uiElement = currentScreen.GetUIElements()[currentScreen.GetTabIndex()];
			uiElement.OnEnter();
		}, true);

		SystemsManager.Register<IUI>(this);
	}

	public void Render() {
		if (currentScreen == null) {
			return;
		}

		uiAtlas.SetActive();
		uiAtlas.Bind();
        gl.Disable(EnableCap.DepthTest);
        stackedScreens.Peek().Render();
		gl.Enable(EnableCap.DepthTest);
    }

	public void AddScreen(AssetStringID screenName) {
		OVERRIDE_LOG_NAME("UI");
		for (int iterator = 0; iterator < uiScreens.Count; ++iterator) {
			if (uiScreens[iterator].name == screenName) {
				KCRITICAL("Tried to register UI screen with same name \"" + screenName + "\" twice, aborting");
				//psnip_trap();
			}
		}
		uiScreens.Add(new UIScreen(screenName));
		screenNameToIndex.Add(screenName, uiScreens.Count - 1);
	}

	public void SetCurrentScreen(AssetStringID screenName) {
		OVERRIDE_LOG_NAME("UI");
		UIScreen? uiScreen = GetScreen(screenName);
		if (uiScreen == null) {
			KERR("Tried to set current screen to a screen with name " + screenName + " that didn't exist");
			return;
		}
		stackedScreens.Push(uiScreen);
		currentScreen = uiScreen;
		globalWindow.SetFocused(false);
	}

	public void AddElementToScreen(AssetStringID screenName, IUIElement uiElement) {
		UIScreen? uiScreen = GetScreen(screenName);
		if (uiScreen == null) {
			KERR("Tried to add a UI element to a screen with name " + screenName + " that didn't exist");
			return;
		}
		uiScreen.AddUIElement(uiElement);
	}

	public void AddCustomDrawCommandToScreen(AssetStringID screenName, Action<IUIScreen> drawCommand) {
		GetScreen(screenName).AddCustomRenderCommand(drawCommand);
	}

	public void MoveScreenBack() {
		if (currentScreen == null) {
			return;
		}

		stackedScreens.Pop();
		if (stackedScreens.Count != 0) {
			currentScreen = stackedScreens.Peek();
		} else {
			currentScreen = null;
			// big idea, but some kind of layer system
			// something with world and ui layer and such so that i can easily do things like see if mouse should be visible
			// because this is dumb and kind of assumes stuff that it shouldnt
			// id rather do something like ``layers.LoseContext(this);``
			globalWindow.SetFocused(true);
		}
	}

	public UIScreen? GetScreen(AssetStringID screenName) {
		OVERRIDE_LOG_NAME("UI");
		if (screenNameToIndex.TryGetValue(screenName, out int screenIndex)) {
			return uiScreens[screenIndex];
		}
		KCRITICAL("Tried to get UIScreen with name " + screenName + " that did not exist");
		//psnip_trap();
		return null;
	}

	public UIScreen GetCurrentScreen() {
		return currentScreen;
	}

	public AssetStringID GetCurrentScreenName() {
		if (currentScreen == null) {
			return new AssetStringID();
		}
		return currentScreen.name;
	}

	public void DisableUI() {
		stackedScreens = new();
		currentScreen = null;

		globalWindow.SetFocused(true);
	}

	public bool IsDisabled() {
		return currentScreen == null;
	}

	public IInputHandler GetInputHandler() {
		return (IInputHandler)inputHandler;
	}

	public IShader GetUIShader() {
		return (IShader)uiShader;
	}

	public ITexture GetUIAtlas() {
		return (ITexture)uiAtlas;
	}

	public IVirtualWindow GetGlobalWindow() {
		return (IVirtualWindow)globalWindow;
	}

	public IRenderBuffers GetRenderBuffers() {
		return (IRenderBuffers)(new RenderBuffers(vertexArrayObject, vertexBufferObject, indexBufferObject));
	}

	public VertexArrayObject GetVertexArrayObject() {
		return vertexArrayObject;
	}

	public VertexBufferObject GetVertexBufferObject() {
		return vertexBufferObject;
	}

	public IndexBufferObject GetIndexBufferObject() {
		return indexBufferObject;
	}

	public void Delete() {
		OVERRIDE_LOG_NAME("UI");
		KINFO("Deleting screens");
		for (int iterator = 0; iterator < uiScreens.Count; ++iterator) {
			uiScreens[iterator].Dispose();
		}
		uiScreens.Clear();
	}
}

public class UIScreen : IUIScreen, IDisposable {
	public readonly AssetStringID name;
	private UI ui;
	private VertexArrayObject vertexArrayObject;
	private VertexBufferObject vertexBufferObject;
	private IndexBufferObject indexBufferObject;
	private List<IUIElement> uiElements;
	private List<Action<UIScreen>> customRenderCommands;
	private int tabIndex;

	public UIScreen(AssetStringID screenName) {
		OVERRIDE_LOG_NAME("UI");
		name = screenName;
		ui = (UI)SystemsManager.Get<IUI>();
		vertexArrayObject = ui.GetVertexArrayObject();
		vertexBufferObject = ui.GetVertexBufferObject();
		indexBufferObject = ui.GetIndexBufferObject();
		uiElements = new();
		customRenderCommands = new();
		tabIndex = 0;

		KINFO("Successfully created ui screen with name " + screenName);
	}

	public void Render() {
		for (int iterator = 0; iterator < customRenderCommands.Count; iterator++) {
			customRenderCommands[iterator](this);
		}
		for (int iterator = 0; iterator < uiElements.Count; iterator++) {
			if (uiElements[iterator].GetVisible()) {
				uiElements[iterator].Render();
			}
		}
	}

	public void AddCustomRenderCommand(Action<UIScreen> command) {
		customRenderCommands.Add(command);
	}

	public void ClearCustomRenderCommands() {
		customRenderCommands.Clear();
	}

	public void AddUIElement(IUIElement uiElement) {
		uiElements.Add(uiElement);
		uiElement.AddElementToScreen((IUIScreen)this);
	}

	public int GetTabIndex() {
		return tabIndex;
	}

	public void SetTabIndex(int newTabIndex) {
		tabIndex = newTabIndex;

		if (tabIndex == 0) {
			uiElements[uiElements.Count - 1].SetSelected(false);
		} else {
			uiElements[tabIndex - 1].SetSelected(false);
		}
		uiElements[tabIndex].SetSelected(true);
	}

	public IUI GetUI() {
		return (IUI)ui;
	}

	public List<IUIElement> GetUIElements() {
		return uiElements;
	}

	public void Dispose() {
		OVERRIDE_LOG_NAME("UI Screen");

		uiElements.Clear();

		KINFO("Deleted screen \"" + name + "\" with {" + uiElements.Count + "} elements");
	}
}