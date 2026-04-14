using SpawnDev.BlazorJS.JSObjects;

namespace SpawnDev.GameUI.Input;

/// <summary>
/// Haptic feedback manager for VR controllers and gamepads.
/// Triggers vibration pulses on UI interactions (button hover, click, scroll).
/// Uses the Gamepad Haptic API via SpawnDev.BlazorJS typed wrappers.
///
/// Usage:
///   var haptics = new HapticFeedback();
///
///   // On button hover:
///   haptics.Pulse(pointer, HapticType.Hover);
///
///   // On button click:
///   haptics.Pulse(pointer, HapticType.Click);
///
///   // On error/rejection:
///   haptics.Pulse(pointer, HapticType.Error);
///
///   // Custom:
///   haptics.Pulse(pointer, intensity: 0.5f, duration: 100);
/// </summary>
public class HapticFeedback
{
    /// <summary>Whether haptic feedback is enabled globally.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Global intensity multiplier (0-1).</summary>
    public float IntensityScale { get; set; } = 1f;

    // Preset intensities and durations
    private static readonly (float intensity, int durationMs)[] Presets = new[]
    {
        (0.1f, 10),   // Hover - subtle tick
        (0.4f, 30),   // Click - firm tap
        (0.2f, 20),   // Release - soft release
        (0.15f, 15),  // Scroll - light step
        (0.6f, 80),   // Error - strong buzz
        (0.3f, 50),   // Success - medium pulse
        (0.8f, 150),  // Impact - heavy thud (damage taken)
    };

    /// <summary>
    /// Trigger a haptic pulse on the controller associated with a pointer.
    /// Only works for Controller pointers that have a Gamepad with haptic support.
    /// </summary>
    public void Pulse(Pointer pointer, HapticType type)
    {
        if (!Enabled || pointer.Type != PointerType.Controller) return;
        var (intensity, duration) = Presets[(int)type];
        Pulse(pointer, intensity * IntensityScale, duration);
    }

    /// <summary>
    /// Trigger a custom haptic pulse.
    /// </summary>
    /// <param name="pointer">The pointer (must be a Controller type).</param>
    /// <param name="intensity">Vibration intensity 0-1.</param>
    /// <param name="durationMs">Duration in milliseconds.</param>
    public void Pulse(Pointer pointer, float intensity, int durationMs)
    {
        if (!Enabled || pointer.Type != PointerType.Controller) return;
        if (intensity <= 0 || durationMs <= 0) return;

        // The pointer's source Gamepad has haptic actuators
        // We need to get the gamepad from the XR input source
        // For now, store the gamepad reference on the pointer via the XRControllerProvider
        // and use it here for vibration
        try
        {
            // Access via the GamepadHapticActuator if available
            // The XRControllerProvider should set this on the pointer
            if (pointer.HapticActuator != null)
            {
                pointer.HapticActuator.Pulse(intensity, durationMs);
            }
        }
        catch
        {
            // Haptics not available on this device - silently ignore
        }
    }
}

/// <summary>Preset haptic feedback types for common UI interactions.</summary>
public enum HapticType
{
    /// <summary>Subtle tick when hovering over an interactive element.</summary>
    Hover = 0,
    /// <summary>Firm tap when clicking/pressing a button.</summary>
    Click = 1,
    /// <summary>Soft feedback when releasing a button.</summary>
    Release = 2,
    /// <summary>Light step feedback per scroll increment.</summary>
    Scroll = 3,
    /// <summary>Strong buzz for error/rejection (can't drop here, invalid action).</summary>
    Error = 4,
    /// <summary>Medium pulse for success confirmation.</summary>
    Success = 5,
    /// <summary>Heavy thud for impact/damage events.</summary>
    Impact = 6,
}
