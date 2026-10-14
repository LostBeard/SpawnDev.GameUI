using System.Numerics;

namespace SpawnDev.GameUI.Input;

/// <summary>
/// Input provider fed by its host instead of by DOM events: the host calls <see cref="MouseMove"/>, <see cref="MouseDown"/>,
/// <see cref="KeyDown"/> and friends as the input happens, and <see cref="Poll"/> hands the buffered frame to
/// <see cref="GameInput"/>. For a UI that draws somewhere there is no DOM to listen to - above all a render loop in a Web
/// Worker, where the main thread owns the canvas's events and forwards them - and for tests and replays.
/// <see cref="MouseKeyboardProvider"/> is this class with DOM listeners calling the push methods, so both buffer a frame
/// the same way.
///
/// Single-threaded by design (the Blazor WASM thread, or the worker's): push and Poll from the thread that runs the UI.
/// </summary>
public class PushedInputProvider : IInputProvider
{
    // Mouse state - event-buffered between polls
    private Vector2 _mousePos;
    private readonly bool[] _mouseDown = new bool[3];
    private readonly bool[] _pendingPressed = new bool[3];
    private readonly bool[] _pendingReleased = new bool[3];
    private float _scrollAccum;

    // Keyboard state - event-buffered between polls
    private readonly HashSet<string> _keysDown = new();
    private readonly HashSet<string> _pendingKeyPressed = new();
    private readonly HashSet<string> _pendingKeyReleased = new();
    private string _textInputAccum = "";

    /// <summary>
    /// Whether Poll reports a mouse pointer. Turn it off while something else owns the mouse (a camera holding pointer lock):
    /// the UI then sees no pointer, so nothing hovers or clicks under a cursor that is not on screen. Events pushed while
    /// it is off still update the buffered state, and keys keep flowing either way.
    /// </summary>
    public bool PointerActive { get; set; } = true;

    /// <summary>Move the mouse to (<paramref name="x"/>, <paramref name="y"/>) in the UI's own pixels (canvas-relative).</summary>
    public void MouseMove(float x, float y) => _mousePos = new Vector2(x, y);

    /// <summary>Press mouse <paramref name="button"/> (0 primary, 1 middle, 2 secondary). Other buttons are ignored.</summary>
    public void MouseDown(int button)
    {
        if ((uint)button >= 3) return;
        _mouseDown[button] = true;
        _pendingPressed[button] = true;
    }

    /// <summary>Release mouse <paramref name="button"/> (0 primary, 1 middle, 2 secondary). Other buttons are ignored.</summary>
    public void MouseUp(int button)
    {
        if ((uint)button >= 3) return;
        _mouseDown[button] = false;
        _pendingReleased[button] = true;
    }

    /// <summary>Scroll by <paramref name="deltaY"/> (DOM wheel units: positive is down). Accumulates until the next Poll.</summary>
    public void Wheel(float deltaY) => _scrollAccum += deltaY;

    /// <summary>
    /// Press the key with DOM <c>KeyboardEvent.code</c> <paramref name="code"/>. A key already down (OS auto-repeat) is not a new
    /// press. <paramref name="text"/> is the printable character it types (DOM <c>key</c> for a single character with no
    /// Ctrl/Alt/Meta held), or null when it types nothing; it is appended to the frame's text input on every call, repeats
    /// included, so holding a letter types it again.
    /// </summary>
    public void KeyDown(string code, string? text = null)
    {
        if (_keysDown.Add(code))
            _pendingKeyPressed.Add(code);
        if (!string.IsNullOrEmpty(text))
            _textInputAccum += text;
    }

    /// <summary>Release the key with DOM code <paramref name="code"/>.</summary>
    public void KeyUp(string code) => _pendingKeyReleased.Add(code);

    /// <summary>
    /// Let go of every held key and mouse button: the host lost focus (window blur, pointer lock taken, tab hidden) and will
    /// never see the matching up events, which would leave them held down for good. Keys report a normal release. A held mouse
    /// button is CANCELLED, not released: it stops being pressed but raises no release, so the button under the cursor when
    /// focus left does not count as clicked.
    /// </summary>
    public void ReleaseAll()
    {
        foreach (var code in _keysDown)
            _pendingKeyReleased.Add(code);
        for (int i = 0; i < 3; i++)
        {
            _mouseDown[i] = false;
            _pendingPressed[i] = false;
        }
    }

    /// <summary>Called by <see cref="GameInput.Poll"/>: hands the events buffered since the last poll to <paramref name="gameInput"/>.</summary>
    public void Poll(GameInput gameInput)
    {
        // Snapshot the frame's mouse events
        Span<bool> framePressed = stackalloc bool[3];
        Span<bool> frameReleased = stackalloc bool[3];
        for (int i = 0; i < 3; i++)
        {
            framePressed[i] = _pendingPressed[i];
            frameReleased[i] = _pendingReleased[i];
            _pendingPressed[i] = false;
            _pendingReleased[i] = false;
        }
        float scrollDelta = _scrollAccum;
        _scrollAccum = 0;

        // Snapshot the frame's keyboard events
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

        if (!PointerActive) return;
        gameInput.AddPointer(new Pointer
        {
            Type = PointerType.Mouse,
            ScreenPosition = _mousePos,
            IsPressed = _mouseDown[0],
            WasPressed = framePressed[0],
            WasReleased = frameReleased[0],
            IsSecondaryPressed = _mouseDown[2],
            WasSecondaryPressed = framePressed[2],
            WasSecondaryReleased = frameReleased[2],
            ScrollDelta = scrollDelta,
        });
    }

    public void Dispose() { }
}
