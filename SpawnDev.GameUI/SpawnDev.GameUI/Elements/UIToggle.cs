using System.Drawing;
using SpawnDev.GameUI.Input;
using SpawnDev.GameUI.Animation;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// iOS-style on/off toggle switch.
/// Animated thumb slides between on and off positions.
/// Works with mouse, VR controller, hand pinch.
/// </summary>
public class UIToggle : UIElement
{
    private bool _isOn;
    private float _thumbPosition; // 0 = off (left), 1 = on (right)

    /// <summary>Whether the toggle is on.</summary>
    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (_isOn != value)
            {
                _isOn = value;
                // Animate thumb position
                TweenManager.Global.Start(v => _thumbPosition = v,
                    _thumbPosition, value ? 1f : 0f, 0.15f, EasingType.EaseOut);
                OnChanged?.Invoke(value);
            }
        }
    }

    /// <summary>Optional label text next to the toggle.</summary>
    public string Text { get; set; } = "";

    /// <summary>Font size for label.</summary>
    public FontSize FontSize { get; set; } = FontSize.Body;

    /// <summary>Called when the toggle state changes.</summary>
    public Action<bool>? OnChanged { get; set; }

    // Theme-aware colors
    private Color? _offColor, _onColor, _thumbColor, _textColor;
    public Color OffColor { get => _offColor ?? Color.FromArgb(255, 60, 60, 75); set => _offColor = value; }
    public Color OnColor { get => _onColor ?? UITheme.Current.ButtonNormal; set => _onColor = value; }
    public Color ThumbColor { get => _thumbColor ?? Color.White; set => _thumbColor = value; }
    public Color TextColor { get => _textColor ?? UITheme.Current.TextPrimary; set => _textColor = value; }

    private const float TrackWidth = 44f;
    private const float TrackHeight = 24f;
    private const float ThumbSize = 20f;
    private const float ThumbPad = 2f;
    private const float LabelGap = 10f;
    private bool _isHovered;

    public UIToggle()
    {
        Width = TrackWidth;
        Height = TrackHeight;
    }

    public override void Update(GameInput input, float dt)
    {
        if (!Visible || !Enabled) return;

        _isHovered = false;
        bool wasClicked = false;

        foreach (var pointer in input.Pointers)
        {
            bool hit = false;
            if (pointer.ScreenPosition.HasValue)
            {
                var bounds = ScreenBounds;
                var mp = pointer.ScreenPosition.Value;
                // Hit test includes the label area
                float totalWidth = TrackWidth + (string.IsNullOrEmpty(Text) ? 0 : LabelGap + 200);
                hit = mp.X >= bounds.X && mp.X < bounds.X + totalWidth &&
                      mp.Y >= bounds.Y && mp.Y < bounds.Y + bounds.Height;
            }
            else if (pointer.RayOrigin.HasValue && pointer.RayDirection.HasValue)
            {
                hit = RayIntersectsPanel(pointer.RayOrigin.Value, pointer.RayDirection.Value, out _);
            }

            if (hit)
            {
                _isHovered = true;
                if (pointer.WasReleased) wasClicked = true;
            }
        }

        if (wasClicked)
            IsOn = !IsOn;

        base.Update(input, dt);
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        var bounds = ScreenBounds;

        // Track (interpolate color based on thumb position)
        Color trackColor = InterpolateColor(OffColor, OnColor, _thumbPosition);
        if (_isHovered)
            trackColor = Lighten(trackColor, 0.1f);

        renderer.DrawRect(bounds.X, bounds.Y, TrackWidth, TrackHeight, trackColor);

        // Thumb (slides from left to right)
        float thumbX = bounds.X + ThumbPad + _thumbPosition * (TrackWidth - ThumbSize - ThumbPad * 2);
        float thumbY = bounds.Y + ThumbPad;
        renderer.DrawRect(thumbX, thumbY, ThumbSize, ThumbSize, ThumbColor);

        // Label text
        if (!string.IsNullOrEmpty(Text))
        {
            float textX = bounds.X + TrackWidth + LabelGap;
            float textY = bounds.Y + (TrackHeight - renderer.GetLineHeight(FontSize)) / 2f;
            renderer.DrawText(Text, textX, textY, FontSize, Enabled ? TextColor : UITheme.Current.TextMuted);
        }

        base.Draw(renderer);
    }

    private static Color InterpolateColor(Color a, Color b, float t)
    {
        return Color.FromArgb(
            (int)(a.A + (b.A - a.A) * t),
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t)
        );
    }

    private static Color Lighten(Color c, float amount)
    {
        return Color.FromArgb(c.A,
            Math.Min(255, (int)(c.R + 255 * amount)),
            Math.Min(255, (int)(c.G + 255 * amount)),
            Math.Min(255, (int)(c.B + 255 * amount)));
    }
}
