namespace KiwiCubed;

using ImGuiNET;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

using static KiwiCubed.Api.KLogger;
using static VirtualWindow;

public class KiwiCubedEngine {
    private VirtualWindow globalWindow;
    private GL gl;
    private ImGuiController imGui;
    private InputHandler inputHandler;
	private ModHandler modHandler;
	private AssetManager assetManager;
    private World world;
    private Chunk chunk = null!;
    private Shader shader;
    private Texture texture;
    
    public void StartEngine() {
        OVERRIDE_LOG_NAME("Initialization");
        KINFO("Initializing KiwiCubed Engine...");

        globalWindow = new VirtualWindow(1280, 720, "KiwiCubed Engine", WindowType.WINDOW);
        IWindow window = globalWindow.GetWindow();

        // Must do all OpenGL setup after the window is loaded
        window.Load += LoadGame;
		window.Render += RunGameLoop;
		window.Closing += ExitGame;
		window.FramebufferResize += FramebufferResizeCallback;

        window.Run();
    }

    private void LoadGame() {
		OVERRIDE_LOG_NAME("Initialization");
		IWindow window = globalWindow.GetWindow();

		// Setup OpenGL
		gl = window.CreateOpenGL();
		gl.Disable(EnableCap.CullFace);
		gl.Enable(EnableCap.DepthTest);
		gl.Enable(EnableCap.DebugOutput);
		gl.Enable(EnableCap.DebugOutputSynchronous);
		unsafe {
			gl.DebugMessageCallback(DebugCallback, null);
		}
		SystemsManager.Register<GL>(gl);

		// Temporary resource setup
		shader = new Shader(gl, "../../../Mods/kiwicubed/Resources/Shaders/Terrain_Vertex.vert", "../../../Mods/kiwicubed/Resources/Shaders/Terrain_Fragment.frag");
		shader.Bind();
		texture = new Texture("../../../Mods/kiwicubed/Resources/Textures/terrain_atlas.png", TextureTarget.Texture2D, TextureUnit.Texture0, PixelFormat.Rgba, PixelType.UnsignedByte, "texture/terrain");
		texture.TextureUnit(shader, "tex0");
		texture.SetActive();
		texture.Bind();

		// InputHandler setup
		inputHandler = new InputHandler("debug");
		SystemsManager.Register<InputHandler>(inputHandler);

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

		// AssetManager setup
		assetManager = new AssetManager();

		// ModHandler setup
		modHandler = new ModHandler();
		modHandler.LoadMods();

		// Temporary world creation
		world = new World(2, 3);
		world.GenerateWorld();
	}

	private void RunGameLoop(double delta) {
		OVERRIDE_LOG_NAME("KiwiCubed Engine");
		gl.ClearColor(0.85f, 0.65f, 0.8f, 1.0f);
		gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
		world.Update(shader);
		world.Render();
		globalWindow.UpdateMouse(inputHandler.GetMouse());
	}

	private void ExitGame() {
		OVERRIDE_LOG_NAME("Cleanup");
		KINFO("Exiting KiwiCubed Engine...");
	}

	private void FramebufferResizeCallback(Vector2D<int> size) {
		gl.Viewport(0, 0, (uint)size.X, (uint)size.Y);
	}

	private unsafe void DebugCallback(GLEnum source, GLEnum type, int id, GLEnum severity, int length, nint message, nint userParam) {
		OVERRIDE_LOG_NAME("OpenGL Error");
		string msg = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(message, length);
		string logEntry = "[GL " + type.ToString() + "] " + msg;

		if (severity == GLEnum.DebugSeverityHigh) {
			KERR(logEntry);
		} else if (severity == GLEnum.DebugSeverityMedium || severity == GLEnum.DebugSeverityLow) {
			KWARN(logEntry);
		} else {
			KINFO(logEntry);
		}
	}
}