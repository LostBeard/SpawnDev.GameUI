using System.Drawing;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Simple color picker with preset swatches and RGB sliders.
/// For character customization, base building colors, UI theming.
/// </summary>
public class UIColorPicker : UIFlexPanel
{
    private Color _selectedColor = Color.White;
    private readonly UISlider _rSlider, _gSlider, _bSlider;
    private readonly UILabel _hexLabel;

    /// <summary>Currently selected color.</summary>
    public Color SelectedColor
    {
        get => _selectedColor;
        set
        {
            _selectedColor = value;
            _rSlider.Value = value.R / 255f;
            _gSlider.Value = value.G / 255f;
            _bSlider.Value = value.B / 255f;
            _hexLabel.Text = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
            OnChanged?.Invoke(value);
        }
    }

    /// <summary>Called when color changes.</summary>
    public Action<Color>? OnChanged { get; set; }

    /// <summary>Preset color swatches.</summary>
    public Color[] Presets { get; set; } = new[]
    {
        Color.White, Color.FromArgb(255, 200, 200, 200), Color.FromArgb(255, 128, 128, 128), Color.FromArgb(255, 64, 64, 64), Color.Black,
        Color.Red, Color.FromArgb(255, 255, 128, 0), Color.Yellow, Color.FromArgb(255, 128, 255, 0), Color.Green,
        Color.FromArgb(255, 0, 255, 128), Color.Cyan, Color.FromArgb(255, 0, 128, 255), Color.Blue, Color.FromArgb(255, 128, 0, 255),
        Color.Magenta, Color.FromArgb(255, 255, 0, 128), Color.FromArgb(255, 139, 90, 43), Color.FromArgb(255, 60, 80, 45), Color.FromArgb(255, 80, 60, 60),
    };

    private const float SwatchSize = 22f;
    private const float SwatchGap = 3f;
    private int _swatchColumns = 5;
    private int _hoveredSwatch = -1;

    public UIColorPicker()
    {
        Direction = FlexDirection.Column;
        Gap = 6;
        Padding = 10;
        Width = 200;
        BackgroundColor = Color.FromArgb(200, 20, 20, 30);

        _rSlider = new UISlider { Label = "R", Width = 180, Height = 34, MinValue = 0, MaxValue = 1, Value = 1,
            FillColor = Color.FromArgb(255, 200, 60, 60) };
        _gSlider = new UISlider { Label = "G", Width = 180, Height = 34, MinValue = 0, MaxValue = 1, Value = 1,
            FillColor = Color.FromArgb(255, 60, 200, 60) };
        _bSlider = new UISlider { Label = "B", Width = 180, Height = 34, MinValue = 0, MaxValue = 1, Value = 1,
            FillColor = Color.FromArgb(255, 60, 60, 200) };
        _hexLabel = new UILabel { Text = "#FFFFFF", FontSize = FontSize.Caption, Color = Color.FromArgb(200, 180, 180, 200) };

        _rSlider.OnChanged = _ => UpdateFromSliders();
        _gSlider.OnChanged = _ => UpdateFromSliders();
        _bSlider.OnChanged = _ => UpdateFromSliders();

        AddChild(_hexLabel);
        AddChild(_rSlider);
        AddChild(_gSlider);
        AddChild(_bSlider);
    }

    private void UpdateFromSliders()
    {
        int r = (int)(_rSlider.Value * 255);
        int g = (int)(_gSlider.Value * 255);
        int b = (int)(_bSlider.Value * 255);
        _selectedColor = Color.FromArgb(255, r, g, b);
        _hexLabel.Text = $"#{r:X2}{g:X2}{b:X2}";
        OnChanged?.Invoke(_selectedColor);
    }

    public override void Update(GameInput input, float dt)
    {
        if (!Visible || !Enabled) { base.Update(input, dt); return; }

        _hoveredSwatch = -1;
        foreach (var pointer in input.Pointers)
        {
            if (!pointer.ScreenPosition.HasValue) continue;
            var mp = pointer.ScreenPosition.Value;
            var bounds = ScreenBounds;

            // Swatch area is at the bottom after the sliders
            float swatchAreaY = bounds.Y + Height - GetSwatchAreaHeight() - Padding;
            float localX = mp.X - bounds.X - Padding;
            float localY = mp.Y - swatchAreaY;

            if (localX >= 0 && localY >= 0)
            {
                int col = (int)(localX / (SwatchSize + SwatchGap));
                int row = (int)(localY / (SwatchSize + SwatchGap));
                int idx = row * _swatchColumns + col;

                if (col >= 0 && col < _swatchColumns && idx >= 0 && idx < Presets.Length)
                {
                    float cellX = localX - col * (SwatchSize + SwatchGap);
                    float cellY = localY - row * (SwatchSize + SwatchGap);
                    if (cellX <= SwatchSize && cellY <= SwatchSize)
                    {
                        _hoveredSwatch = idx;
                        if (pointer.WasReleased)
                            SelectedColor = Presets[idx];
                    }
                }
            }
        }

        base.Update(input, dt);
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        // Calculate total height
        float swatchH = GetSwatchAreaHeight();
        Height = Padding * 2 + 20 + 34 * 3 + Gap * 4 + swatchH + 8; // hex + 3 sliders + gaps + swatches

        var bounds = ScreenBounds;
        renderer.DrawRect(bounds.X, bounds.Y, Width, Height, BackgroundColor);

        // Preview swatch (current color)
        renderer.DrawRect(bounds.X + Padding, bounds.Y + Padding, 30, 18, _selectedColor);
        _hexLabel.X = 40;
        _hexLabel.Y = Padding + 2;

        // Draw children (hex label + sliders)
        _hexLabel.Draw(renderer);

        _rSlider.X = Padding;
        _rSlider.Y = Padding + 22;
        _rSlider.Draw(renderer);

        _gSlider.X = Padding;
        _gSlider.Y = Padding + 22 + 34 + Gap;
        _gSlider.Draw(renderer);

        _bSlider.X = Padding;
        _bSlider.Y = Padding + 22 + 68 + Gap * 2;
        _bSlider.Draw(renderer);

        // Preset swatches
        float swatchY = bounds.Y + Height - swatchH - Padding;
        for (int i = 0; i < Presets.Length; i++)
        {
            int col = i % _swatchColumns;
            int row = i / _swatchColumns;
            float sx = bounds.X + Padding + col * (SwatchSize + SwatchGap);
            float sy = swatchY + row * (SwatchSize + SwatchGap);

            renderer.DrawRect(sx, sy, SwatchSize, SwatchSize, Presets[i]);
            if (i == _hoveredSwatch)
                renderer.DrawRect(sx - 1, sy - 1, SwatchSize + 2, SwatchSize + 2, Color.White);
        }
    }

    private float GetSwatchAreaHeight()
    {
        int rows = (Presets.Length + _swatchColumns - 1) / _swatchColumns;
        return rows * (SwatchSize + SwatchGap) - SwatchGap;
    }
}
