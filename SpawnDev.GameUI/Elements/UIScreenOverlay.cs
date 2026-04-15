using System.Drawing;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Fullscreen overlay effects rendered on top of all UI.
/// Used for damage flash, low health vignette, screen tint, fade in/out.
///
/// Each active effect has a duration and fade curve. Multiple effects can
/// stack (drawn in order, alpha-blended). Effects auto-remove when expired.
///
/// Usage:
///   var overlay = new UIScreenOverlay();
///
///   // Damage flash (red, fades out over 0.3s)
///   overlay.Flash(Color.FromArgb(100, 255, 0, 0), 0.3f);
///
///   // Fade to black (1 second)
///   overlay.FadeIn(Color.Black, 1.0f);
///
///   // Persistent low health vignette
///   overlay.SetPersistent("lowHealth", Color.FromArgb(60, 180, 0, 0));
///
///   // Per frame:
///   overlay.Update(dt);
///   overlay.Draw(renderer, viewportW, viewportH);
/// </summary>
public class UIScreenOverlay
{
    private readonly List<OverlayEffect> _effects = new();
    private readonly Dictionary<string, PersistentOverlay> _persistent = new();

    /// <summary>
    /// Flash the screen with a color that fades out over duration.
    /// Common use: damage flash (red), heal flash (green), pickup flash (gold).
    /// </summary>
    public void Flash(Color color, float duration)
    {
        _effects.Add(new OverlayEffect
        {
            Color = color,
            StartAlpha = color.A / 255f,
            Duration = duration,
            Remaining = duration,
            FadeType = FadeType.Out,
        });
    }

    /// <summary>
    /// Fade the screen to a solid color over duration.
    /// Common use: fade to black on death, fade to white on teleport.
    /// </summary>
    public void FadeIn(Color color, float duration)
    {
        _effects.Add(new OverlayEffect
        {
            Color = color,
            StartAlpha = color.A / 255f,
            Duration = duration,
            Remaining = duration,
            FadeType = FadeType.In,
        });
    }

    /// <summary>
    /// Fade FROM a solid color back to clear over duration.
    /// Common use: fade from black on respawn.
    /// </summary>
    public void FadeOut(Color color, float duration)
    {
        _effects.Add(new OverlayEffect
        {
            Color = color,
            StartAlpha = color.A / 255f,
            Duration = duration,
            Remaining = duration,
            FadeType = FadeType.Out,
        });
    }

    /// <summary>
    /// Set a persistent overlay by name. Stays until removed.
    /// Common use: low health warning, underwater tint, night vision.
    /// </summary>
    public void SetPersistent(string name, Color color)
    {
        _persistent[name] = new PersistentOverlay { Color = color };
    }

    /// <summary>Remove a persistent overlay.</summary>
    public void ClearPersistent(string name) => _persistent.Remove(name);

    /// <summary>Remove all persistent overlays.</summary>
    public void ClearAllPersistent() => _persistent.Clear();

    /// <summary>Check if a persistent overlay is active.</summary>
    public bool HasPersistent(string name) => _persistent.ContainsKey(name);

    /// <summary>Remove all effects (timed and persistent).</summary>
    public void ClearAll()
    {
        _effects.Clear();
        _persistent.Clear();
    }

    /// <summary>Number of active timed effects.</summary>
    public int ActiveEffectCount => _effects.Count;

    /// <summary>Number of active persistent overlays.</summary>
    public int PersistentCount => _persistent.Count;

    /// <summary>Update effect timers. Call once per frame.</summary>
    public void Update(float deltaTime)
    {
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            var e = _effects[i];
            e.Remaining -= deltaTime;
            if (e.Remaining <= 0)
            {
                _effects.RemoveAt(i);
            }
            else
            {
                _effects[i] = e;
            }
        }
    }

    /// <summary>
    /// Draw all active overlays as fullscreen quads.
    /// Call after all other UI drawing, before renderer.End().
    /// </summary>
    public void Draw(UIRenderer renderer, int viewportWidth, int viewportHeight)
    {
        // Draw persistent overlays first (background)
        foreach (var kvp in _persistent)
        {
            renderer.DrawRect(0, 0, viewportWidth, viewportHeight, kvp.Value.Color);
        }

        // Draw timed effects on top
        foreach (var e in _effects)
        {
            float t = 1f - (e.Remaining / e.Duration); // 0 = start, 1 = end
            float alpha;

            switch (e.FadeType)
            {
                case FadeType.In:
                    // Starts transparent, ends at full alpha
                    alpha = t * e.StartAlpha;
                    break;
                case FadeType.Out:
                    // Starts at full alpha, ends transparent
                    alpha = (1f - t) * e.StartAlpha;
                    break;
                default:
                    alpha = e.StartAlpha;
                    break;
            }

            if (alpha > 0.001f)
            {
                var color = Color.FromArgb(
                    (int)(alpha * 255),
                    e.Color.R,
                    e.Color.G,
                    e.Color.B);
                renderer.DrawRect(0, 0, viewportWidth, viewportHeight, color);
            }
        }
    }

    private struct OverlayEffect
    {
        public Color Color;
        public float StartAlpha;
        public float Duration;
        public float Remaining;
        public FadeType FadeType;
    }

    private struct PersistentOverlay
    {
        public Color Color;
    }

    private enum FadeType
    {
        In,   // transparent -> opaque
        Out,  // opaque -> transparent
    }
}
