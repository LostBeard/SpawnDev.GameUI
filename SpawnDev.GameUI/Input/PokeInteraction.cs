using System.Numerics;

namespace SpawnDev.GameUI.Input;

/// <summary>
/// Poke interaction for hand tracking in VR.
/// Detects when the index finger tip intersects a world-space UI panel
/// and triggers button presses based on penetration depth.
///
/// This is how Meta's Interaction SDK works - your finger physically
/// pushes through the panel surface. The button visually depresses
/// proportional to finger depth. Haptic feedback (if available) fires
/// at the activation threshold.
///
/// Usage:
///   var poke = new PokeInteraction();
///
///   // Per frame, for each hand pointer with joint data:
///   if (pointer.Type == PointerType.Hand && pointer.JointPositions != null) {
///       var result = poke.TestPoke(pointer, worldPanel);
///       if (result.IsPoking) {
///           // Finger is touching or through the panel
///           button.VisualDepth = result.PenetrationDepth * 20f; // visual feedback
///       }
///       if (result.JustActivated) {
///           button.OnClick?.Invoke();
///           haptics.Pulse(pointer, HapticType.Click);
///       }
///   }
/// </summary>
public class PokeInteraction
{
    /// <summary>Distance from panel surface where poke begins (meters).</summary>
    public float PokeStartDistance { get; set; } = 0.02f; // 2cm

    /// <summary>Penetration depth to activate a press (meters).</summary>
    public float ActivationDepth { get; set; } = 0.015f; // 1.5cm

    /// <summary>Penetration depth to deactivate (release). Hysteresis prevents flicker.</summary>
    public float DeactivationDepth { get; set; } = 0.005f; // 0.5cm

    /// <summary>Index finger tip joint index in the 25-joint hand model.</summary>
    private const int IndexTipJoint = 9;

    // Per-hand state
    private bool _leftActive, _rightActive;
    private bool _prevLeftActive, _prevRightActive;

    /// <summary>
    /// Test if a hand pointer is poking a world-space panel.
    /// Returns the poke state including penetration depth.
    /// </summary>
    public PokeResult TestPoke(Pointer handPointer, Elements.UIWorldPanel panel)
    {
        if (handPointer.Type != PointerType.Hand || handPointer.JointPositions == null)
            return PokeResult.None;

        var fingerTip = handPointer.JointPositions[IndexTipJoint];
        var panelTransform = panel.WorldTransform;

        // Get panel position and normal from transform
        var panelPos = new Vector3(panelTransform.M41, panelTransform.M42, panelTransform.M43);
        var panelNormal = new Vector3(panelTransform.M31, panelTransform.M32, panelTransform.M33);
        panelNormal = Vector3.Normalize(panelNormal);

        // Signed distance from finger tip to panel plane
        // Positive = in front of panel, Negative = behind (poked through)
        float signedDist = Vector3.Dot(fingerTip - panelPos, panelNormal);

        // Check if finger is within the panel's XY bounds
        if (!Matrix4x4.Invert(panelTransform, out var invTransform))
            return PokeResult.None;

        var localPos = Vector3.Transform(fingerTip, invTransform);
        float halfW = panel.PanelWidth * panel.WorldScale * 0.5f;
        float halfH = panel.PanelHeight * panel.WorldScale * 0.5f;

        bool inBounds = localPos.X >= -halfW && localPos.X <= halfW &&
                        localPos.Y >= -halfH && localPos.Y <= halfH;

        if (!inBounds)
            return PokeResult.None;

        // Determine poke state
        float penetration = -signedDist; // positive when finger is through the panel
        bool isPoking = signedDist < PokeStartDistance;

        // Activation with hysteresis
        ref bool isActive = ref (handPointer.Hand == Handedness.Left ? ref _leftActive : ref _rightActive);
        ref bool prevActive = ref (handPointer.Hand == Handedness.Left ? ref _prevLeftActive : ref _prevRightActive);

        if (isActive)
            isActive = penetration > DeactivationDepth;
        else
            isActive = penetration > ActivationDepth;

        bool justActivated = isActive && !prevActive;
        bool justDeactivated = !isActive && prevActive;
        prevActive = isActive;

        // Compute local hit position on the panel (for UI element targeting)
        float hitU = (localPos.X + halfW) / (halfW * 2); // 0-1 across panel width
        float hitV = (localPos.Y + halfH) / (halfH * 2); // 0-1 across panel height
        var panelHitPos = new Vector2(hitU * panel.PanelWidth, hitV * panel.PanelHeight);

        return new PokeResult
        {
            IsPoking = isPoking,
            IsActive = isActive,
            JustActivated = justActivated,
            JustDeactivated = justDeactivated,
            PenetrationDepth = Math.Max(0, penetration),
            SignedDistance = signedDist,
            FingerTipWorld = fingerTip,
            PanelHitPosition = panelHitPos,
            Hand = handPointer.Hand,
        };
    }

    /// <summary>Reset state (call when switching scenes or panels).</summary>
    public void Reset()
    {
        _leftActive = _rightActive = false;
        _prevLeftActive = _prevRightActive = false;
    }
}

/// <summary>Result of a poke interaction test.</summary>
public struct PokeResult
{
    /// <summary>No poke detected.</summary>
    public static readonly PokeResult None = new();

    /// <summary>Whether the finger is near or through the panel.</summary>
    public bool IsPoking;

    /// <summary>Whether the poke has passed the activation threshold (button "pressed").</summary>
    public bool IsActive;

    /// <summary>True on the frame the poke activates (press event).</summary>
    public bool JustActivated;

    /// <summary>True on the frame the poke deactivates (release event).</summary>
    public bool JustDeactivated;

    /// <summary>How far the finger has penetrated through the panel (meters, >= 0).</summary>
    public float PenetrationDepth;

    /// <summary>Signed distance from panel surface (positive = in front, negative = behind).</summary>
    public float SignedDistance;

    /// <summary>Finger tip position in world space.</summary>
    public Vector3 FingerTipWorld;

    /// <summary>Hit position in panel-local pixel coordinates.</summary>
    public Vector2 PanelHitPosition;

    /// <summary>Which hand is poking.</summary>
    public Handedness Hand;
}
