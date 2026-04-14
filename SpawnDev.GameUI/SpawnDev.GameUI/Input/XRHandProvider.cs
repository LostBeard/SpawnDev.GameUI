using SpawnDev.BlazorJS.JSObjects;
using System.Numerics;

namespace SpawnDev.GameUI.Input;

/// <summary>
/// Input provider for WebXR hand tracking.
/// Reads 25 joint positions per hand from XRHand, detects pinch gestures,
/// and produces ray-casting Pointer from index finger direction.
///
/// Pinch detection: measures distance between thumb tip and index finger tip.
/// When distance is below threshold, pinch is active (equivalent to trigger press).
///
/// Ray direction: from wrist through index finger tip (pointing gesture).
///
/// Quest 3/3S hand tracking provides sub-millimeter joint positions.
/// All WebXR access via SpawnDev.BlazorJS typed wrappers.
/// </summary>
public class XRHandProvider : IInputProvider
{
    /// <summary>Pinch threshold in meters. Quest hand tracking is ~1mm precision.</summary>
    public float PinchThreshold { get; set; } = 0.025f; // 2.5cm

    /// <summary>Pinch release threshold (hysteresis to prevent flicker).</summary>
    public float PinchReleaseThreshold { get; set; } = 0.04f; // 4cm

    private XRFrame? _currentFrame;
    private XRReferenceSpace? _referenceSpace;
    private XRSession? _session;

    // Per-hand state
    private bool _leftPinching, _rightPinching;
    private bool _prevLeftPinch, _prevRightPinch;

    // WebXR hand joint names (W3C spec order)
    private static readonly string[] JointNames = new[]
    {
        "wrist",
        "thumb-metacarpal", "thumb-phalanx-proximal", "thumb-phalanx-distal", "thumb-tip",
        "index-finger-metacarpal", "index-finger-phalanx-proximal", "index-finger-phalanx-intermediate", "index-finger-phalanx-distal", "index-finger-tip",
        "middle-finger-metacarpal", "middle-finger-phalanx-proximal", "middle-finger-phalanx-intermediate", "middle-finger-phalanx-distal", "middle-finger-tip",
        "ring-finger-metacarpal", "ring-finger-phalanx-proximal", "ring-finger-phalanx-intermediate", "ring-finger-phalanx-distal", "ring-finger-tip",
        "pinky-finger-metacarpal", "pinky-finger-phalanx-proximal", "pinky-finger-phalanx-intermediate", "pinky-finger-phalanx-distal", "pinky-finger-tip",
    };

    // Joint indices for quick access
    private const int Wrist = 0;
    private const int ThumbTip = 4;
    private const int IndexTip = 9;
    private const int IndexProximal = 6;

    public void SetSession(XRSession session) => _session = session;
    public void ClearSession() { _session = null; _currentFrame = null; _referenceSpace = null; }
    public void UpdateFrame(XRFrame frame, XRReferenceSpace referenceSpace)
    {
        _currentFrame = frame;
        _referenceSpace = referenceSpace;
    }

    public void Poll(GameInput gameInput)
    {
        if (_session == null || _currentFrame == null || _referenceSpace == null) return;

        var inputSources = _session.InputSources;
        if (inputSources == null) return;

        foreach (var source in inputSources)
        {
            if (source == null) continue;

            // Only process hand inputs
            var hand = source.Hand;
            if (hand == null) continue;

            var handedness = source.Handedness;
            var handEnum = handedness switch
            {
                "left" => Handedness.Left,
                "right" => Handedness.Right,
                _ => Handedness.None
            };

            // Read all 25 joint positions
            var jointPositions = new Vector3[25];
            var jointRadii = new float[25];
            bool hasJoints = false;

            for (int i = 0; i < JointNames.Length && i < 25; i++)
            {
                var jointSpace = hand.Get(JointNames[i]);
                if (jointSpace == null) continue;

                using var jointPose = _currentFrame.GetJointPose(jointSpace, _referenceSpace);
                if (jointPose == null) continue;

                var transform = jointPose.Transform;
                if (transform == null) continue;

                using var pos = transform.Position;
                jointPositions[i] = new Vector3((float)pos.X, (float)pos.Y, (float)pos.Z);
                jointRadii[i] = (float)jointPose.Radius;
                hasJoints = true;
            }

            if (!hasJoints) continue;

            // Compute pinch: distance between thumb tip and index finger tip
            var thumbTipPos = jointPositions[ThumbTip];
            var indexTipPos = jointPositions[IndexTip];
            float pinchDistance = Vector3.Distance(thumbTipPos, indexTipPos);

            // Hysteresis: harder to start pinch, easier to maintain
            ref bool isPinching = ref (handEnum == Handedness.Left ? ref _leftPinching : ref _rightPinching);
            ref bool prevPinch = ref (handEnum == Handedness.Left ? ref _prevLeftPinch : ref _prevRightPinch);

            if (isPinching)
                isPinching = pinchDistance < PinchReleaseThreshold;
            else
                isPinching = pinchDistance < PinchThreshold;

            float pinchStrength = 1f - Math.Clamp(pinchDistance / PinchReleaseThreshold, 0f, 1f);

            // Ray: from wrist through index finger tip (pointing direction)
            var wristPos = jointPositions[Wrist];
            var rayOrigin = jointPositions[IndexProximal]; // start from index proximal for better aiming
            var rayDirection = Vector3.Normalize(indexTipPos - wristPos);

            bool wasPressed = isPinching && !prevPinch;
            bool wasReleased = !isPinching && prevPinch;
            prevPinch = isPinching;

            var pointer = new Pointer
            {
                Type = PointerType.Hand,
                Hand = handEnum,
                RayOrigin = rayOrigin,
                RayDirection = rayDirection,
                IsPressed = isPinching,
                WasPressed = wasPressed,
                WasReleased = wasReleased,
                PinchStrength = pinchStrength,
                JointPositions = jointPositions,
                JointRadii = jointRadii,
            };

            gameInput.AddPointer(pointer);
        }
    }

    public void Dispose()
    {
        ClearSession();
    }
}
