using Microsoft.AspNetCore.Components;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;
using System.Numerics;

namespace SpawnDev.GameUI.Input;

/// <summary>
/// Input provider for mouse, keyboard, and gamepad via DOM events.
/// Uses SpawnDev.SpawnJS strongly typed wrappers - no IJSRuntime, no eval, no raw JS.
/// Events use ActionCallback += / -= pattern with proper lifecycle management.
///
/// Adapted from SpawnScene.UI.InputManager for the GameInput provider model.
/// </summary>
public class MouseKeyboardProvider : IInputProvider
{
    // The DOM handlers push into this; it buffers the events between polls and builds the frame's pointer and keys.
    // The same class serves a host with no DOM (a Web Worker): there the host pushes the events itself.
    private readonly PushedInputProvider _pushed = new();

    // Gamepad
    private const float DeadZone = 0.15f;

    // DOM event callbacks - prevent GC, properly disposed
    private ActionCallback<MouseEvent>? _onMouseMove;
    private ActionCallback<MouseEvent>? _onMouseDown;
    private ActionCallback<MouseEvent>? _onMouseUp;
    private ActionCallback<WheelEvent>? _onWheel;
    private ActionCallback<KeyboardEvent>? _onKeyDown;
    private ActionCallback<KeyboardEvent>? _onKeyUp;
    // Chrome defaults wheel listeners to passive:true; must register non-passive
    // so PreventDefault can stop the page from scrolling under the canvas.
    private readonly AddEventListenerOptions _wheelListenOpts = new() { Passive = false };

    // SpawnJS typed wrappers - owned, disposed in Dispose()
    private HTMLCanvasElement? _canvas;
    private Window? _window;
    private bool _attached;
    private bool _ownsCanvas;

    /// <summary>
    /// Attach DOM event listeners to the canvas element.
    /// Must be called once before Poll() produces data.
    /// Blazor WASM: ElementReference.As&lt;T&gt;() is correct.
    /// SpawnDomRenderer hosts: resolve with Renderer.GetElement&lt;HTMLCanvasElement&gt; and call Attach(HTMLCanvasElement).
    /// </summary>
    public void Attach(ElementReference canvasRef) => Attach(canvasRef.As<HTMLCanvasElement>(), ownsCanvas: true);

    /// <summary>
    /// Attach DOM event listeners to an already-resolved canvas (required under SpawnDomRenderer).
    /// Caller retains ownership of <paramref name="canvas"/>; Dispose will not dispose it.
    /// </summary>
    public void Attach(HTMLCanvasElement canvas) => Attach(canvas, ownsCanvas: false);

    private void Attach(HTMLCanvasElement canvas, bool ownsCanvas)
    {
        if (_attached) return;
        _attached = true;
        _ownsCanvas = ownsCanvas;

        _canvas = canvas;
        _window = new Window();

        // Create callbacks (prevent GC)
        _onMouseMove = new ActionCallback<MouseEvent>(OnMouseMove);
        _onMouseDown = new ActionCallback<MouseEvent>(OnMouseDown);
        _onMouseUp = new ActionCallback<MouseEvent>(OnMouseUp);
        _onWheel = new ActionCallback<WheelEvent>(OnWheel);
        _onKeyDown = new ActionCallback<KeyboardEvent>(OnKeyDown);
        _onKeyUp = new ActionCallback<KeyboardEvent>(OnKeyUp);

        // Attach via SpawnJS typed events
        _canvas.OnMouseMove += _onMouseMove;
        _canvas.OnMouseDown += _onMouseDown;
        _canvas.OnMouseUp += _onMouseUp;
        // Wheel must be non-passive or PreventDefault is ignored (Chrome/Firefox).
        _canvas.AddEventListener("wheel", _onWheel, _wheelListenOpts);
        _window.OnKeyDown += _onKeyDown;
        _window.OnKeyUp += _onKeyUp;
    }

