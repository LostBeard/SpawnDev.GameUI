using System.Drawing;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Dropdown select box. Shows the selected value, expands on click to show options.
/// Supports keyboard navigation (up/down arrows, Enter to select, Escape to close).
/// </summary>
public class UIDropdown : UIElement
{
    private readonly List<string> _options = new();
    private int _selectedIndex = -1;
    private int _hoveredOptionIndex = -1;
    private bool _isOpen;

    /// <summary>Font size for the dropdown text.</summary>
    public FontSize FontSize { get; set; } = FontSize.Body;

    /// <summary>Placeholder text when nothing is selected.</summary>
    public string Placeholder { get; set; } = "Select...";

    /// <summary>Height of each option row when expanded.</summary>
    public float OptionHeight { get; set; } = 28f;

    /// <summary>Maximum visible options before scrolling (0 = show all).</summary>
    public int MaxVisibleOptions { get; set; } = 8;

    /// <summary>Called when selection changes.</summary>
    public Action<int, string>? OnChanged { get; set; }

    // Theme-aware colors
    private Color? _bgColor, _textColor, _hoverColor, _borderColor, _optionBgColor;
    public Color BackgroundColor { get => _bgColor ?? UITheme.Current.PanelBackground; set => _bgColor = value; }
    public Color TextColor { get => _textColor ?? UITheme.Current.TextPrimary; set => _textColor = value; }
    public Color HoverColor { get => _hoverColor ?? Color.FromArgb(60, 108, 92, 231); set => _hoverColor = value; }
    public Color BorderColor { get => _borderColor ?? UITheme.Current.PanelBorder; set => _borderColor = value; }
    public Color OptionBgColor { get => _optionBgColor ?? Color.FromArgb(230, 25, 25, 35); set => _optionBgColor = value; }

    public int SelectedIndex => _selectedIndex;
    public string? SelectedValue => _selectedIndex >= 0 && _selectedIndex < _options.Count ? _options[_selectedIndex] : null;
    public bool IsOpen => _isOpen;

    public void AddOption(string text) => _options.Add(text);
    public void ClearOptions() { _options.Clear(); _selectedIndex = -1; _isOpen = false; }
    public void SetOptions(IEnumerable<string> options) { _options.Clear(); _options.AddRange(options); _selectedIndex = -1; }

    /// <summary>Set selected by index.</summary>
    public void Select(int index)
    {
        if (index >= 0 && index < _options.Count)
        {
            _selectedIndex = index;
            OnChanged?.Invoke(index, _options[index]);
        }
    }

    public override void Update(GameInput input, float dt)
    {
        if (!Visible || !Enabled) return;

        bool clickedInside = false;
        bool clickedOption = false;

        foreach (var pointer in input.Pointers)
        {
            if (!pointer.ScreenPosition.HasValue) continue;
            var mp = pointer.ScreenPosition.Value;
            var bounds = ScreenBounds;

            // Check click on main box
            bool inMainBox = mp.X >= bounds.X && mp.X < bounds.X + bounds.Width &&
                             mp.Y >= bounds.Y && mp.Y < bounds.Y + bounds.Height;

            if (inMainBox && pointer.WasReleased)
            {
                _isOpen = !_isOpen;
                clickedInside = true;
            }

            // Check hover/click on options when open
            if (_isOpen)
            {
                float optionsY = bounds.Y + bounds.Height + 2;
                int visibleCount = MaxVisibleOptions > 0 ? Math.Min(_options.Count, MaxVisibleOptions) : _options.Count;

                _hoveredOptionIndex = -1;
                for (int i = 0; i < visibleCount; i++)
                {
                    float oy = optionsY + i * OptionHeight;
                    if (mp.X >= bounds.X && mp.X < bounds.X + bounds.Width &&
                        mp.Y >= oy && mp.Y < oy + OptionHeight)
                    {
                        _hoveredOptionIndex = i;
                        if (pointer.WasReleased)
                        {
                            Select(i);
                            _isOpen = false;
                            clickedOption = true;
                        }
                    }
                }

                // Click outside closes
                if (pointer.WasReleased && !inMainBox && !clickedOption)
                    _isOpen = false;
            }
        }

        // Keyboard navigation when open
        if (_isOpen)
        {
            if (input.Keyboard.WasKeyPressed("ArrowDown"))
                _hoveredOptionIndex = Math.Min(_hoveredOptionIndex + 1, _options.Count - 1);
            if (input.Keyboard.WasKeyPressed("ArrowUp"))
                _hoveredOptionIndex = Math.Max(_hoveredOptionIndex - 1, 0);
            if (input.Keyboard.WasKeyPressed("Enter") && _hoveredOptionIndex >= 0)
            {
                Select(_hoveredOptionIndex);
                _isOpen = false;
            }
            if (input.Keyboard.WasKeyPressed("Escape"))
                _isOpen = false;
        }

        base.Update(input, dt);
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        var bounds = ScreenBounds;

        // Main box border + background
        renderer.DrawRect(bounds.X - 1, bounds.Y - 1, bounds.Width + 2, bounds.Height + 2, BorderColor);
        renderer.DrawRect(bounds.X, bounds.Y, bounds.Width, bounds.Height, BackgroundColor);

        // Selected text or placeholder
        float textY = bounds.Y + (bounds.Height - renderer.GetLineHeight(FontSize)) / 2f;
        string displayText = SelectedValue ?? Placeholder;
        Color displayColor = SelectedValue != null ? TextColor : UITheme.Current.TextMuted;
        renderer.DrawText(displayText, bounds.X + 8, textY, FontSize, displayColor);

        // Arrow indicator
        renderer.DrawText(_isOpen ? "^" : "v", bounds.X + bounds.Width - 18, textY, FontSize, UITheme.Current.TextMuted);

        // Options dropdown
        if (_isOpen && _options.Count > 0)
        {
            float optionsY = bounds.Y + bounds.Height + 2;
            int visibleCount = MaxVisibleOptions > 0 ? Math.Min(_options.Count, MaxVisibleOptions) : _options.Count;
            float totalH = visibleCount * OptionHeight;

            // Options background
            renderer.DrawRect(bounds.X - 1, optionsY - 1, bounds.Width + 2, totalH + 2, BorderColor);
            renderer.DrawRect(bounds.X, optionsY, bounds.Width, totalH, OptionBgColor);

            for (int i = 0; i < visibleCount; i++)
            {
                float oy = optionsY + i * OptionHeight;

                // Hover highlight
                if (i == _hoveredOptionIndex)
                    renderer.DrawRect(bounds.X, oy, bounds.Width, OptionHeight, HoverColor);

                // Selected indicator
                if (i == _selectedIndex)
                    renderer.DrawRect(bounds.X, oy, 3, OptionHeight, UITheme.Current.FocusBorder);

                float optTextY = oy + (OptionHeight - renderer.GetLineHeight(FontSize)) / 2f;
                renderer.DrawText(_options[i], bounds.X + 10, optTextY, FontSize, TextColor);
            }
        }
    }
}
