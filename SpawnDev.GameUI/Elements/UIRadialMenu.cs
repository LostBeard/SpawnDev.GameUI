using System.Drawing;
using System.Numerics;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Radial context menu for DayZ-style interaction.
/// Opens centered on screen/pointer. Items arranged in a circle.
/// Mouse/thumbstick direction selects an option. Release/click confirms.
/// Hold-to-confirm optional for dangerous actions (drop, eat, etc.).
///
/// Usage:
///   var menu = new UIRadialMenu();
///   menu.AddOption("Open", Icons.Open, () => OpenDoor());
///   menu.AddOption("Lock", Icons.Lock, () => LockDoor());
///   menu.AddOption("Kick", Icons.Boot, () => KickDoor(), holdToConfirm: true);
///   menu.Show(screenCenter);
/// </summary>
public class UIRadialMenu : UIElement
{
    private readonly List<RadialOption> _options = new();
    private int _hoveredIndex = -1;
    private int _selectedIndex = -1;
    private float _holdProgress; // 0-1 for hold-to-confirm
    private bool _isHolding;
    private Vector2 _center;

    /// <summary>Outer radius of the radial menu.</summary>
    public float OuterRadius { get; set; } = 120f;

    /// <summary>Inner radius (dead zone in the center).</summary>
    public float InnerRadius { get; set; } = 30f;

    /// <summary>Hold-to-confirm duration in seconds.</summary>
    public float HoldDuration { get; set; } = 1.0f;

    /// <summary>Called when the menu is dismissed without selection.</summary>
    public Action? OnDismiss { get; set; }

    // Theme-aware colors
    private Color? _bgColor, _hoverColor, _textColor, _holdColor;
    public Color BackgroundColor { get => _bgColor ?? Color.FromArgb(180, 20, 20, 30); set => _bgColor = value; }
    public Color HoverColor { get => _hoverColor ?? Color.FromArgb(200, 108, 92, 231); set => _hoverColor = value; }
    public Color TextColor { get => _textColor ?? UITheme.Current.TextPrimary; set => _textColor = value; }
    public Color HoldColor { get => _holdColor ?? Color.FromArgb(200, 255, 180, 60); set => _holdColor = value; }

    /// <summary>Add an option to the radial menu.</summary>
    public void AddOption(string label, Action? action = null, bool holdToConfirm = false)
    {
        _options.Add(new RadialOption { Label = label, Action = action, HoldToConfirm = holdToConfirm });
    }

    /// <summary>Clear all options.</summary>
    public void ClearOptions()
    {
        _options.Clear();
        _hoveredIndex = -1;
    }

    /// <summary>Show the menu centered at the given screen position.</summary>
    public void Show(Vector2 center)
    {
        _center = center;
        X = center.X - OuterRadius;
        Y = center.Y - OuterRadius;
        Width = OuterRadius * 2;
        Height = OuterRadius * 2;
        Visible = true;
        _hoveredIndex = -1;
        _selectedIndex = -1;
        _holdProgress = 0;
        _isHolding = false;
    }

    /// <summary>Hide the menu.</summary>
    public void Hide()
    {
        Visible = false;
        _hoveredIndex = -1;
        _holdProgress = 0;
        _isHolding = false;
    }

    public override void Update(GameInput input, float dt)
    {
        if (!Visible || _options.Count == 0) return;

        Vector2? pointerPos = null;
        bool isPressed = false;
        bool wasReleased = false;

        // Get pointer position
        foreach (var pointer in input.Pointers)
        {
            if (pointer.ScreenPosition.HasValue)
            {
                pointerPos = pointer.ScreenPosition.Value;
                isPressed = pointer.IsPressed;
                wasReleased = pointer.WasReleased;
                break;
            }
        }

        // Gamepad thumbstick as direction
        if (pointerPos == null && input.Gamepad.Connected)
        {
            var stick = input.Gamepad.RightStick;
            if (stick.Length() > 0.3f)
            {
                pointerPos = _center + stick * OuterRadius * 0.8f;
                isPressed = input.Gamepad.IsButtonDown(0); // A button
                wasReleased = input.Gamepad.WasButtonPressed(0);
            }
        }

        if (pointerPos.HasValue)
        {
            var delta = pointerPos.Value - _center;
            float dist = delta.Length();

            if (dist > InnerRadius && dist < OuterRadius * 1.5f && _options.Count > 0)
            {
                // Determine which wedge the pointer is in
                float angle = MathF.Atan2(delta.Y, delta.X);
                if (angle < 0) angle += MathF.PI * 2;
                float wedgeSize = MathF.PI * 2 / _options.Count;
                // Offset so first option is at top (- PI/2)
                float adjustedAngle = angle + MathF.PI / 2 + wedgeSize / 2;
                if (adjustedAngle > MathF.PI * 2) adjustedAngle -= MathF.PI * 2;
                _hoveredIndex = (int)(adjustedAngle / wedgeSize) % _options.Count;
            }
            else if (dist <= InnerRadius)
            {
                _hoveredIndex = -1; // in dead zone
            }
        }

        // Hold-to-confirm logic
        if (_hoveredIndex >= 0 && _hoveredIndex < _options.Count)
        {
            var option = _options[_hoveredIndex];
            if (option.HoldToConfirm)
            {
                if (isPressed)
                {
                    _isHolding = true;
                    _holdProgress += dt / HoldDuration;
                    if (_holdProgress >= 1f)
                    {
                        // Confirmed
                        option.Action?.Invoke();
                        Hide();
                        return;
                    }
                }
                else
                {
                    _holdProgress = 0;
                    _isHolding = false;
                }
            }
            else if (wasReleased)
            {
                // Instant confirm
                option.Action?.Invoke();
                Hide();
                return;
            }
        }
        else
        {
            _holdProgress = 0;
            _isHolding = false;
        }

        // Escape to dismiss
        if (input.Keyboard.WasKeyPressed("Escape"))
        {
            OnDismiss?.Invoke();
            Hide();
        }

        base.Update(input, dt);
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible || _options.Count == 0) return;

