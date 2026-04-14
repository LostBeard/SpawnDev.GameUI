using System.Drawing;
using System.Numerics;

namespace SpawnDev.GameUI.Input;

/// <summary>
/// Manages drag-and-drop operations between UI elements.
/// Any element can be a drag source or drop target.
///
/// Usage:
///   var dragDrop = new DragDropManager();
///
///   // Start drag from inventory grid:
///   grid.OnDragStart = (idx) => dragDrop.BeginDrag(
///       new DragData { ItemId = items[idx].Id, SourceSlot = idx },
///       "Iron Axe");
///
///   // Register drop targets:
///   dragDrop.RegisterTarget(otherGrid, (data, pos) => {
///       MoveItem(data.SourceSlot, otherGrid.GetCellAtPosition(pos));
///   });
///
///   // Per frame:
///   dragDrop.Update(gameInput);
///
///   // Draw the drag ghost (after all other UI):
///   dragDrop.Draw(renderer);
/// </summary>
public class DragDropManager
{
    private readonly List<DropTarget> _targets = new();

    /// <summary>Whether a drag is currently in progress.</summary>
    public bool IsDragging { get; private set; }

    /// <summary>The data being dragged.</summary>
    public object? DragData { get; private set; }

    /// <summary>Label displayed on the drag ghost.</summary>
    public string DragLabel { get; private set; } = "";

    /// <summary>Current drag position (screen space).</summary>
    public Vector2 DragPosition { get; private set; }

    /// <summary>Size of the drag ghost indicator.</summary>
    public float GhostSize { get; set; } = 40f;

    /// <summary>Called when drag is cancelled (released over no target).</summary>
    public Action? OnCancelled { get; set; }

    /// <summary>Begin a drag operation.</summary>
    public void BeginDrag(object data, string label = "")
    {
        IsDragging = true;
        DragData = data;
        DragLabel = label;
    }

    /// <summary>Cancel the current drag.</summary>
    public void CancelDrag()
    {
        if (!IsDragging) return;
        IsDragging = false;
        OnCancelled?.Invoke();
        DragData = null;
        DragLabel = "";
    }

    /// <summary>Register a UI element as a drop target.</summary>
    public void RegisterTarget(UIElement element, Action<object, Vector2> onDrop)
    {
        _targets.Add(new DropTarget { Element = element, OnDrop = onDrop });
    }

    /// <summary>Unregister a drop target.</summary>
    public void UnregisterTarget(UIElement element)
    {
        _targets.RemoveAll(t => t.Element == element);
    }

    /// <summary>Update drag state. Call per frame.</summary>
    public void Update(GameInput input)
    {
        if (!IsDragging) return;

        var pointer = input.PrimaryPointer;
        if (pointer?.ScreenPosition == null) return;

        DragPosition = pointer.ScreenPosition.Value;

        if (pointer.WasReleased)
        {
            // Check drop targets
            bool dropped = false;
            foreach (var target in _targets)
            {
                if (!target.Element.Visible || !target.Element.Enabled) continue;
                var bounds = target.Element.ScreenBounds;
                if (DragPosition.X >= bounds.X && DragPosition.X < bounds.X + bounds.Width &&
                    DragPosition.Y >= bounds.Y && DragPosition.Y < bounds.Y + bounds.Height)
                {
                    target.OnDrop(DragData!, DragPosition);
                    dropped = true;
                    break;
                }
            }

            if (!dropped) OnCancelled?.Invoke();

            IsDragging = false;
            DragData = null;
            DragLabel = "";
        }
    }

    /// <summary>Draw the drag ghost. Call after all other UI drawing.</summary>
    public void Draw(UIRenderer renderer)
    {
        if (!IsDragging) return;

        // Ghost box at cursor
        float x = DragPosition.X - GhostSize / 2;
        float y = DragPosition.Y - GhostSize / 2;
        renderer.DrawRect(x, y, GhostSize, GhostSize, Color.FromArgb(160, 80, 80, 120));
        renderer.DrawRect(x, y, GhostSize, 1, Color.FromArgb(120, 200, 200, 255));
        renderer.DrawRect(x, y, 1, GhostSize, Color.FromArgb(120, 200, 200, 255));

        // Label below cursor
        if (!string.IsNullOrEmpty(DragLabel))
        {
            float labelW = renderer.MeasureText(DragLabel, Elements.FontSize.Caption);
            float labelX = DragPosition.X - labelW / 2;
            float labelY = DragPosition.Y + GhostSize / 2 + 4;
            renderer.DrawRect(labelX - 4, labelY - 2, labelW + 8, 18, Color.FromArgb(200, 20, 20, 30));
            renderer.DrawText(DragLabel, labelX, labelY, Elements.FontSize.Caption, Color.White);
        }

        // Highlight valid drop targets
        foreach (var target in _targets)
        {
            if (!target.Element.Visible || !target.Element.Enabled) continue;
            var bounds = target.Element.ScreenBounds;
            bool isOver = DragPosition.X >= bounds.X && DragPosition.X < bounds.X + bounds.Width &&
                          DragPosition.Y >= bounds.Y && DragPosition.Y < bounds.Y + bounds.Height;
            if (isOver)
            {
                renderer.DrawRect(bounds.X, bounds.Y, bounds.Width, 2, Color.FromArgb(180, 100, 200, 255));
                renderer.DrawRect(bounds.X, bounds.Y, 2, bounds.Height, Color.FromArgb(180, 100, 200, 255));
                renderer.DrawRect(bounds.X + bounds.Width - 2, bounds.Y, 2, bounds.Height, Color.FromArgb(180, 100, 200, 255));
                renderer.DrawRect(bounds.X, bounds.Y + bounds.Height - 2, bounds.Width, 2, Color.FromArgb(180, 100, 200, 255));
            }
        }
    }

    private struct DropTarget
    {
        public UIElement Element;
        public Action<object, Vector2> OnDrop;
    }
}
