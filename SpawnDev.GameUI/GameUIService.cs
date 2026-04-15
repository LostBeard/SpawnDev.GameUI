using Microsoft.AspNetCore.Components;
using SpawnDev.BlazorJS.JSObjects;
using SpawnDev.GameUI.Animation;
using SpawnDev.GameUI.Elements;
using SpawnDev.GameUI.Input;
using SpawnDev.GameUI.Rendering;
using System.Numerics;

namespace SpawnDev.GameUI;

/// <summary>
/// All-in-one service that wires up the complete GameUI stack.
/// Register as singleton in DI, call InitAsync once, then Update/Render per frame.
///
/// Consuming projects add ONE service and get:
/// - UIRenderer (WebGPU batched quad rendering + font atlas)
/// - GameInput (unified mouse/keyboard/gamepad/VR/hand/touch)
/// - TweenManager (animations)
/// - UIScreenManager (screen stack)
/// - UIFocusManager (keyboard/gamepad navigation)
/// - HapticFeedback (VR controller vibration)
/// - DragDropManager (inventory drag-and-drop)
/// - PokeInteraction + AdaptiveInteraction (VR hand tracking)
/// - UIDebugOverlay (F3 debug display)
///
/// Usage in Program.cs:
///   builder.Services.AddSingleton&lt;GameUIService&gt;();
///
/// Usage in a Razor page:
///   @inject GameUIService UI
///
///   // Once, after WebGPU is ready:
///   await UI.InitAsync(device, queue, canvasFormat, canvasRef);
///
///   // Per frame (in RAF loop):
///   UI.Update(dt);
///   UI.BeginRender(viewportW, viewportH);
///   UI.Screens.Draw(UI.Renderer);
///   UI.EndRender(encoder, targetView);
/// </summary>
public class GameUIService : IDisposable
{
    // Core systems
    public UIRenderer Renderer { get; } = new();
    public GameInput Input { get; } = new();
    public TweenManager Tweens => TweenManager.Global;
    public UIScreenManager Screens { get; private set; } = null!;
    public UIFocusManager Focus { get; } = new();
    public HapticFeedback Haptics { get; } = new();
    public DragDropManager DragDrop { get; } = new();
    public PokeInteraction Poke { get; } = new();
    public AdaptiveInteraction Adaptive { get; } = new();

    // Input providers (created during init)
    public MouseKeyboardProvider? MouseKeyboard { get; private set; }
    public XRControllerProvider? XRControllers { get; private set; }
    public XRHandProvider? XRHands { get; private set; }
    public TouchProvider? Touch { get; private set; }

    // State
    public bool IsInitialized { get; private set; }
    public float DeltaTime { get; private set; }
    public float TotalTime { get; private set; }
    public int FrameCount { get; private set; }
    public int ViewportWidth { get; private set; }
    public int ViewportHeight { get; private set; }

    /// <summary>
    /// Initialize the UI system. Call once after WebGPU device is available.
    /// </summary>
    public void Init(GPUDevice device, GPUQueue queue, string canvasFormat,
        ElementReference canvasRef, int viewportWidth, int viewportHeight)
    {
        // Renderer + font atlases (bitmap fallback + SDF for resolution-independent text)
        var fontAtlas = new FontAtlas();
        fontAtlas.Init(device, queue);
        var sdfFontAtlas = new SDFFontAtlas();
        sdfFontAtlas.Init(device, queue);
        Renderer.Init(device, queue, fontAtlas, canvasFormat, sdfFontAtlas);

        // Screen manager
        Screens = new UIScreenManager(viewportWidth, viewportHeight);
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;

        // Input providers
        MouseKeyboard = new MouseKeyboardProvider();
        MouseKeyboard.Attach(canvasRef);
        Input.AddProvider(MouseKeyboard);

        Touch = new TouchProvider();
        Touch.Attach(canvasRef);
        Input.AddProvider(Touch);

        XRControllers = new XRControllerProvider();
        Input.AddProvider(XRControllers);

        XRHands = new XRHandProvider();
        Input.AddProvider(XRHands);

        // Debug overlay
        UIDebugOverlay.IsVisible = false; // off by default, F3 to toggle

        IsInitialized = true;
    }

    /// <summary>
    /// Update viewport dimensions (on window resize).
    /// </summary>
    public void SetViewport(int width, int height)
    {
        ViewportWidth = width;
        ViewportHeight = height;
        Screens?.SetViewport(width, height);
    }

    /// <summary>
    /// Set the active XR session for VR/AR input.
    /// Call when entering immersive mode.
    /// </summary>
    public void SetXRSession(XRSession session)
    {
        XRControllers?.SetSession(session);
        XRHands?.SetSession(session);
    }

    /// <summary>
    /// Clear the XR session when exiting VR/AR.
    /// </summary>
    public void ClearXRSession()
    {
        XRControllers?.ClearSession();
        XRHands?.ClearSession();
    }

    /// <summary>
    /// Update XR frame data for controller/hand tracking.
    /// Call each XR animation frame before Update().
    /// </summary>
    public void UpdateXRFrame(XRFrame frame, XRReferenceSpace referenceSpace)
    {
        XRControllers?.UpdateFrame(frame, referenceSpace);
        XRHands?.UpdateFrame(frame, referenceSpace);
    }

    /// <summary>
    /// Per-frame update. Polls input, advances animations, updates UI.
    /// Call once at the start of each frame.
    /// </summary>
    public void Update(float deltaTime)
    {
        DeltaTime = deltaTime;
        TotalTime += deltaTime;
        FrameCount++;

        // Poll all input sources
        Input.Poll();

        // F3 toggles debug overlay
        if (Input.Keyboard.WasKeyPressed("F3"))
            UIDebugOverlay.Toggle();

        // Advance animations
        Tweens.Update(deltaTime);

        // Update focus navigation
        Focus.Update(Input);

        // Update drag-and-drop
        DragDrop.Update(Input);

        // Update screen stack (only top screen gets input)
        Screens.Update(Input, deltaTime);

        // Debug overlay stats
        if (UIDebugOverlay.IsVisible)
        {
            UIDebugOverlay.Text($"FPS: {(int)(1f / Math.Max(deltaTime, 0.001f))}");
            UIDebugOverlay.Text($"Frame: {FrameCount}");
            UIDebugOverlay.Text($"Tweens: {Tweens.ActiveCount}");
            UIDebugOverlay.Text($"Pointers: {Input.Pointers.Count}");
            UIDebugOverlay.Text($"Screen: {Screens.ActiveScreen ?? "none"} (depth {Screens.StackDepth})");
            if (Input.Gamepad.Connected)
                UIDebugOverlay.Text($"Gamepad: connected");
            UIDebugOverlay.Separator();
        }
    }

    /// <summary>Begin 2D screen-space rendering. Call before drawing UI elements.</summary>
    public void BeginRender(int viewportWidth, int viewportHeight)
    {
        Renderer.Begin(viewportWidth, viewportHeight);
    }

    /// <summary>End 2D screen-space rendering. Flushes the batch to GPU.</summary>
    public void EndRender(GPUCommandEncoder encoder, GPUTextureView target)
    {
        // Draw debug overlay last (on top of everything)
        if (UIDebugOverlay.IsVisible)
            UIDebugOverlay.Instance.Draw(Renderer);

        // Draw drag-and-drop ghost
        DragDrop.Draw(Renderer);

        Renderer.End(encoder, target);
    }

    public void Dispose()
    {
        MouseKeyboard?.Dispose();
        Touch?.Dispose();
        XRControllers?.Dispose();
        XRHands?.Dispose();
        Renderer.Dispose();
    }
}
