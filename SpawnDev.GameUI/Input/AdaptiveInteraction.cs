using System.Numerics;

namespace SpawnDev.GameUI.Input;

/// <summary>
/// Automatically switches between ray pointing and direct poke interaction
/// based on hand distance to the nearest UI panel.
///
/// Far (> 0.5m): ray pointer mode - index finger direction casts a ray
/// Near (< 0.3m): poke mode - finger tip position directly interacts
/// Transition zone (0.3-0.5m): smooth blend
///
/// This is the "just works" interaction model for hand tracking.
/// Users don't think about modes - they point at far panels and poke near ones.
/// </summary>
public class AdaptiveInteraction
{
    /// <summary>Distance below which poke mode activates (meters).</summary>
    public float PokeDistance { get; set; } = 0.3f;

    /// <summary>Distance above which ray mode activates (meters).</summary>
    public float RayDistance { get; set; } = 0.5f;

    /// <summary>Current interaction mode per hand.</summary>
    public InteractionMode LeftMode { get; private set; } = InteractionMode.Ray;
    public InteractionMode RightMode { get; private set; } = InteractionMode.Ray;

    /// <summary>Blend factor per hand (0 = full ray, 1 = full poke).</summary>
    public float LeftBlend { get; private set; }
    public float RightBlend { get; private set; }

    /// <summary>
    /// Update the interaction mode based on hand distance to the nearest panel.
    /// Call per frame with each hand's wrist position and the nearest panel distance.
    /// </summary>
    public void Update(Pointer handPointer, float distanceToNearestPanel)
    {
        float blend;
        InteractionMode mode;

        if (distanceToNearestPanel < PokeDistance)
        {
            blend = 1f; // full poke
            mode = InteractionMode.Poke;
        }
        else if (distanceToNearestPanel > RayDistance)
        {
            blend = 0f; // full ray
            mode = InteractionMode.Ray;
        }
        else
        {
            // Transition zone - smooth blend
            blend = 1f - (distanceToNearestPanel - PokeDistance) / (RayDistance - PokeDistance);
            mode = blend > 0.5f ? InteractionMode.Poke : InteractionMode.Ray;
        }

        if (handPointer.Hand == Handedness.Left)
        {
            LeftMode = mode;
            LeftBlend = blend;
        }
        else
        {
            RightMode = mode;
            RightBlend = blend;
        }
    }

    /// <summary>Get the current mode for a hand.</summary>
    public InteractionMode GetMode(Handedness hand) =>
        hand == Handedness.Left ? LeftMode : RightMode;

    /// <summary>Get the blend factor for a hand (0 = ray, 1 = poke).</summary>
    public float GetBlend(Handedness hand) =>
        hand == Handedness.Left ? LeftBlend : RightBlend;
}

/// <summary>How the user interacts with UI panels.</summary>
public enum InteractionMode
{
    /// <summary>Ray cast from hand/finger for far panels.</summary>
    Ray,
    /// <summary>Direct finger touch for near panels.</summary>
    Poke,
}
