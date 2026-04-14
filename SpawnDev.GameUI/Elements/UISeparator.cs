using System.Drawing;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Horizontal or vertical divider line.
/// Use between sections in settings panels, menus, and lists.
/// </summary>
public class UISeparator : UIElement
{
    /// <summary>Line direction.</summary>
    public SeparatorDirection Direction { get; set; } = SeparatorDirection.Horizontal;

    /// <summary>Line thickness in pixels.</summary>
    public float Thickness { get; set; } = 1f;

    /// <summary>Margin on each end of the line.</summary>
    public float Margin { get; set; } = 4f;

    private Color? _color;
    public Color Color { get => _color ?? Color.FromArgb(60, 255, 255, 255); set => _color = value; }

    public UISeparator()
    {
        Height = 9; // margin + thickness + margin
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;
        var bounds = ScreenBounds;

        if (Direction == SeparatorDirection.Horizontal)
        {
            float y = bounds.Y + (bounds.Height - Thickness) / 2f;
            renderer.DrawRect(bounds.X + Margin, y, bounds.Width - Margin * 2, Thickness, Color);
        }
        else
        {
            float x = bounds.X + (bounds.Width - Thickness) / 2f;
            renderer.DrawRect(x, bounds.Y + Margin, Thickness, bounds.Height - Margin * 2, Color);
        }
    }
}

public enum SeparatorDirection { Horizontal, Vertical }
