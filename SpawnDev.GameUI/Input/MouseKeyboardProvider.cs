using Microsoft.AspNetCore.Components;
using SpawnDev.BlazorJS;
using SpawnDev.BlazorJS.JSObjects;
using System.Numerics;

namespace SpawnDev.GameUI.Input;

/// <summary>
/// Input provider for mouse, keyboard, and gamepad via DOM events.
/// Uses SpawnDev.BlazorJS strongly typed wrappers - no IJSRuntime, no eval, no raw JS.
/// Events use ActionCallback += / -= pattern with proper lifecycle management.
///
/// Adapted from SpawnScene.UI.InputManager for the GameInput provider model.
/// </summary>
public class MouseKeyboardProvider : IInputProvider
{
    // Mouse state - event-buffered between polls
    private Vector2 _mousePos;
    private readonly bool[] _mouseDown = new bool[3];
    private readonly bool[] _pendingPressed = new bool[3];
    private readonly bool[] _pendingReleased = new bool[3];
    private readonly bool[] _framePressed = new bool[3];
    private readonly bool[] _frameReleased = new bool[3];
    private float _scrollAccum;

    // Keyboard state - event-buffered between polls
    private readonly HashSet<string> _keysDown = new();
    private readonly HashSet<string> _pendingKeyPressed = new();
    private readonly HashSet<string> _pendingKeyReleased = new();
    private string _textInputAccum = "";

    // Gamepad
    private const float DeadZone = 0.15f;

    // DOM event callbacks - prevent GC, properly disposed
    private ActionCallback<MouseEvent>? _onMouseMove;
    private ActionCallback<MouseEvent>? _onMouseDown;
    private ActionCallback<MouseEvent>? _onMouseUp;
    private ActionCallback<WheelEvent>? _onWheel;
    private ActionCallback<KeyboardEvent>? _onKeyDown;
    private ActionCallback<KeyboardEvent>? _onKeyUp;

    // BlazorJS typed wrappers - owned, disposed in Dispose()
    private HTMLCanvasElement? _canvas;
    private Window? _window;
    private bool _attached;

    /// <summary>
    /// Attach DOM event listeners to the canvas element.
    /// Must be called once before Poll() produces data.
    /// </summary>
    public void Attach(ElementReference canvasRef)
    {
        if (_attached) return;
        _attached = true;

        _canvas = new HTMLCanvasElement(canvasRef);
        _window = new Window();

        // Create callbacks (prevent GC)
        _onMouseMove = new ActionCallback<MouseEvent>(OnMouseMove);
        _onMouseDown = new ActionCallback<MouseEvent>(OnMouseDown);
        _onMouseUp = new ActionCallback<MouseEvent>(OnMouseUp);
        _onWheel = new ActionCallback<WheelEvent>(OnWheel);
        _onKeyDown = new ActionCallback<KeyboardEvent>(OnKeyDown);
        _onKeyUp = new ActionCallback<KeyboardEvent>(OnKeyUp);

        // Attach via BlazorJS typed events
        _canvas.OnMouseMove += _onMouseMove;
        _canvas.OnMouseDown += _onMouseDown;
        _canvas.OnMouseUp += _onMouseUp;
        _canvas.OnWheel += _onWheel;
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

        // Snapshot mouse events
        for (int i = 0; i < 3; i++)
        {
            _framePressed[i] = _pendingPressed[i];
            _frameReleased[i] = _pendingReleased[i];
            _pendingPressed[i] = false;
            _pendingReleased[i] = false;
        }
        float scrollDelta = _scrollAccum;
        _scrollAccum = 0;

        // Snapshot keyboard events
        foreach (var key in _pendingKeyPressed)
            gameInput.Keyboard.SetKeyDown(key);
        foreach (var key in _pendingKeyReleased)
        {
            gameInput.Keyboard.SetKeyUp(key);
            _keysDown.Remove(key);
        }
        if (_textInputAccum.Length > 0)
        {
            gameInput.Keyboard.AppendTextInput(_textInputAccum);
            _textInputAccum = "";
        }
        _pendingKeyPressed.Clear();
        _pendingKeyReleased.Clear();

        // Add mouse pointer
        var mousePointer = new Pointer
        {
            Type = PointerType.Mouse,
            ScreenPosition = _mousePos,
            IsPressed = _mouseDown[0],
            WasPressed = _framePressed[0],
            WasReleased = _frameReleased[0],
            IsSecondaryPressed = _mouseDown[2],
            WasSecondaryPressed = _framePressed[2],
            WasSecondaryReleased = _frameReleased[2],
            ScrollDelta = scrollDelta,
        };
        gameInput.AddPointer(mousePointer);

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
    private void OnMouseMove(MouseEvent e)
    {
        _mousePos = new Vector2((float)e.OffsetX, (float)e.OffsetY);
    }

    private void OnMouseDown(MouseEvent e)
    {
        int btn = (int)e.Button;
        if (btn < 3) { _mouseDown[btn] = true; _pendingPressed[btn] = true; }
    }

    private void OnMouseUp(MouseEvent e)
    {
        int btn = (int)e.Button;
        if (btn < 3) { _mouseDown[btn] = false; _pendingReleased[btn] = true; }
    }

    private void OnWheel(WheelEvent e)
    {
        _scrollAccum += (float)e.DeltaY;
    }

    private void OnKeyDown(KeyboardEvent e)
    {
        string code = e.Code;
        if (!_keysDown.Contains(code))
        {
            _keysDown.Add(code);
            _pendingKeyPressed.Add(code);
        }
        // Accumulate printable text input
        if (e.Key.Length == 1 && !e.CtrlKey && !e.AltKey && !e.MetaKey)
            _textInputAccum += e.Key;
    }

    private void OnKeyUp(KeyboardEvent e)
    {
        _pendingKeyReleased.Add(e.Code);
    }

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
            if (_onWheel != null) _canvas.OnWheel -= _onWheel;
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

        // Dispose owned BlazorJS wrappers
        _canvas?.Dispose();
        _window?.Dispose();
    }
}
