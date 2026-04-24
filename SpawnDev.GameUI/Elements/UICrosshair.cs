using System.Drawing;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Center-screen crosshair/targeting reticle.
/// Supports multiple styles: dot, cross, circle, plus custom.
/// Changes color when targeting an interactive object.
///
/// Usage:
///   var crosshair = new UICrosshair { Style = CrosshairStyle.Cross };
///   root.AddAnchored(crosshair, Anchor.Center);
///
///   // When aiming at something interactive:
///   crosshair.IsTargeting = true;
/// </summary>
public class UICrosshair : UIElement
{
    /// <summary>Crosshair visual style.</summary>
    public CrosshairStyle Style { get; set; } = CrosshairStyle.Cross;

    /// <summary>Size of the crosshair in pixels.</summary>
    public float Size { get; set; } = 20f;

    /// <summary>Line thickness.</summary>
    public float Thickness { get; set; } = 2f;

    /// <summary>Gap in the center (for cross style).</summary>
    public float CenterGap { get; set; } = 4f;

    /// <summary>Whether currently aiming at an interactive object.</summary>
    public bool IsTargeting { get; set; }

    /// <summary>Normal color.</summary>
    public Color NormalColor { get; set; } = Color.FromArgb(200, 255, 255, 255);

    /// <summary>Color when targeting an interactive object.</summary>
    public Color TargetColor { get; set; } = Color.FromArgb(255, 100, 220, 100);

    /// <summary>Color when targeting a hostile.</summary>
    public Color HostileColor { get; set; } = Color.FromArgb(255, 220, 60, 60);

    /// <summary>Target type affects color.</summary>
    public CrosshairTarget TargetType { get; set; } = CrosshairTarget.None;

    public UICrosshair()
    {
        Width = 24;
        Height = 24;
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        var bounds = ScreenBounds;
        float cx = bounds.X + bounds.Width / 2;
        float cy = bounds.Y + bounds.Height / 2;
        float half = Size / 2;

        // IsTargeting is kept as a back-compat shortcut: consumers that only
        // toggle the bool promote it to Interactive if they haven't set a
        // TargetType. Explicit TargetType always wins.
        var effective = TargetType == CrosshairTarget.None && IsTargeting
            ? CrosshairTarget.Interactive
            : TargetType;

        Color color = effective switch
        {
            CrosshairTarget.Interactive => TargetColor,
            CrosshairTarget.Hostile => HostileColor,
            _ => NormalColor,
        };

        switch (Style)
        {
            case CrosshairStyle.Dot:
                renderer.DrawRect(cx - Thickness, cy - Thickness, Thickness * 2, Thickness * 2, color);
                break;

            case CrosshairStyle.Cross:
                // Horizontal lines (left and right of center gap)
                renderer.DrawRect(cx - half, cy - Thickness / 2, half - CenterGap, Thickness, color);
                renderer.DrawRect(cx + CenterGap, cy - Thickness / 2, half - CenterGap, Thickness, color);
                // Vertical lines (top and bottom of center gap)
                renderer.DrawRect(cx - Thickness / 2, cy - half, Thickness, half - CenterGap, color);
                renderer.DrawRect(cx - Thickness / 2, cy + CenterGap, Thickness, half - CenterGap, color);
                break;

            case CrosshairStyle.Plus:
                // Full plus without center gap
                renderer.DrawRect(cx - half, cy - Thickness / 2, Size, Thickness, color);
                renderer.DrawRect(cx - Thickness / 2, cy - half, Thickness, Size, color);
                break;

            case CrosshairStyle.Brackets:
                float bracketLen = half * 0.6f;
                float bracketOffset = half;
                // Top-left bracket
                renderer.DrawRect(cx - bracketOffset, cy - bracketOffset, bracketLen, Thickness, color);
                renderer.DrawRect(cx - bracketOffset, cy - bracketOffset, Thickness, bracketLen, color);
                // Top-right bracket
                renderer.DrawRect(cx + bracketOffset - bracketLen, cy - bracketOffset, bracketLen, Thickness, color);
                renderer.DrawRect(cx + bracketOffset - Thickness, cy - bracketOffset, Thickness, bracketLen, color);
                // Bottom-left bracket
                renderer.DrawRect(cx - bracketOffset, cy + bracketOffset - Thickness, bracketLen, Thickness, color);
                renderer.DrawRect(cx - bracketOffset, cy + bracketOffset - bracketLen, Thickness, bracketLen, color);
                // Bottom-right bracket
                renderer.DrawRect(cx + bracketOffset - bracketLen, cy + bracketOffset - Thickness, bracketLen, Thickness, color);
                renderer.DrawRect(cx + bracketOffset - Thickness, cy + bracketOffset - bracketLen, Thickness, bracketLen, color);
                // Center dot
                renderer.DrawRect(cx - 1, cy - 1, 2, 2, color);
                break;
        }
    }
}

/// <summary>Crosshair visual style.</summary>
public enum CrosshairStyle
{
    /// <summary>Single center dot.</summary>
    Dot,
    /// <summary>Four lines with center gap (CS/Valorant style).</summary>
    Cross,
    /// <summary>Full plus sign without gap.</summary>
    Plus,
    /// <summary>Corner brackets with center dot (tactical/military).</summary>
    Brackets,
}

/// <summary>What the crosshair is currently targeting.</summary>
public enum CrosshairTarget
{
    None,
    Interactive,
    Hostile,
}
