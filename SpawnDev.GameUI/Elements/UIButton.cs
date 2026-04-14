using System.Drawing;
using System.Numerics;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Clickable button: background rectangle + centered text label.
/// Supports hover and pressed visual states.
/// Works with any pointer type (mouse, VR controller ray, hand pinch).
/// </summary>
public class UIButton : UIElement
{
    public string Text { get; set; } = "";
    public FontSize FontSize { get; set; } = FontSize.Body;
    public Action? OnClick { get; set; }

    // Colors - default to theme values, can be overridden per-button
    private Color? _normalColor, _hoverColor, _pressedColor, _disabledColor, _textColor;
    public Color NormalColor { get => _normalColor ?? UITheme.Current.ButtonNormal; set => _normalColor = value; }
    public Color HoverColor { get => _hoverColor ?? UITheme.Current.ButtonHover; set => _hoverColor = value; }
    public Color PressedColor { get => _pressedColor ?? UITheme.Current.ButtonPressed; set => _pressedColor = value; }
    public Color DisabledColor { get => _disabledColor ?? UITheme.Current.ButtonDisabled; set => _disabledColor = value; }
    public Color TextColor { get => _textColor ?? UITheme.Current.ButtonText; set => _textColor = value; }

    // State
    public bool IsHovered { get; private set; }
    public bool IsPressed { get; private set; }

    public float PaddingX { get; set; } = 16;
    public float PaddingY { get; set; } = 8;

    public override void Update(GameInput input, float dt)
    {
        if (!Visible || !Enabled)
        {
            IsHovered = false;
            IsPressed = false;
            return;
        }

        // Check all pointers for interaction (mouse, VR controller, hand)
        IsHovered = false;
        IsPressed = false;
        bool wasReleased = false;

        foreach (var pointer in input.Pointers)
        {
            bool hit = false;

            if (RenderMode == UIRenderMode.ScreenSpace && pointer.ScreenPosition.HasValue)
            {
                // 2D screen-space hit test
                var bounds = ScreenBounds;
                var mp = pointer.ScreenPosition.Value;
                hit = mp.X >= bounds.X && mp.X < bounds.X + bounds.Width &&
                      mp.Y >= bounds.Y && mp.Y < bounds.Y + bounds.Height;
            }
            else if (pointer.RayOrigin.HasValue && pointer.RayDirection.HasValue)
            {
                // 3D ray hit test for VR/AR
                hit = RayIntersectsPanel(pointer.RayOrigin.Value, pointer.RayDirection.Value, out _);
            }

            if (hit)
            {
                IsHovered = true;
                if (pointer.IsPressed) IsPressed = true;
                if (pointer.WasReleased) wasReleased = true;
            }
        }

        if (IsHovered && wasReleased)
            OnClick?.Invoke();

        base.Update(input, dt);
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        var bounds = ScreenBounds;
        Color bgColor = !Enabled ? DisabledColor :
                         IsPressed ? PressedColor :
                         IsHovered ? HoverColor :
                         NormalColor;

        // Background
        renderer.DrawRect(bounds.X, bounds.Y, bounds.Width, bounds.Height, bgColor);

        // Centered text
        if (!string.IsNullOrEmpty(Text))
        {
            float textW = renderer.MeasureText(Text, FontSize);
            float textH = renderer.GetLineHeight(FontSize);
            float textX = bounds.X + (bounds.Width - textW) / 2;
            float textY = bounds.Y + (bounds.Height - textH) / 2;
            renderer.DrawText(Text, textX, textY, FontSize, Enabled ? TextColor : Color.Gray);
        }

        base.Draw(renderer);
    }

    /// <summary>Auto-size the button to fit its text + padding.</summary>
    public void AutoSize(UIRenderer renderer)
    {
        float textW = renderer.MeasureText(Text, FontSize);
        float textH = renderer.GetLineHeight(FontSize);
        Width = textW + PaddingX * 2;
        Height = textH + PaddingY * 2;
    }
}
