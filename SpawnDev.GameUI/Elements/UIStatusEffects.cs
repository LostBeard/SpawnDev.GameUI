using System.Drawing;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Horizontal row of status effect icons with duration timers.
/// Shows active buffs, debuffs, status conditions with countdown.
/// Auto-removes expired effects. Supports stacking (multiple of same type).
///
/// Usage:
///   var effects = new UIStatusEffects();
///   root.AddAnchored(effects, Anchor.TopLeft, offsetX: 20, offsetY: 50);
///
///   // Add effects:
///   effects.AddEffect("Bleeding", 30f, EffectType.Debuff);
///   effects.AddEffect("Well Fed", 300f, EffectType.Buff);
///   effects.AddEffect("Cold", 0f, EffectType.Warning); // no timer = permanent until removed
///
///   // Per frame:
///   effects.Update(gameInput, dt); // auto-decrements timers
/// </summary>
public class UIStatusEffects : UIElement
{
    private readonly List<StatusEffect> _effects = new();

    /// <summary>Size of each effect icon.</summary>
    public float IconSize { get; set; } = 32f;

    /// <summary>Gap between icons.</summary>
    public float IconGap { get; set; } = 4f;

    /// <summary>Whether to show remaining time text.</summary>
    public bool ShowTimers { get; set; } = true;

    /// <summary>Add a status effect.</summary>
    public void AddEffect(string name, float duration, EffectType type = EffectType.Neutral, int stacks = 1)
    {
        // Check if already exists - update stacks/duration
        for (int i = 0; i < _effects.Count; i++)
        {
            if (_effects[i].Name == name)
            {
                _effects[i] = _effects[i] with
                {
                    Duration = Math.Max(_effects[i].Duration, duration),
                    Stacks = _effects[i].Stacks + stacks,
                };
                return;
            }
        }

        _effects.Add(new StatusEffect
        {
            Name = name,
            Duration = duration,
            MaxDuration = duration,
            Type = type,
            Stacks = stacks,
        });
    }

    /// <summary>Remove a status effect by name.</summary>
    public void RemoveEffect(string name) => _effects.RemoveAll(e => e.Name == name);

    /// <summary>Clear all effects.</summary>
    public void ClearEffects() => _effects.Clear();

    /// <summary>Check if an effect is active.</summary>
    public bool HasEffect(string name) => _effects.Any(e => e.Name == name);

    /// <summary>Get remaining duration for an effect (0 if not active).</summary>
    public float GetDuration(string name) => _effects.FirstOrDefault(e => e.Name == name).Duration;

    public override void Update(Input.GameInput input, float dt)
    {
        if (!Visible) return;

        // Decrement timers and remove expired
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            var eff = _effects[i];
            if (eff.MaxDuration > 0) // 0 = permanent
            {
                eff.Duration -= dt;
                if (eff.Duration <= 0)
                {
                    _effects.RemoveAt(i);
                    continue;
                }
                _effects[i] = eff;
            }
        }

        base.Update(input, dt);
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible || _effects.Count == 0) return;

        var bounds = ScreenBounds;
        Width = _effects.Count * (IconSize + IconGap) - IconGap;
        Height = IconSize + (ShowTimers ? 14 : 0);

        for (int i = 0; i < _effects.Count; i++)
        {
            var eff = _effects[i];
            float x = bounds.X + i * (IconSize + IconGap);
            float y = bounds.Y;

            // Icon background color based on type
            Color bgColor = eff.Type switch
            {
                EffectType.Buff => Color.FromArgb(180, 30, 60, 30),
                EffectType.Debuff => Color.FromArgb(180, 60, 20, 20),
                EffectType.Warning => Color.FromArgb(180, 60, 50, 15),
                _ => Color.FromArgb(180, 30, 30, 40),
            };

            Color borderColor = eff.Type switch
            {
                EffectType.Buff => Color.FromArgb(150, 80, 200, 80),
                EffectType.Debuff => Color.FromArgb(150, 200, 60, 60),
                EffectType.Warning => Color.FromArgb(150, 220, 180, 50),
                _ => Color.FromArgb(100, 150, 150, 170),
            };

            // Icon box
            renderer.DrawRect(x, y, IconSize, IconSize, bgColor);
            renderer.DrawRect(x, y, IconSize, 1, borderColor);
            renderer.DrawRect(x, y, 1, IconSize, borderColor);
            renderer.DrawRect(x + IconSize - 1, y, 1, IconSize, borderColor);
            renderer.DrawRect(x, y + IconSize - 1, IconSize, 1, borderColor);

            // Duration progress bar (bottom of icon, drains left to right)
            if (eff.MaxDuration > 0)
            {
                float pct = eff.Duration / eff.MaxDuration;
                renderer.DrawRect(x + 1, y + IconSize - 3, (IconSize - 2) * pct, 2, borderColor);
            }

            // Effect name abbreviation (centered in icon)
            string abbrev = eff.Name.Length <= 3 ? eff.Name : eff.Name[..3];
            float tw = renderer.MeasureText(abbrev, FontSize.Caption);
            float th = renderer.GetLineHeight(FontSize.Caption);
            renderer.DrawText(abbrev, x + (IconSize - tw) / 2, y + (IconSize - th) / 2,
                FontSize.Caption, Color.White);

            // Stack count (top-right corner)
            if (eff.Stacks > 1)
            {
                string stackText = eff.Stacks.ToString();
                float sw = renderer.MeasureText(stackText, FontSize.Caption);
                renderer.DrawText(stackText, x + IconSize - sw - 2, y + 1,
                    FontSize.Caption, Color.FromArgb(220, 255, 220, 100));
            }

            // Timer text below icon
            if (ShowTimers && eff.MaxDuration > 0)
            {
                string timeText = FormatTime(eff.Duration);
                float ttw = renderer.MeasureText(timeText, FontSize.Caption);
                renderer.DrawText(timeText, x + (IconSize - ttw) / 2, y + IconSize + 2,
                    FontSize.Caption, UITheme.Current.TextMuted);
            }
        }
    }

    private static string FormatTime(float seconds)
    {
        if (seconds >= 60) return $"{(int)(seconds / 60)}m";
        if (seconds >= 10) return $"{(int)seconds}s";
        return $"{seconds:F1}s";
    }
}

/// <summary>Status effect visual type.</summary>
public enum EffectType
{
    Neutral,
    Buff,
    Debuff,
    Warning,
}

/// <summary>Active status effect data.</summary>
public record struct StatusEffect
{
    public string Name;
    public float Duration;
    public float MaxDuration;
    public EffectType Type;
    public int Stacks;
}
