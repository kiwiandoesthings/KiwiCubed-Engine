namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.Numerics;
using static KiwiCubed.Api.AssetDefinitions;

// TODO: make ui less forgiving and throw errors from calling w/ bad preconditions
public class UI : IUI {
	private readonly KLogger logger;
	private readonly GL gl;
	private readonly InputHandler inputHandler;
	private readonly Shader uiShader;
	private readonly Texture uiAtlas;
	private readonly VirtualWindow globalWindow;

	private readonly VertexArrayObject vertexArrayObject;
	private readonly VertexBufferObject vertexBufferObject;
	private readonly IndexBufferObject indexBufferObject;

	private List<UIScreenDefinition> uiScreens;
	private Dictionary<AssetStringID, int> screenNameToIndex;
	private UIScreen? currentScreen;
	private Stack<AssetStringID> stackedScreens;

	public unsafe UI(Shader uiShader, Texture uiAtlas) {
		logger = new KLogger("UI");
		gl = MetaHandler.Get<GL>();
		inputHandler = (InputHandler)MetaHandler.Get<IInputHandler>();
		this.uiShader = uiShader;
		this.uiAtlas = uiAtlas;
		globalWindow = (VirtualWindow)MetaHandler.Get<IVirtualWindow>();

		vertexArrayObject = new VertexArrayObject();
		vertexBufferObject = new VertexBufferObject();
		indexBufferObject = new IndexBufferObject();
		vertexArrayObject.LinkAttribute(vertexBufferObject, 0, 2, VertexAttribPointerType.Float, false, sizeof(float) * 4, (void*)0);
		vertexArrayObject.LinkAttribute(vertexBufferObject, 1, 2, VertexAttribPointerType.Float, false, sizeof(float) * 4, (void*)(sizeof(float) * 2));

		uiScreens = [];
		screenNameToIndex = [];
		stackedScreens = [];

		inputHandler.RegisterMouseButtonCallback(MouseButton.Left, (MouseButton button) => {
			if (currentScreen == null) {
				return;
			}

			List<UIElement> elements = currentScreen.GetUIElements();
			for (int iterator = 0; iterator < elements.Count; ++iterator) {
				UIElement uiElement = elements[iterator];
				if (uiElement.GetHovered()) {
					uiElement.OnClickDown();
				}
			}
		}, true);
        inputHandler.RegisterMouseButtonCallback(MouseButton.Left, (MouseButton button) => {
            if (currentScreen == null) {
                return;
            }

            List<UIElement> elements = currentScreen.GetUIElements();
            for (int iterator = 0; iterator < elements.Count; ++iterator) {
                UIElement uiElement = elements[iterator];
				if (uiElement.GetHovered()) {
					uiElement.OnClickUp();
				}
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

			UIElement uiElement = currentScreen.GetUIElements()[currentScreen.GetTabIndex()];
			uiElement.OnEnter();
		}, true);

		MetaHandler.Register<IUI>(this);
	}

	public void Render() {
		if (currentScreen == null) {
			return;
		}

		uiAtlas.SetActive();
		uiAtlas.Bind();
        gl.Disable(EnableCap.DepthTest);
		currentScreen.Render();
		gl.Enable(EnableCap.DepthTest);
    }

	public void Rearrange(Vector2 windowSize) {
		currentScreen?.Rearrange(windowSize);
	}

	public void AddScreen(UIScreenDefinition screenDefinition) {
        if (screenNameToIndex.ContainsKey(screenDefinition.screenName)) {
            logger.CRITICAL("Tried to register UI screen with same name " + screenDefinition.screenName + " twice, aborting");
			logger.BREAK();
		}
		uiScreens.Add(screenDefinition);
		screenNameToIndex.Add(screenDefinition.screenName, uiScreens.Count - 1);
	}

    private void CleanupCurrentScreen() {
		if (currentScreen == null) {
			return;
		}

        if (screenNameToIndex.TryGetValue(currentScreen.name, out int screenIndex)) {
			List<UIElement> currentScreenElements = currentScreen.GetUIElements();
            logger.INFO("Destroying current screen with ID " + currentScreen.name + " and {" + currentScreenElements.Count + "} elements");
            uiScreens[screenIndex].screenDestructor?.Invoke(currentScreenElements);
        } else {
            logger.ERR("Couldn't find the screen definition for current screen with string ID " + currentScreen.name);
			logger.BREAK();
        }
        currentScreen.Dispose();
        currentScreen = null;
    }

    public void SetCurrentScreen(AssetStringID screenName) {
		UIScreen? uiScreen = GetScreen(screenName);
		if (uiScreen == null) {
			logger.ERR("Tried to set current screen to a screen with name " + screenName + " that didn't exist");
			logger.BREAK();
		}

		CleanupCurrentScreen();

        stackedScreens.Push(screenName);
		currentScreen = uiScreen;
		currentScreen.Rearrange(globalWindow.GetSize());
		globalWindow.SetFocused(false);
	}

	public void MoveScreenBack() {
		if (currentScreen == null) {
			return;
		}

		CleanupCurrentScreen();
		stackedScreens.Pop();

		if (stackedScreens.Count != 0) {
			currentScreen = GetScreen(stackedScreens.Peek());
		} else {
			currentScreen = null;
			// big idea, but some kind of layer system
			// something with world and ui layer and such so that i can easily do things like see if mouse should be visible
			// because this is dumb and kind of assumes stuff that it shouldnt
			// id rather do something like ``layers.LoseContext(this);``
			globalWindow.SetFocused(true);
		}
	}

	public void ArrangeElements() {
        currentScreen?.Rearrange(globalWindow.GetSize());
    }

	public UIScreen? GetScreen(AssetStringID screenName) {
		if (screenNameToIndex.TryGetValue(screenName, out int screenIndex)) {
			UIScreen newScreen = new UIScreen(uiScreens[screenIndex].screenName);
			List<UIElement> screenElements = [];
			uiScreens[screenIndex].screenConstructor(screenElements);
            newScreen.AddUIElements(screenElements);
			newScreen.Rearrange(globalWindow.GetSize());
			logger.INFO("Created UI screen with name " + screenName + " with {" + screenElements.Count + "} elements");
            return newScreen;
		}
		logger.CRITICAL("Tried to get UIScreen with name " + screenName + " that did not exist");
		logger.BREAK();
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
		stackedScreens = [];
		currentScreen = null;

		globalWindow.SetFocused(true);
	}

	public bool IsDisabled() {
		return currentScreen == null;
	}

	public KLogger GetLogger() {
		return logger;
	}

	public IInputHandler GetInputHandler() {
		return inputHandler;
	}

	public IShader GetUIShader() {
		return uiShader;
	}

	public ITexture GetUIAtlas() {
		return uiAtlas;
	}

	public IVirtualWindow GetGlobalWindow() {
		return globalWindow;
	}

	public IRenderBuffers GetRenderBuffers() {
		return new RenderBuffers(vertexArrayObject, vertexBufferObject, indexBufferObject);
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
		logger.INFO("Deleting screens");
        if (screenNameToIndex.TryGetValue(currentScreen.name, out int screenIndex)) {
			uiScreens[screenIndex].screenDestructor(currentScreen.GetUIElements());
		} else {
			logger.ERR("Couldn't find the screen definition for current screen with string ID " + currentScreen.name + ", skipping destructor");
        }
		currentScreen.Dispose();
		uiScreens.Clear();
	}
}

public class UIScreen : IUIScreen, IDisposable {
	public readonly AssetStringID name;
    private UI ui;
	private VertexArrayObject vertexArrayObject;
	private VertexBufferObject vertexBufferObject;
	private IndexBufferObject indexBufferObject;
	private List<UIElement> uiElements;
	private int tabIndex;
	private int lastTabIndex;

