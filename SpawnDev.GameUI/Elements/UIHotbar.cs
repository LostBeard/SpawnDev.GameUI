using System.Drawing;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Quick-access hotbar for items/weapons/tools. Horizontal row of slots
/// with number key selection (1-9), scroll wheel cycling, and click selection.
///
/// Usage:
///   var hotbar = new UIHotbar { SlotCount = 9 };
///   hotbar.SetSlot(0, "Axe");
///   hotbar.SetSlot(1, "Pickaxe");
///   hotbar.SetSlot(5, "Bandage");
///   hotbar.OnSlotChanged = (idx) => player.EquipSlot(idx);
///   root.AddAnchored(hotbar, Anchor.BottomCenter, offsetY: -10);
/// </summary>
public class UIHotbar : UIElement
{
    private readonly List<HotbarSlot> _slots = new();
    private int _selectedSlot;

    /// <summary>Number of slots.</summary>
    public int SlotCount
    {
        get => _slots.Count;
        set
        {
            while (_slots.Count < value) _slots.Add(new HotbarSlot());
            while (_slots.Count > value) _slots.RemoveAt(_slots.Count - 1);
            AutoSize();
        }
    }

    /// <summary>Size of each slot in pixels.</summary>
    public float SlotSize { get; set; } = 48f;

    /// <summary>Gap between slots.</summary>
    public float SlotGap { get; set; } = 4f;

    /// <summary>Currently selected slot index.</summary>
    public int SelectedSlot
    {
        get => _selectedSlot;
        set
        {
            int clamped = Math.Clamp(value, 0, Math.Max(0, _slots.Count - 1));
            if (_selectedSlot != clamped)
            {
                _selectedSlot = clamped;
                OnSlotChanged?.Invoke(clamped);
            }
        }
    }

    /// <summary>Called when the selected slot changes.</summary>
    public Action<int>? OnSlotChanged { get; set; }

    /// <summary>Called when a slot is right-clicked (context action).</summary>
    public Action<int>? OnSlotContext { get; set; }

    // Theme-aware colors
    private Color? _slotColor, _selectedColor, _hoverColor, _borderColor;
    public Color SlotColor { get => _slotColor ?? Color.FromArgb(180, 25, 25, 35); set => _slotColor = value; }
    public Color SelectedColor { get => _selectedColor ?? Color.FromArgb(220, 108, 92, 231); set => _selectedColor = value; }
    public Color HoverColor { get => _hoverColor ?? Color.FromArgb(140, 60, 60, 80); set => _hoverColor = value; }
    public Color BorderColor { get => _borderColor ?? Color.FromArgb(80, 255, 255, 255); set => _borderColor = value; }

    private int _hoveredSlot = -1;

    public UIHotbar()
    {
        SlotCount = 9;
    }

    /// <summary>Set content for a slot.</summary>
    public void SetSlot(int index, string? label = null, object? tag = null)
    {
        if (index >= 0 && index < _slots.Count)
            _slots[index] = new HotbarSlot { Label = label, Tag = tag };
    }

    /// <summary>Clear a slot.</summary>
    public void ClearSlot(int index)
    {
        if (index >= 0 && index < _slots.Count)
            _slots[index] = new HotbarSlot();
    }

    /// <summary>Get slot data.</summary>
    public HotbarSlot GetSlot(int index) => index >= 0 && index < _slots.Count ? _slots[index] : new HotbarSlot();

    private void AutoSize()
    {
        Width = _slots.Count * SlotSize + (_slots.Count - 1) * SlotGap + 12; // padding
        Height = SlotSize + 12;
    }

