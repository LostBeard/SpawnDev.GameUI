using System.Drawing;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Panel with automatic flex layout for children.
/// Stacks children horizontally or vertically with configurable gap and alignment.
/// Replaces manual X/Y positioning for most UI construction.
///
/// Example: vertical menu
///   var menu = new UIFlexPanel { Direction = FlexDirection.Column, Gap = 8, Padding = 12 };
///   menu.AddChild(new UILabel { Text = "Settings" });
///   menu.AddChild(new UICheckbox { Text = "VSync" });
///   menu.AddChild(new UISlider { Label = "FOV", MinValue = 60, MaxValue = 120 });
/// </summary>
public class UIFlexPanel : UIPanel
{
    /// <summary>Stack direction for children.</summary>
    public FlexDirection Direction { get; set; } = FlexDirection.Column;

    /// <summary>Gap between children in pixels.</summary>
    public float Gap { get; set; } = 4f;

    /// <summary>Cross-axis alignment.</summary>
    public FlexAlign Align { get; set; } = FlexAlign.Start;

    /// <summary>
    /// If true, automatically sizes this panel to fit its children.
    /// If false, children are laid out within the existing Width/Height.
    /// </summary>
    public bool AutoSize { get; set; } = true;

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        // Layout children before drawing
        LayoutChildren(renderer);

        // Draw panel background + children via base
        base.Draw(renderer);
    }

    private void LayoutChildren(UIRenderer renderer)
    {
        if (Children.Count == 0) return;

        float pad = Padding;
        float cursor = pad; // start after padding
        float maxCross = 0; // track max cross-axis size for auto-sizing

        foreach (var child in Children)
        {
            if (!child.Visible) continue;

            if (Direction == FlexDirection.Column)
            {
                child.Y = cursor;
                child.X = Align switch
                {
                    FlexAlign.Center => pad + (Width - 2 * pad - child.Width) / 2f,
                    FlexAlign.End => Width - pad - child.Width,
                    _ => pad, // Start
                };
                cursor += child.Height + Gap;
                maxCross = Math.Max(maxCross, child.Width);
            }
            else // Row
            {
                child.X = cursor;
                child.Y = Align switch
                {
                    FlexAlign.Center => pad + (Height - 2 * pad - child.Height) / 2f,
                    FlexAlign.End => Height - pad - child.Height,
                    _ => pad, // Start
                };
                cursor += child.Width + Gap;
                maxCross = Math.Max(maxCross, child.Height);
            }
        }

        // Auto-size the panel to fit content
        if (AutoSize)
        {
            float contentSize = cursor - Gap + pad; // remove trailing gap, add end padding
            if (Direction == FlexDirection.Column)
            {
                Height = contentSize;
                if (maxCross > 0) Width = maxCross + 2 * pad;
            }
            else
            {
                Width = contentSize;
                if (maxCross > 0) Height = maxCross + 2 * pad;
            }
        }
    }
}

/// <summary>Flex layout direction.</summary>
public enum FlexDirection
{
    /// <summary>Stack children top to bottom.</summary>
    Column,
    /// <summary>Stack children left to right.</summary>
    Row,
}

/// <summary>Cross-axis alignment for flex children.</summary>
public enum FlexAlign
{
    /// <summary>Align to start (left for column, top for row).</summary>
    Start,
    /// <summary>Center on cross axis.</summary>
    Center,
    /// <summary>Align to end (right for column, bottom for row).</summary>
    End,
}
