using System.Drawing;
using System.Numerics;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Fixed-size grid of cells for inventory-style item display.
/// Each cell can hold an item with an icon texture and optional stack count.
/// Supports selection, hover highlight, drag-and-drop between grids, and right-click context.
/// Built for inventory grids, crafting ingredient slots, loot windows.
///
/// Drag-and-drop: set EnableDragDrop = true. The grid detects drag gestures
/// and fires OnDragStart. Use with DragDropManager for cross-grid item transfer.
/// </summary>
public class UIGrid : UIPanel
{
    /// <summary>Number of columns.</summary>
    public int Columns { get; set; } = 6;

    /// <summary>Number of rows.</summary>
    public int Rows { get; set; } = 4;

    /// <summary>Cell size in pixels (square).</summary>
    public float CellSize { get; set; } = 48f;

    /// <summary>Gap between cells.</summary>
    public float CellGap { get; set; } = 4f;

    /// <summary>Selected cell index. -1 = none.</summary>
    public int SelectedIndex { get; set; } = -1;

    /// <summary>Hovered cell index. -1 = none.</summary>
    public int HoveredIndex { get; private set; } = -1;

    /// <summary>Called when a cell is clicked.</summary>
    public Action<int>? OnCellClicked { get; set; }

    /// <summary>Called when a cell right-click or secondary action fires.</summary>
    public Action<int>? OnCellSecondary { get; set; }

    /// <summary>Called when a cell drag starts (for inventory drag-and-drop).</summary>
    public Action<int>? OnDragStart { get; set; }

    /// <summary>
    /// Called when an item is dropped onto a cell in this grid.
    /// Parameters: (sourceGrid or null, sourceCellIndex, targetCellIndex, dragData).
    /// Return true to accept the drop, false to reject.
    /// </summary>
    public Func<UIGrid?, int, int, object, bool>? OnDrop { get; set; }

    /// <summary>Enable drag-and-drop from this grid's cells.</summary>
    public bool EnableDragDrop { get; set; }

    /// <summary>Minimum drag distance in pixels before a drag gesture begins.</summary>
    public float DragThreshold { get; set; } = 6f;

    // Theme-aware colors
    private Color? _cellColor, _cellHoverColor, _cellSelectedColor, _cellBorderColor, _dropHighlightColor;
    public Color CellColor { get => _cellColor ?? Color.FromArgb(160, 30, 30, 40); set => _cellColor = value; }
    public Color CellHoverColor { get => _cellHoverColor ?? Color.FromArgb(200, 50, 50, 65); set => _cellHoverColor = value; }
    public Color CellSelectedColor { get => _cellSelectedColor ?? Color.FromArgb(200, 108, 92, 231); set => _cellSelectedColor = value; }
    public Color CellBorderColor { get => _cellBorderColor ?? Color.FromArgb(60, 255, 255, 255); set => _cellBorderColor = value; }
    public Color DropHighlightColor { get => _dropHighlightColor ?? Color.FromArgb(100, 100, 200, 255); set => _dropHighlightColor = value; }

    /// <summary>Cell currently highlighted as a drop target. -1 = none.</summary>
    public int DropTargetIndex { get; set; } = -1;

    /// <summary>Cell content data. Set via SetCell/GetCell.</summary>
    private readonly Dictionary<int, GridCell> _cells = new();

    // Drag gesture tracking
    private bool _dragPending;
    private int _dragSourceCell = -1;
    private Vector2 _dragStartPos;

    /// <summary>Set content for a grid cell.</summary>
    public void SetCell(int index, string? label = null, Color? labelColor = null, object? tag = null)
    {
        _cells[index] = new GridCell { Label = label, LabelColor = labelColor, Tag = tag };
    }

    /// <summary>Clear a grid cell.</summary>
    public void ClearCell(int index) => _cells.Remove(index);

    /// <summary>Clear all cells.</summary>
    public void ClearAllCells() => _cells.Clear();

    /// <summary>Get cell data, or null if empty.</summary>
    public GridCell? GetCell(int index) => _cells.TryGetValue(index, out var cell) ? cell : null;

    /// <summary>Total number of cells (Rows * Columns).</summary>
    public int TotalCells => Rows * Columns;

    /// <summary>Check if a cell index is occupied.</summary>
    public bool IsCellOccupied(int index) => _cells.ContainsKey(index);

    /// <summary>
    /// Swap the contents of two cells. Works across grids when called on both.
    /// </summary>
    public void SwapCells(int indexA, UIGrid otherGrid, int indexB)
    {
        var cellA = GetCell(indexA);
        var cellB = otherGrid.GetCell(indexB);

        if (cellA != null)
            otherGrid.SetCell(indexB, cellA.Label, cellA.LabelColor, cellA.Tag);
        else
            otherGrid.ClearCell(indexB);

        if (cellB != null)
            SetCell(indexA, cellB.Label, cellB.LabelColor, cellB.Tag);
        else
            ClearCell(indexA);
    }

    /// <summary>Move a cell's contents to another cell (in this or another grid).</summary>
    public void MoveCell(int sourceIndex, UIGrid targetGrid, int targetIndex)
    {
        var cell = GetCell(sourceIndex);
        if (cell == null) return;

        targetGrid.SetCell(targetIndex, cell.Label, cell.LabelColor, cell.Tag);
        ClearCell(sourceIndex);
    }

