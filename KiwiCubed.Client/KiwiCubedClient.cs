namespace KiwiCubed.Client;

using Texture = KiwiCubed.Engine.Texture;
using Shader = KiwiCubed.Engine.Shader;
using ImGuiNET;
using KiwiCubed.Api;
using KiwiCubed.Engine;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using System.Diagnostics;
using System.Runtime.InteropServices;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.KLogger;
using static KiwiCubed.Engine.VirtualWindow;

public class KiwiCubedClient {
    private VirtualWindow globalWindow;
    private ClientServerInterface clientServerInterface;
    private EventManager eventManager;
    private NetworkHandler networkHandler;
    private GL gl;
    private AssetManager assetManager;
    private SingleplayerHandler singleplayerHandler;
    private InputHandler inputHandler;
    private ImGuiController imGui;
    private UI ui;
    private ModHandler modHandler;

    private Stopwatch gameTime = Stopwatch.StartNew();
    private int fps = 0;
    private int frameCount = 0;

    public void StartClient() {
        OVERRIDE_LOG_NAME("Initialization");

        KINFO("Initializing KiwiCubed Engine...");

        MetaHandler.SetupThreadMeta(GameType.CLIENT);

        playerUsername += Random.Shared.Next(0, int.MaxValue);

        globalWindow = new VirtualWindow(1280, 720, "KiwiCubed Engine", WindowType.WINDOW_MAXIMIZED);
        IWindow window = globalWindow.GetWindow();

        clientServerInterface = new ClientServerInterface();
        eventManager = new EventManager();
        networkHandler = new NetworkHandler();

        // Must do all OpenGL setup after the window is loaded
        window.Load += LoadGame;
        window.Render += RunGameLoop;
        window.Closing += ExitGame;
        window.FramebufferResize += FramebufferResizeCallback;

        window.Run();
    }

    public unsafe void LoadGame() {
        OVERRIDE_LOG_NAME("Initialization");

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
        gl.DebugMessageCallback(DebugCallback, null);
        gl.Viewport(0, 0, (uint)globalWindow.GetWidth(), (uint)globalWindow.GetHeight());
        MetaHandler.Register<GL>(gl);

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

        // InputHandler setup
        inputHandler = new InputHandler("debug");

        // SingleplayerHandler setup
        singleplayerHandler = new SingleplayerHandler();

        // ImGui setup
        imGui = new ImGuiController(gl, window, inputHandler.GetInputContext());
        ImGuiIOPtr io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
        io.ConfigFlags |= ImGuiConfigFlags.NoMouseCursorChange;
        unsafe {
            io.NativePtr->IniFilename = null;
        }
        MetaHandler.Register<ImGuiController>(imGui);

        inputHandler.SetupImGui();

        // ModHandler setup
        modHandler = new ModHandler();
        modHandler.LoadModAssets();

        Texture gameAtlas = assetManager.GetTextureAtlas(new AssetStringID("kiwicubed", "atlas/main"));
        Shader terrainShader = (Shader)assetManager.GetShader(new AssetStringID("kiwicubed", "shader/terrain"));
        gameAtlas.TextureUnit(terrainShader, "tex0");
        gameAtlas.SetActive();
        gameAtlas.Bind();

        // ClientRenderer setup
        ClientRenderer.SetupRenderResources();

        // TextRenderer setup
        TextRenderer.AddFont(Path.Combine(topSaveFolder, "Mods/kiwicubed/Resources/Fonts/PixiFont.ttf"));

        // UI setup
        ui = new UI((Shader)assetManager.GetShader(new AssetStringID("kiwicubed", "shader/ui")), assetManager.GetTextureAtlas(new AssetStringID("kiwicubed", "atlas/main")));

        modHandler.LoadModScripts();

        KINFO("Took " + gameTime.Elapsed.TotalMilliseconds + "ms to start KiwiCubed Engine");
		gameTime.Restart();
	}

    private void RunGameLoop(double delta) {
        deltaTime = delta;
        currentFrame++;

        if (gameTime.Elapsed.TotalSeconds >= 1) {
            fps = frameCount;
            frameCount = 0;
            gameTime.Restart();
		}

		gl.ClearColor(0.85f, 0.65f, 0.8f, 1.0f);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        imGui.Update((float)delta);
        ImGui.Begin("Debug");
        ImGui.Text("FPS: " + fps.ToString("F1"));
        ImGui.Text("Delta Time: " + deltaTime.ToString("F4"));

        // Update game state
        if (singleplayerHandler.IsLoadedIntoWorld()) {
            singleplayerHandler.Update();
            ClientRenderer.UpdateBuffers();
        } else {
			networkHandler.PollEvents();
		}
        globalWindow.UpdateMouse(inputHandler.GetMouse());

        // Render everything
        if (singleplayerHandler.IsLoadedIntoWorld()) {
            ClientRenderer.RenderWorld(deltaTime);
        }
        ui.Render();
        ImGui.End();
        imGui.Render();

        frameCount++;
    }

    private void ExitGame() {
        OVERRIDE_LOG_NAME("Cleanup");

        KINFO("Cleaning up resources...");

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