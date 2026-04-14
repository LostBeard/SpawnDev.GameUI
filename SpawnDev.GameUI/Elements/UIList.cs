using System.Drawing;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Scrollable list with selectable items.
/// Each item is a text label with optional icon placeholder.
/// Supports single selection, highlight on hover, and click callback.
/// Built for inventory lists, player lists, server browsers, file pickers.
/// </summary>
public class UIList : UIScrollView
{
    private readonly List<ListItem> _items = new();
    private int _selectedIndex = -1;
    private int _hoveredIndex = -1;

    /// <summary>Height of each list item row.</summary>
    public float ItemHeight { get; set; } = 28f;

    /// <summary>Font size for item text.</summary>
    public FontSize ItemFontSize { get; set; } = FontSize.Body;

    /// <summary>Called when selection changes. Parameter is the selected index (-1 = none).</summary>
    public Action<int>? OnSelectionChanged { get; set; }

    /// <summary>Called when an item is double-clicked/activated.</summary>
    public Action<int>? OnItemActivated { get; set; }

    // Theme-aware colors
    private Color? _itemColor, _selectedColor, _hoverColor, _itemTextColor;
    public Color ItemColor { get => _itemColor ?? Color.Transparent; set => _itemColor = value; }
    public Color SelectedColor { get => _selectedColor ?? Color.FromArgb(80, 108, 92, 231); set => _selectedColor = value; }
    public Color HoverColor { get => _hoverColor ?? Color.FromArgb(40, 255, 255, 255); set => _hoverColor = value; }
    public Color ItemTextColor { get => _itemTextColor ?? UITheme.Current.TextPrimary; set => _itemTextColor = value; }

    /// <summary>Currently selected index. -1 = no selection.</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex != value)
            {
                _selectedIndex = value;
                OnSelectionChanged?.Invoke(value);
            }
        }
    }

    /// <summary>Get the selected item, or null if nothing is selected.</summary>
    public ListItem? SelectedItem => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

    /// <summary>Number of items in the list.</summary>
    public int ItemCount => _items.Count;

    /// <summary>Add an item to the list.</summary>
    public void AddItem(string text, object? tag = null)
    {
        _items.Add(new ListItem { Text = text, Tag = tag });
        RebuildLayout();
    }

    /// <summary>Remove an item by index.</summary>
    public void RemoveAt(int index)
    {
        _items.RemoveAt(index);
        if (_selectedIndex >= _items.Count) _selectedIndex = _items.Count - 1;
        RebuildLayout();
    }

    /// <summary>Clear all items.</summary>
    public void ClearItems()
    {
        _items.Clear();
        _selectedIndex = -1;
        ClearChildren();
    }

    /// <summary>Get item at index.</summary>
    public ListItem GetItem(int index) => _items[index];

    public override void Update(GameInput input, float dt)
    {
        if (!Visible || !Enabled) { base.Update(input, dt); return; }

        _hoveredIndex = -1;

        foreach (var pointer in input.Pointers)
        {
            if (pointer.ScreenPosition.HasValue)
            {
                var bounds = ScreenBounds;
                var mp = pointer.ScreenPosition.Value;
                bool inBounds = mp.X >= bounds.X && mp.X < bounds.X + bounds.Width &&
                                mp.Y >= bounds.Y && mp.Y < bounds.Y + bounds.Height;

                if (inBounds)
                {
                    // Which item is the mouse over?
                    float localY = mp.Y - bounds.Y - Padding + ScrollOffset;
                    int idx = (int)(localY / ItemHeight);
                    if (idx >= 0 && idx < _items.Count)
                    {
                        _hoveredIndex = idx;
                        if (pointer.WasReleased)
                            SelectedIndex = idx;
                    }
                }
            }
        }

        base.Update(input, dt);
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        var bounds = ScreenBounds;

        // Draw panel background
        renderer.DrawRect(bounds.X, bounds.Y, bounds.Width, bounds.Height, BackgroundColor);

        // Draw visible items
        float viewTop = ScrollOffset;
        float viewBottom = ScrollOffset + Height - Padding * 2;
        int firstVisible = Math.Max(0, (int)(viewTop / ItemHeight));
        int lastVisible = Math.Min(_items.Count - 1, (int)(viewBottom / ItemHeight));

        for (int i = firstVisible; i <= lastVisible; i++)
        {
            float itemY = bounds.Y + Padding + i * ItemHeight - ScrollOffset;

            // Background: selected, hovered, or default
            if (i == _selectedIndex)
                renderer.DrawRect(bounds.X + 2, itemY, bounds.Width - 4 - (ShowScrollbar ? ScrollbarWidth + 4 : 0), ItemHeight, SelectedColor);
            else if (i == _hoveredIndex)
                renderer.DrawRect(bounds.X + 2, itemY, bounds.Width - 4 - (ShowScrollbar ? ScrollbarWidth + 4 : 0), ItemHeight, HoverColor);

            // Item text
            float textY = itemY + (ItemHeight - renderer.GetLineHeight(ItemFontSize)) / 2f;
            renderer.DrawText(_items[i].Text, bounds.X + Padding + 4, textY, ItemFontSize, ItemTextColor);
        }

        // Update content height for scrollbar
        ContentHeight = _items.Count * ItemHeight + Padding * 2;

        // Draw scrollbar (from UIScrollView)
        float maxScroll = Math.Max(0, ContentHeight - Height);
        if (ShowScrollbar && maxScroll > 0)
        {
            float scrollbarX = bounds.X + bounds.Width - ScrollbarWidth - 2;
            float viewH = Height - Padding * 2;
            float thumbH = Math.Max(20, viewH * (viewH / ContentHeight));
            float thumbY = bounds.Y + Padding + (viewH - thumbH) * (maxScroll > 0 ? ScrollOffset / maxScroll : 0);
            renderer.DrawRect(scrollbarX, bounds.Y + Padding, ScrollbarWidth, viewH, ScrollbarColor);
            renderer.DrawRect(scrollbarX, thumbY, ScrollbarWidth, thumbH, ScrollbarThumbColor);
        }
    }

    private void RebuildLayout()
    {
        ContentHeight = _items.Count * ItemHeight + Padding * 2;
    }
}

/// <summary>A single item in a UIList.</summary>
public class ListItem
{
    /// <summary>Display text.</summary>
    public string Text { get; set; } = "";

    /// <summary>Optional user data attached to this item.</summary>
    public object? Tag { get; set; }
}
