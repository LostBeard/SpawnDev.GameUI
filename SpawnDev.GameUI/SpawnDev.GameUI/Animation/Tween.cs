namespace SpawnDev.GameUI.Animation;

/// <summary>
/// A single active animation tween. Struct-based for zero allocation.
/// Tweens interpolate a float value from start to end over a duration using an easing function.
/// The setter callback writes the interpolated value back to the target property.
/// </summary>
public struct Tween
{
    /// <summary>Unique ID for this tween (for cancellation).</summary>
    public int Id;

    /// <summary>Start value.</summary>
    public float From;

    /// <summary>End value.</summary>
    public float To;

    /// <summary>Duration in seconds.</summary>
    public float Duration;

    /// <summary>Elapsed time in seconds.</summary>
    public float Elapsed;

    /// <summary>Easing function to apply.</summary>
    public EasingType Easing;

    /// <summary>Delay before starting (seconds).</summary>
    public float Delay;

    /// <summary>Whether this tween has completed.</summary>
    public bool IsComplete;

    /// <summary>Callback index into the TweenManager's setter list.</summary>
    internal int SetterIndex;

    /// <summary>Callback index for completion handler.</summary>
    internal int CompletionIndex;
}

/// <summary>
/// Standard easing functions for animations.
/// </summary>
public enum EasingType
{
    Linear,
    EaseIn,          // quadratic ease in
    EaseOut,         // quadratic ease out
    EaseInOut,       // quadratic ease in-out
    EaseInCubic,
    EaseOutCubic,
    EaseInOutCubic,
    EaseOutBack,     // slight overshoot
    EaseOutElastic,  // springy overshoot
    EaseOutBounce,   // bouncy landing
}

/// <summary>
/// Easing function implementations. All take t in [0,1] and return mapped t.
/// </summary>
public static class Easing
{
    public static float Apply(EasingType type, float t)
    {
        return type switch
        {
            EasingType.Linear => t,
            EasingType.EaseIn => t * t,
            EasingType.EaseOut => t * (2 - t),
            EasingType.EaseInOut => t < 0.5f ? 2 * t * t : -1 + (4 - 2 * t) * t,
            EasingType.EaseInCubic => t * t * t,
            EasingType.EaseOutCubic => 1 - MathF.Pow(1 - t, 3),
            EasingType.EaseInOutCubic => t < 0.5f ? 4 * t * t * t : 1 - MathF.Pow(-2 * t + 2, 3) / 2,
            EasingType.EaseOutBack => EaseOutBack(t),
            EasingType.EaseOutElastic => EaseOutElastic(t),
            EasingType.EaseOutBounce => EaseOutBounce(t),
            _ => t,
        };
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1;
        return 1 + c3 * MathF.Pow(t - 1, 3) + c1 * MathF.Pow(t - 1, 2);
    }

    private static float EaseOutElastic(float t)
    {
        if (t <= 0) return 0;
        if (t >= 1) return 1;
        const float c4 = (2 * MathF.PI) / 3;
        return MathF.Pow(2, -10 * t) * MathF.Sin((t * 10 - 0.75f) * c4) + 1;
    }

    private static float EaseOutBounce(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;
        if (t < 1 / d1) return n1 * t * t;
        if (t < 2 / d1) return n1 * (t -= 1.5f / d1) * t + 0.75f;
        if (t < 2.5f / d1) return n1 * (t -= 2.25f / d1) * t + 0.9375f;
        return n1 * (t -= 2.625f / d1) * t + 0.984375f;
    }
}
