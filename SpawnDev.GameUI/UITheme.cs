using System.Drawing;
using SpawnDev.GameUI.Elements;

namespace SpawnDev.GameUI;

/// <summary>
/// Defines the visual style for all UI elements. Apply a theme to change
/// the look of an entire UI tree without modifying individual elements.
/// Create game-specific themes (DayZ gritty for Lost Spawns, Minecraft bright for AubsCraft).
///
/// Performance: all fields are value types (Color is a struct). No allocations from reading theme values.
/// Designed for dual-use: flat HUD overlays and floating AR/VR panels (silhouette borders, world text outlines).
/// </summary>
public class UITheme
{
    // Panel defaults
    public Color PanelBackground { get; set; } = Color.FromArgb(220, 18, 22, 28);
    public Color PanelBorder { get; set; } = Color.FromArgb(140, 120, 140, 155);
    public float PanelBorderWidth { get; set; } = 1;
    public float PanelPadding { get; set; } = 8;
    public float PanelCornerRadius { get; set; } = 8;

    // Shared control radii
    /// <summary>Default corner radius for checkboxes, text fields, progress tracks.</summary>
    public float ControlCornerRadius { get; set; } = 6;
    /// <summary>Corner radius for buttons.</summary>
    public float ButtonCornerRadius { get; set; } = 6;

    // Button defaults - cool slate + cyan accent (readable on light and dark scene backgrounds)
    public Color ButtonNormal { get; set; } = Color.FromArgb(255, 36, 52, 64);
    public Color ButtonHover { get; set; } = Color.FromArgb(255, 48, 78, 96);
    public Color ButtonPressed { get; set; } = Color.FromArgb(255, 24, 38, 48);
    public Color ButtonDisabled { get; set; } = Color.FromArgb(255, 40, 44, 50);
    public Color ButtonText { get; set; } = Color.FromArgb(255, 236, 244, 248);
    public Color ButtonBorder { get; set; } = Color.FromArgb(200, 70, 190, 210);
    public float ButtonBorderWidth { get; set; } = 1;
    public FontSize ButtonFontSize { get; set; } = FontSize.Body;
    public float ButtonPaddingX { get; set; } = 16;
    public float ButtonPaddingY { get; set; } = 8;

    // Label defaults
    public Color LabelColor { get; set; } = Color.White;
    public FontSize LabelFontSize { get; set; } = FontSize.Body;

    // Slider / progress defaults
    public Color SliderTrack { get; set; } = Color.FromArgb(255, 32, 38, 46);
    public Color SliderFill { get; set; } = Color.FromArgb(255, 56, 176, 196);
    public Color SliderThumb { get; set; } = Color.FromArgb(255, 240, 248, 252);
    public Color SliderLabel { get; set; } = Color.FromArgb(255, 190, 200, 210);

    // Global text
    public Color TextPrimary { get; set; } = Color.FromArgb(255, 240, 244, 248);
    public Color TextSecondary { get; set; } = Color.FromArgb(255, 170, 182, 194);
    public Color TextMuted { get; set; } = Color.FromArgb(255, 110, 120, 132);

    /// <summary>
    /// Global font scale multiplier (default 1.0). Applies to all text rendering.
    /// Use for accessibility: 1.25 = large text, 1.5 = extra large.
    /// SDF rendering makes any scale crisp without blur.
    /// </summary>
    public float FontScale { get; set; } = 1.0f;

    // Focus/selection
    public Color FocusBorder { get; set; } = Color.FromArgb(255, 80, 210, 230);
    public float FocusBorderWidth { get; set; } = 2;

    // Tooltip
    public Color TooltipBackground { get; set; } = Color.FromArgb(230, 16, 20, 26);
    public Color TooltipText { get; set; } = Color.White;
    public Color TooltipBorder { get; set; } = Color.FromArgb(160, 120, 140, 155);

    // Separator
    public Color SeparatorColor { get; set; } = Color.FromArgb(100, 140, 150, 160);

