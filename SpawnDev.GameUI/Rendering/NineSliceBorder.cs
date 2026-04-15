namespace SpawnDev.GameUI.Rendering;

/// <summary>
/// Border insets for nine-slice rendering.
/// Defines how much of each edge is a fixed-size border (does not stretch).
/// Used for both screen-space pixel insets and texture-space pixel insets.
///
/// Example for a 64x64 panel texture with 12px borders:
///   new NineSliceBorder(12, 12, 12, 12)
///
/// The corners (12x12) are drawn at fixed size.
/// The edges stretch in one direction.
/// The center stretches in both directions.
/// </summary>
public struct NineSliceBorder
{
    /// <summary>Left border width in pixels.</summary>
    public float Left;

    /// <summary>Top border height in pixels.</summary>
    public float Top;

    /// <summary>Right border width in pixels.</summary>
    public float Right;

    /// <summary>Bottom border height in pixels.</summary>
    public float Bottom;

    public NineSliceBorder(float left, float top, float right, float bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    /// <summary>Create a uniform border (same size on all sides).</summary>
    public NineSliceBorder(float all) : this(all, all, all, all) { }

    /// <summary>Create a border with horizontal and vertical sizes.</summary>
    public NineSliceBorder(float horizontal, float vertical) : this(horizontal, vertical, horizontal, vertical) { }

    /// <summary>Zero border (no slicing).</summary>
    public static readonly NineSliceBorder Zero = new(0, 0, 0, 0);
}
