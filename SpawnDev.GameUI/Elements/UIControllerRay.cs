using System.Drawing;
using System.Numerics;
using SpawnDev.BlazorJS.JSObjects;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Visual laser pointer ray from a VR controller.
/// Renders as a thin line from the controller origin in the ray direction.
/// Changes color when hovering over a UI element.
/// Optional dot cursor at the intersection point.
///
/// Usage:
///   var ray = new UIControllerRay();
///
///   // Per frame, after input poll:
///   foreach (var pointer in gameInput.Pointers) {
///       if (pointer.Type == PointerType.Controller && pointer.RayOrigin.HasValue) {
///           ray.SetRay(pointer.RayOrigin.Value, pointer.RayDirection.Value);
///           ray.IsHovering = someElementIsHit;
///           ray.HitDistance = hitDist; // or MaxLength if no hit
///       }
///   }
///
///   // Draw in world space:
///   ray.DrawWorld(renderer, viewProjection, encoder, colorTarget, depthTarget);
/// </summary>
public class UIControllerRay
{
    /// <summary>Ray origin in world space.</summary>
    public Vector3 Origin { get; private set; }

    /// <summary>Ray direction (normalized) in world space.</summary>
    public Vector3 Direction { get; private set; }

    /// <summary>Maximum ray length in meters.</summary>
    public float MaxLength { get; set; } = 5f;

    /// <summary>Ray line thickness in world units.</summary>
    public float Thickness { get; set; } = 0.002f;

    /// <summary>Whether the ray is currently hitting a UI element.</summary>
    public bool IsHovering { get; set; }

    /// <summary>Distance to the hit point (used to draw the cursor dot).</summary>
    public float HitDistance { get; set; } = 5f;

    /// <summary>Cursor dot size at the intersection point.</summary>
    public float CursorSize { get; set; } = 0.008f;

    /// <summary>Normal color (no hit).</summary>
    public Color NormalColor { get; set; } = Color.FromArgb(180, 150, 200, 255);

    /// <summary>Color when hovering over a UI element.</summary>
    public Color HoverColor { get; set; } = Color.FromArgb(220, 100, 255, 150);

    /// <summary>Cursor dot color.</summary>
    public Color CursorColor { get; set; } = Color.White;

    /// <summary>Whether to show the ray. Set false when controller is not tracked.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Set the ray origin and direction from controller tracking data.</summary>
    public void SetRay(Vector3 origin, Vector3 direction)
    {
        Origin = origin;
        Direction = Vector3.Normalize(direction);
    }

    /// <summary>
    /// Draw the laser ray and cursor in world space.
    /// Uses the renderer's world-space pipeline.
    /// </summary>
    public void DrawWorld(UIRenderer renderer, Matrix4x4 viewProjection,
        GPUCommandEncoder encoder, GPUTextureView colorTarget, GPUTextureView depthTarget)
    {
        if (!Visible) return;

        Color rayColor = IsHovering ? HoverColor : NormalColor;
        float length = IsHovering ? HitDistance : MaxLength;
        var endPoint = Origin + Direction * length;

        // Build a thin quad along the ray direction (billboard toward camera)
        // For now, draw as a thin world-space line using the MVP pipeline
        // The line is a quad with width = Thickness, oriented along the ray

        // Compute right vector perpendicular to ray (use world up as reference)
        var up = Vector3.UnitY;
        var right = Vector3.Normalize(Vector3.Cross(Direction, up));
        if (right.Length() < 0.01f)
            right = Vector3.Normalize(Vector3.Cross(Direction, Vector3.UnitX));
        right *= Thickness;

        // Four corners of the ray quad
        var p0 = Origin - right;
        var p1 = Origin + right;
        var p2 = endPoint - right;
        var p3 = endPoint + right;

        float r = rayColor.R / 255f, g = rayColor.G / 255f, b = rayColor.B / 255f, a = rayColor.A / 255f;

        // Use identity MVP (positions are already in world space)
        // Actually we need VP only since positions are world-space
        // Emit raw world-space quads and flush with VP matrix (model = identity)
        renderer.DrawWorldRayQuad(p0, p1, p2, p3, r, g, b, a);

        // Cursor dot at hit point (small quad facing camera)
        if (IsHovering)
        {
            var hitPoint = Origin + Direction * HitDistance;
            var cursorRight = right * (CursorSize / Thickness);
            var cursorUp = Vector3.Normalize(Vector3.Cross(right, Direction)) * CursorSize;

            var c0 = hitPoint - cursorRight - cursorUp;
            var c1 = hitPoint + cursorRight - cursorUp;
            var c2 = hitPoint - cursorRight + cursorUp;
            var c3 = hitPoint + cursorRight + cursorUp;

            float cr = CursorColor.R / 255f, cg = CursorColor.G / 255f, cb = CursorColor.B / 255f, ca = CursorColor.A / 255f;
            renderer.DrawWorldRayQuad(c0, c1, c2, c3, cr, cg, cb, ca);
        }

        // Flush with identity model (world-space positions) * VP
        renderer.EndWorldSpace(encoder, colorTarget, depthTarget, viewProjection);
    }
}