    /// <summary>
    /// SDF outline width applied automatically to text when RenderMode is not ScreenSpace.
    /// Thin dark outline keeps glyphs readable against bright AR passthrough / busy 3D scenes.
    /// 0 = off. Typical: 0.06 thin, 0.12 thick (SDF units).
    /// </summary>
    public float WorldTextOutlineWidth { get; set; } = 0.08f;

    /// <summary>Outline color for world/view/AR text.</summary>
    public Color WorldTextOutlineColor { get; set; } = Color.FromArgb(220, 0, 0, 0);

    /// <summary>The currently active global theme. Set this to change all unthemed elements.</summary>
    public static UITheme Current { get; set; } = new();

    /// <summary>Dark theme - default SpawnDev style (slate + cyan).</summary>
    public static UITheme Dark => new();

    /// <summary>DayZ-inspired gritty theme for Lost Spawns.</summary>
    public static UITheme LostSpawns => new()
    {
        PanelBackground = Color.FromArgb(210, 15, 18, 12),
        PanelBorder = Color.FromArgb(120, 140, 130, 90),
        PanelBorderWidth = 1,
        PanelCornerRadius = 4,
        ControlCornerRadius = 3,
        ButtonCornerRadius = 3,
        ButtonNormal = Color.FromArgb(255, 60, 70, 45),
        ButtonHover = Color.FromArgb(255, 90, 100, 65),
        ButtonPressed = Color.FromArgb(255, 40, 48, 30),
        ButtonText = Color.FromArgb(255, 220, 210, 180),
        ButtonBorder = Color.FromArgb(160, 140, 130, 90),
        ButtonBorderWidth = 1,
        SliderFill = Color.FromArgb(255, 140, 110, 60),
        SliderTrack = Color.FromArgb(255, 28, 32, 22),
        TextPrimary = Color.FromArgb(255, 220, 210, 180),
        TextSecondary = Color.FromArgb(255, 160, 150, 120),
        TextMuted = Color.FromArgb(255, 100, 95, 75),
        FocusBorder = Color.FromArgb(255, 180, 140, 60),
        SeparatorColor = Color.FromArgb(90, 140, 130, 90),
        WorldTextOutlineWidth = 0.1f,
        WorldTextOutlineColor = Color.FromArgb(230, 0, 0, 0),
    };

    /// <summary>Bright blocky theme for AubsCraft.</summary>
    public static UITheme AubsCraft => new()
    {
        PanelBackground = Color.FromArgb(200, 30, 30, 50),
        PanelBorder = Color.FromArgb(120, 100, 180, 255),
        PanelBorderWidth = 2,
        PanelCornerRadius = 0,
        ControlCornerRadius = 0,
        ButtonCornerRadius = 0,
        ButtonNormal = Color.FromArgb(255, 70, 130, 200),
        ButtonHover = Color.FromArgb(255, 100, 160, 230),
        ButtonPressed = Color.FromArgb(255, 45, 100, 170),
        ButtonText = Color.White,
        ButtonBorder = Color.FromArgb(200, 40, 80, 140),
        ButtonBorderWidth = 2,
        SliderFill = Color.FromArgb(255, 80, 200, 120),
        TextPrimary = Color.White,
        TextSecondary = Color.FromArgb(255, 180, 200, 230),
        FocusBorder = Color.FromArgb(255, 100, 200, 255),
        SeparatorColor = Color.FromArgb(100, 100, 180, 255),
        WorldTextOutlineWidth = 0.1f,
    };

