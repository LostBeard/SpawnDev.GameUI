using System.Drawing;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Toggle checkbox with label text.
/// Click to toggle on/off. Works with mouse, VR controller, hand pinch.
/// Renders a box (checked/unchecked) + label text to the right.
/// </summary>
public class UICheckbox : UIElement
{
    public string Text { get; set; } = "";
    public bool IsChecked { get; set; }
    public Action<bool>? OnChanged { get; set; }
    public FontSize FontSize { get; set; } = FontSize.Body;

    // Theme-aware colors
    private Color? _boxColor, _checkColor, _textColor;
    public Color BoxColor { get => _boxColor ?? UITheme.Current.PanelBorder; set => _boxColor = value; }
    public Color CheckColor { get => _checkColor ?? UITheme.Current.SliderFill; set => _checkColor = value; }
    public Color TextColor { get => _textColor ?? UITheme.Current.TextPrimary; set => _textColor = value; }

    private const float BoxSize = 18f;
    private const float BoxMargin = 8f;
    private bool _isHovered;

    public override void Update(GameInput input, float dt)
    {
        if (!Visible || !Enabled) return;

        _isHovered = false;
        bool wasReleased = false;

        foreach (var pointer in input.Pointers)
        {
            if (pointer.ScreenPosition.HasValue)
            {
                var bounds = ScreenBounds;
                var mp = pointer.ScreenPosition.Value;
                bool hit = mp.X >= bounds.X && mp.X < bounds.X + bounds.Width &&
                           mp.Y >= bounds.Y && mp.Y < bounds.Y + bounds.Height;
                if (hit)
                {
                    _isHovered = true;
                    if (pointer.WasReleased) wasReleased = true;
                }
            }
            else if (pointer.RayOrigin.HasValue && pointer.RayDirection.HasValue)
            {
                if (RayIntersectsPanel(pointer.RayOrigin.Value, pointer.RayDirection.Value, out _))
                {
                    _isHovered = true;
                    if (pointer.WasReleased) wasReleased = true;
                }
            }
        }

        if (wasReleased)
        {
            IsChecked = !IsChecked;
            OnChanged?.Invoke(IsChecked);
        }

        base.Update(input, dt);
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        var bounds = ScreenBounds;
        float boxY = bounds.Y + (bounds.Height - BoxSize) / 2f;
        float radius = UITheme.Current.ControlCornerRadius;
        Color outline = _isHovered ? UITheme.Current.FocusBorder : BoxColor;
        Color innerEmpty = Color.FromArgb(180,
            UITheme.Current.PanelBackground.R,
            UITheme.Current.PanelBackground.G,
            UITheme.Current.PanelBackground.B);

        renderer.DrawBorderedRoundedRect(bounds.X, boxY, BoxSize, BoxSize,
            radius, 1.5f, outline, IsChecked ? CheckColor : innerEmpty);

        // Label text
        if (!string.IsNullOrEmpty(Text))
        {
            float textX = bounds.X + BoxSize + BoxMargin;
            float textY = bounds.Y + (bounds.Height - renderer.GetLineHeight(FontSize)) / 2f;
            renderer.DrawText(Text, textX, textY, FontSize, Enabled ? TextColor : UITheme.Current.TextMuted);
        }

        base.Draw(renderer);
    }

    /// <summary>Auto-size to fit checkbox + label.</summary>
    public void AutoSize(UIRenderer renderer)
    {
        float textW = string.IsNullOrEmpty(Text) ? 0 : renderer.MeasureText(Text, FontSize) + BoxMargin;
        Width = BoxSize + textW;
        Height = Math.Max(BoxSize, renderer.GetLineHeight(FontSize));
    }
}