    /// <summary>
    /// Called by GameInput.Poll() each frame.
    /// Snapshots pending events into frame state, adds mouse pointer, updates keyboard/gamepad.
    /// </summary>
    public void Poll(GameInput gameInput)
    {
        if (!_attached) return;

        // The frame's mouse pointer and keyboard events
        _pushed.Poll(gameInput);

        // Poll gamepad
        PollGamepad(gameInput);
    }

    private void PollGamepad(GameInput gameInput)
    {
        using var navigator = new Navigator();
        var gamepads = navigator.GetGamepads();
        if (gamepads == null) return;

        foreach (var gp in gamepads)
        {
            if (gp == null || !gp.Connected) continue;

            var axes = gp.Axes;
            Vector2 left = Vector2.Zero, right = Vector2.Zero;
            if (axes.Length >= 2)
                left = ApplyDeadZone(new Vector2((float)axes[0], (float)axes[1]));
            if (axes.Length >= 4)
                right = ApplyDeadZone(new Vector2((float)axes[2], (float)axes[3]));

            gameInput.Gamepad.Connected = true;
            gameInput.Gamepad.LeftStick = left;
            gameInput.Gamepad.RightStick = right;

            var buttons = gp.Buttons;
            for (int i = 0; i < buttons.Length && i < 20; i++)
                gameInput.Gamepad.SetButton(i, buttons[i].Pressed);

            gp.Dispose();
            break; // Use first connected gamepad
        }
    }

    private static Vector2 ApplyDeadZone(Vector2 stick)
    {
        float mag = stick.Length();
        if (mag < DeadZone) return Vector2.Zero;
        return stick * ((mag - DeadZone) / (1f - DeadZone) / mag);
    }

    // DOM event handlers
    private void OnMouseMove(MouseEvent e) => _pushed.MouseMove((float)e.OffsetX, (float)e.OffsetY);

    private void OnMouseDown(MouseEvent e) => _pushed.MouseDown((int)e.Button);

    private void OnMouseUp(MouseEvent e) => _pushed.MouseUp((int)e.Button);

    private void OnWheel(WheelEvent e)
    {
        // Canvas owns the wheel while the pointer is over it - do not scroll the host page.
        e.PreventDefault();
        _pushed.Wheel((float)e.DeltaY);
    }

    private void OnKeyDown(KeyboardEvent e)
    {
        // Printable text input: a single character typed with no Ctrl/Alt/Meta held.
        bool types = e.Key.Length == 1 && !e.CtrlKey && !e.AltKey && !e.MetaKey;
        _pushed.KeyDown(e.Code, types ? e.Key : null);
    }

    private void OnKeyUp(KeyboardEvent e) => _pushed.KeyUp(e.Code);

    public void Dispose()
    {
        if (!_attached) return;
        _attached = false;

        // Detach events (equal -= for every +=)
        if (_canvas != null)
        {
            if (_onMouseMove != null) _canvas.OnMouseMove -= _onMouseMove;
            if (_onMouseDown != null) _canvas.OnMouseDown -= _onMouseDown;
            if (_onMouseUp != null) _canvas.OnMouseUp -= _onMouseUp;
            if (_onWheel != null) _canvas.RemoveEventListener("wheel", _onWheel, _wheelListenOpts);
        }
        if (_window != null)
        {
            if (_onKeyDown != null) _window.OnKeyDown -= _onKeyDown;
            if (_onKeyUp != null) _window.OnKeyUp -= _onKeyUp;
        }

        // Dispose callbacks
        _onMouseMove?.Dispose();
        _onMouseDown?.Dispose();
        _onMouseUp?.Dispose();
        _onWheel?.Dispose();
        _onKeyDown?.Dispose();
        _onKeyUp?.Dispose();

        // Dispose owned SpawnJS wrappers (canvas only if Attach created it)
        if (_ownsCanvas)
            _canvas?.Dispose();
        _canvas = null;
        _window?.Dispose();
        _window = null;
    }
}
