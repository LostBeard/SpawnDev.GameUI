using System.Drawing;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Multi-line text block with word wrapping and overflow handling.
/// Unlike UILabel (single line, auto-size), UITextBlock wraps text to
/// fit within a fixed width, supports newlines, and handles overflow.
///
/// Usage:
///   var desc = new UITextBlock {
///       Text = "Iron Sword\nDamage: 25\nA sturdy blade forged in the fires of Mount Vulcan. " +
///              "Its edge never dulls and it cuts through armor like butter.",
///       Width = 250,
///       MaxLines = 5,
///       Overflow = TextOverflow.Ellipsis,
///   };
/// </summary>
public class UITextBlock : UIElement
{
    private string _text = "";
    private float _lastMeasuredWidth;
    private readonly List<string> _wrappedLines = new();
    private bool _dirty = true;

    /// <summary>Text content. Supports \n for explicit line breaks.</summary>
    public string Text
    {
        get => _text;
        set { if (_text != value) { _text = value ?? ""; _dirty = true; } }
    }

    /// <summary>Font size.</summary>
    public FontSize FontSize { get; set; } = FontSize.Body;

    /// <summary>Text color.</summary>
    public Color Color { get; set; } = Color.White;

    /// <summary>Line spacing multiplier (1.0 = normal).</summary>
    public float LineSpacing { get; set; } = 1.2f;

    /// <summary>Maximum number of lines to display. 0 = unlimited.</summary>
    public int MaxLines { get; set; } = 0;

    /// <summary>How to handle text that doesn't fit.</summary>
    public TextOverflow Overflow { get; set; } = TextOverflow.Clip;

    /// <summary>Text alignment within the block.</summary>
    public TextAlign Align { get; set; } = TextAlign.Left;

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible || string.IsNullOrEmpty(_text)) return;

        // Re-wrap if dirty or width changed
        if (_dirty || MathF.Abs(_lastMeasuredWidth - Width) > 0.5f)
        {
            WrapText(renderer);
            _dirty = false;
            _lastMeasuredWidth = Width;
        }

        var bounds = ScreenBounds;
        float lineH = renderer.GetLineHeight(FontSize) * LineSpacing;
        float y = bounds.Y;

        int lineCount = _wrappedLines.Count;
        if (MaxLines > 0 && lineCount > MaxLines) lineCount = MaxLines;

        for (int i = 0; i < lineCount; i++)
        {
            string line = _wrappedLines[i];

            // Add ellipsis on last visible line if truncated
            if (MaxLines > 0 && i == MaxLines - 1 && _wrappedLines.Count > MaxLines && Overflow == TextOverflow.Ellipsis)
            {
                // Trim line to fit "..." at the end
                while (line.Length > 0 && renderer.MeasureText(line + "...", FontSize) > Width)
                    line = line[..^1];
                line += "...";
            }

            float textW = renderer.MeasureText(line, FontSize);
            float x = Align switch
            {
                TextAlign.Center => bounds.X + (Width - textW) / 2,
                TextAlign.Right => bounds.X + Width - textW,
                _ => bounds.X,
            };

            renderer.DrawText(line, x, y, FontSize, Color);
            y += lineH;
        }

        // Auto-size height to fit content
        Height = lineCount * lineH;
    }

    private void WrapText(UIRenderer renderer)
    {
        _wrappedLines.Clear();
        if (string.IsNullOrEmpty(_text) || Width <= 0) return;

        // Split by explicit newlines first
        var paragraphs = _text.Split('\n');

        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrEmpty(paragraph))
            {
                _wrappedLines.Add("");
                continue;
            }

            // Word wrap within each paragraph
            var words = paragraph.Split(' ');
            string currentLine = "";

            foreach (var word in words)
            {
                if (string.IsNullOrEmpty(word)) continue;

                string testLine = currentLine.Length == 0 ? word : currentLine + " " + word;
                float testWidth = renderer.MeasureText(testLine, FontSize);

                if (testWidth <= Width)
                {
                    currentLine = testLine;
                }
                else
                {
                    // Current line is full - wrap
                    if (currentLine.Length > 0)
                        _wrappedLines.Add(currentLine);

                    // Handle single word wider than width
                    if (renderer.MeasureText(word, FontSize) > Width)
                    {
                        // Character-level break for very long words
                        string remaining = word;
                        while (remaining.Length > 0)
                        {
                            int fit = 1;
                            while (fit < remaining.Length && renderer.MeasureText(remaining[..fit], FontSize) <= Width)
                                fit++;
                            if (fit > 1) fit--;
                            _wrappedLines.Add(remaining[..fit]);
                            remaining = remaining[fit..];
                        }
                        currentLine = "";
                    }
                    else
                    {
                        currentLine = word;
                    }
                }
            }

            if (currentLine.Length > 0)
                _wrappedLines.Add(currentLine);
        }
    }
}

/// <summary>How to handle text that exceeds the available space.</summary>
public enum TextOverflow
{
    /// <summary>Simply cut off at the boundary.</summary>
    Clip,
    /// <summary>Show "..." at the end of the last visible line.</summary>
    Ellipsis,
}
