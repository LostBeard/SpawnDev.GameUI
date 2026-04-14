using System.Drawing;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Minimap container with compass bearing, coordinates, and zoom indicator.
/// The actual map texture is provided by the game engine via UIImage.
/// This element provides the chrome (border, compass, coords, zoom buttons).
///
/// Usage:
///   var minimap = new UIMinimapFrame { Size = 200 };
///   minimap.Bearing = player.Yaw;  // degrees, 0=North
///   minimap.Coordinates = $"{(int)player.X}, {(int)player.Z}";
///   minimap.ZoomLevel = 2;
///   root.AddAnchored(minimap, Anchor.TopRight, offsetX: -20, offsetY: 20);
/// </summary>
public class UIMinimapFrame : UIElement
{
    /// <summary>Size of the minimap (square).</summary>
    public float Size { get; set; } = 180f;

    /// <summary>Compass bearing in degrees (0=North, 90=East, 180=South, 270=West).</summary>
    public float Bearing { get; set; }

    /// <summary>Coordinate display text.</summary>
    public string Coordinates { get; set; } = "";

    /// <summary>Current zoom level (display only).</summary>
    public int ZoomLevel { get; set; } = 1;

    /// <summary>Max zoom level.</summary>
    public int MaxZoom { get; set; } = 5;

    /// <summary>Map texture view (set by game engine).</summary>
    public SpawnDev.BlazorJS.JSObjects.GPUTextureView? MapTexture { get; set; }

    // Colors
    private Color? _bgColor, _borderColor, _compassColor;
    public Color BackgroundColor { get => _bgColor ?? Color.FromArgb(200, 15, 20, 15); set => _bgColor = value; }
    public Color BorderColor { get => _borderColor ?? Color.FromArgb(180, 60, 70, 60); set => _borderColor = value; }
    public Color CompassColor { get => _compassColor ?? Color.FromArgb(220, 200, 200, 180); set => _compassColor = value; }

    private const float BorderWidth = 2f;
    private const float CompassHeight = 18f;
    private const float CoordsHeight = 16f;

    public UIMinimapFrame()
    {
        Width = 180;
        Height = 180 + CompassHeight + CoordsHeight;
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        Width = Size;
        Height = Size + CompassHeight + CoordsHeight;

        var bounds = ScreenBounds;

        // Border
        renderer.DrawRect(bounds.X - BorderWidth, bounds.Y - BorderWidth,
            Width + BorderWidth * 2, Height + BorderWidth * 2, BorderColor);

        // Map area background
        renderer.DrawRect(bounds.X, bounds.Y + CompassHeight, Size, Size, BackgroundColor);

        // Map texture (if provided)
        if (MapTexture != null)
        {
            renderer.DrawImage(MapTexture, bounds.X, bounds.Y + CompassHeight, Size, Size);
        }

        // Compass bar (top)
        renderer.DrawRect(bounds.X, bounds.Y, Size, CompassHeight, Color.FromArgb(200, 10, 12, 10));

        // Compass bearing text
        string compassText = GetCompassText(Bearing);
        float compassW = renderer.MeasureText(compassText, FontSize.Caption);
        renderer.DrawText(compassText, bounds.X + (Size - compassW) / 2, bounds.Y + 1,
            FontSize.Caption, CompassColor);

        // Cardinal direction markers
        float bearingNorm = ((Bearing % 360) + 360) % 360;
        DrawCompassMarker(renderer, bounds.X, bounds.Y, "N", 0, bearingNorm, Color.FromArgb(220, 220, 80, 80));
        DrawCompassMarker(renderer, bounds.X, bounds.Y, "E", 90, bearingNorm, CompassColor);
        DrawCompassMarker(renderer, bounds.X, bounds.Y, "S", 180, bearingNorm, CompassColor);
        DrawCompassMarker(renderer, bounds.X, bounds.Y, "W", 270, bearingNorm, CompassColor);

        // Center dot (player position)
        float cx = bounds.X + Size / 2;
        float cy = bounds.Y + CompassHeight + Size / 2;
        renderer.DrawRect(cx - 2, cy - 2, 4, 4, Color.White);

        // Coordinates bar (bottom)
        float coordsY = bounds.Y + CompassHeight + Size;
        renderer.DrawRect(bounds.X, coordsY, Size, CoordsHeight, Color.FromArgb(200, 10, 12, 10));

        if (!string.IsNullOrEmpty(Coordinates))
        {
            float coordsW = renderer.MeasureText(Coordinates, FontSize.Caption);
            renderer.DrawText(Coordinates, bounds.X + 4, coordsY + 1,
                FontSize.Caption, UITheme.Current.TextSecondary);
        }

        // Zoom indicator (bottom right)
        string zoomText = $"x{ZoomLevel}";
        float zoomW = renderer.MeasureText(zoomText, FontSize.Caption);
        renderer.DrawText(zoomText, bounds.X + Size - zoomW - 4, coordsY + 1,
            FontSize.Caption, UITheme.Current.TextMuted);
    }

    private void DrawCompassMarker(UIRenderer renderer, float baseX, float baseY,
        string label, float degrees, float bearing, Color color)
    {
        // Position on the compass bar based on bearing offset
        float delta = ((degrees - bearing + 180 + 360) % 360) - 180; // -180 to 180
        float t = delta / 90f; // -2 to 2, 0 = center
        if (MathF.Abs(t) > 1f) return; // off screen

        float x = baseX + Size / 2 + t * (Size / 2 - 10);
        renderer.DrawText(label, x - 3, baseY + 1, FontSize.Caption, color);
    }

    private static string GetCompassText(float bearing)
    {
        float norm = ((bearing % 360) + 360) % 360;
        return $"{(int)norm}\u00B0";
    }
}
