namespace KiwiCubed.Engine;

using ImGuiNET;
using KiwiCubed.Api;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using System.Diagnostics;
using System.Runtime.InteropServices;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.KLogger;
using static VirtualWindow;

public class KiwiCubedEngine {
    private VirtualWindow globalWindow;
	private MetaHandler metaHandler;
    private GL gl;
	private AssetManager assetManager;
    private InputHandler inputHandler;
    private ImGuiController imGui;
	private UI ui;
	private ModHandler modHandler;
    
    public void StartEngine() {
        OVERRIDE_LOG_NAME("Initialization");

        KINFO("Initializing KiwiCubed Engine...");

        globalWindow = new VirtualWindow(1280, 720, "KiwiCubed Engine", WindowType.WINDOW_MAXIMIZED);
        IWindow window = globalWindow.GetWindow();

		metaHandler = new MetaHandler();

        // Must do all OpenGL setup after the window is loaded
        window.Load += LoadGame;
		window.Render += RunGameLoop;
		window.Closing += ExitGame;
		window.FramebufferResize += FramebufferResizeCallback;

        window.Run();
    }

    private unsafe void LoadGame() {
		OVERRIDE_LOG_NAME("Initialization");

		Stopwatch stopwatch = Stopwatch.StartNew();

		IWindow window = globalWindow.GetWindow();

		// OpenGL setup
		gl = window.CreateOpenGL();
		gl.FrontFace(FrontFaceDirection.CW);
		gl.Enable(EnableCap.DepthTest);
		gl.Enable(EnableCap.Blend);
		gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
		gl.Enable(EnableCap.Multisample);
		gl.Enable(EnableCap.DebugOutput);
		gl.Enable(EnableCap.DebugOutputSynchronous);
		unsafe {
			gl.DebugMessageCallback(DebugCallback, null);
		}
		gl.Viewport(0, 0, (uint)globalWindow.GetWidth(), (uint)globalWindow.GetHeight());
		SystemsManager.Register<GL>(gl);

		// System info
		if (Environment.Is64BitProcess) {
			Globals.bitness = 64;
		} else {
			Globals.bitness = 32;
		}
		KINFO("Machine bitness: " + Globals.bitness);
		KINFO("Using OpenGL version: " + Marshal.PtrToStringAnsi((IntPtr)gl.GetString(GLEnum.Version)));
		KINFO("Using graphics device: " + Marshal.PtrToStringAnsi((IntPtr)gl.GetString(GLEnum.Renderer)));
		KINFO("Using resolution: {" + globalWindow.GetWidth() + " x " + globalWindow.GetHeight() + "}");

		// AssetManager setup
		assetManager = new AssetManager();

		// Temporary resource setup
		Shader terrainShader = new Shader(gl, "../../../Mods/kiwicubed/Resources/Shaders/Terrain_Vertex.vert", "../../../Mods/kiwicubed/Resources/Shaders/Terrain_Fragment.frag");
		Shader uiShader = new Shader(gl, "../../../Mods/kiwicubed/Resources/Shaders/UI_Vertex.vert", "../../../Mods/kiwicubed/Resources/Shaders/UI_Fragment.frag");
		Shader textShader = new Shader(gl, "../../../Mods/kiwicubed/Resources/Shaders/Text_Vertex.vert", "../../../Mods/kiwicubed/Resources/Shaders/Text_Fragment.frag");
		Shader entityShader = new Shader(gl, "../../../Mods/kiwicubed/Resources/Shaders/Entity_Vertex.vert", "../../../Mods/kiwicubed/Resources/Shaders/Entity_Fragment.frag");
		Shader chunkDebugShader = new Shader(gl, "../../../Mods/kiwicubed/Resources/Shaders/ChunkDebug_Vertex.vert", "../../../Mods/kiwicubed/Resources/Shaders/ChunkDebug_Fragment.frag");
		Shader wireframeShader = new Shader(gl, "../../../Mods/kiwicubed/Resources/Shaders/Wireframe_Vertex.vert", "../../../Mods/kiwicubed/Resources/Shaders/Wireframe_Fragment.frag");
		assetManager.RegisterShader(new AssetStringID("kiwicubed", "shader/terrain"), terrainShader);
		assetManager.RegisterShader(new AssetStringID("kiwicubed", "shader/ui"), uiShader);
		assetManager.RegisterShader(new AssetStringID("kiwicubed", "shader/text"), textShader);
		assetManager.RegisterShader(new AssetStringID("kiwicubed", "shader/entity"), entityShader);
		assetManager.RegisterShader(new AssetStringID("kiwicubed", "shader/chunk_debug"), chunkDebugShader);
		assetManager.RegisterShader(new AssetStringID("kiwicubed", "shader/wireframe"), wireframeShader);

		// InputHandler setup
		inputHandler = new InputHandler("debug");

		// ImGui setup
		imGui = new ImGuiController(gl, window, inputHandler.GetInputContext());
		ImGuiIOPtr io = ImGui.GetIO();
		io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
		io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
		io.ConfigFlags |= ImGuiConfigFlags.NoMouseCursorChange;
		unsafe {
			io.NativePtr->IniFilename = null;
		}
		SystemsManager.Register<ImGuiController>(imGui);

		inputHandler.SetupImGui();

		// ModHandler setup
		modHandler = new ModHandler();

		Texture gameAtlas = assetManager.GetTextureAtlas(new AssetStringID("kiwicubed", "atlas/main"));
		gameAtlas.TextureUnit(terrainShader, "tex0");
		gameAtlas.SetActive();
		gameAtlas.Bind();

		// TextRenderer setup
		TextRenderer.AddFont("../../../Mods/kiwicubed/Resources/Fonts/PixiFont.ttf");

		// UI setup
		ui = new UI((Shader)assetManager.GetShader(new AssetStringID("kiwicubed", "shader/ui")), assetManager.GetTextureAtlas(new AssetStringID("kiwicubed", "atlas/main")));

		modHandler.LoadMods();

		KINFO("Took " + stopwatch.Elapsed.TotalMilliseconds + "ms to start KiwiCubed Engine");
	}

