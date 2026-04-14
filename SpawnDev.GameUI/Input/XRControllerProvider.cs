using SpawnDev.BlazorJS.JSObjects;
using System.Numerics;

namespace SpawnDev.GameUI.Input;

/// <summary>
/// Input provider for WebXR tracked controllers.
/// Reads controller poses, trigger/grip values, and thumbstick from XRInputSource.
/// Produces Pointer objects with 3D ray for UI interaction.
///
/// Usage:
///   var provider = new XRControllerProvider();
///   gameInput.AddProvider(provider);
///   // Each XR frame:
///   provider.UpdateFrame(xrFrame, referenceSpace);
///   gameInput.Poll();
///
/// All WebXR access via SpawnDev.BlazorJS typed wrappers.
/// </summary>
public class XRControllerProvider : IInputProvider
{
    private XRFrame? _currentFrame;
    private XRReferenceSpace? _referenceSpace;
    private XRSession? _session;

    // Track previous trigger state for press/release detection
    private readonly Dictionary<string, bool> _prevTriggerState = new(); // key: handedness

    /// <summary>
    /// Set the active XR session. Call when entering VR/AR.
    /// The provider reads InputSources from this session each frame.
    /// </summary>
    public void SetSession(XRSession session)
    {
        _session = session;
    }

    /// <summary>
    /// Clear the session when exiting VR/AR.
    /// </summary>
    public void ClearSession()
    {
        _session = null;
        _currentFrame = null;
        _referenceSpace = null;
    }

    /// <summary>
    /// Update with the current XR frame data.
    /// Call this each XR animation frame BEFORE GameInput.Poll().
    /// </summary>
    public void UpdateFrame(XRFrame frame, XRReferenceSpace referenceSpace)
    {
        _currentFrame = frame;
        _referenceSpace = referenceSpace;
    }

    /// <summary>
    /// Called by GameInput.Poll(). Reads controller state and adds pointers.
    /// </summary>
    public void Poll(GameInput gameInput)
    {
        if (_session == null || _currentFrame == null || _referenceSpace == null) return;

        var inputSources = _session.InputSources;
        if (inputSources == null) return;

        foreach (var source in inputSources)
        {
            if (source == null) continue;

            // Only process tracked-pointer controllers (not gaze or screen)
            var targetRayMode = source.TargetRayMode;
            if (targetRayMode != "tracked-pointer") continue;

            var handedness = source.Handedness;
            var hand = handedness switch
            {
                "left" => Handedness.Left,
                "right" => Handedness.Right,
                _ => Handedness.None
            };

            // Get the target ray pose (controller pointing direction)
            var targetRaySpace = source.TargetRaySpace;
            if (targetRaySpace == null) continue;

            using var pose = _currentFrame.GetPose(targetRaySpace, _referenceSpace);
            if (pose == null) continue;

            var transform = pose.Transform;
            if (transform == null) continue;

            // Extract ray origin from transform position
            using var position = transform.Position;
            var rayOrigin = new Vector3(
                (float)position.X,
                (float)position.Y,
                (float)position.Z
            );

            // Extract ray direction from transform orientation (forward = -Z in WebXR)
            using var orientation = transform.Orientation;
            var quat = new Quaternion(
                (float)orientation.X,
                (float)orientation.Y,
                (float)orientation.Z,
                (float)orientation.W
            );
            var rayDirection = Vector3.Transform(-Vector3.UnitZ, quat);
            rayDirection = Vector3.Normalize(rayDirection);

            // Read gamepad state (trigger, grip, thumbstick)
            float triggerValue = 0;
            float gripValue = 0;
            float thumbstickX = 0, thumbstickY = 0;
            bool triggerPressed = false;
            bool gripPressed = false;

            var gamepad = source.Gamepad;
            if (gamepad != null)
            {
                var buttons = gamepad.Buttons;
                if (buttons.Length > 0)
                {
                    triggerValue = (float)buttons[0].Value;
                    triggerPressed = buttons[0].Pressed;
                }
                if (buttons.Length > 1)
                {
                    gripValue = (float)buttons[1].Value;
                    gripPressed = buttons[1].Pressed;
                }

                var axes = gamepad.Axes;
                if (axes.Length >= 4)
                {
                    thumbstickX = (float)axes[2];
                    thumbstickY = (float)axes[3];
                }
            }

            // Detect press/release transitions
            string handKey = handedness ?? "none";
            bool wasTriggerPressed = _prevTriggerState.TryGetValue(handKey, out var prev) && !prev && triggerPressed;
            bool wasTriggerReleased = _prevTriggerState.TryGetValue(handKey, out var prev2) && prev2 && !triggerPressed;
            _prevTriggerState[handKey] = triggerPressed;

            var pointer = new Pointer
            {
                Type = PointerType.Controller,
                Hand = hand,
                RayOrigin = rayOrigin,
                RayDirection = rayDirection,
                IsPressed = triggerPressed,
                WasPressed = wasTriggerPressed,
                WasReleased = wasTriggerReleased,
                IsSecondaryPressed = gripPressed,
                TriggerValue = triggerValue,
                GripValue = gripValue,
                ScrollDelta = thumbstickY,
                HapticActuator = gamepad?.VibrationActuator,
            };

            gameInput.AddPointer(pointer);
        }
    }

    public void Dispose()
    {
        ClearSession();
    }
}
