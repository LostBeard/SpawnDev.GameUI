using System.Drawing;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Tabbed container. Each tab has a header button and a content panel.
/// Only the active tab's content is visible. Click headers to switch.
/// Keyboard: left/right arrows to cycle tabs.
///
/// Usage:
///   var tabs = new UITabPanel { Width = 500, Height = 400 };
///   tabs.AddTab("General", generalSettings);
///   tabs.AddTab("Graphics", graphicsSettings);
///   tabs.AddTab("Audio", audioSettings);
///   tabs.AddTab("Controls", controlSettings);
/// </summary>
public class UITabPanel : UIPanel
{
    private readonly List<Tab> _tabs = new();
    private int _activeIndex;
    private int _hoveredIndex = -1;

    /// <summary>Height of the tab header row.</summary>
    public float TabHeight { get; set; } = 32f;

    /// <summary>Font size for tab labels.</summary>
    public FontSize TabFontSize { get; set; } = FontSize.Body;

    /// <summary>
    /// Width of each tab header in pixels, tabs left-aligned; 0 (the default) splits the panel's width equally. Three
    /// tabs across a wide panel made three huge bars; a tab strip sized to its labels reads as tabs.
    /// </summary>
    public float TabWidth { get; set; }

    private bool _pointerInside;

    private float HeaderWidth => TabWidth > 0 ? TabWidth : (Width - Padding * 2) / Math.Max(1, _tabs.Count);

    /// <summary>Called when the active tab changes.</summary>
    public Action<int, string>? OnTabChanged { get; set; }

    // Theme-aware colors
    private Color? _tabColor, _activeTabColor, _hoverTabColor, _tabTextColor, _activeTabTextColor;
    public Color TabColor { get => _tabColor ?? Color.FromArgb(180, 30, 30, 45); set => _tabColor = value; }
    public Color ActiveTabColor { get => _activeTabColor ?? UITheme.Current.ButtonNormal; set => _activeTabColor = value; }
    public Color HoverTabColor { get => _hoverTabColor ?? Color.FromArgb(200, 50, 50, 70); set => _hoverTabColor = value; }
    public Color TabTextColor { get => _tabTextColor ?? UITheme.Current.TextSecondary; set => _tabTextColor = value; }
    public Color ActiveTabTextColor { get => _activeTabTextColor ?? Color.White; set => _activeTabTextColor = value; }

    /// <summary>Active tab index.</summary>
    public int ActiveIndex
    {
        get => _activeIndex;
        set
        {
            int clamped = Math.Clamp(value, 0, Math.Max(0, _tabs.Count - 1));
            if (_activeIndex != clamped)
            {
                _activeIndex = clamped;
                UpdateVisibility();
                if (clamped < _tabs.Count)
                    OnTabChanged?.Invoke(clamped, _tabs[clamped].Label);
            }
        }
    }

    /// <summary>Add a tab with a label and content element.</summary>
    public void AddTab(string label, UIElement content)
    {
        content.X = Padding;
        content.Y = TabHeight + Padding;
        content.Visible = _tabs.Count == _activeIndex;
        AddChild(content);
        _tabs.Add(new Tab { Label = label, Content = content });
    }

    /// <summary>Remove a tab by index.</summary>
    public void RemoveTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        RemoveChild(_tabs[index].Content);
        _tabs.RemoveAt(index);
        if (_activeIndex >= _tabs.Count) _activeIndex = Math.Max(0, _tabs.Count - 1);
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        for (int i = 0; i < _tabs.Count; i++)
            _tabs[i].Content.Visible = i == _activeIndex;
    }

    public override void Update(GameInput input, float dt)
    {
        if (!Visible || !Enabled) return;

        _hoveredIndex = -1;
        _pointerInside = false;

        // Tab header click detection
        foreach (var pointer in input.Pointers)
        {
            if (!pointer.ScreenPosition.HasValue) continue;
            var mp = pointer.ScreenPosition.Value;
            var bounds = ScreenBounds;
            if (mp.X >= bounds.X && mp.X < bounds.X + Width && mp.Y >= bounds.Y && mp.Y < bounds.Y + Height)
                _pointerInside = true;

            if (mp.Y >= bounds.Y && mp.Y < bounds.Y + TabHeight && _tabs.Count > 0)
            {
                float tabW = HeaderWidth;
                float localX = mp.X - bounds.X - Padding;
                int idx = (int)(localX / tabW);
                if (idx >= 0 && idx < _tabs.Count && localX >= 0)
                {
                    _hoveredIndex = idx;
                    if (pointer.WasReleased)
                        ActiveIndex = idx;
                }
            }
        }

        // Keyboard: left/right to cycle tabs - only while the pointer is over this panel. It used to take the arrow keys
        // from anywhere on the page, so a page with tabs switched tabs whenever arrows were pressed for anything else.
        if (_pointerInside)
        {
            if (input.Keyboard.WasKeyPressed("ArrowLeft") && _activeIndex > 0)
                ActiveIndex = _activeIndex - 1;
            if (input.Keyboard.WasKeyPressed("ArrowRight") && _activeIndex < _tabs.Count - 1)
                ActiveIndex = _activeIndex + 1;
        }

        // Update active tab content
        if (_activeIndex < _tabs.Count)
            _tabs[_activeIndex].Content.Update(input, dt);
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        var bounds = ScreenBounds;

        // Panel background
        renderer.DrawRect(bounds.X, bounds.Y, Width, Height, BackgroundColor);

        // Tab headers, over a rule across the panel that separates them from the content
        if (_tabs.Count > 0)
        {
            float tabW = HeaderWidth;
            renderer.DrawRect(bounds.X, bounds.Y + TabHeight - 1, Width, 1, UITheme.Current.SeparatorColor);

            for (int i = 0; i < _tabs.Count; i++)
            {
                float tx = bounds.X + Padding + i * tabW;
                float ty = bounds.Y;

                // Tab background
                Color bg = i == _activeIndex ? ActiveTabColor :
                           i == _hoveredIndex ? HoverTabColor :
                           TabColor;
                renderer.DrawRect(tx, ty, tabW - 1, TabHeight, bg);

                // Active indicator (bottom line)
                if (i == _activeIndex)
                    renderer.DrawRect(tx, ty + TabHeight - 2, tabW - 1, 2, UITheme.Current.FocusBorder);

                // Tab label (centered)
                Color textColor = i == _activeIndex ? ActiveTabTextColor : TabTextColor;
                float textW = renderer.MeasureText(_tabs[i].Label, TabFontSize);
                float textH = renderer.GetLineHeight(TabFontSize);
                float textX = tx + (tabW - 1 - textW) / 2;
                float textY = ty + (TabHeight - textH) / 2;
                renderer.DrawText(_tabs[i].Label, textX, textY, TabFontSize, textColor);
            }
        }

        // Draw active tab content
        if (_activeIndex < _tabs.Count && _tabs[_activeIndex].Content.Visible)
        {
            // Set content area size
            var content = _tabs[_activeIndex].Content;
            content.Width = Width - Padding * 2;
            content.Height = Height - TabHeight - Padding * 2;
            content.Draw(renderer);
        }
    }

    private struct Tab
    {
        public string Label;
        public UIElement Content;
    }
}
