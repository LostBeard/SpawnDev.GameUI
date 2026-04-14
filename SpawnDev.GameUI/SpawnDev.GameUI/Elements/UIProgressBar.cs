using System.Drawing;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Horizontal fill bar with optional label.
/// Use for health, stamina, hunger, thirst, loading progress, crafting timers.
/// Not interactive - display only. Set Value to update.
/// </summary>
public class UIProgressBar : UIElement
{
    public float MinValue { get; set; } = 0f;
    public float MaxValue { get; set; } = 1f;
    public float Value { get; set; } = 1f;
    public string Label { get; set; } = "";
    public bool ShowPercentage { get; set; } = false;

    // Theme-aware colors
    private Color? _trackColor, _fillColor, _labelColor;
    public Color TrackColor { get => _trackColor ?? UITheme.Current.SliderTrack; set => _trackColor = value; }
    public Color FillColor { get => _fillColor ?? UITheme.Current.SliderFill; set => _fillColor = value; }
    public Color LabelColor { get => _labelColor ?? UITheme.Current.TextPrimary; set => _labelColor = value; }

    /// <summary>Optional: color thresholds. When Value/MaxValue drops below threshold, use this color instead.</summary>
    public Color? LowColor { get; set; }
    public float LowThreshold { get; set; } = 0.25f;
    public Color? CriticalColor { get; set; }
    public float CriticalThreshold { get; set; } = 0.1f;

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        var bounds = ScreenBounds;
        float t = (MaxValue > MinValue) ? Math.Clamp((Value - MinValue) / (MaxValue - MinValue), 0f, 1f) : 0f;

        // Track background
        renderer.DrawRect(bounds.X, bounds.Y, bounds.Width, bounds.Height, TrackColor);

        // Fill - use threshold colors if set
        Color fill = FillColor;
        if (CriticalColor.HasValue && t <= CriticalThreshold)
            fill = CriticalColor.Value;
        else if (LowColor.HasValue && t <= LowThreshold)
            fill = LowColor.Value;

        float fillW = bounds.Width * t;
        if (fillW > 0.5f)
            renderer.DrawRect(bounds.X, bounds.Y, fillW, bounds.Height, fill);

        // Label (centered in bar)
        string text = Label;
        if (ShowPercentage)
            text = string.IsNullOrEmpty(Label) ? $"{(int)(t * 100)}%" : $"{Label}: {(int)(t * 100)}%";

        if (!string.IsNullOrEmpty(text))
        {
            float textW = renderer.MeasureText(text, FontSize.Caption);
            float textH = renderer.GetLineHeight(FontSize.Caption);
            float textX = bounds.X + (bounds.Width - textW) / 2f;
            float textY = bounds.Y + (bounds.Height - textH) / 2f;
            renderer.DrawText(text, textX, textY, FontSize.Caption, LabelColor);
        }

        base.Draw(renderer);
    }
}
