using System.Drawing;
using System.Numerics;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI;

/// <summary>
/// Base class for all GPU-rendered UI elements.
/// Retained-mode tree: elements have position, size, children, and support hit testing.
/// Coordinates are relative to parent (absolute for root).
/// Supports four rendering modes: screen-space 2D, world-space 3D,
/// view-anchored (VR HUD), and world-anchored AR.
/// </summary>
public class UIElement
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public bool Visible { get; set; } = true;
    public bool Enabled { get; set; } = true;

    /// <summary>Opacity 0-1. Used by animation system. Elements should multiply their colors by this.</summary>
    public float Opacity { get; set; } = 1f;

    /// <summary>External margin (space outside the element). Used by layout panels.</summary>
    public float MarginTop { get; set; }
    public float MarginBottom { get; set; }
    public float MarginLeft { get; set; }
    public float MarginRight { get; set; }

    /// <summary>Set all margins at once.</summary>
    public float Margin { set { MarginTop = MarginBottom = MarginLeft = MarginRight = value; } }

    public UIElement? Parent { get; private set; }
    public List<UIElement> Children { get; } = new();

    /// <summary>
    /// How this element is positioned and rendered.
    /// Screen-space is the default for traditional 2D HUD overlay.
    /// </summary>
    public UIRenderMode RenderMode { get; set; } = UIRenderMode.ScreenSpace;

    /// <summary>
    /// World-space transform for 3D rendering modes.
    /// Used when RenderMode is WorldSpace, ViewAnchored, or WorldAnchored.
    /// </summary>
    public Matrix4x4 WorldTransform { get; set; } = Matrix4x4.Identity;

    /// <summary>Absolute screen-space bounds (computed from parent chain). For ScreenSpace mode.</summary>
    public RectangleF ScreenBounds
    {
        get
        {
            float ax = X, ay = Y;
            var p = Parent;
            while (p != null)
            {
                ax += p.X;
                ay += p.Y;
                p = p.Parent;
            }
            return new RectangleF(ax, ay, Width, Height);
        }
    }

    /// <summary>Add a child element.</summary>
    public T AddChild<T>(T child) where T : UIElement
    {
        child.Parent = this;
        Children.Add(child);
        return child;
    }

    /// <summary>Remove a child element.</summary>
    public void RemoveChild(UIElement child)
    {
        child.Parent = null;
        Children.Remove(child);
    }

    /// <summary>Remove all children.</summary>
    public void ClearChildren()
    {
        foreach (var child in Children)
            child.Parent = null;
        Children.Clear();
    }

    /// <summary>
    /// 2D hit test: find the deepest visible+enabled element at the given screen position.
    /// For screen-space UI. Returns null if no element is hit.
    /// </summary>
    public UIElement? HitTest(Vector2 screenPos)
    {
        if (!Visible || !Enabled) return null;

        // Check children in reverse order (front-to-back, last child is on top)
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            var hit = Children[i].HitTest(screenPos);
            if (hit != null) return hit;
        }

        // Check self
        var bounds = ScreenBounds;
        if (screenPos.X >= bounds.X && screenPos.X < bounds.X + bounds.Width &&
            screenPos.Y >= bounds.Y && screenPos.Y < bounds.Y + bounds.Height)
        {
            return this;
        }

        return null;
    }

    /// <summary>
    /// 3D ray hit test: find the deepest visible+enabled element hit by a ray.
    /// For VR controller/gaze raycasting against world-space UI panels.
    /// Returns null if no element is hit. Distance is set to the hit distance.
    /// </summary>
    public UIElement? HitTestRay(Vector3 rayOrigin, Vector3 rayDirection, out float distance)
    {
        distance = float.MaxValue;
        if (!Visible || !Enabled) return null;

        // Check children in reverse order
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            var hit = Children[i].HitTestRay(rayOrigin, rayDirection, out float childDist);
            if (hit != null && childDist < distance)
            {
                distance = childDist;
                return hit;
            }
        }

        // Check self - ray vs plane intersection for world-space panels
        if (RenderMode != UIRenderMode.ScreenSpace)
        {
            if (RayIntersectsPanel(rayOrigin, rayDirection, out float dist))
            {
                distance = dist;
                return this;
            }
        }

        return null;
    }

    /// <summary>Update element state from input. Override in subclasses for interactive behavior.</summary>
    public virtual void Update(GameInput input, float dt)
    {
        if (!Visible || !Enabled) return;
        // Snapshot children to avoid InvalidOperationException if OnClick modifies the tree
        var snapshot = Children.ToArray();
        foreach (var child in snapshot)
            child.Update(input, dt);
    }

    /// <summary>Draw this element and its children. Override in subclasses for custom rendering.</summary>
    public virtual void Draw(UIRenderer renderer)
    {
        if (!Visible) return;
        // Snapshot for same reason as Update
        var snapshot = Children.ToArray();
        foreach (var child in snapshot)
            child.Draw(renderer);
    }

    /// <summary>
    /// Tests if a ray intersects this element's world-space panel.
    /// The panel is defined by WorldTransform, Width, and Height.
    /// </summary>
    protected bool RayIntersectsPanel(Vector3 rayOrigin, Vector3 rayDirection, out float distance)
    {
        distance = float.MaxValue;

        // Panel normal is the Z axis of the world transform
        var normal = new Vector3(WorldTransform.M31, WorldTransform.M32, WorldTransform.M33);
        var panelPos = new Vector3(WorldTransform.M41, WorldTransform.M42, WorldTransform.M43);

        float denom = Vector3.Dot(normal, rayDirection);
        if (MathF.Abs(denom) < 1e-6f) return false; // parallel

        float t = Vector3.Dot(panelPos - rayOrigin, normal) / denom;
        if (t < 0) return false; // behind ray

        // Hit point in world space
        var hitWorld = rayOrigin + rayDirection * t;

        // Transform to panel local space to check bounds
        if (!Matrix4x4.Invert(WorldTransform, out var invTransform)) return false;
        var hitLocal = Vector3.Transform(hitWorld, invTransform);

        // Check if within panel bounds (local XY, origin at center)
        if (hitLocal.X >= -Width / 2 && hitLocal.X <= Width / 2 &&
            hitLocal.Y >= -Height / 2 && hitLocal.Y <= Height / 2)
        {
            distance = t;
            return true;
        }

        return false;
    }
}

/// <summary>
/// How a UI element is positioned and rendered.
/// </summary>
public enum UIRenderMode
{
    /// <summary>Screen-space 2D overlay. PC/tablet HUD. Coordinates in screen pixels.</summary>
    ScreenSpace,

    /// <summary>World-space 3D panel. VR floating menus. Uses WorldTransform for positioning.</summary>
    WorldSpace,

    /// <summary>View-anchored. VR HUD that moves with the head, fixed distance. Uses WorldTransform offset from camera.</summary>
    ViewAnchored,

    /// <summary>World-anchored AR. Labels/guides pinned to real-world positions via XRAnchor.</summary>
    WorldAnchored
}
