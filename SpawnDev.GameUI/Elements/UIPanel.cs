using SpawnDev.BlazorJS.JSObjects;
using SpawnDev.GameUI.Rendering;
using System.Drawing;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Container panel with a solid background color or nine-slice textured background.
/// Children are positioned relative to this panel's top-left corner.
///
/// Nine-slice rendering: set BackgroundTexture, TextureSize, and TextureBorder
/// to render a decorative panel frame that scales without distorting corners.
/// </summary>
public class UIPanel : UIElement
{
    // Theme-aware defaults - read from UITheme.Current, overridable per-panel
    private Color? _backgroundColor, _borderColor;
    private float? _borderWidth, _padding, _cornerRadius;
    public Color BackgroundColor { get => _backgroundColor ?? UITheme.Current.PanelBackground; set => _backgroundColor = value; }
    public Color BorderColor { get => _borderColor ?? UITheme.Current.PanelBorder; set => _borderColor = value; }
    public float BorderWidth { get => _borderWidth ?? UITheme.Current.PanelBorderWidth; set => _borderWidth = value; }
    public float Padding { get => _padding ?? UITheme.Current.PanelPadding; set => _padding = value; }

    /// <summary>Corner radius for rounded panels. 0 = sharp corners.</summary>
    public float CornerRadius { get => _cornerRadius ?? UITheme.Current.PanelCornerRadius; set => _cornerRadius = value; }

    // Nine-slice background texture
    /// <summary>
    /// Optional background texture for nine-slice rendering.
    /// When set, the panel background is rendered as a nine-slice textured frame
    /// instead of a solid color rectangle.
    /// </summary>
    public GPUTextureView? BackgroundTexture { get; set; }

    /// <summary>
    /// Source texture dimensions in pixels. Required for UV calculation when using nine-slice.
    /// </summary>
    public (int Width, int Height) TextureSize { get; set; }

    /// <summary>
    /// Border insets in texture pixels - defines where the source texture is sliced.
    /// The nine regions: corners (fixed), edges (stretch 1D), center (stretch 2D).
    /// </summary>
    public NineSliceBorder TextureBorder { get; set; }

    /// <summary>
    /// Screen-space border insets. Defaults to TextureBorder values if not set.
    /// Controls how large the fixed-size corners and edges appear on screen.
    /// </summary>
    public NineSliceBorder? ScreenBorder { get; set; }

    /// <summary>Tint color for the nine-slice texture. White = no tint.</summary>
    public Color TextureTint { get; set; } = Color.White;

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        var bounds = ScreenBounds;

        // Nine-slice textured background
        if (BackgroundTexture != null && TextureSize.Width > 0 && TextureSize.Height > 0)
        {
            var screenBorder = ScreenBorder ?? TextureBorder;
            renderer.DrawNineSlice(BackgroundTexture, bounds.X, bounds.Y, bounds.Width, bounds.Height,
                screenBorder, TextureSize.Width, TextureSize.Height, TextureBorder, TextureTint);
        }
        else
        {
            // Solid color background
            // Border (drawn as a slightly larger rect behind the background)
            if (BorderWidth > 0)
            {
                renderer.DrawRect(bounds.X - BorderWidth, bounds.Y - BorderWidth,
                                  bounds.Width + BorderWidth * 2, bounds.Height + BorderWidth * 2,
                                  BorderColor);
            }

            renderer.DrawRect(bounds.X, bounds.Y, bounds.Width, bounds.Height, BackgroundColor);
        }

        // Children
        base.Draw(renderer);
    }
}
