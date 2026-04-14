namespace SpawnDev.GameUI.Animation;

/// <summary>
/// Manages active tweens for UI animations. One instance per UI tree (typically global).
/// Call Update(dt) each frame to advance all active tweens.
///
/// Zero-allocation hot path: tweens are stored in a pre-allocated array.
/// Completed tweens are recycled. Setter callbacks are stored separately
/// to keep the Tween struct small and blittable.
///
/// Usage:
///   // Fade in a panel
///   TweenManager.Global.Start(v => panel.Opacity = v, from: 0, to: 1, duration: 0.3f);
///
///   // Slide a notification up with bounce
///   TweenManager.Global.Start(v => toast.Y = v, from: 100, to: 0, duration: 0.5f,
///       easing: EasingType.EaseOutBounce, onComplete: () => toast.Visible = false);
///
///   // Each frame:
///   TweenManager.Global.Update(deltaTime);
/// </summary>
public class TweenManager
{
    /// <summary>Global tween manager instance.</summary>
    public static TweenManager Global { get; } = new();

    private const int MaxTweens = 256;
    private readonly Tween[] _tweens = new Tween[MaxTweens];
    private readonly Action<float>?[] _setters = new Action<float>?[MaxTweens];
    private readonly Action?[] _completions = new Action?[MaxTweens];
    private int _activeTweenCount;
    private int _nextId = 1;

    /// <summary>Number of currently active tweens.</summary>
    public int ActiveCount => _activeTweenCount;

    /// <summary>
    /// Start a new tween animation.
    /// </summary>
    /// <param name="setter">Callback that receives the interpolated value each frame.</param>
    /// <param name="from">Start value.</param>
    /// <param name="to">End value.</param>
    /// <param name="duration">Duration in seconds.</param>
    /// <param name="easing">Easing function.</param>
    /// <param name="delay">Delay before starting (seconds).</param>
    /// <param name="onComplete">Called when the tween finishes.</param>
    /// <returns>Tween ID for cancellation.</returns>
    public int Start(Action<float> setter, float from, float to, float duration,
        EasingType easing = EasingType.EaseOut, float delay = 0f, Action? onComplete = null)
    {
        if (_activeTweenCount >= MaxTweens) return -1; // pool full

        int slot = _activeTweenCount;
        int id = _nextId++;

        _setters[slot] = setter;
        _completions[slot] = onComplete;
        _tweens[slot] = new Tween
        {
            Id = id,
            From = from,
            To = to,
            Duration = Math.Max(0.001f, duration),
            Elapsed = 0,
            Easing = easing,
            Delay = delay,
            IsComplete = false,
            SetterIndex = slot,
            CompletionIndex = slot,
        };

        // Set initial value
        setter(from);

        _activeTweenCount++;
        return id;
    }

    /// <summary>
    /// Cancel a tween by ID. The property stays at its current value.
    /// </summary>
    public void Cancel(int tweenId)
    {
        for (int i = 0; i < _activeTweenCount; i++)
        {
            if (_tweens[i].Id == tweenId)
            {
                RemoveTween(i);
                return;
            }
        }
    }

    /// <summary>
    /// Cancel all active tweens.
    /// </summary>
    public void CancelAll()
    {
        for (int i = 0; i < _activeTweenCount; i++)
        {
            _setters[i] = null;
            _completions[i] = null;
        }
        _activeTweenCount = 0;
    }

    /// <summary>
    /// Update all active tweens. Call once per frame with the frame's delta time.
    /// </summary>
    public void Update(float dt)
    {
        int i = 0;
        while (i < _activeTweenCount)
        {
            ref var tween = ref _tweens[i];

            // Handle delay
            if (tween.Delay > 0)
            {
                tween.Delay -= dt;
                i++;
                continue;
            }

            tween.Elapsed += dt;
            float t = Math.Clamp(tween.Elapsed / tween.Duration, 0f, 1f);
            float eased = Easing.Apply(tween.Easing, t);
            float value = tween.From + (tween.To - tween.From) * eased;

            // Apply value
            _setters[tween.SetterIndex]?.Invoke(value);

            if (t >= 1f)
            {
                // Ensure final value is exact
                _setters[tween.SetterIndex]?.Invoke(tween.To);

                // Fire completion callback
                _completions[tween.CompletionIndex]?.Invoke();

                RemoveTween(i);
                // Don't increment i - the swap brought a new tween to this slot
            }
            else
            {
                i++;
            }
        }
    }

    private void RemoveTween(int index)
    {
        // Swap with last active tween
        int last = _activeTweenCount - 1;
        if (index < last)
        {
            _tweens[index] = _tweens[last];
            _setters[index] = _setters[last];
            _completions[index] = _completions[last];
            // Update setter/completion indices
            _tweens[index].SetterIndex = index;
            _tweens[index].CompletionIndex = index;
        }
        _setters[last] = null;
        _completions[last] = null;
        _activeTweenCount--;
    }
}
