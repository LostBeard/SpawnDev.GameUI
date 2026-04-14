namespace SpawnDev.GameUI.Input;

/// <summary>
/// Input provider interface. Implementations feed pointers and input state
/// into GameInput during each frame's Poll() call.
///
/// Built-in providers:
/// - MouseKeyboardProvider: DOM mouse/keyboard events + gamepad polling
/// - XRControllerProvider: WebXR tracked controllers (trigger, grip, thumbstick)
/// - XRHandProvider: WebXR hand tracking (25 joints, pinch detection)
/// - TouchProvider: DOM touch events
/// </summary>
public interface IInputProvider : IDisposable
{
    /// <summary>
    /// Called by GameInput.Poll() each frame.
    /// The provider should add its pointers via gameInput.AddPointer()
    /// and update keyboard/gamepad state.
    /// </summary>
    void Poll(GameInput gameInput);
}