	public UIScreen(AssetStringID screenName) {
		name = screenName;
		ui = (UI)MetaHandler.Get<IUI>();
		vertexArrayObject = ui.GetVertexArrayObject();
		vertexBufferObject = ui.GetVertexBufferObject();
		indexBufferObject = ui.GetIndexBufferObject();
		uiElements = [];
		tabIndex = 0;
		lastTabIndex = 0;
	}

	public void Render() {
		for (int iterator = 0; iterator < uiElements.Count; iterator++) {
			if (uiElements[iterator].GetVisible()) {
				uiElements[iterator].Render();
			}
		}
	}

	public void Rearrange(Vector2 windowSize) {
		Vector2 usableRegion = windowSize; // gui scaling

		for (int iterator = 0; iterator < uiElements.Count; iterator++) {
			uiElements[iterator].RecalculateElement(Vector2.Zero, usableRegion);
		}
	}

	public void AddUIElements(IEnumerable<UIElement> uiElements) {
		foreach (UIElement element in uiElements) {
			this.uiElements.Add(element);
			element.AddElementToScreen(this);
		}
	}

	public int GetTabIndex() {
		return tabIndex;
	}

	public void SetTabIndex(int newTabIndex) {
		lastTabIndex = tabIndex;
		tabIndex = newTabIndex;

		uiElements[lastTabIndex].SetSelected(false);
		uiElements[tabIndex].SetSelected(true);
	}

	public IUI GetUI() {
		return ui;
	}

	public List<UIElement> GetUIElements() {
		return uiElements;
	}

	public void Dispose() {
		uiElements.Clear();

		GC.SuppressFinalize(this);
	}
}