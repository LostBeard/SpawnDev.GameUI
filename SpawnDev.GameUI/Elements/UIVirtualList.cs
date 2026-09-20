using System.Drawing;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Fixed-height virtualized list. Only the visible window (plus overscan) is
/// requested from <see cref="IListDataSource"/> and drawn - no per-row UIElement nodes.
/// Use for large collections (project lists, server browsers, file pickers).
/// For small in-memory lists, <see cref="UIList"/> with AddItem is fine.
/// </summary>
public class UIVirtualList : UIScrollView
{
    private IListDataSource? _dataSource;
    private int _selectedIndex = -1;
    private int _hoveredIndex = -1;
    private int _windowStart;
    private int _windowCount;
    private int _lastEnsureStart = int.MinValue;
    private int _lastEnsureCount = int.MinValue;

    /// <summary>Height of each list item row (fixed; required for virtualization).</summary>
    public float ItemHeight { get; set; } = 28f;

    /// <summary>Extra rows above/below the viewport to prefetch.</summary>
    public int Overscan { get; set; } = 3;

    /// <summary>Font size for item text.</summary>
    public FontSize ItemFontSize { get; set; } = FontSize.Body;

    /// <summary>Drawn when TryGetItem returns false (row not loaded yet).</summary>
    public string PlaceholderText { get; set; } = "...";

    /// <summary>Called when selection changes. Parameter is the selected index (-1 = none).</summary>
    public Action<int>? OnSelectionChanged { get; set; }

    /// <summary>Called when an item is activated (double-click / secondary).</summary>
    public Action<int>? OnItemActivated { get; set; }

    private Color? _selectedColor, _hoverColor, _itemTextColor, _placeholderColor;
    public Color SelectedColor { get => _selectedColor ?? Color.FromArgb(80, UITheme.Current.SliderFill.R, UITheme.Current.SliderFill.G, UITheme.Current.SliderFill.B); set => _selectedColor = value; }
    public Color HoverColor { get => _hoverColor ?? Color.FromArgb(40, 255, 255, 255); set => _hoverColor = value; }
    public Color ItemTextColor { get => _itemTextColor ?? UITheme.Current.TextPrimary; set => _itemTextColor = value; }
    public Color PlaceholderColor { get => _placeholderColor ?? UITheme.Current.TextMuted; set => _placeholderColor = value; }

    /// <summary>
    /// Data provider. Setting resets scroll/selection and warms the visible window.
    /// </summary>
    public IListDataSource? DataSource
    {
        get => _dataSource;
        set
        {
            _dataSource = value;
            ScrollOffset = 0;
            _selectedIndex = -1;
            _lastEnsureStart = int.MinValue;
            _lastEnsureCount = int.MinValue;
            EnsureVisibleRange(force: true);
        }
    }

