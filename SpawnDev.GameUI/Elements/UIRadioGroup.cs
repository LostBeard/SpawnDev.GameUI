using System.Drawing;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Mutually exclusive option group. Only one option can be selected at a time.
/// Renders as vertical list of radio buttons with labels.
/// </summary>
public class UIRadioGroup : UIFlexPanel
{
    private readonly List<string> _options = new();
    private int _selectedIndex = -1;

    /// <summary>Called when selection changes.</summary>
    public Action<int, string>? OnChanged { get; set; }

    /// <summary>Font size for option labels.</summary>
    public FontSize ItemFontSize { get; set; } = FontSize.Body;

    /// <summary>Currently selected index. -1 = none.</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex != value && value >= -1 && value < _options.Count)
            {
                _selectedIndex = value;
                if (value >= 0) OnChanged?.Invoke(value, _options[value]);
            }
        }
    }

    /// <summary>Selected value, or null.</summary>
    public string? SelectedValue => _selectedIndex >= 0 && _selectedIndex < _options.Count ? _options[_selectedIndex] : null;

    // Colors
    private Color? _dotColor, _textColor;
    public Color DotColor { get => _dotColor ?? UITheme.Current.ButtonNormal; set => _dotColor = value; }
    public Color TextColor { get => _textColor ?? UITheme.Current.TextPrimary; set => _textColor = value; }

    private const float CircleSize = 16f;
    private const float CircleGap = 8f;
    private const float ItemHeight = 26f;

    public UIRadioGroup()
    {
        Direction = FlexDirection.Column;
        Gap = 2;
    }

    /// <summary>Add an option.</summary>
    public void AddOption(string text)
    {
        _options.Add(text);
        if (_selectedIndex == -1) _selectedIndex = 0;
    }

    /// <summary>Set all options at once.</summary>
    public void SetOptions(params string[] options)
    {
        _options.Clear();
        _options.AddRange(options);
        _selectedIndex = _options.Count > 0 ? 0 : -1;
    }

    public override void Update(GameInput input, float dt)
    {
        if (!Visible || !Enabled) return;

        foreach (var pointer in input.Pointers)
        {
            if (!pointer.ScreenPosition.HasValue || !pointer.WasReleased) continue;
            var mp = pointer.ScreenPosition.Value;
            var bounds = ScreenBounds;

            for (int i = 0; i < _options.Count; i++)
            {
                float itemY = bounds.Y + Padding + i * ItemHeight;
                if (mp.X >= bounds.X && mp.X < bounds.X + bounds.Width &&
                    mp.Y >= itemY && mp.Y < itemY + ItemHeight)
                {
                    SelectedIndex = i;
                    break;
                }
            }
        }

        base.Update(input, dt);
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        // Auto-size
        Height = Padding * 2 + _options.Count * ItemHeight;

        // Draw panel background
        var bounds = ScreenBounds;
        if (BackgroundColor.A > 0)
            renderer.DrawRect(bounds.X, bounds.Y, bounds.Width, bounds.Height, BackgroundColor);

        // Draw each option
        for (int i = 0; i < _options.Count; i++)
        {
            float itemY = bounds.Y + Padding + i * ItemHeight;
            float cx = bounds.X + Padding + CircleSize / 2;
            float cy = itemY + ItemHeight / 2;

            // Outer circle (approximated as square for now - proper circles need SDF or more quads)
            renderer.DrawRect(cx - CircleSize / 2, cy - CircleSize / 2,
                CircleSize, CircleSize,
                Color.FromArgb(180, 80, 80, 90));

            // Inner dot if selected
            if (i == _selectedIndex)
            {
                float dotSize = CircleSize - 6;
                renderer.DrawRect(cx - dotSize / 2, cy - dotSize / 2, dotSize, dotSize, DotColor);
            }

            // Label
            float textX = bounds.X + Padding + CircleSize + CircleGap;
            float textY = itemY + (ItemHeight - renderer.GetLineHeight(ItemFontSize)) / 2;
            renderer.DrawText(_options[i], textX, textY, ItemFontSize, TextColor);
        }
    }
}