    /// <summary>Get the cell index at a screen position, or -1 if none.</summary>
    public int GetCellAtPosition(Vector2 screenPos)
    {
        var bounds = ScreenBounds;
        float localX = screenPos.X - bounds.X - Padding;
        float localY = screenPos.Y - bounds.Y - Padding;

        if (localX < 0 || localY < 0) return -1;

        int col = (int)(localX / (CellSize + CellGap));
        int row = (int)(localY / (CellSize + CellGap));

        float cellLocalX = localX - col * (CellSize + CellGap);
        float cellLocalY = localY - row * (CellSize + CellGap);

        if (col >= 0 && col < Columns && row >= 0 && row < Rows &&
            cellLocalX <= CellSize && cellLocalY <= CellSize)
        {
            return row * Columns + col;
        }
        return -1;
    }

    public UIGrid()
    {
        AutoSizeGrid();
    }

    /// <summary>Auto-size the grid panel to fit all cells.</summary>
    public void AutoSizeGrid()
    {
        Width = Padding * 2 + Columns * CellSize + (Columns - 1) * CellGap;
        Height = Padding * 2 + Rows * CellSize + (Rows - 1) * CellGap;
    }

    public override void Update(GameInput input, float dt)
    {
        if (!Visible || !Enabled) return;

        HoveredIndex = -1;

        foreach (var pointer in input.Pointers)
        {
            if (pointer.ScreenPosition.HasValue)
            {
                var mp = pointer.ScreenPosition.Value;
                int cellIdx = GetCellAtPosition(mp);

                if (cellIdx >= 0)
                {
                    HoveredIndex = cellIdx;

                    // Drag gesture detection
                    if (EnableDragDrop && pointer.IsPressed && !_dragPending && _cells.ContainsKey(cellIdx))
                    {
                        _dragPending = true;
                        _dragSourceCell = cellIdx;
                        _dragStartPos = mp;
                    }

                    if (pointer.WasReleased && !_dragPending)
                    {
                        SelectedIndex = cellIdx;
                        OnCellClicked?.Invoke(cellIdx);
                    }

                    // Secondary action (right-click or grip)
                    if (pointer.IsSecondaryPressed)
                    {
                        OnCellSecondary?.Invoke(cellIdx);
                    }
                }

                // Check if drag threshold exceeded
                if (_dragPending && pointer.IsPressed)
                {
                    float dist = Vector2.Distance(mp, _dragStartPos);
                    if (dist >= DragThreshold)
                    {
                        _dragPending = false;
                        OnDragStart?.Invoke(_dragSourceCell);
                    }
                }

                // Cancel drag gesture if released before threshold
                if (_dragPending && pointer.WasReleased)
                {
                    _dragPending = false;
                    // Treat as a click since drag didn't start
                    if (_dragSourceCell >= 0)
                    {
                        SelectedIndex = _dragSourceCell;
                        OnCellClicked?.Invoke(_dragSourceCell);
                    }
                }
            }
        }

        base.Update(input, dt);
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        var bounds = ScreenBounds;

        // Panel background
        renderer.DrawRect(bounds.X, bounds.Y, bounds.Width, bounds.Height, BackgroundColor);

        // Draw cells
        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Columns; col++)
            {
                int idx = row * Columns + col;
                float cx = bounds.X + Padding + col * (CellSize + CellGap);
                float cy = bounds.Y + Padding + row * (CellSize + CellGap);

                // Cell background
                Color bgColor;
                if (idx == DropTargetIndex)
                    bgColor = DropHighlightColor;
                else if (idx == SelectedIndex)
                    bgColor = CellSelectedColor;
                else if (idx == HoveredIndex)
                    bgColor = CellHoverColor;
                else
                    bgColor = CellColor;

                renderer.DrawRect(cx, cy, CellSize, CellSize, bgColor);

                // Cell border
                renderer.DrawRect(cx, cy, CellSize, 1, CellBorderColor);
                renderer.DrawRect(cx, cy, 1, CellSize, CellBorderColor);
                renderer.DrawRect(cx + CellSize - 1, cy, 1, CellSize, CellBorderColor);
                renderer.DrawRect(cx, cy + CellSize - 1, CellSize, 1, CellBorderColor);

                // Cell content
                if (_cells.TryGetValue(idx, out var cell) && !string.IsNullOrEmpty(cell.Label))
                {
                    var textColor = cell.LabelColor ?? UITheme.Current.TextPrimary;
                    float textW = renderer.MeasureText(cell.Label, FontSize.Caption);
                    float textH = renderer.GetLineHeight(FontSize.Caption);
                    float tx = cx + (CellSize - textW) / 2f;
                    float ty = cy + CellSize - textH - 2;
                    renderer.DrawText(cell.Label, tx, ty, FontSize.Caption, textColor);
                }

                // Stack count (if cell has a numeric tag)
                if (_cells.TryGetValue(idx, out var cellData) && cellData.Tag is int count && count > 1)
                {
                    string countStr = count.ToString();
                    float cw = renderer.MeasureText(countStr, FontSize.Caption);
                    renderer.DrawText(countStr, cx + CellSize - cw - 2, cy + 2, FontSize.Caption, Color.White);
                }
            }
        }
    }
}

/// <summary>Content data for a single grid cell.</summary>
public class GridCell
{
    /// <summary>Text label displayed at the bottom of the cell.</summary>
    public string? Label { get; set; }

    /// <summary>Label color override.</summary>
    public Color? LabelColor { get; set; }

    /// <summary>Optional user data (item reference, stack count, etc.).</summary>
    public object? Tag { get; set; }
}
