namespace KiwiCubed.Engine;

using KiwiCubed.Api;
using Silk.NET.Input; 
using Silk.NET.Maths;
using Silk.NET.Windowing;
using System.Numerics;

using static KiwiCubed.Api.KLogger;

public class VirtualWindow : IVirtualWindow {
	private IWindow window = null;
	private uint width = 0;
	private uint height = 0;
	private string title;
	private WindowType type;

	private bool isFocused = false;
	
	// Width and height will be ignored if windowType is anything other than WindowType.WINDOW
	public VirtualWindow(uint width, uint height, string title, WindowType windowType) {
		OVERRIDE_LOG_NAME("Window");
		this.width = width;
		this.height = height;
		this.title = title;
		this.type = windowType;

		WindowOptions options = WindowOptions.Default;
		options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Debug, new APIVersion(4, 3));
		options.PreferredDepthBufferBits = 24;
		options.Title = title;
		options.Size = new Vector2D<int>((int)width, (int)height);
		options.Samples = 4;

		IMonitor monitor = Window.Platforms.First().GetMainMonitor();
		VideoMode mode = monitor.VideoMode;
		if (windowType == WindowType.FULLSCREEN) {
			options.WindowState = WindowState.Fullscreen;
			options.Size = (Vector2D<int>)mode.Resolution;
		} else if (windowType == WindowType.WINDOW_BORDERLESS) {
			options.WindowBorder = WindowBorder.Hidden;
			options.Size = (Vector2D<int>)mode.Resolution;
		} else if (windowType == WindowType.WINDOW_MAXIMIZED) {
			options.WindowState = WindowState.Maximized;
		} else if (windowType == WindowType.WINDOW) {
		}

		window = Window.Create(options);
		if (window == null) {
			KERR("Failed to create Silk.NET window");
			return;
		}

		window.Load += () => {
			this.width = (uint)window.FramebufferSize.X;
			this.height = (uint)window.FramebufferSize.Y;
		};

		KINFO("Successfully created window with width {" + width + "} and height {" + height + "} with title \"" + title + "\"");

		MetaHandler.Register<IVirtualWindow>(this);
	}

	public void UpdateMouse(IMouse mouse) {
		if (!isFocused) {
			mouse.Cursor.CursorMode = CursorMode.Normal;
		} else {
			mouse.Cursor.CursorMode = CursorMode.Raw;
		}
	}

	public bool GetFocused() {
		return isFocused;
	}

	public bool SetFocused(bool focus) {
		bool isSame = isFocused == focus;
		isFocused = focus;
		return isSame;
	}

	public void SetSize(int width, int height) {
		window.Size = new Vector2D<int>(width, height);
	}

	public Vector2 GetSize() {
		return new Vector2(width, height);
	}

	public uint GetWidth() {
		return width;
	}

	public uint GetHeight() {
		return height;
	}

	public IWindow GetWindow() {
		return window;
	}

	public enum WindowType : byte {
		WINDOW,
		WINDOW_MAXIMIZED,
		WINDOW_BORDERLESS,
		FULLSCREEN
	}
}