        int count = _options.Count;
        float wedgeSize = MathF.PI * 2 / count;
        float midRadius = (InnerRadius + OuterRadius) / 2f;

        // Draw center circle (dead zone)
        DrawCircleApprox(renderer, _center, InnerRadius, Color.FromArgb(160, 15, 15, 20), 16);

        // Draw wedges
        for (int i = 0; i < count; i++)
        {
            // Wedge angle (first option at top = -PI/2)
            float startAngle = -MathF.PI / 2 + i * wedgeSize;
            float midAngle = startAngle + wedgeSize / 2;

            // Wedge center point
            float cx = _center.X + MathF.Cos(midAngle) * midRadius;
            float cy = _center.Y + MathF.Sin(midAngle) * midRadius;

            // Draw wedge background (approximated as a rectangle at the wedge position)
            float wedgeW = OuterRadius * 0.6f;
            float wedgeH = 28f;
            Color bgColor = i == _hoveredIndex ? HoverColor : BackgroundColor;

            if (i == _hoveredIndex && _isHolding)
            {
                // Hold-to-confirm: fill progress
                renderer.DrawRect(cx - wedgeW / 2, cy - wedgeH / 2, wedgeW, wedgeH, BackgroundColor);
                renderer.DrawRect(cx - wedgeW / 2, cy - wedgeH / 2, wedgeW * _holdProgress, wedgeH, HoldColor);
            }
            else
            {
                renderer.DrawRect(cx - wedgeW / 2, cy - wedgeH / 2, wedgeW, wedgeH, bgColor);
            }

            // Label text (centered in wedge)
            string label = _options[i].Label;
            float textW = renderer.MeasureText(label, FontSize.Body);
            float textH = renderer.GetLineHeight(FontSize.Body);
            renderer.DrawText(label, cx - textW / 2, cy - textH / 2, FontSize.Body, TextColor);
        }

        // Draw hover indicator line from center to hovered option
        if (_hoveredIndex >= 0)
        {
            float midAngle = -MathF.PI / 2 + _hoveredIndex * wedgeSize + wedgeSize / 2;
            float lineEndX = _center.X + MathF.Cos(midAngle) * InnerRadius * 1.5f;
            float lineEndY = _center.Y + MathF.Sin(midAngle) * InnerRadius * 1.5f;
            // Simple line as a thin rect
            DrawLine(renderer, _center.X, _center.Y, lineEndX, lineEndY, 2f, HoverColor);
        }
    }

    /// <summary>Approximate a filled circle using quads (for the center dead zone).</summary>
    private static void DrawCircleApprox(UIRenderer renderer, Vector2 center, float radius, Color color, int segments)
    {
        float step = MathF.PI * 2 / segments;
        for (int i = 0; i < segments; i++)
        {
            float a1 = i * step;
            float a2 = (i + 1) * step;
            float x1 = center.X + MathF.Cos(a1) * radius;
            float y1 = center.Y + MathF.Sin(a1) * radius;
            float x2 = center.X + MathF.Cos(a2) * radius;
            float y2 = center.Y + MathF.Sin(a2) * radius;
            // Triangle from center to edge (approximated as thin rect)
            float midX = (center.X + x1 + x2) / 3f;
            float midY = (center.Y + y1 + y2) / 3f;
            float size = radius * step * 0.5f;
            renderer.DrawRect(midX - size / 2, midY - size / 2, size, size, color);
        }
    }

    /// <summary>Draw a line as a thin rotated rectangle.</summary>
    private static void DrawLine(UIRenderer renderer, float x1, float y1, float x2, float y2, float thickness, Color color)
    {
        float dx = x2 - x1, dy = y2 - y1;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.1f) return;
        // Approximate: draw a thin rect along the line direction
        float midX = (x1 + x2) / 2f;
        float midY = (y1 + y2) / 2f;
        // For simplicity, draw as a horizontal rect (works well enough for short lines)
        float w = MathF.Max(MathF.Abs(dx), thickness);
        float h = MathF.Max(MathF.Abs(dy), thickness);
        renderer.DrawRect(MathF.Min(x1, x2), MathF.Min(y1, y2), w, h, color);
    }
}

/// <summary>A single option in a radial menu.</summary>
public class RadialOption
{
    /// <summary>Display label.</summary>
    public string Label { get; set; } = "";

    /// <summary>Action when selected.</summary>
    public Action? Action { get; set; }

    /// <summary>If true, requires holding the action button for HoldDuration to confirm.</summary>
    public bool HoldToConfirm { get; set; }
}