	private void RunGameLoop(double delta) {
		Globals.deltaTime = delta;

		gl.ClearColor(0.85f, 0.65f, 0.8f, 1.0f);
		gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

		imGui.Update((float)delta);
		ImGui.Begin("Debug");
		ImGui.Text("FPS: " + (1.0f / (float)delta).ToString("F0"));
		ImGui.Text("Delta Time: " + Globals.deltaTime.ToString("F4"));

		// Update game state
		SingleplayerHandler.Update();
		globalWindow.UpdateMouse(inputHandler.GetMouse());

		// Render everything
		SingleplayerHandler.Render();
		ui.Render();
		ImGui.End();
		imGui.Render();
	}

	private void ExitGame() {
		OVERRIDE_LOG_NAME("Cleanup");
		
		KINFO("Cleaning up resources...");

		if (SingleplayerHandler.IsLoadedIntoSingleplayerWorld()) {
			SingleplayerHandler.ExitWorld();
		}

		modHandler.UnloadMods();

		KINFO("Finished cleanup, exiting");
	}

	private void FramebufferResizeCallback(Vector2D<int> size) {
		globalWindow.SetSize(size.X, size.Y);
		gl.Viewport(0, 0, (uint)size.X, (uint)size.Y);
	}

	private void DebugCallback(GLEnum source, GLEnum type, int id, GLEnum severity, int length, nint message, nint userParam) {
		OVERRIDE_LOG_NAME("OpenGL Message");
		string msg = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(message, length);
		string logEntry = "[GL " + type.ToString() + "] " + msg;

		if (severity == GLEnum.DebugSeverityHigh) {
			KERR(logEntry);
			//KERR(Environment.StackTrace);
		} else if (severity == GLEnum.DebugSeverityMedium || severity == GLEnum.DebugSeverityLow) {
			KWARN(logEntry);
			//KWARN(Environment.StackTrace);
		} else {
			//KINFO(logEntry);
		}
	}
}