    public override void Update(GameInput input, float dt)
    {
        if (!Visible || !Enabled) return;

        // Number keys 1-9 select slots
        for (int i = 0; i < Math.Min(9, _slots.Count); i++)
        {
            if (input.Keyboard.WasKeyPressed($"Digit{i + 1}"))
                SelectedSlot = i;
        }

        // Scroll wheel cycles slots
        var pointer = input.PrimaryPointer;
        if (pointer != null && MathF.Abs(pointer.ScrollDelta) > 1f)
        {
            int dir = pointer.ScrollDelta > 0 ? 1 : -1;
            int next = (_selectedSlot + dir + _slots.Count) % _slots.Count;
            SelectedSlot = next;
        }

        // Mouse hover and click
        _hoveredSlot = -1;
        if (pointer?.ScreenPosition != null)
        {
            var bounds = ScreenBounds;
            var mp = pointer.ScreenPosition.Value;
            float localX = mp.X - bounds.X - 6; // padding
            float localY = mp.Y - bounds.Y - 6;

            if (localY >= 0 && localY < SlotSize)
            {
                int col = (int)(localX / (SlotSize + SlotGap));
                float cellLocal = localX - col * (SlotSize + SlotGap);
                if (col >= 0 && col < _slots.Count && cellLocal <= SlotSize)
                {
                    _hoveredSlot = col;
                    if (pointer.WasReleased) SelectedSlot = col;
                    if (pointer.IsSecondaryPressed) OnSlotContext?.Invoke(col);
                }
            }
        }

        base.Update(input, dt);
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        AutoSize();
        var bounds = ScreenBounds;

        // Background
        renderer.DrawRect(bounds.X, bounds.Y, Width, Height,
            Color.FromArgb(160, 10, 10, 15));

        for (int i = 0; i < _slots.Count; i++)
        {
            float sx = bounds.X + 6 + i * (SlotSize + SlotGap);
            float sy = bounds.Y + 6;

            // Slot background
            Color bg = i == _selectedSlot ? SelectedColor :
                       i == _hoveredSlot ? HoverColor :
                       SlotColor;
            renderer.DrawRect(sx, sy, SlotSize, SlotSize, bg);

            // Border
            renderer.DrawRect(sx, sy, SlotSize, 1, BorderColor);
            renderer.DrawRect(sx, sy, 1, SlotSize, BorderColor);
            renderer.DrawRect(sx + SlotSize - 1, sy, 1, SlotSize, BorderColor);
            renderer.DrawRect(sx, sy + SlotSize - 1, SlotSize, 1, BorderColor);

            // Selected highlight border
            if (i == _selectedSlot)
            {
                renderer.DrawRect(sx - 1, sy - 1, SlotSize + 2, 2, UITheme.Current.FocusBorder);
                renderer.DrawRect(sx - 1, sy - 1, 2, SlotSize + 2, UITheme.Current.FocusBorder);
                renderer.DrawRect(sx + SlotSize - 1, sy - 1, 2, SlotSize + 2, UITheme.Current.FocusBorder);
                renderer.DrawRect(sx - 1, sy + SlotSize - 1, SlotSize + 2, 2, UITheme.Current.FocusBorder);
            }

            // Slot number (top-left corner)
            string numStr = (i + 1 <= 9) ? (i + 1).ToString() : "";
            if (!string.IsNullOrEmpty(numStr))
                renderer.DrawText(numStr, sx + 3, sy + 1, FontSize.Caption,
                    Color.FromArgb(120, 200, 200, 220));

            // Item label (bottom center)
            var slot = _slots[i];
            if (!string.IsNullOrEmpty(slot.Label))
            {
                float tw = renderer.MeasureText(slot.Label, FontSize.Caption);
                float tx = sx + (SlotSize - tw) / 2;
                float ty = sy + SlotSize - renderer.GetLineHeight(FontSize.Caption) - 2;
                renderer.DrawText(slot.Label, tx, ty, FontSize.Caption, UITheme.Current.TextPrimary);
            }
        }
    }
}

/// <summary>Content of a hotbar slot.</summary>
public struct HotbarSlot
{
    /// <summary>Item label.</summary>
    public string? Label { get; set; }
    /// <summary>User data (item reference).</summary>
    public object? Tag { get; set; }
}
