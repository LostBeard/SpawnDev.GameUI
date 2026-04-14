using System.Numerics;

namespace SpawnDev.GameUI.Input;

/// <summary>
/// Gaze-based input provider for VR/AR.
/// Uses the head direction (XRViewerPose forward) as a pointer ray.
/// Supports dwell selection - look at a button for DwellTime seconds to activate.
///
/// Essential for:
/// - Accessibility (users who can't use hands)
/// - Eye tracking (future XREye API)
/// - Hands-free mode (controller in pocket)
/// - Fallback when hand tracking loses tracking
///
/// Usage:
///   var gaze = new GazeProvider();
///   gameInput.AddProvider(gaze);
///
///   // Per XR frame:
///   gaze.UpdateFromViewerPose(viewerPose, referenceSpace);
///   // Or manually:
///   gaze.SetGaze(headPosition, headForward);
/// </summary>
public class GazeProvider : IInputProvider
{
    private Vector3 _position;
    private Vector3 _direction = -Vector3.UnitZ;
    private bool _hasData;

    // Dwell selection state
    private UIElement? _dwellTarget;
    private float _dwellTimer;
    private bool _dwellActivated;

    /// <summary>Time in seconds to look at a button to activate it.</summary>
    public float DwellTime { get; set; } = 1.5f;

    /// <summary>Whether dwell selection is enabled.</summary>
    public bool DwellEnabled { get; set; } = true;

    /// <summary>Current dwell progress (0-1).</summary>
    public float DwellProgress => _dwellTarget != null ? Math.Clamp(_dwellTimer / DwellTime, 0, 1) : 0;

    /// <summary>The element currently being dwelled on.</summary>
    public UIElement? DwellTarget => _dwellTarget;

    /// <summary>Set gaze direction manually (head position + forward vector).</summary>
    public void SetGaze(Vector3 position, Vector3 forward)
    {
        _position = position;
        _direction = Vector3.Normalize(forward);
        _hasData = true;
    }

    /// <summary>
    /// Update gaze from XR viewer pose.
    /// Extract head position and forward direction from the pose transform.
    /// </summary>
    public void UpdateFromViewerPose(Vector3 headPosition, Quaternion headOrientation)
    {
        _position = headPosition;
        _direction = Vector3.Transform(-Vector3.UnitZ, headOrientation);
        _direction = Vector3.Normalize(_direction);
        _hasData = true;
    }

    /// <summary>
    /// Update dwell selection. Call per frame with the currently hovered element.
    /// Returns true if the dwell activated this frame.
    /// </summary>
    public bool UpdateDwell(UIElement? hoveredElement, float dt)
    {
        if (!DwellEnabled || hoveredElement == null)
        {
            _dwellTarget = null;
            _dwellTimer = 0;
            _dwellActivated = false;
            return false;
        }

        if (hoveredElement != _dwellTarget)
        {
            // New target - reset timer
            _dwellTarget = hoveredElement;
            _dwellTimer = 0;
            _dwellActivated = false;
        }

        _dwellTimer += dt;

        if (_dwellTimer >= DwellTime && !_dwellActivated)
        {
            _dwellActivated = true;
            return true; // activated this frame
        }

        return false;
    }

    public void Poll(GameInput gameInput)
    {
        if (!_hasData) return;

        var pointer = new Pointer
        {
            Type = PointerType.Gaze,
            Hand = Handedness.None,
            RayOrigin = _position,
            RayDirection = _direction,
            IsPressed = _dwellActivated && _dwellTimer >= DwellTime,
            WasPressed = false, // dwell activation is handled separately
            WasReleased = false,
        };

        gameInput.AddPointer(pointer);
    }

    public void Dispose() { }
}
