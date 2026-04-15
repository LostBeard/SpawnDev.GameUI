using System.Drawing;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Text label rendered via the font atlas.
/// Auto-sizes Width/Height to fit the text if not explicitly set.
/// Supports SDF outline rendering for text readability over any background.
/// </summary>
public class UILabel : UIElement
{
    private string _text = "";
    private FontSize _fontSize = FontSize.Body;
    private bool _dirty = true;

    public string Text
    {
        get => _text;
        set { if (_text != value) { _text = value; _dirty = true; } }
    }

    public FontSize FontSize
    {
        get => _fontSize;
        set { if (_fontSize != value) { _fontSize = value; _dirty = true; } }
    }

    public Color Color { get; set; } = Color.White;

    /// <summary>Text alignment within the label bounds.</summary>
    public TextAlign Align { get; set; } = TextAlign.Left;

    /// <summary>
    /// SDF outline width. 0 = no outline (default).
    /// Typical values: 0.05 (thin) to 0.15 (thick).
    /// Only effective when SDF rendering is available.
    /// </summary>
    public float OutlineWidth { get; set; }

    /// <summary>
    /// Outline color (only used when OutlineWidth > 0).
    /// Default: Black - provides contrast against any background.
    /// </summary>
    public Color OutlineColor { get; set; } = Color.Black;

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible || string.IsNullOrEmpty(Text)) return;

        // Auto-size on first draw or when text changes
        if (_dirty)
        {
            Width = renderer.MeasureText(Text, FontSize);
            Height = renderer.GetLineHeight(FontSize);
            _dirty = false;
        }

        // Apply outline style if configured
        bool hasOutline = OutlineWidth > 0;
        if (hasOutline)
            renderer.SetTextStyle(OutlineWidth, OutlineColor);

        var bounds = ScreenBounds;
        renderer.DrawText(Text, bounds.X, bounds.Y, FontSize, Color);

        // Restore default style after drawing
        if (hasOutline)
            renderer.ResetTextStyle();

        base.Draw(renderer);
    }
}

/// <summary>Font size presets matching the font atlas tiers.</summary>
public enum FontSize
{
    Caption = 12,
    Body = 16,
    Heading = 24,
    Title = 32
}

/// <summary>Text alignment within a label or text area.</summary>
public enum TextAlign
{
    Left,
    Center,
    Right
}
