using System.Drawing;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Container panel with a solid background color.
/// Children are positioned relative to this panel's top-left corner.
/// </summary>
public class UIPanel : UIElement
{
    // Theme-aware defaults - read from UITheme.Current, overridable per-panel
    private Color? _backgroundColor, _borderColor;
    private float? _borderWidth, _padding, _cornerRadius;
    public Color BackgroundColor { get => _backgroundColor ?? UITheme.Current.PanelBackground; set => _backgroundColor = value; }
    public Color BorderColor { get => _borderColor ?? UITheme.Current.PanelBorder; set => _borderColor = value; }
    public float BorderWidth { get => _borderWidth ?? UITheme.Current.PanelBorderWidth; set => _borderWidth = value; }
    public float Padding { get => _padding ?? UITheme.Current.PanelPadding; set => _padding = value; }

    /// <summary>Corner radius for rounded panels. 0 = sharp corners.</summary>
    public float CornerRadius { get => _cornerRadius ?? UITheme.Current.PanelCornerRadius; set => _cornerRadius = value; }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        var bounds = ScreenBounds;

        // Border (drawn as a slightly larger rect behind the background)
        if (BorderWidth > 0)
        {
            renderer.DrawRect(bounds.X - BorderWidth, bounds.Y - BorderWidth,
                              bounds.Width + BorderWidth * 2, bounds.Height + BorderWidth * 2,
                              BorderColor);
        }

        // Background
        renderer.DrawRect(bounds.X, bounds.Y, bounds.Width, bounds.Height, BackgroundColor);

        // Children
        base.Draw(renderer);
    }
}
