using System.Drawing;
using SpawnDev.GameUI.Elements;

namespace SpawnDev.GameUI;

/// <summary>
/// Defines the visual style for all UI elements. Apply a theme to change
/// the look of an entire UI tree without modifying individual elements.
/// Create game-specific themes (DayZ gritty for Lost Spawns, Minecraft bright for AubsCraft).
///
/// Performance: all fields are value types (Color is a struct). No allocations from reading theme values.
/// </summary>
public class UITheme
{
    // Panel defaults
    public Color PanelBackground { get; set; } = Color.FromArgb(200, 20, 20, 30);
    public Color PanelBorder { get; set; } = Color.FromArgb(60, 255, 255, 255);
    public float PanelBorderWidth { get; set; } = 0;
    public float PanelPadding { get; set; } = 8;
    public float PanelCornerRadius { get; set; } = 0;

    // Button defaults
    public Color ButtonNormal { get; set; } = Color.FromArgb(255, 108, 92, 231);
    public Color ButtonHover { get; set; } = Color.FromArgb(255, 129, 116, 236);
    public Color ButtonPressed { get; set; } = Color.FromArgb(255, 86, 72, 200);
    public Color ButtonDisabled { get; set; } = Color.FromArgb(255, 60, 60, 70);
    public Color ButtonText { get; set; } = Color.White;
    public FontSize ButtonFontSize { get; set; } = FontSize.Body;
    public float ButtonPaddingX { get; set; } = 16;
    public float ButtonPaddingY { get; set; } = 8;

    // Label defaults
    public Color LabelColor { get; set; } = Color.White;
    public FontSize LabelFontSize { get; set; } = FontSize.Body;

    // Slider defaults
    public Color SliderTrack { get; set; } = Color.FromArgb(255, 50, 50, 65);
    public Color SliderFill { get; set; } = Color.FromArgb(255, 108, 92, 231);
    public Color SliderThumb { get; set; } = Color.White;
    public Color SliderLabel { get; set; } = Color.FromArgb(255, 200, 200, 220);

    // Global text
    public Color TextPrimary { get; set; } = Color.White;
    public Color TextSecondary { get; set; } = Color.FromArgb(255, 180, 180, 200);
    public Color TextMuted { get; set; } = Color.FromArgb(255, 120, 120, 140);

    // Focus/selection
    public Color FocusBorder { get; set; } = Color.FromArgb(255, 108, 92, 231);
    public float FocusBorderWidth { get; set; } = 2;

    // Tooltip
    public Color TooltipBackground { get; set; } = Color.FromArgb(230, 30, 30, 40);
    public Color TooltipText { get; set; } = Color.White;
    public Color TooltipBorder { get; set; } = Color.FromArgb(100, 255, 255, 255);

    /// <summary>The currently active global theme. Set this to change all unthemed elements.</summary>
    public static UITheme Current { get; set; } = new();

    /// <summary>Dark theme - default SpawnDev style.</summary>
    public static UITheme Dark => new();

    /// <summary>DayZ-inspired gritty theme for Lost Spawns.</summary>
    public static UITheme LostSpawns => new()
    {
        PanelBackground = Color.FromArgb(210, 15, 18, 12),      // dark olive
        PanelBorder = Color.FromArgb(80, 140, 130, 90),         // muted khaki
        ButtonNormal = Color.FromArgb(255, 60, 70, 45),         // military green
        ButtonHover = Color.FromArgb(255, 75, 85, 55),
        ButtonPressed = Color.FromArgb(255, 45, 55, 35),
        ButtonText = Color.FromArgb(255, 220, 210, 180),        // warm off-white
        SliderFill = Color.FromArgb(255, 140, 110, 60),         // amber
        TextPrimary = Color.FromArgb(255, 220, 210, 180),
        TextSecondary = Color.FromArgb(255, 160, 150, 120),
        TextMuted = Color.FromArgb(255, 100, 95, 75),
        FocusBorder = Color.FromArgb(255, 180, 140, 60),
    };

    /// <summary>Bright blocky theme for AubsCraft.</summary>
    public static UITheme AubsCraft => new()
    {
        PanelBackground = Color.FromArgb(200, 30, 30, 50),
        PanelBorder = Color.FromArgb(80, 100, 180, 255),        // soft blue
        ButtonNormal = Color.FromArgb(255, 70, 130, 200),       // Minecraft-ish blue
        ButtonHover = Color.FromArgb(255, 90, 150, 220),
        ButtonPressed = Color.FromArgb(255, 50, 110, 180),
        ButtonText = Color.White,
        SliderFill = Color.FromArgb(255, 80, 200, 120),         // green
        TextPrimary = Color.White,
        TextSecondary = Color.FromArgb(255, 180, 200, 230),
        FocusBorder = Color.FromArgb(255, 100, 200, 255),
    };
}
