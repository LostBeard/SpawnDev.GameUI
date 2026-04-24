using System.Numerics;

namespace SpawnDev.GameUI.Input;

/// <summary>
/// Unified input abstraction for all input sources.
/// Aggregates mouse, keyboard, gamepad, VR controllers, and hand tracking
/// into a common interface that UI elements consume.
/// Call Poll() at the start of each frame to snapshot input state.
/// </summary>
public class GameInput
{
    /// <summary>All active pointers this frame (mouse, VR controllers, hands, touch).</summary>
    public IReadOnlyList<Pointer> Pointers => _pointers;
    private readonly List<Pointer> _pointers = new();

    /// <summary>The primary pointer (first active pointer, usually mouse or dominant hand).</summary>
    public Pointer? PrimaryPointer => _pointers.Count > 0 ? _pointers[0] : null;

    /// <summary>Keyboard state.</summary>
    public KeyboardState Keyboard { get; } = new();

    /// <summary>Gamepad state (first connected gamepad).</summary>
    public GamepadState Gamepad { get; } = new();

    /// <summary>Active input providers that feed this GameInput.</summary>
    private readonly List<IInputProvider> _providers = new();

    /// <summary>Register an input provider (mouse, VR controller, hand tracker, etc.).</summary>
    public void AddProvider(IInputProvider provider)
    {
        _providers.Add(provider);
    }

    /// <summary>Remove an input provider.</summary>
    public void RemoveProvider(IInputProvider provider)
    {
        _providers.Remove(provider);
    }

    /// <summary>
    /// Poll all input providers and update state.
    /// Call once at the start of each frame before UI updates.
    /// </summary>
    public void Poll()
    {
        _pointers.Clear();
        Keyboard.BeginFrame();
        Gamepad.BeginFrame();

        foreach (var provider in _providers)
        {
            provider.Poll(this);
        }
    }

    /// <summary>Add a pointer from a provider during Poll().</summary>
    internal void AddPointer(Pointer pointer)
    {
        _pointers.Add(pointer);
    }
}

/// <summary>
/// A pointer is any input that can point at UI elements.
/// Unified representation of mouse cursor, VR controller ray, hand index finger, gaze, or touch point.
/// </summary>
public class Pointer
{
    /// <summary>What kind of input device this pointer represents.</summary>
    public PointerType Type { get; init; }

    /// <summary>Which hand (for VR controllers and hand tracking).</summary>
    public Handedness Hand { get; init; } = Handedness.None;

    /// <summary>Screen-space position (for mouse/touch). Null for 3D-only pointers.</summary>
    public Vector2? ScreenPosition { get; set; }

    /// <summary>World-space ray origin (for VR controllers, gaze, hand pointing).</summary>
    public Vector3? RayOrigin { get; set; }

    /// <summary>World-space ray direction (normalized).</summary>
    public Vector3? RayDirection { get; set; }

    /// <summary>Primary action (left click, trigger press, pinch).</summary>
    public bool IsPressed { get; set; }

    /// <summary>Primary action was just pressed this frame.</summary>
    public bool WasPressed { get; set; }

    /// <summary>Primary action was just released this frame.</summary>
    public bool WasReleased { get; set; }

    /// <summary>Secondary action (right click, grip/squeeze).</summary>
    public bool IsSecondaryPressed { get; set; }

    /// <summary>Secondary action was just pressed this frame.</summary>
    public bool WasSecondaryPressed { get; set; }

    /// <summary>Secondary action was just released this frame.</summary>
    public bool WasSecondaryReleased { get; set; }

    /// <summary>Scroll/thumbstick Y axis (mouse wheel, thumbstick up/down).</summary>
    public float ScrollDelta { get; set; }

    /// <summary>Trigger analog value 0-1 (for VR controllers).</summary>
    public float TriggerValue { get; set; }

    /// <summary>Grip analog value 0-1 (for VR controllers).</summary>
    public float GripValue { get; set; }

    /// <summary>
    /// Hand joint positions (25 joints per hand, index into XRHand joint enum).
    /// Only populated for hand tracking pointers.
    /// </summary>
    public Vector3[]? JointPositions { get; set; }

    /// <summary>
    /// Hand joint radii (distance from skin, 25 values).
    /// Only populated for hand tracking pointers.
    /// </summary>
    public float[]? JointRadii { get; set; }

    /// <summary>Pinch strength 0-1 for hand tracking (thumb-to-index distance).</summary>
    public float PinchStrength { get; set; }

    /// <summary>
    /// Haptic actuator for this controller (if available).
    /// Set by XRControllerProvider from the Gamepad's vibrationActuator.
    /// </summary>
    public SpawnDev.BlazorJS.JSObjects.GamepadHapticActuator? HapticActuator { get; set; }
}

/// <summary>Type of input device a Pointer represents.</summary>
public enum PointerType
{
    /// <summary>Mouse cursor on screen.</summary>
    Mouse,
    /// <summary>Touch point on screen.</summary>
    Touch,
    /// <summary>VR controller with tracked position and trigger.</summary>
    Controller,
    /// <summary>Hand tracking with joint positions and pinch detection.</summary>
    Hand,
    /// <summary>Gaze direction (eye tracking or head-locked reticle).</summary>
    Gaze
}

/// <summary>Which hand a pointer belongs to.</summary>
public enum Handedness
{
    None,
    Left,
    Right
}

/// <summary>Keyboard state snapshot for the current frame.</summary>
public class KeyboardState
{
    private readonly HashSet<string> _keysDown = new();
    private readonly HashSet<string> _keysPressed = new();
    private readonly HashSet<string> _keysReleased = new();
    private string _textInput = "";

    public bool IsKeyDown(string key) => _keysDown.Contains(key);
    public bool WasKeyPressed(string key) => _keysPressed.Contains(key);
    public bool WasKeyReleased(string key) => _keysReleased.Contains(key);
    public IReadOnlyCollection<string> KeysDown => _keysDown;
    public IReadOnlyCollection<string> KeysPressed => _keysPressed;
    public string TextInput => _textInput;

    internal void BeginFrame()
    {
        _keysPressed.Clear();
        _keysReleased.Clear();
        _textInput = "";
    }

    internal void SetKeyDown(string key) { _keysDown.Add(key); _keysPressed.Add(key); }
    internal void SetKeyUp(string key) { _keysDown.Remove(key); _keysReleased.Add(key); }
    internal void AppendTextInput(string text) { _textInput += text; }
}

/// <summary>Gamepad state snapshot.</summary>
public class GamepadState
{
    public bool Connected { get; internal set; }
    public Vector2 LeftStick { get; internal set; }
    public Vector2 RightStick { get; internal set; }
    private readonly bool[] _buttons = new bool[20];
    private readonly bool[] _buttonsPressed = new bool[20];

    public bool IsButtonDown(int button) => button < _buttons.Length && _buttons[button];
    public bool WasButtonPressed(int button) => button < _buttonsPressed.Length && _buttonsPressed[button];

    internal void BeginFrame()
    {
        Array.Clear(_buttonsPressed);
    }

    internal void SetButton(int button, bool pressed)
    {
        if (button >= _buttons.Length) return;
        if (pressed && !_buttons[button]) _buttonsPressed[button] = true;
        _buttons[button] = pressed;
    }
}
