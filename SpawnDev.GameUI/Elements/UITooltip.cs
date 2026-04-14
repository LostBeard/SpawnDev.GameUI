using System.Drawing;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Tooltip popup that appears near the cursor after a hover delay.
/// Follows the pointer position. Supports multi-line text.
/// Auto-sizes to content. Stays within viewport bounds.
///
/// Usage:
///   // Attach to any element
///   button.Tooltip = new UITooltip { Text = "Click to craft" };
///
///   // Or use the static show/hide for dynamic tooltips
///   UITooltip.Show("Iron Sword\nDamage: 25\nDurability: 80%", mousePos);
///   UITooltip.Hide();
/// </summary>
public class UITooltip : UIElement
{
    /// <summary>Tooltip text (supports \n for line breaks).</summary>
    public string Text { get; set; } = "";

    /// <summary>Delay before showing (seconds).</summary>
    public float ShowDelay { get; set; } = 0.4f;

    /// <summary>Font size for tooltip text.</summary>
    public FontSize FontSize { get; set; } = FontSize.Caption;

    // Theme-aware colors
    private Color? _bgColor, _textColor, _borderColor;
    public Color BackgroundColor { get => _bgColor ?? UITheme.Current.TooltipBackground; set => _bgColor = value; }
    public Color TextColor { get => _textColor ?? UITheme.Current.TooltipText; set => _textColor = value; }
    public Color BorderColor { get => _borderColor ?? UITheme.Current.TooltipBorder; set => _borderColor = value; }

    private const float PadX = 8f;
    private const float PadY = 6f;
    private const float BorderWidth = 1f;
    private const float OffsetY = 20f; // pixels below cursor

    /// <summary>Global static tooltip instance for quick show/hide.</summary>
    public static UITooltip? Active { get; private set; }

    /// <summary>Show a tooltip at the given screen position.</summary>
    public static void Show(string text, System.Numerics.Vector2 position, float viewportW = 0, float viewportH = 0)
    {
        if (Active == null)
        {
            Active = new UITooltip();
        }
        Active.Text = text;
        Active.X = position.X + 12;
        Active.Y = position.Y + OffsetY;
        Active.Visible = true;

        // Keep within viewport if dimensions are known
        if (viewportW > 0 && Active.X + Active.Width > viewportW)
            Active.X = viewportW - Active.Width - 4;
        if (viewportH > 0 && Active.Y + Active.Height > viewportH)
            Active.Y = position.Y - Active.Height - 4;
    }

    /// <summary>Hide the active tooltip.</summary>
    public static void Hide()
    {
        if (Active != null) Active.Visible = false;
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible || string.IsNullOrEmpty(Text)) return;

        // Split text into lines
        var lines = Text.Split('\n');
        float lineH = renderer.GetLineHeight(FontSize);
        float maxLineW = 0;
        foreach (var line in lines)
        {
            float w = renderer.MeasureText(line, FontSize);
            if (w > maxLineW) maxLineW = w;
        }

        Width = maxLineW + PadX * 2;
        Height = lines.Length * (lineH + 2) - 2 + PadY * 2;

        var bounds = ScreenBounds;

        // Border
        renderer.DrawRect(bounds.X - BorderWidth, bounds.Y - BorderWidth,
                          Width + BorderWidth * 2, Height + BorderWidth * 2, BorderColor);

        // Background
        renderer.DrawRect(bounds.X, bounds.Y, Width, Height, BackgroundColor);

        // Text lines
        float y = bounds.Y + PadY;
        foreach (var line in lines)
        {
            renderer.DrawText(line, bounds.X + PadX, y, FontSize, TextColor);
            y += lineH + 2;
        }
    }
}
