using System.Drawing;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Immediate-mode debug overlay for development.
/// Call static methods during your frame update to display debug text.
/// Auto-clears each frame. Toggle visibility with F3 or programmatically.
///
/// Usage:
///   UIDebugOverlay.Text($"FPS: {fps}");
///   UIDebugOverlay.Text($"Quads: {quadCount}");
///   UIDebugOverlay.Separator();
///   UIDebugOverlay.Text($"Hand tracking: {handJoints > 0}");
///
/// Add the overlay element to your root UI:
///   root.AddChild(UIDebugOverlay.Instance);
/// </summary>
public class UIDebugOverlay : UIElement
{
    /// <summary>Singleton instance. Add to your root UI tree.</summary>
    public static UIDebugOverlay Instance { get; } = new();

    private static readonly List<DebugLine> _lines = new();
    private static bool _visible = false;

    /// <summary>Toggle overlay visibility.</summary>
    public static bool IsVisible
    {
        get => _visible;
        set { _visible = value; Instance.Visible = value; }
    }

    private UIDebugOverlay()
    {
        Visible = false;
        X = 8;
        Y = 8;
    }

    /// <summary>Add a text line for this frame.</summary>
    public static void Text(string text)
    {
        _lines.Add(new DebugLine { Text = text, Color = Color.FromArgb(220, 200, 255, 200) });
    }

    /// <summary>Add a colored text line for this frame.</summary>
    public static void Text(string text, Color color)
    {
        _lines.Add(new DebugLine { Text = text, Color = color });
    }

    /// <summary>Add a warning line (yellow).</summary>
    public static void Warn(string text)
    {
        _lines.Add(new DebugLine { Text = text, Color = Color.FromArgb(220, 255, 220, 80) });
    }

    /// <summary>Add an error line (red).</summary>
    public static void Error(string text)
    {
        _lines.Add(new DebugLine { Text = text, Color = Color.FromArgb(220, 255, 80, 80) });
    }

    /// <summary>Add a horizontal separator line.</summary>
    public static void Separator()
    {
        _lines.Add(new DebugLine { IsSeparator = true });
    }

    /// <summary>Toggle visibility (call from F3 key handler).</summary>
    public static void Toggle() => IsVisible = !IsVisible;

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible || _lines.Count == 0) return;

        var bounds = ScreenBounds;
        float lineH = renderer.GetLineHeight(FontSize.Caption);
        float pad = 6f;

        // Measure content
        float maxW = 0;
        float totalH = pad * 2;
        foreach (var line in _lines)
        {
            if (line.IsSeparator) { totalH += 6; continue; }
            float w = renderer.MeasureText(line.Text, FontSize.Caption);
            maxW = Math.Max(maxW, w);
            totalH += lineH + 2;
        }

        Width = maxW + pad * 2;
        Height = totalH;

        // Semi-transparent background
        renderer.DrawRect(bounds.X, bounds.Y, Width, Height, Color.FromArgb(180, 10, 10, 15));

        // Draw lines
        float y = bounds.Y + pad;
        foreach (var line in _lines)
        {
            if (line.IsSeparator)
            {
                renderer.DrawRect(bounds.X + pad, y + 2, Width - pad * 2, 1,
                    Color.FromArgb(60, 255, 255, 255));
                y += 6;
                continue;
            }

            renderer.DrawText(line.Text, bounds.X + pad, y, FontSize.Caption, line.Color);
            y += lineH + 2;
        }

        // Clear for next frame (immediate mode)
        _lines.Clear();
    }

    private struct DebugLine
    {
        public string Text;
        public Color Color;
        public bool IsSeparator;
    }
}
