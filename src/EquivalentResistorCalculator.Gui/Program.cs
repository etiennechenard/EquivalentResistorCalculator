using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.OpenGL3;
using ImBackendSDL2 = Hexa.NET.ImGui.Backends.SDL2;
using Hexa.NET.SDL2;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using EquivalentResistorCalculator.Gui.App;

namespace EquivalentResistorCalculator.Gui;

internal static class Program
{
    [DllImport("opengl32.dll")]
    private static extern void glClearColor(float r, float g, float b, float a);
    [DllImport("opengl32.dll")]
    private static extern void glClear(uint mask);
    [DllImport("opengl32.dll")]
    private static extern void glViewport(int x, int y, int width, int height);
    private const uint GL_COLOR_BUFFER_BIT = 0x00004000u;

    private static nint _iniPathPtr; // unmanaged lifetime — freed at shutdown

    [STAThread]
    private static unsafe int Main(string[] args)
    {
        if (SDL.Init(SDL.SDL_INIT_VIDEO) < 0)
        {
            Console.Error.WriteLine("SDL_Init failed");
            return 1;
        }

        SDL.GLSetAttribute(SDLGLattr.GlContextFlags, (int)SDLGLcontextFlag.GlContextForwardCompatibleFlag);
        SDL.GLSetAttribute(SDLGLattr.GlContextProfileMask, (int)SDLGLprofile.GlContextProfileCore);
        SDL.GLSetAttribute(SDLGLattr.GlContextMajorVersion, 3);
        SDL.GLSetAttribute(SDLGLattr.GlContextMinorVersion, 3);
        SDL.GLSetAttribute(SDLGLattr.GlDoublebuffer, 1);
        SDL.GLSetAttribute(SDLGLattr.GlDepthSize, 24);
        SDL.GLSetAttribute(SDLGLattr.GlStencilSize, 8);

        int centered = (int)SDL.SDL_WINDOWPOS_CENTERED_MASK;
        uint windowFlags = (uint)(SDLWindowFlags.Opengl | SDLWindowFlags.Shown | SDLWindowFlags.Resizable | SDLWindowFlags.AllowHighdpi);
        SDLWindowPtr window = SDL.CreateWindow("Equivalent Resistor Calculator", centered, centered, 1280, 800, windowFlags);
        if (window.IsNull)
        {
            Console.Error.WriteLine("SDL_CreateWindow failed");
            SDL.Quit();
            return 1;
        }

        nint glCtx = (nint)SDL.GLCreateContext(window);
        if (glCtx == 0)
        {
            Console.Error.WriteLine("SDL_GL_CreateContext failed");
            SDL.DestroyWindow(window);
            SDL.Quit();
            return 1;
        }

        SDL.GLMakeCurrent(window, (void*)glCtx);
        SDL.GLSetSwapInterval(1); // vsync

        // ── ImGui setup ──────────────────────────────────────────────────────
        ImGui.CreateContext();

        // Route imgui.ini to %APPDATA%\EquivalentResistorCalculator\ instead of the working directory.
        var iniDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EquivalentResistorCalculator");
        Directory.CreateDirectory(iniDir);
        _iniPathPtr = Marshal.StringToHGlobalAnsi(Path.Combine(iniDir, "imgui.ini"));
        var io = ImGui.GetIO();
        io.IniFilename = (byte*)_iniPathPtr;

        // Each backend DLL ships its own cimgui copy — sync the context pointer into both.
        var ctx = ImGui.GetCurrentContext();
        ImBackendSDL2.ImGuiImplSDL2.SetCurrentContext(ctx);
        ImGuiImplOpenGL3.SetCurrentContext(ctx);

        ImGui.StyleColorsDark();

        ImBackendSDL2.ImGuiImplSDL2.InitForOpenGL(AsBackend(window), (void*)glCtx);
        ImGuiImplOpenGL3.Init("#version 330 core");

        void ResetWindowGeometry()
        {
            SDL.SetWindowSize(window, 1280, 800);
            SDL.SetWindowPosition(window, centered, centered);
        }

        var mainWindow = new MainWindow(ResetWindowGeometry);

        // ── Render loop ───────────────────────────────────────────────────────
        bool running = true;
        while (running)
        {
            SDLEvent sdlEvent = default;
            while (SDL.PollEvent(ref sdlEvent) != 0)
            {
                // ProcessEvent needs ImBackendSDL2.SDLEvent — same native layout, reinterpret
                ImBackendSDL2.ImGuiImplSDL2.ProcessEvent(
                    ref Unsafe.As<SDLEvent, ImBackendSDL2.SDLEvent>(ref sdlEvent));

                if (sdlEvent.Type == (uint)SDLEventType.Quit)
                    running = false;
                if (sdlEvent.Type == (uint)SDLEventType.Windowevent
                    && sdlEvent.Window.Event == (byte)SDLWindowEventID.Close
                    && sdlEvent.Window.WindowID == SDL.GetWindowID(window))
                    running = false;
            }
            if (mainWindow.ShouldClose) running = false;
            if (!running) break;

            ImGuiImplOpenGL3.NewFrame();
            ImBackendSDL2.ImGuiImplSDL2.NewFrame();
            ImGui.NewFrame();

            mainWindow.Render();

            ImGui.Render();

            int w = 0, h = 0;
            SDL.GetWindowSizeInPixels(window, ref w, ref h);
            glViewport(0, 0, w, h);
            glClearColor(0.10f, 0.10f, 0.10f, 1.00f);
            glClear(GL_COLOR_BUFFER_BIT);
            ImGuiImplOpenGL3.RenderDrawData(ImGui.GetDrawData());

            SDL.GLSwapWindow(window);
        }

        // ── Shutdown ──────────────────────────────────────────────────────────
        mainWindow.Dispose();

        ImGuiImplOpenGL3.Shutdown();
        ImBackendSDL2.ImGuiImplSDL2.Shutdown();
        ImGui.DestroyContext();
        if (_iniPathPtr != 0) { Marshal.FreeHGlobal(_iniPathPtr); _iniPathPtr = 0; }
        SDL.GLDeleteContext((void*)glCtx);
        SDL.DestroyWindow(window);
        SDL.Quit();

        return 0;
    }

    // Bridge Hexa.NET.SDL2.SDLWindowPtr → Hexa.NET.ImGui.Backends.SDL2.SDLWindowPtr.
    // Both are structs with a single native-pointer field; the pointer reinterpret is safe.
    private static unsafe ImBackendSDL2.SDLWindowPtr AsBackend(SDLWindowPtr w)
        => *(ImBackendSDL2.SDLWindowPtr*)&w;
}
