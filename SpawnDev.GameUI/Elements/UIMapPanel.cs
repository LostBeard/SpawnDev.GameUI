using System.Drawing;
using System.Numerics;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Map marker type for visual distinction.
/// </summary>
public enum MapMarkerType
{
    Player,
    OtherPlayer,
    POI,
    Quest,
    Enemy,
    Waypoint,
    Custom,
}

/// <summary>
/// A marker on the map.
/// </summary>
public class MapMarker
{
    /// <summary>Unique identifier.</summary>
    public string Id { get; set; } = "";

    /// <summary>Display label (shown on hover or always).</summary>
    public string Label { get; set; } = "";

    /// <summary>World position (X, Z for top-down 2D map).</summary>
    public Vector2 WorldPosition { get; set; }

    /// <summary>Marker type (determines icon/color).</summary>
    public MapMarkerType Type { get; set; } = MapMarkerType.POI;

    /// <summary>Custom color override (null = use type default).</summary>
    public Color? Color { get; set; }

    /// <summary>Marker size in pixels.</summary>
    public float Size { get; set; } = 6f;

    /// <summary>Whether to always show the label (vs only on hover).</summary>
    public bool AlwaysShowLabel { get; set; }

    /// <summary>Whether the marker is visible.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>User data.</summary>
    public object? Tag { get; set; }
}

/// <summary>
/// 2D minimap panel with player position indicator, named markers, and zoom.
/// Displays a top-down view of the game world with markers for POIs,
/// other players, quests, enemies, and custom waypoints.
///
/// The map does NOT render terrain - it provides the frame, markers, and
/// player indicator. Terrain rendering is the game engine's responsibility
/// (via DrawImage with a pre-rendered map texture, or procedural generation).
///
/// Usage:
///   var map = new UIMapPanel { Width = 200, Height = 200 };
///   map.PlayerPosition = new Vector2(worldX, worldZ);
///   map.PlayerRotation = playerYaw;  // radians
///   map.ZoomLevel = 2f;  // 2 world units per pixel
///
///   map.AddMarker(new MapMarker {
///       Id = "base", Label = "Home Base",
///       WorldPosition = new Vector2(100, 200),
///       Type = MapMarkerType.POI,
///   });
/// </summary>
public class UIMapPanel : UIPanel
{
    private readonly List<MapMarker> _markers = new();

    /// <summary>Player's world position (X, Z for top-down view).</summary>
    public Vector2 PlayerPosition { get; set; }

    /// <summary>Player's Y (vertical) position. Displayed alongside XZ when ShowCoordinates is true.</summary>
    public float PlayerAltitude { get; set; }

    /// <summary>Player's rotation in radians (0 = North/+Z, clockwise).</summary>
    public float PlayerRotation { get; set; }

    /// <summary>Zoom level: world units per pixel. Lower = more zoomed in.</summary>
    public float ZoomLevel { get; set; } = 1f;

    /// <summary>Minimum zoom level.</summary>
    public float MinZoom { get; set; } = 0.1f;

    /// <summary>Maximum zoom level.</summary>
    public float MaxZoom { get; set; } = 20f;

    /// <summary>Whether the map rotates with the player (true) or stays North-up (false).</summary>
    public bool RotateWithPlayer { get; set; }

    /// <summary>Show cardinal direction indicators (N/E/S/W).</summary>
    public bool ShowCardinals { get; set; } = true;

    /// <summary>Show coordinate text at bottom.</summary>
    public bool ShowCoordinates { get; set; } = true;

    /// <summary>Player marker size.</summary>
    public float PlayerMarkerSize { get; set; } = 8f;

    /// <summary>Player marker color.</summary>
    public Color PlayerMarkerColor { get; set; } = Color.FromArgb(255, 100, 200, 255);

    /// <summary>Map background color.</summary>
    public Color MapBackgroundColor { get; set; } = Color.FromArgb(200, 15, 20, 15);

    /// <summary>Map border color.</summary>
    public Color MapBorderColor { get; set; } = Color.FromArgb(140, 100, 100, 80);

    /// <summary>Called when a marker is clicked.</summary>
    public Action<MapMarker>? OnMarkerClicked { get; set; }

    /// <summary>Currently hovered marker, or null.</summary>
    public MapMarker? HoveredMarker { get; private set; }

    /// <summary>All markers.</summary>
    public IReadOnlyList<MapMarker> Markers => _markers;

