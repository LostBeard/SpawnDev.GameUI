using System.Drawing;
using System.Numerics;
using SpawnDev.BlazorJS.JSObjects;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// A UI panel that renders in 3D world space using the MVP pipeline.
/// Place it anywhere in the scene with a Matrix4x4 transform.
/// Children are drawn on the panel surface using panel-local coordinates.
///
/// Supports billboarding (always face the camera) and view-anchored mode
/// (fixed distance from camera, moves with head).
///
/// Usage:
///   var vrMenu = new UIWorldPanel {
///       PanelWidth = 400, PanelHeight = 300,
///       WorldTransform = Matrix4x4.CreateTranslation(0, 1.5f, -1.5f),
///   };
///   vrMenu.AddChild(new UILabel { Text = "VR Menu", FontSize = FontSize.Heading });
///   vrMenu.AddChild(new UIButton { Text = "Resume", Width = 200, Height = 40 });
///
///   // Per frame:
///   vrMenu.DrawWorld(renderer, viewProjectionMatrix);
/// </summary>
public class UIWorldPanel : UIPanel
{
    /// <summary>Width of the panel surface in virtual pixels.</summary>
    public float PanelWidth { get; set; } = 400;

    /// <summary>Height of the panel surface in virtual pixels.</summary>
    public float PanelHeight { get; set; } = 300;

    /// <summary>World-space scale (meters per panel unit). 0.001 = 1 pixel = 1mm.</summary>
    public float WorldScale { get; set; } = 0.001f;

    /// <summary>If true, the panel always faces the camera (Y-axis billboard).</summary>
    public bool Billboard { get; set; }

    /// <summary>If true, the panel follows the camera at a fixed offset (VR HUD mode).</summary>
    public bool ViewAnchored { get; set; }

    /// <summary>Offset from camera when ViewAnchored (meters). Z = distance forward.</summary>
    public Vector3 ViewAnchorOffset { get; set; } = new(0, 0, -1.5f);

    /// <summary>Camera position in world space (needed for billboarding).</summary>
    public Vector3 CameraPosition { get; set; }

    /// <summary>Camera forward direction (needed for view-anchored mode).</summary>
    public Vector3 CameraForward { get; set; } = -Vector3.UnitZ;

    /// <summary>Camera up direction.</summary>
    public Vector3 CameraUp { get; set; } = Vector3.UnitY;

    public UIWorldPanel()
    {
        RenderMode = UIRenderMode.WorldSpace;
        Width = 400;
        Height = 300;
    }

    /// <summary>
    /// Draw this panel and its children in world space.
    /// Call this instead of the normal Draw() for world-space panels.
    /// </summary>
    /// <param name="renderer">The UI renderer.</param>
    /// <param name="viewProjection">The camera's View * Projection matrix.</param>
    /// <param name="encoder">GPU command encoder.</param>
    /// <param name="colorTarget">Color render target.</param>
    /// <param name="depthTarget">Depth render target (for proper occlusion).</param>
    public void DrawWorld(UIRenderer renderer, Matrix4x4 viewProjection,
        GPUCommandEncoder encoder, GPUTextureView colorTarget, GPUTextureView depthTarget)
    {
        if (!Visible) return;

        // Compute the model matrix
        var model = ComputeModelMatrix();

        // Scale: panel pixels to world meters
        var scale = Matrix4x4.CreateScale(PanelWidth * WorldScale, PanelHeight * WorldScale, 1f);
        var mvp = scale * model * viewProjection;

        // Set panel dimensions for children to use
        Width = PanelWidth;
        Height = PanelHeight;

        // Draw background
        renderer.DrawWorldRect(0, 0, PanelWidth, PanelHeight, PanelWidth, PanelHeight, BackgroundColor);

        if (BorderWidth > 0)
        {
            renderer.DrawWorldRect(0, 0, PanelWidth, BorderWidth, PanelWidth, PanelHeight, BorderColor);
            renderer.DrawWorldRect(0, 0, BorderWidth, PanelHeight, PanelWidth, PanelHeight, BorderColor);
            renderer.DrawWorldRect(PanelWidth - BorderWidth, 0, BorderWidth, PanelHeight, PanelWidth, PanelHeight, BorderColor);
            renderer.DrawWorldRect(0, PanelHeight - BorderWidth, PanelWidth, BorderWidth, PanelWidth, PanelHeight, BorderColor);
        }

        // Draw children (they use screen-space Draw which emits to world batch)
        // Children think they're drawing in screen space (pixel coords on the panel)
        // The MVP matrix transforms their panel-local coords to world space
        var snapshot = Children.ToArray();
        foreach (var child in snapshot)
        {
            if (!child.Visible) continue;
            DrawChildWorld(renderer, child);
        }

        // Flush the world batch
        renderer.EndWorldSpace(encoder, colorTarget, depthTarget, mvp);
    }

