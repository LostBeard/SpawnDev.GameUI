using System.Drawing;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Panel that positions children relative to its edges using anchor points.
/// Each child can be pinned to any corner or edge with offsets.
/// Perfect for HUD layouts:
///   - Health bar: anchor bottom-left with margin
///   - Minimap: anchor top-right
///   - Chat: anchor bottom-center
///   - Crosshair: anchor center
///   - Hotbar: anchor bottom-center
///
/// Usage:
///   var hud = new UIAnchorPanel { Width = viewportW, Height = viewportH };
///   hud.AddAnchored(healthBar, Anchor.BottomLeft, offsetX: 20, offsetY: -20);
///   hud.AddAnchored(minimap, Anchor.TopRight, offsetX: -20, offsetY: 20);
///   hud.AddAnchored(crosshair, Anchor.Center);
/// </summary>
public class UIAnchorPanel : UIElement
{
    private readonly List<AnchoredChild> _anchoredChildren = new();

    /// <summary>
    /// Add a child with anchor positioning.
    /// The child's X/Y will be computed from the anchor point and offsets during Draw.
    /// </summary>
    public T AddAnchored<T>(T child, Anchor anchor, float offsetX = 0, float offsetY = 0) where T : UIElement
    {
        AddChild(child);
        _anchoredChildren.Add(new AnchoredChild { Element = child, Anchor = anchor, OffsetX = offsetX, OffsetY = offsetY });
        return child;
    }

    /// <summary>Remove an anchored child.</summary>
    public void RemoveAnchored(UIElement child)
    {
        RemoveChild(child);
        _anchoredChildren.RemoveAll(a => a.Element == child);
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        // Position each anchored child before drawing
        foreach (var ac in _anchoredChildren)
        {
            var child = ac.Element;
            if (!child.Visible) continue;

            (child.X, child.Y) = ac.Anchor switch
            {
                Anchor.TopLeft => (ac.OffsetX, ac.OffsetY),
                Anchor.TopCenter => ((Width - child.Width) / 2 + ac.OffsetX, ac.OffsetY),
                Anchor.TopRight => (Width - child.Width + ac.OffsetX, ac.OffsetY),
                Anchor.CenterLeft => (ac.OffsetX, (Height - child.Height) / 2 + ac.OffsetY),
                Anchor.Center => ((Width - child.Width) / 2 + ac.OffsetX, (Height - child.Height) / 2 + ac.OffsetY),
                Anchor.CenterRight => (Width - child.Width + ac.OffsetX, (Height - child.Height) / 2 + ac.OffsetY),
                Anchor.BottomLeft => (ac.OffsetX, Height - child.Height + ac.OffsetY),
                Anchor.BottomCenter => ((Width - child.Width) / 2 + ac.OffsetX, Height - child.Height + ac.OffsetY),
                Anchor.BottomRight => (Width - child.Width + ac.OffsetX, Height - child.Height + ac.OffsetY),
                _ => (ac.OffsetX, ac.OffsetY),
            };
        }

        // Draw children (no background for anchor panel by default)
        var snapshot = Children.ToArray();
        foreach (var child in snapshot)
            child.Draw(renderer);
    }

    private class AnchoredChild
    {
        public UIElement Element = null!;
        public Anchor Anchor;
        public float OffsetX;
        public float OffsetY;
    }
}

/// <summary>Anchor point within a parent panel.</summary>
public enum Anchor
{
    TopLeft, TopCenter, TopRight,
    CenterLeft, Center, CenterRight,
    BottomLeft, BottomCenter, BottomRight,
}