    /// <summary>Number of markers.</summary>
    public int MarkerCount => _markers.Count;

    public UIMapPanel()
    {
        Width = 200;
        Height = 200;
        Padding = 2;
    }

    /// <summary>Add a marker to the map.</summary>
    public void AddMarker(MapMarker marker)
    {
        var existing = _markers.FindIndex(m => m.Id == marker.Id);
        if (existing >= 0)
            _markers[existing] = marker;
        else
            _markers.Add(marker);
    }

    /// <summary>Remove a marker by Id.</summary>
    public bool RemoveMarker(string id)
    {
        int idx = _markers.FindIndex(m => m.Id == id);
        if (idx < 0) return false;
        _markers.RemoveAt(idx);
        return true;
    }

    /// <summary>Get a marker by Id.</summary>
    public MapMarker? GetMarker(string id) => _markers.Find(m => m.Id == id);

    /// <summary>Check if a marker exists.</summary>
    public bool HasMarker(string id) => _markers.Exists(m => m.Id == id);

    /// <summary>Update all marker positions (for moving entities).</summary>
    public void UpdateMarkerPosition(string id, Vector2 worldPos)
    {
        var marker = GetMarker(id);
        if (marker != null) marker.WorldPosition = worldPos;
    }

    /// <summary>Remove all markers.</summary>
    public void ClearMarkers() => _markers.Clear();

    /// <summary>Convert a world position to map-local pixel position.</summary>
    public Vector2 WorldToMap(Vector2 worldPos)
    {
        float cx = Width / 2f;
        float cy = Height / 2f;

        // Offset from player
        Vector2 delta = worldPos - PlayerPosition;

        // Apply rotation if map rotates with player
        if (RotateWithPlayer)
        {
            float cos = MathF.Cos(-PlayerRotation);
            float sin = MathF.Sin(-PlayerRotation);
            delta = new Vector2(
                delta.X * cos - delta.Y * sin,
                delta.X * sin + delta.Y * cos);
        }

        // Scale to pixels
        return new Vector2(
            cx + delta.X / ZoomLevel,
            cy - delta.Y / ZoomLevel); // Y flipped (screen Y goes down, world Z goes up)
    }

