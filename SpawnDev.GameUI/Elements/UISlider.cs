using System.Drawing;
using System.Numerics;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Horizontal drag slider for float values.
/// Renders a track bar with a draggable thumb and value label.
/// Works with mouse drag, mouse wheel (when hovered), and VR controller ray drag.
/// </summary>
public class UISlider : UIElement
{
    public float MinValue { get; set; } = 0f;
    public float MaxValue { get; set; } = 1f;
    public float Value { get; set; } = 0.5f;
    public string Label { get; set; } = "";
    public string Format { get; set; } = "F2";
    public Action<float>? OnChanged { get; set; }

    /// <summary>
    /// Fraction of (MaxValue - MinValue) applied per 100 units of wheel deltaY.
    /// Scroll up increases value; scroll down decreases. Default 5% of range.
    /// </summary>
    public float WheelStep { get; set; } = 0.05f;

    // Theme-aware colors (nullable overrides)
    private Color? _trackColor, _fillColor, _thumbColor, _labelColor;
    public Color TrackColor { get => _trackColor ?? UITheme.Current.SliderTrack; set => _trackColor = value; }
    public Color FillColor { get => _fillColor ?? UITheme.Current.SliderFill; set => _fillColor = value; }
    public Color ThumbColor { get => _thumbColor ?? UITheme.Current.SliderThumb; set => _thumbColor = value; }
    public Color LabelColor { get => _labelColor ?? UITheme.Current.SliderLabel; set => _labelColor = value; }

    private const float TrackHeight = 6f;
    private const float ThumbRadius = 8f;
    private bool _dragging;

    public override void Update(GameInput input, float dt)
    {
        if (!Visible || !Enabled) return;

        var bounds = ScreenBounds;

        foreach (var pointer in input.Pointers)
        {
            if (!pointer.ScreenPosition.HasValue) continue;

            var mp = pointer.ScreenPosition.Value;
            bool inBounds = mp.X >= bounds.X && mp.X < bounds.X + bounds.Width &&
                            mp.Y >= bounds.Y - 4 && mp.Y < bounds.Y + bounds.Height + 4;

            // Mouse wheel nudges value while hovered
            if (inBounds && MathF.Abs(pointer.ScrollDelta) > 0.1f)
            {
                float range = MaxValue - MinValue;
                // deltaY > 0 = scroll down = decrease (matches browser wheel sign)
                float delta = -pointer.ScrollDelta * (range * WheelStep / 100f);
                float newValue = Math.Clamp(Value + delta, MinValue, MaxValue);
                if (MathF.Abs(newValue - Value) > 0.0001f)
                {
                    Value = newValue;
                    OnChanged?.Invoke(Value);
                }
            }

            // Primary pointer drag (mouse / ray)
            if (pointer != input.PrimaryPointer) continue;

            if (inBounds && pointer.WasPressed)
                _dragging = true;

            if (_dragging)
            {
                if (pointer.IsPressed)
                {
                    float trackX = bounds.X;
                    float trackW = bounds.Width;
                    float t = Math.Clamp((mp.X - trackX) / trackW, 0f, 1f);
                    float newValue = MinValue + t * (MaxValue - MinValue);
                    if (MathF.Abs(newValue - Value) > 0.001f)
                    {
                        Value = newValue;
                        OnChanged?.Invoke(Value);
                    }
                }
                else
                {
                    _dragging = false;
                }
            }
        }

        if (input.PrimaryPointer == null || !input.PrimaryPointer.ScreenPosition.HasValue)
            _dragging = false;

        base.Update(input, dt);
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        var bounds = ScreenBounds;
        float t = (MaxValue > MinValue) ? (Value - MinValue) / (MaxValue - MinValue) : 0f;

        // Label + value
        if (!string.IsNullOrEmpty(Label))
        {
            string text = $"{Label}: {Value.ToString(Format)}";
            renderer.DrawText(text, bounds.X, bounds.Y, FontSize.Caption, LabelColor);
        }

        float labelOffset = string.IsNullOrEmpty(Label) ? 0 : 18;
        float sliderY = bounds.Y + labelOffset + (bounds.Height - labelOffset) / 2f - TrackHeight / 2f;
        float trackRadius = TrackHeight * 0.5f;

        // Rounded track
        renderer.DrawRoundedRect(bounds.X, sliderY, bounds.Width, TrackHeight, trackRadius, TrackColor);

        // Filled portion (rounded; clamp radius when fill is short)
        float fillW = bounds.Width * t;
        if (fillW > 1)
        {
            float fillR = MathF.Min(trackRadius, fillW * 0.5f);
            renderer.DrawRoundedRect(bounds.X, sliderY, fillW, TrackHeight, fillR, FillColor);
        }

        // Circular thumb
        float thumbCx = bounds.X + fillW;
        float thumbCy = sliderY + TrackHeight * 0.5f;
        renderer.DrawCircleFill(thumbCx, thumbCy, ThumbRadius, _dragging ? FillColor : ThumbColor);

        base.Draw(renderer);
    }
}