    /// <summary>Currently selected index. -1 = no selection.</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int count = _dataSource?.Count ?? 0;
            int clamped = count <= 0 ? -1 : Math.Clamp(value, -1, count - 1);
            if (_selectedIndex != clamped)
            {
                _selectedIndex = clamped;
                OnSelectionChanged?.Invoke(clamped);
            }
        }
    }

    /// <summary>Selected item if loaded; null if none selected or not yet available.</summary>
    public ListItem? SelectedItem
    {
        get
        {
            if (_selectedIndex < 0 || _dataSource == null) return null;
            return _dataSource.TryGetItem(_selectedIndex, out var item) ? item : null;
        }
    }

    /// <summary>Total items from the data source (0 if none).</summary>
    public int ItemCount => _dataSource?.Count ?? 0;

    /// <summary>Last EnsureRange start index (for tests).</summary>
    public int LastEnsureStart => _lastEnsureStart == int.MinValue ? 0 : _lastEnsureStart;

    /// <summary>Last EnsureRange count (for tests).</summary>
    public int LastEnsureCount => _lastEnsureCount == int.MinValue ? 0 : _lastEnsureCount;

    /// <summary>
    /// Call after the data source finishes async loads or changes Count.
    /// Clamps selection, refreshes content height, re-ensures the visible window.
    /// </summary>
    public void NotifyDataChanged()
    {
        int count = _dataSource?.Count ?? 0;
        if (_selectedIndex >= count)
            SelectedIndex = count > 0 ? count - 1 : -1;

        float maxScroll = Math.Max(0, MeasureContentHeight() - Height + Padding * 2);
        ScrollOffset = Math.Clamp(ScrollOffset, 0, maxScroll);
        EnsureVisibleRange(force: true);
    }

    protected override float MeasureContentHeight()
        => ItemCount * ItemHeight + Padding * 2;

    public override void Update(GameInput input, float dt)
    {
        if (!Visible || !Enabled)
        {
            base.Update(input, dt);
            return;
        }

        EnsureVisibleRange();

        _hoveredIndex = -1;
        foreach (var pointer in input.Pointers)
        {
            if (!pointer.ScreenPosition.HasValue) continue;

            var bounds = ScreenBounds;
            var mp = pointer.ScreenPosition.Value;
            bool inBounds = mp.X >= bounds.X && mp.X < bounds.X + bounds.Width &&
                            mp.Y >= bounds.Y && mp.Y < bounds.Y + bounds.Height;
            if (!inBounds) continue;

            float localY = mp.Y - bounds.Y - Padding + ScrollOffset;
            int idx = (int)(localY / ItemHeight);
            if (idx >= 0 && idx < ItemCount)
            {
                _hoveredIndex = idx;
                if (pointer.WasReleased)
                    SelectedIndex = idx;
            }
        }

        // UIScrollView handles wheel / thumbstick / scrollbar using MeasureContentHeight()
        base.Update(input, dt);

        // Scroll may have moved the window
        EnsureVisibleRange();
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        var bounds = ScreenBounds;
        float radius = UITheme.Current.ControlCornerRadius;
        renderer.DrawBorderedRoundedRect(bounds.X, bounds.Y, bounds.Width, bounds.Height,
            radius, BorderWidth, BorderColor, BackgroundColor);

        ContentHeight = MeasureContentHeight();
        float viewTop = ScrollOffset;
        float viewBottom = ScrollOffset + Height - Padding * 2;
        int firstVisible = Math.Max(0, (int)(viewTop / ItemHeight));
        int lastVisible = ItemCount == 0
            ? -1
            : Math.Min(ItemCount - 1, (int)(viewBottom / ItemHeight));

        float rowW = bounds.Width - 4 - (ShowScrollbar ? ScrollbarWidth + 4 : 0);

        // Clip rows to the padded content box so partial top/bottom items cannot bleed
        // past the inner border (UIScrollView culls fully-outside children only).
        float clipY = bounds.Y + Padding;
        float clipH = Math.Max(0, Height - Padding * 2);
        renderer.PushClip(bounds.X, clipY, bounds.Width, clipH);
        try
        {
            for (int i = firstVisible; i <= lastVisible; i++)
            {
                float itemY = bounds.Y + Padding + i * ItemHeight - ScrollOffset;

                if (i == _selectedIndex)
                    renderer.DrawRoundedRect(bounds.X + 2, itemY, rowW, ItemHeight, 3, SelectedColor);
                else if (i == _hoveredIndex)
                    renderer.DrawRoundedRect(bounds.X + 2, itemY, rowW, ItemHeight, 3, HoverColor);

                float textY = itemY + (ItemHeight - renderer.GetLineHeight(ItemFontSize)) / 2f;
                if (_dataSource != null && _dataSource.TryGetItem(i, out var item))
                    renderer.DrawText(item.Text, bounds.X + Padding + 4, textY, ItemFontSize, ItemTextColor);
                else
                    renderer.DrawText(PlaceholderText, bounds.X + Padding + 4, textY, ItemFontSize, PlaceholderColor);
            }
        }
        finally
        {
            renderer.PopClip();
        }

        // Scrollbar (same chrome as UIScrollView) - outside clip so track stays fully visible
        float maxScroll = Math.Max(0, ContentHeight - Height + Padding * 2);
        if (ShowScrollbar && maxScroll > 0)
        {
            float scrollbarX = bounds.X + bounds.Width - ScrollbarWidth - 2;
            float viewH = Height - Padding * 2;
            float thumbH = Math.Max(20, viewH * (viewH / ContentHeight));
            float thumbY = bounds.Y + Padding + (viewH - thumbH) * (ScrollOffset / maxScroll);
            renderer.DrawRect(scrollbarX, bounds.Y + Padding, ScrollbarWidth, viewH, ScrollbarColor);
            renderer.DrawRect(scrollbarX, thumbY, ScrollbarWidth, thumbH, ScrollbarThumbColor);
        }
    }

    private void EnsureVisibleRange(bool force = false)
    {
        int count = ItemCount;
        if (_dataSource == null || count <= 0 || ItemHeight <= 0)
        {
            _windowStart = 0;
            _windowCount = 0;
            return;
        }

        float viewH = Math.Max(0, Height - Padding * 2);
        int firstVisible = Math.Max(0, (int)(ScrollOffset / ItemHeight));
        int visibleCount = Math.Max(1, (int)MathF.Ceiling(viewH / ItemHeight) + 1);
        int start = Math.Max(0, firstVisible - Overscan);
        int end = Math.Min(count, firstVisible + visibleCount + Overscan);
        int rangeCount = Math.Max(0, end - start);

        _windowStart = start;
        _windowCount = rangeCount;

        if (!force && start == _lastEnsureStart && rangeCount == _lastEnsureCount)
            return;

        _lastEnsureStart = start;
        _lastEnsureCount = rangeCount;
        _dataSource.EnsureRange(start, rangeCount);
    }
}
