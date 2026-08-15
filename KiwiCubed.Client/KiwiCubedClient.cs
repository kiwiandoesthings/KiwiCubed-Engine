namespace KiwiCubed.Client;

using Texture = Engine.Texture;
using Shader = Engine.Shader;
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
using static KiwiCubed.Engine.VirtualWindow;

public class KiwiCubedClient : Engine {
    private VirtualWindow globalWindow;
    private ClientServerInterface clientServerInterface;
    private EventManager eventManager;
    private NetworkHandler networkHandler;
    private GL gl;
    private AssetManager assetManager;
    private WorldClientHandler worldHandler;
    private InputHandler inputHandler;
    private ImGuiController imGui;
    private UI ui;
    private ModHandler modHandler;
    private KLogger logger;
    private KLogger glLogger;

    private Stopwatch gameTime = Stopwatch.StartNew();
    private int fps = 0;
    private int frameCount = 0;

    public override void StartGame() {
        logger = new KLogger("Client");
        glLogger = new KLogger("OpenGL");

        playerUsername += Random.Shared.Next(0, int.MaxValue);

        globalWindow = new VirtualWindow(1280, 720, "KiwiCubed Engine", WindowType.WINDOW_MAXIMIZED);
        IWindow window = globalWindow.GetWindow();

        clientServerInterface = new ClientServerInterface();
        eventManager = new EventManager();
        networkHandler = new NetworkHandler();

        MetaHandler.Register<Engine>(this);

        // Must do all OpenGL setup after the window is loaded
        window.Load += LoadGame;
        window.Render += RunGame;
        window.Closing += ExitGame;
        window.FramebufferResize += FramebufferResizeCallback;

        window.Run();
    }

    public unsafe void LoadGame() {
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
        gl.Viewport(0, 0, globalWindow.GetWidth(), globalWindow.GetHeight());
        MetaHandler.Register(gl);

        // System info
        if (Environment.Is64BitProcess) {
            bitness = 64;
        } else {
            bitness = 32;
        }
        logger.INFO("Machine bitness: " + bitness);
        logger.INFO("Using OpenGL version: " + Marshal.PtrToStringAnsi((IntPtr)gl.GetString(GLEnum.Version)));
        logger.INFO("Using graphics device: " + Marshal.PtrToStringAnsi((IntPtr)gl.GetString(GLEnum.Renderer)));
        logger.INFO("Using resolution: {" + globalWindow.GetWidth() + " x " + globalWindow.GetHeight() + "}");

        // AssetManager setup
        assetManager = new AssetManager();

        // InputHandler setup
        inputHandler = new InputHandler("debug");

        // World handler setup
        worldHandler = new WorldClientHandler();

        // ImGui setup
        imGui = new ImGuiController(gl, window, inputHandler.GetInputContext());
        ImGuiIOPtr io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
        io.ConfigFlags |= ImGuiConfigFlags.NoMouseCursorChange;
        unsafe {
            io.NativePtr->IniFilename = null;
        }
        MetaHandler.Register(imGui);

        inputHandler.SetupImGui();

        // Shader setup
        Shader.SetupShaderResources();

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

        logger.INFO("Took " + gameTime.Elapsed.TotalMilliseconds + "ms to start KiwiCubed Engine");
		gameTime.Restart();
	}

    private void RunGame(double delta) {
        if (shouldExit) {
            globalWindow.GetWindow().Close();
            return;
        }

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
        if (worldHandler.IsLoadedIntoWorld()) {
            worldHandler.Update();
            ClientRenderer.UpdateBuffers();
        } else {
			networkHandler.PollEvents(); // we should only be polling in the tick thread, yeah?
		}
        globalWindow.UpdateMouse(inputHandler.GetMouse());

        // Render everything
        if (worldHandler.IsLoadedIntoWorld()) {
            ClientRenderer.RenderWorld(deltaTime);
        }
        ui.Render();
        ImGui.End();
        imGui.Render();

        frameCount++;
    }

    public override void ExitGame() {
        logger.INFO("Cleaning up resources...");

        worldHandler.ExitWorld();
        worldHandler.Update();
        modHandler.UnloadMods();
        assetManager.ClearAssets();

        logger.INFO("Finished cleanup, exiting");
    }

    private void FramebufferResizeCallback(Vector2D<int> size) {
        globalWindow.SetSize(size.X, size.Y);
        gl.Viewport(0, 0, (uint)size.X, (uint)size.Y);
    }

    private void DebugCallback(GLEnum source, GLEnum type, int id, GLEnum severity, int length, nint message, nint userParam) {
        string messageString = Marshal.PtrToStringAnsi(message, length);
        string logEntry = "[GL " + type.ToString() + "] " + messageString;

        if (severity == GLEnum.DebugSeverityHigh) {
            glLogger.ERR(logEntry);
            glLogger.ERR(Environment.StackTrace);
            glLogger.BREAK();
        } else if (severity == GLEnum.DebugSeverityMedium || severity == GLEnum.DebugSeverityLow) {
            glLogger.WARN(logEntry);
            //glLogger.WARN(Environment.StackTrace);
        } else {
            //glLogger.INFO(logEntry);
        }
    }
}