    private void DrawChildWorld(UIRenderer renderer, UIElement child)
    {
        // For now, draw basic elements in world space
        // This is a simplified version - full element rendering in world space
        // would need each element type to support DrawWorld() override
        if (child is UILabel label && !string.IsNullOrEmpty(label.Text))
        {
            var bounds = child.ScreenBounds;
            renderer.DrawWorldText(label.Text, bounds.X, bounds.Y, PanelWidth, PanelHeight,
                label.FontSize, label.Color);
        }
        else if (child is UIButton button)
        {
            var bounds = child.ScreenBounds;
            Color bgColor = !button.Enabled ? button.DisabledColor :
                             button.IsPressed ? button.PressedColor :
                             button.IsHovered ? button.HoverColor :
                             button.NormalColor;
            renderer.DrawWorldRect(bounds.X, bounds.Y, bounds.Width, bounds.Height,
                PanelWidth, PanelHeight, bgColor);
            if (!string.IsNullOrEmpty(button.Text))
            {
                float textW = renderer.MeasureText(button.Text, button.FontSize);
                float textH = renderer.GetLineHeight(button.FontSize);
                float textX = bounds.X + (bounds.Width - textW) / 2;
                float textY = bounds.Y + (bounds.Height - textH) / 2;
                renderer.DrawWorldText(button.Text, textX, textY, PanelWidth, PanelHeight,
                    button.FontSize, button.Enabled ? button.TextColor : Color.Gray);
            }
        }
        else if (child is UIPanel panel)
        {
            var bounds = child.ScreenBounds;
            renderer.DrawWorldRect(bounds.X, bounds.Y, bounds.Width, bounds.Height,
                PanelWidth, PanelHeight, panel.BackgroundColor);
            foreach (var grandchild in child.Children)
                DrawChildWorld(renderer, grandchild);
        }

        // Recurse into children
        foreach (var grandchild in child.Children)
        {
            if (grandchild.Visible)
                DrawChildWorld(renderer, grandchild);
        }
    }

    private Matrix4x4 ComputeModelMatrix()
    {
        if (ViewAnchored)
        {
            // Panel follows camera at fixed offset
            var right = Vector3.Normalize(Vector3.Cross(CameraForward, CameraUp));
            var up = Vector3.Normalize(Vector3.Cross(right, CameraForward));
            var pos = CameraPosition
                + CameraForward * ViewAnchorOffset.Z
                + right * ViewAnchorOffset.X
                + up * ViewAnchorOffset.Y;

            // Face the camera
            return CreateLookAtMatrix(pos, CameraPosition, CameraUp);
        }

        if (Billboard)
        {
            // Face the camera from the panel's world position
            var pos = new Vector3(WorldTransform.M41, WorldTransform.M42, WorldTransform.M43);
            return CreateLookAtMatrix(pos, CameraPosition, CameraUp);
        }

        // Fixed world transform
        return WorldTransform;
    }

    /// <summary>Create a matrix that positions an object at 'position' facing 'target'.</summary>
    private static Matrix4x4 CreateLookAtMatrix(Vector3 position, Vector3 target, Vector3 up)
    {
        var forward = Vector3.Normalize(target - position);
        var right = Vector3.Normalize(Vector3.Cross(up, forward));
        var correctedUp = Vector3.Cross(forward, right);

        return new Matrix4x4(
            right.X, right.Y, right.Z, 0,
            correctedUp.X, correctedUp.Y, correctedUp.Z, 0,
            forward.X, forward.Y, forward.Z, 0,
            position.X, position.Y, position.Z, 1
        );
    }
}
