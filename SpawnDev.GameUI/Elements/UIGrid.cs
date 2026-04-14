using System.Drawing;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Fixed-size grid of cells for inventory-style item display.
/// Each cell can hold an item with an icon texture and optional stack count.
/// Supports selection, hover highlight, and drag start detection.
/// Built for inventory grids, crafting ingredient slots, equipment slots.
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

    /// <summary>Called when a cell drag starts (for inventory drag-and-drop).</summary>
    public Action<int>? OnDragStart { get; set; }

    // Theme-aware colors
    private Color? _cellColor, _cellHoverColor, _cellSelectedColor, _cellBorderColor;
    public Color CellColor { get => _cellColor ?? Color.FromArgb(160, 30, 30, 40); set => _cellColor = value; }
    public Color CellHoverColor { get => _cellHoverColor ?? Color.FromArgb(200, 50, 50, 65); set => _cellHoverColor = value; }
    public Color CellSelectedColor { get => _cellSelectedColor ?? Color.FromArgb(200, 108, 92, 231); set => _cellSelectedColor = value; }
    public Color CellBorderColor { get => _cellBorderColor ?? Color.FromArgb(60, 255, 255, 255); set => _cellBorderColor = value; }

    /// <summary>Cell content data. Set via SetCell/GetCell.</summary>
    private readonly Dictionary<int, GridCell> _cells = new();

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
                var bounds = ScreenBounds;
                var mp = pointer.ScreenPosition.Value;
                float localX = mp.X - bounds.X - Padding;
                float localY = mp.Y - bounds.Y - Padding;

                if (localX >= 0 && localY >= 0)
                {
                    int col = (int)(localX / (CellSize + CellGap));
                    int row = (int)(localY / (CellSize + CellGap));

                    // Verify we're actually inside a cell, not in the gap
                    float cellLocalX = localX - col * (CellSize + CellGap);
                    float cellLocalY = localY - row * (CellSize + CellGap);

                    if (col >= 0 && col < Columns && row >= 0 && row < Rows &&
                        cellLocalX <= CellSize && cellLocalY <= CellSize)
                    {
                        int idx = row * Columns + col;
                        HoveredIndex = idx;

                        if (pointer.WasReleased)
                        {
                            SelectedIndex = idx;
                            OnCellClicked?.Invoke(idx);
                        }
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
                Color bgColor = idx == SelectedIndex ? CellSelectedColor :
                                 idx == HoveredIndex ? CellHoverColor :
                                 CellColor;
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
