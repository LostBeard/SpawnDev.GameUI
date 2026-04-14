using System.Drawing;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Scrollable container that clips children to its bounds.
/// Supports mouse wheel, touch drag, VR thumbstick, and scrollbar thumb drag.
/// Essential for inventory lists, chat windows, settings panels, and any
/// content taller than the viewport.
///
/// Children are positioned relative to the scroll offset. Only children
/// within the visible region are drawn (simple culling for performance).
/// </summary>
public class UIScrollView : UIPanel
{
    /// <summary>Current vertical scroll offset in pixels. 0 = top.</summary>
    public float ScrollOffset { get; set; }

    /// <summary>Total content height (computed from children or set by subclasses).</summary>
    public float ContentHeight { get; protected set; }

    /// <summary>Scroll speed multiplier for mouse wheel.</summary>
    public float WheelSpeed { get; set; } = 40f;

    /// <summary>Scroll speed multiplier for VR thumbstick.</summary>
    public float ThumbstickSpeed { get; set; } = 200f;

    /// <summary>Show scrollbar when content exceeds viewport.</summary>
    public bool ShowScrollbar { get; set; } = true;

    /// <summary>Scrollbar width in pixels.</summary>
    public float ScrollbarWidth { get; set; } = 6f;

    // Theme-aware colors
    private Color? _scrollbarColor, _scrollbarThumbColor;
    public Color ScrollbarColor { get => _scrollbarColor ?? Color.FromArgb(40, 255, 255, 255); set => _scrollbarColor = value; }
    public Color ScrollbarThumbColor { get => _scrollbarThumbColor ?? Color.FromArgb(120, 255, 255, 255); set => _scrollbarThumbColor = value; }

    private bool _isDraggingThumb;
    private float _dragStartY;
    private float _dragStartOffset;

    public override void Update(GameInput input, float dt)
    {
        if (!Visible || !Enabled) return;

        // Compute content height from children
        float maxBottom = 0;
        foreach (var child in Children)
        {
            if (!child.Visible) continue;
            float bottom = child.Y + child.Height;
            if (bottom > maxBottom) maxBottom = bottom;
        }
        ContentHeight = maxBottom + Padding;

        float maxScroll = Math.Max(0, ContentHeight - Height + Padding * 2);

        // Handle scroll input from all pointers
        foreach (var pointer in input.Pointers)
        {
            bool isHovered = false;

            if (pointer.ScreenPosition.HasValue)
            {
                var bounds = ScreenBounds;
                var mp = pointer.ScreenPosition.Value;
                isHovered = mp.X >= bounds.X && mp.X < bounds.X + bounds.Width &&
                            mp.Y >= bounds.Y && mp.Y < bounds.Y + bounds.Height;

                // Scrollbar thumb drag
                if (ShowScrollbar && maxScroll > 0 && isHovered)
                {
                    float scrollbarX = bounds.X + bounds.Width - ScrollbarWidth - 2;
                    float viewH = Height - Padding * 2;
                    float thumbH = Math.Max(20, viewH * (viewH / ContentHeight));
                    float thumbY = bounds.Y + Padding + (viewH - thumbH) * (ScrollOffset / maxScroll);

                    bool overThumb = mp.X >= scrollbarX && mp.Y >= thumbY && mp.Y <= thumbY + thumbH;

                    if (overThumb && pointer.WasPressed)
                    {
                        _isDraggingThumb = true;
                        _dragStartY = mp.Y;
                        _dragStartOffset = ScrollOffset;
                    }
                }

                if (_isDraggingThumb && pointer.IsPressed)
                {
                    float viewH = Height - Padding * 2;
                    float thumbH = Math.Max(20, viewH * (viewH / ContentHeight));
                    float trackRange = viewH - thumbH;
                    if (trackRange > 0)
                    {
                        float deltaY = mp.Y - _dragStartY;
                        ScrollOffset = Math.Clamp(_dragStartOffset + deltaY * (maxScroll / trackRange), 0, maxScroll);
                    }
                }
                else
                {
                    _isDraggingThumb = false;
                }
            }

            // Mouse wheel scroll
            if (isHovered && MathF.Abs(pointer.ScrollDelta) > 0.1f)
            {
                ScrollOffset = Math.Clamp(ScrollOffset + pointer.ScrollDelta * WheelSpeed * 0.01f, 0, maxScroll);
            }

            // VR thumbstick scroll
            if (pointer.Type == PointerType.Controller && MathF.Abs(pointer.ScrollDelta) > 0.1f)
            {
                ScrollOffset = Math.Clamp(ScrollOffset + pointer.ScrollDelta * ThumbstickSpeed * dt, 0, maxScroll);
            }
        }

        // Update children with offset applied
        // Children see their normal coordinates - scroll offset is applied during Draw
        base.Update(input, dt);
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        var bounds = ScreenBounds;

        // Draw panel background
        if (BorderWidth > 0)
        {
            renderer.DrawRect(bounds.X - BorderWidth, bounds.Y - BorderWidth,
                              bounds.Width + BorderWidth * 2, bounds.Height + BorderWidth * 2,
                              BorderColor);
        }
        renderer.DrawRect(bounds.X, bounds.Y, bounds.Width, bounds.Height, BackgroundColor);

        // Draw visible children with scroll offset
        // Simple culling: skip children fully above or below the viewport
        float viewTop = ScrollOffset;
        float viewBottom = ScrollOffset + Height - Padding * 2;

        var snapshot = Children.ToArray();
        foreach (var child in snapshot)
        {
            if (!child.Visible) continue;

            float childTop = child.Y;
            float childBottom = child.Y + child.Height;

            // Skip if fully outside viewport
            if (childBottom < viewTop || childTop > viewBottom) continue;

            // Temporarily offset child Y for drawing
            float originalY = child.Y;
            child.Y = originalY - ScrollOffset + Padding;
            child.Draw(renderer);
            child.Y = originalY; // restore
        }

        // Draw scrollbar
        float maxScroll = Math.Max(0, ContentHeight - Height + Padding * 2);
        if (ShowScrollbar && maxScroll > 0)
        {
            float scrollbarX = bounds.X + bounds.Width - ScrollbarWidth - 2;
            float viewH = Height - Padding * 2;
            float thumbH = Math.Max(20, viewH * (viewH / ContentHeight));
            float thumbY = bounds.Y + Padding + (viewH - thumbH) * (maxScroll > 0 ? ScrollOffset / maxScroll : 0);

            // Track
            renderer.DrawRect(scrollbarX, bounds.Y + Padding, ScrollbarWidth, viewH,
                ScrollbarColor);
            // Thumb
            renderer.DrawRect(scrollbarX, thumbY, ScrollbarWidth, thumbH,
                _isDraggingThumb ? UITheme.Current.FocusBorder : ScrollbarThumbColor);
        }
    }

    /// <summary>Scroll to make a specific Y position visible.</summary>
    public void ScrollTo(float y)
    {
        float maxScroll = Math.Max(0, ContentHeight - Height + Padding * 2);
        ScrollOffset = Math.Clamp(y - Height / 2, 0, maxScroll);
    }

    /// <summary>Scroll to top.</summary>
    public void ScrollToTop() => ScrollOffset = 0;

    /// <summary>Scroll to bottom.</summary>
    public void ScrollToBottom()
    {
        float maxScroll = Math.Max(0, ContentHeight - Height + Padding * 2);
        ScrollOffset = maxScroll;
    }
}