    public override void Update(GameInput input, float dt)
    {
        if (!Visible || !Enabled) return;

        HoveredMarker = null;

        foreach (var pointer in input.Pointers)
        {
            if (!pointer.ScreenPosition.HasValue) continue;
            var mp = pointer.ScreenPosition.Value;
            var bounds = ScreenBounds;

            // Check if pointer is inside map
            if (mp.X < bounds.X || mp.X >= bounds.X + bounds.Width ||
                mp.Y < bounds.Y || mp.Y >= bounds.Y + bounds.Height)
                continue;

            // Scroll to zoom
            if (pointer.ScrollDelta != 0)
            {
                ZoomLevel *= (1f - pointer.ScrollDelta * 0.1f);
                ZoomLevel = MathF.Max(MinZoom, MathF.Min(ZoomLevel, MaxZoom));
            }

            // Check marker hover/click
            foreach (var marker in _markers)
            {
                if (!marker.Visible) continue;
                var markerScreen = WorldToMap(marker.WorldPosition);
                float sx = bounds.X + markerScreen.X;
                float sy = bounds.Y + markerScreen.Y;

                if (MathF.Abs(mp.X - sx) <= marker.Size &&
                    MathF.Abs(mp.Y - sy) <= marker.Size)
                {
                    HoveredMarker = marker;
                    if (pointer.WasReleased)
                        OnMarkerClicked?.Invoke(marker);
                    break;
                }
            }
        }

        base.Update(input, dt);
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        var bounds = ScreenBounds;

        // Map background
        renderer.DrawRect(bounds.X, bounds.Y, bounds.Width, bounds.Height, MapBackgroundColor);

        // Border
        renderer.DrawRect(bounds.X, bounds.Y, bounds.Width, 1, MapBorderColor);
        renderer.DrawRect(bounds.X, bounds.Y, 1, bounds.Height, MapBorderColor);
        renderer.DrawRect(bounds.X + bounds.Width - 1, bounds.Y, 1, bounds.Height, MapBorderColor);
        renderer.DrawRect(bounds.X, bounds.Y + bounds.Height - 1, bounds.Width, 1, MapBorderColor);

        // Draw markers
        foreach (var marker in _markers)
        {
            if (!marker.Visible) continue;
            var mapPos = WorldToMap(marker.WorldPosition);

            // Clip to map bounds
            if (mapPos.X < 0 || mapPos.X >= bounds.Width ||
                mapPos.Y < 0 || mapPos.Y >= bounds.Height)
                continue;

            float sx = bounds.X + mapPos.X;
            float sy = bounds.Y + mapPos.Y;
            float half = marker.Size / 2f;

            var color = marker.Color ?? GetMarkerColor(marker.Type);
            renderer.DrawRect(sx - half, sy - half, marker.Size, marker.Size, color);

            // Label
            if (marker.AlwaysShowLabel || marker == HoveredMarker)
            {
                float lw = renderer.MeasureText(marker.Label, FontSize.Caption);
                renderer.DrawText(marker.Label, sx - lw / 2, sy - marker.Size - 12,
                    FontSize.Caption, Color.White);
            }
        }

        // Player indicator (center of map)
        float pcx = bounds.X + bounds.Width / 2f;
        float pcy = bounds.Y + bounds.Height / 2f;
        float ps = PlayerMarkerSize / 2f;

        // Player arrow (triangle pointing in facing direction)
        renderer.DrawRect(pcx - ps, pcy - ps, PlayerMarkerSize, PlayerMarkerSize, PlayerMarkerColor);
        // Direction line
        float dirLen = PlayerMarkerSize * 1.5f;
        float dirX, dirY;
        if (RotateWithPlayer)
        {
            dirX = 0; dirY = -dirLen; // always points up when rotating
        }
        else
        {
            dirX = MathF.Sin(PlayerRotation) * dirLen;
            dirY = -MathF.Cos(PlayerRotation) * dirLen;
        }
        // Draw a 2px wide line as direction indicator
        renderer.DrawRect(pcx - 1, pcy - 1, 2, 2, Color.White);
        renderer.DrawRect(pcx + dirX - 1, pcy + dirY - 1, 2, 2, Color.White);

        // Cardinals (N/E/S/W)
        if (ShowCardinals)
        {
            float edgePad = 8;
            float nw = renderer.MeasureText("N", FontSize.Caption);
            renderer.DrawText("N", pcx - nw / 2, bounds.Y + edgePad, FontSize.Caption,
                Color.FromArgb(200, 255, 80, 80));

            float sw = renderer.MeasureText("S", FontSize.Caption);
            renderer.DrawText("S", pcx - sw / 2, bounds.Y + bounds.Height - edgePad - 14,
                FontSize.Caption, Color.FromArgb(140, 200, 200, 200));

            renderer.DrawText("W", bounds.X + edgePad, pcy - 6, FontSize.Caption,
                Color.FromArgb(140, 200, 200, 200));

            float ew = renderer.MeasureText("E", FontSize.Caption);
            renderer.DrawText("E", bounds.X + bounds.Width - edgePad - ew, pcy - 6,
                FontSize.Caption, Color.FromArgb(140, 200, 200, 200));
        }

        // Coordinates
        if (ShowCoordinates)
        {
            string coords = $"{PlayerPosition.X:F0}, {PlayerAltitude:F0}, {PlayerPosition.Y:F0}";
            float cw = renderer.MeasureText(coords, FontSize.Caption);
            renderer.DrawRect(pcx - cw / 2 - 4, bounds.Y + bounds.Height - 18, cw + 8, 16,
                Color.FromArgb(160, 0, 0, 0));
            renderer.DrawText(coords, pcx - cw / 2, bounds.Y + bounds.Height - 17,
                FontSize.Caption, Color.FromArgb(180, 200, 200, 200));
        }
    }

    private static Color GetMarkerColor(MapMarkerType type) => type switch
    {
        MapMarkerType.Player => Color.FromArgb(255, 100, 200, 255),
        MapMarkerType.OtherPlayer => Color.FromArgb(255, 100, 255, 100),
        MapMarkerType.POI => Color.FromArgb(255, 255, 200, 50),
        MapMarkerType.Quest => Color.FromArgb(255, 255, 220, 0),
        MapMarkerType.Enemy => Color.FromArgb(255, 255, 60, 60),
        MapMarkerType.Waypoint => Color.FromArgb(255, 150, 150, 255),
        _ => Color.FromArgb(255, 200, 200, 200),
    };
}