    /// <summary>
    /// High contrast theme for accessibility. Maximum readability.
    /// White text on black, bright borders, large contrast ratios.
    /// </summary>
    public static UITheme HighContrast => new()
    {
        PanelBackground = Color.FromArgb(240, 0, 0, 0),
        PanelBorder = Color.FromArgb(255, 255, 255, 255),
        PanelBorderWidth = 2,
        PanelCornerRadius = 4,
        ControlCornerRadius = 4,
        ButtonCornerRadius = 4,
        ButtonNormal = Color.FromArgb(255, 0, 0, 0),
        ButtonHover = Color.FromArgb(255, 50, 50, 50),
        ButtonPressed = Color.FromArgb(255, 90, 90, 90),
        ButtonText = Color.White,
        ButtonBorder = Color.White,
        ButtonBorderWidth = 2,
        SliderTrack = Color.FromArgb(255, 40, 40, 40),
        SliderFill = Color.FromArgb(255, 255, 255, 0),
        SliderThumb = Color.White,
        TextPrimary = Color.White,
        TextSecondary = Color.FromArgb(255, 255, 255, 100),
        TextMuted = Color.FromArgb(255, 200, 200, 200),
        FocusBorder = Color.FromArgb(255, 255, 255, 0),
        FocusBorderWidth = 3,
        SeparatorColor = Color.White,
        TooltipBackground = Color.FromArgb(255, 0, 0, 0),
        TooltipText = Color.White,
        TooltipBorder = Color.White,
        WorldTextOutlineWidth = 0.12f,
        WorldTextOutlineColor = Color.Black,
    };

    /// <summary>
    /// Colorblind-safe theme (Protanopia/Deuteranopia - red-green deficiency).
    /// Avoids red/green distinction. Uses blue/orange/yellow palette.
    /// </summary>
    public static UITheme ColorblindSafe => new()
    {
        PanelBackground = Color.FromArgb(220, 18, 22, 28),
        PanelBorder = Color.FromArgb(120, 200, 200, 200),
        PanelBorderWidth = 1,
        PanelCornerRadius = 8,
        ControlCornerRadius = 6,
        ButtonCornerRadius = 6,
        ButtonNormal = Color.FromArgb(255, 0, 114, 178),
        ButtonHover = Color.FromArgb(255, 40, 144, 208),
        ButtonPressed = Color.FromArgb(255, 0, 90, 150),
        ButtonText = Color.White,
        ButtonBorder = Color.FromArgb(200, 230, 159, 0),
        ButtonBorderWidth = 1,
        SliderTrack = Color.FromArgb(255, 40, 44, 50),
        SliderFill = Color.FromArgb(255, 230, 159, 0),
        SliderThumb = Color.White,
        TextPrimary = Color.White,
        TextSecondary = Color.FromArgb(255, 204, 204, 204),
        TextMuted = Color.FromArgb(255, 153, 153, 153),
        FocusBorder = Color.FromArgb(255, 240, 228, 66),
        FocusBorderWidth = 2,
        SeparatorColor = Color.FromArgb(100, 200, 200, 200),
        TooltipBackground = Color.FromArgb(230, 18, 22, 28),
        TooltipText = Color.White,
        TooltipBorder = Color.FromArgb(200, 200, 200, 200),
        WorldTextOutlineWidth = 0.08f,
    };

    /// <summary>
    /// Tritanopia-safe theme (blue-yellow deficiency).
    /// Avoids blue/yellow distinction. Uses red/cyan/magenta palette.
    /// </summary>
    public static UITheme TritanopiaSafe => new()
    {
        PanelBackground = Color.FromArgb(220, 18, 22, 28),
        PanelBorder = Color.FromArgb(120, 200, 200, 200),
        PanelBorderWidth = 1,
        PanelCornerRadius = 8,
        ControlCornerRadius = 6,
        ButtonCornerRadius = 6,
        ButtonNormal = Color.FromArgb(255, 204, 51, 17),
        ButtonHover = Color.FromArgb(255, 230, 80, 45),
        ButtonPressed = Color.FromArgb(255, 170, 30, 0),
        ButtonText = Color.White,
        ButtonBorder = Color.FromArgb(200, 0, 158, 115),
        ButtonBorderWidth = 1,
        SliderTrack = Color.FromArgb(255, 40, 44, 50),
        SliderFill = Color.FromArgb(255, 0, 158, 115),
        SliderThumb = Color.White,
        TextPrimary = Color.White,
        TextSecondary = Color.FromArgb(255, 204, 204, 204),
        TextMuted = Color.FromArgb(255, 153, 153, 153),
        FocusBorder = Color.FromArgb(255, 213, 94, 0),
        FocusBorderWidth = 2,
        SeparatorColor = Color.FromArgb(100, 200, 200, 200),
        WorldTextOutlineWidth = 0.08f,
    };
}
