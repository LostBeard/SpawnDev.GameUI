using System.Drawing;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Right-click context menu. Appears at the pointer position, dismisses on click outside
/// or Escape. Items can have sub-labels and separators. Keyboard navigable (up/down/Enter).
///
/// Usage:
///   var menu = new UIContextMenu();
///   menu.AddItem("Inspect", () => InspectTarget());
///   menu.AddItem("Trade", () => OpenTrade());
///   menu.AddSeparator();
///   menu.AddItem("Attack", () => Attack(), color: Color.Red);
///   menu.Show(mousePos);
/// </summary>
public class UIContextMenu : UIElement
{
    private readonly List<ContextMenuItem> _items = new();
    private int _hoveredIndex = -1;

    /// <summary>Width of the context menu.</summary>
    public float MenuWidth { get; set; } = 180f;

    /// <summary>Height of each item row.</summary>
    public float ItemHeight { get; set; } = 28f;

    /// <summary>Height of separator rows.</summary>
    public float SeparatorHeight { get; set; } = 8f;

    /// <summary>Called when menu is dismissed without selection.</summary>
    public Action? OnDismiss { get; set; }

    // Theme-aware colors
    private Color? _bgColor, _hoverColor, _borderColor;
    public Color BackgroundColor { get => _bgColor ?? Color.FromArgb(240, 20, 20, 30); set => _bgColor = value; }
    public Color HoverColor { get => _hoverColor ?? Color.FromArgb(255, 60, 60, 90); set => _hoverColor = value; }
    public Color BorderColor { get => _borderColor ?? Color.FromArgb(80, 100, 100, 130); set => _borderColor = value; }

    public void AddItem(string label, Action? action = null, Color? color = null, string? shortcut = null)
    {
        _items.Add(new ContextMenuItem { Label = label, Action = action, Color = color, Shortcut = shortcut });
    }

    public void AddSeparator()
    {
        _items.Add(new ContextMenuItem { IsSeparator = true });
    }

    public void ClearItems() => _items.Clear();

    /// <summary>Show at the given screen position.</summary>
    public void Show(System.Numerics.Vector2 position)
    {
        X = position.X;
        Y = position.Y;
        Visible = true;
        _hoveredIndex = -1;
        RecalcSize();
    }

    /// <summary>Hide the menu.</summary>
    public void Hide()
    {
        Visible = false;
        _hoveredIndex = -1;
    }

    private void RecalcSize()
    {
        Width = MenuWidth;
        float h = 4; // top padding
        foreach (var item in _items)
            h += item.IsSeparator ? SeparatorHeight : ItemHeight;
        h += 4; // bottom padding
        Height = h;
    }

    public override void Update(GameInput input, float dt)
    {
        if (!Visible) return;

        _hoveredIndex = -1;

        foreach (var pointer in input.Pointers)
        {
            if (!pointer.ScreenPosition.HasValue) continue;
            var mp = pointer.ScreenPosition.Value;
            var bounds = ScreenBounds;

            bool inMenu = mp.X >= bounds.X && mp.X < bounds.X + bounds.Width &&
                          mp.Y >= bounds.Y && mp.Y < bounds.Y + bounds.Height;

            if (inMenu)
            {
                // Find which item is hovered
                float y = bounds.Y + 4;
                for (int i = 0; i < _items.Count; i++)
                {
                    float h = _items[i].IsSeparator ? SeparatorHeight : ItemHeight;
                    if (!_items[i].IsSeparator && mp.Y >= y && mp.Y < y + h)
                    {
                        _hoveredIndex = i;
                        if (pointer.WasReleased && _items[i].Action != null)
                        {
                            _items[i].Action.Invoke();
                            Hide();
                            return;
                        }
                    }
                    y += h;
                }
            }
            else if (pointer.WasPressed)
            {
                OnDismiss?.Invoke();
                Hide();
                return;
            }
        }

        // Keyboard navigation
        if (input.Keyboard.WasKeyPressed("Escape")) { OnDismiss?.Invoke(); Hide(); }
        if (input.Keyboard.WasKeyPressed("ArrowDown")) MoveSelection(1);
        if (input.Keyboard.WasKeyPressed("ArrowUp")) MoveSelection(-1);
        if (input.Keyboard.WasKeyPressed("Enter") && _hoveredIndex >= 0 && _hoveredIndex < _items.Count)
        {
            _items[_hoveredIndex].Action?.Invoke();
            Hide();
        }
    }

    private void MoveSelection(int dir)
    {
        int start = _hoveredIndex;
        for (int attempt = 0; attempt < _items.Count; attempt++)
        {
            start = (start + dir + _items.Count) % _items.Count;
            if (!_items[start].IsSeparator) { _hoveredIndex = start; return; }
        }
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible || _items.Count == 0) return;

        var bounds = ScreenBounds;

        // Border + background
        renderer.DrawRect(bounds.X - 1, bounds.Y - 1, Width + 2, Height + 2, BorderColor);
        renderer.DrawRect(bounds.X, bounds.Y, Width, Height, BackgroundColor);

        float y = bounds.Y + 4;
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];

            if (item.IsSeparator)
            {
                renderer.DrawRect(bounds.X + 8, y + SeparatorHeight / 2 - 0.5f,
                    Width - 16, 1, Color.FromArgb(40, 255, 255, 255));
                y += SeparatorHeight;
                continue;
            }

            // Hover highlight
            if (i == _hoveredIndex)
                renderer.DrawRect(bounds.X + 2, y, Width - 4, ItemHeight, HoverColor);

            // Label
            Color textColor = item.Color ?? UITheme.Current.TextPrimary;
            float textY = y + (ItemHeight - renderer.GetLineHeight(FontSize.Body)) / 2;
            renderer.DrawText(item.Label, bounds.X + 12, textY, FontSize.Body, textColor);

            // Shortcut hint (right-aligned)
            if (!string.IsNullOrEmpty(item.Shortcut))
            {
                float sw = renderer.MeasureText(item.Shortcut, FontSize.Caption);
                renderer.DrawText(item.Shortcut, bounds.X + Width - sw - 12, textY + 2,
                    FontSize.Caption, UITheme.Current.TextMuted);
            }

            y += ItemHeight;
        }
    }

    private struct ContextMenuItem
    {
        public string Label;
        public Action? Action;
        public Color? Color;
        public string? Shortcut;
        public bool IsSeparator;
    }
}
