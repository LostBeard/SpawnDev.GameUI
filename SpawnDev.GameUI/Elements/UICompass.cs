using System.Drawing;
using System.Numerics;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Horizontal compass bar showing bearing direction with cardinal markers.
/// Skyrim/Fallout/DayZ style - sits at the top of the screen.
/// Supports waypoint markers (quest objectives, player markers, POIs).
///
/// Usage:
///   var compass = new UICompass { Width = 400, Height = 30 };
///   root.AddAnchored(compass, Anchor.TopCenter, offsetY: 10);
///
///   // Per frame:
///   compass.Bearing = player.Yaw; // degrees, 0 = North
///
///   // Add waypoints:
///   compass.AddWaypoint("Quest", 45f, Color.Yellow);   // NE
///   compass.AddWaypoint("Base", 180f, Color.Green);     // S
///   compass.AddWaypoint("Danger", 270f, Color.Red);     // W
/// </summary>
public class UICompass : UIElement
{
    private readonly List<CompassWaypoint> _waypoints = new();

    /// <summary>Current bearing in degrees (0=North, 90=East, 180=South, 270=West).</summary>
    public float Bearing { get; set; }

    /// <summary>Field of view visible on the compass bar (degrees).</summary>
    public float FieldOfView { get; set; } = 180f;

    // Theme-aware colors
    private Color? _bgColor, _tickColor, _cardinalColor, _northColor, _bearingColor;
    public Color BackgroundColor { get => _bgColor ?? Color.FromArgb(160, 10, 10, 15); set => _bgColor = value; }
    public Color TickColor { get => _tickColor ?? Color.FromArgb(80, 200, 200, 200); set => _tickColor = value; }
    public Color CardinalColor { get => _cardinalColor ?? Color.FromArgb(200, 200, 200, 220); set => _cardinalColor = value; }
    public Color NorthColor { get => _northColor ?? Color.FromArgb(255, 220, 60, 60); set => _northColor = value; }
    public Color BearingColor { get => _bearingColor ?? UITheme.Current.TextSecondary; set => _bearingColor = value; }

    /// <summary>Add a waypoint marker to the compass.</summary>
    public void AddWaypoint(string label, float bearing, Color color, string? icon = null)
    {
        _waypoints.Add(new CompassWaypoint { Label = label, Bearing = bearing, Color = color, Icon = icon });
    }

    /// <summary>Remove all waypoints.</summary>
    public void ClearWaypoints() => _waypoints.Clear();

    /// <summary>Remove a waypoint by label.</summary>
    public void RemoveWaypoint(string label) => _waypoints.RemoveAll(w => w.Label == label);

    /// <summary>Update a waypoint's bearing (for moving targets).</summary>
    public void UpdateWaypoint(string label, float newBearing)
    {
        for (int i = 0; i < _waypoints.Count; i++)
        {
            if (_waypoints[i].Label == label)
            {
                _waypoints[i] = _waypoints[i] with { Bearing = newBearing };
                return;
            }
        }
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        var bounds = ScreenBounds;
        float halfFov = FieldOfView / 2f;
        float bearingNorm = ((Bearing % 360) + 360) % 360;

        // Background
        renderer.DrawRect(bounds.X, bounds.Y, bounds.Width, bounds.Height, BackgroundColor);

        // Center indicator (current bearing)
        renderer.DrawRect(bounds.X + bounds.Width / 2 - 1, bounds.Y, 2, bounds.Height,
            Color.FromArgb(60, 255, 255, 255));

        // Degree ticks every 15 degrees
        for (int deg = 0; deg < 360; deg += 15)
        {
            float delta = AngleDelta(bearingNorm, deg);
            if (MathF.Abs(delta) > halfFov) continue;

            float x = bounds.X + bounds.Width / 2 + (delta / halfFov) * (bounds.Width / 2);
            bool isMajor = deg % 90 == 0;
            bool isMinor = deg % 45 == 0;
            float tickH = isMajor ? bounds.Height * 0.5f : isMinor ? bounds.Height * 0.35f : bounds.Height * 0.2f;

            renderer.DrawRect(x, bounds.Y + bounds.Height - tickH, 1, tickH, TickColor);
        }

        // Cardinal directions
        DrawCardinal(renderer, bounds, bearingNorm, halfFov, "N", 0, NorthColor);
        DrawCardinal(renderer, bounds, bearingNorm, halfFov, "NE", 45, CardinalColor);
        DrawCardinal(renderer, bounds, bearingNorm, halfFov, "E", 90, CardinalColor);
        DrawCardinal(renderer, bounds, bearingNorm, halfFov, "SE", 135, CardinalColor);
        DrawCardinal(renderer, bounds, bearingNorm, halfFov, "S", 180, CardinalColor);
        DrawCardinal(renderer, bounds, bearingNorm, halfFov, "SW", 225, CardinalColor);
        DrawCardinal(renderer, bounds, bearingNorm, halfFov, "W", 270, CardinalColor);
        DrawCardinal(renderer, bounds, bearingNorm, halfFov, "NW", 315, CardinalColor);

        // Waypoint markers
        foreach (var wp in _waypoints)
        {
            float wpBearing = ((wp.Bearing % 360) + 360) % 360;
            float delta = AngleDelta(bearingNorm, wpBearing);
            if (MathF.Abs(delta) > halfFov) continue;

            float x = bounds.X + bounds.Width / 2 + (delta / halfFov) * (bounds.Width / 2);

            // Marker triangle/diamond
            renderer.DrawRect(x - 3, bounds.Y + 2, 6, 6, wp.Color);
            renderer.DrawRect(x - 1, bounds.Y, 2, 2, wp.Color);

            // Label below the compass
            if (!string.IsNullOrEmpty(wp.Label))
            {
                float labelW = renderer.MeasureText(wp.Label, FontSize.Caption);
                renderer.DrawText(wp.Label, x - labelW / 2, bounds.Y + bounds.Height + 2,
                    FontSize.Caption, wp.Color);
            }
        }

        // Current bearing text (centered below the bar)
        string bearingText = $"{(int)bearingNorm}\u00B0";
        float btW = renderer.MeasureText(bearingText, FontSize.Caption);
        renderer.DrawText(bearingText, bounds.X + (bounds.Width - btW) / 2, bounds.Y + 2,
            FontSize.Caption, BearingColor);

        base.Draw(renderer);
    }

    private void DrawCardinal(UIRenderer renderer, System.Drawing.RectangleF bounds,
        float bearing, float halfFov, string label, float degrees, Color color)
    {
        float delta = AngleDelta(bearing, degrees);
        if (MathF.Abs(delta) > halfFov) return;

        float x = bounds.X + bounds.Width / 2 + (delta / halfFov) * (bounds.Width / 2);
        float labelW = renderer.MeasureText(label, FontSize.Caption);
        float labelY = bounds.Y + bounds.Height * 0.15f;
        renderer.DrawText(label, x - labelW / 2, labelY, FontSize.Caption, color);
    }

    /// <summary>Shortest angle delta from 'from' to 'to' in degrees (-180 to 180).</summary>
    private static float AngleDelta(float from, float to)
    {
        float delta = ((to - from + 180 + 360) % 360) - 180;
        return delta;
    }
}

/// <summary>A waypoint marker on the compass.</summary>
public record struct CompassWaypoint
{
    public string Label;
    public float Bearing;
    public Color Color;
    public string? Icon;
}
