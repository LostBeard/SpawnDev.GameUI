using System.Drawing;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Equipment slot type filter. Each slot only accepts items of the matching type.
/// </summary>
public enum EquipmentSlotType
{
    Head,
    Chest,
    Legs,
    Feet,
    MainHand,
    OffHand,
    Ring,
    Necklace,
    Back,
    Ammo,
    Any,
}

/// <summary>
/// Data for a single equipment slot.
/// </summary>
public class EquipmentSlot
{
    /// <summary>Slot identifier (e.g., "Head", "MainHand").</summary>
    public string Name { get; set; } = "";

    /// <summary>What type of item this slot accepts.</summary>
    public EquipmentSlotType SlotType { get; set; } = EquipmentSlotType.Any;

    /// <summary>Position within the panel (relative to panel top-left).</summary>
    public float X { get; set; }
    public float Y { get; set; }

    /// <summary>Slot size in pixels (square).</summary>
    public float Size { get; set; } = 48f;

    /// <summary>Item label (null = empty slot).</summary>
    public string? ItemLabel { get; set; }

    /// <summary>Item color override.</summary>
    public Color? ItemColor { get; set; }

    /// <summary>User data (item reference).</summary>
    public object? Tag { get; set; }

    /// <summary>Whether the slot is currently occupied.</summary>
    public bool IsOccupied => ItemLabel != null;
}

/// <summary>
/// Paper doll equipment panel with named, typed slots.
/// Each slot is positioned manually to match a character silhouette layout.
/// Supports drag-and-drop with type filtering (only matching items accepted).
///
/// Default layout (DayZ/survival style):
///   [Head]         - top center
///   [Back]         - upper right
///   [Necklace]     - below head
///   [Chest]        - center
///   [MainHand]     - left of chest
///   [OffHand]      - right of chest
///   [Ring] [Ring]  - below hands
///   [Legs]         - below chest
///   [Feet]         - bottom center
///   [Ammo]         - lower right
///
/// Usage:
///   var equip = new UIEquipmentPanel();
///   equip.SetItem("MainHand", "Iron Sword", tag: swordItem);
///   equip.OnSlotClicked = (slot) => ShowItemDetails(slot);
/// </summary>
public class UIEquipmentPanel : UIPanel
{
    private readonly List<EquipmentSlot> _slots = new();

    /// <summary>Currently hovered slot, or null.</summary>
    public EquipmentSlot? HoveredSlot { get; private set; }

    /// <summary>Currently selected slot, or null.</summary>
    public EquipmentSlot? SelectedSlot { get; set; }

    /// <summary>Called when a slot is clicked.</summary>
    public Action<EquipmentSlot>? OnSlotClicked { get; set; }

    /// <summary>Called when a slot is right-clicked / secondary action.</summary>
    public Action<EquipmentSlot>? OnSlotSecondary { get; set; }

    /// <summary>
    /// Called when an item is dropped onto a slot.
    /// Parameters: (slot, dragData). Return true to accept.
    /// </summary>
    public Func<EquipmentSlot, object, bool>? OnDrop { get; set; }

    /// <summary>Slot currently highlighted as drop target.</summary>
    public EquipmentSlot? DropTargetSlot { get; set; }

    // Colors
    private Color? _slotColor, _slotHoverColor, _slotSelectedColor, _slotBorderColor, _slotEmptyColor;
    public Color SlotColor { get => _slotColor ?? Color.FromArgb(160, 30, 30, 40); set => _slotColor = value; }
    public Color SlotHoverColor { get => _slotHoverColor ?? Color.FromArgb(200, 50, 50, 65); set => _slotHoverColor = value; }
    public Color SlotSelectedColor { get => _slotSelectedColor ?? Color.FromArgb(200, 108, 92, 231); set => _slotSelectedColor = value; }
    public Color SlotBorderColor { get => _slotBorderColor ?? Color.FromArgb(60, 255, 255, 255); set => _slotBorderColor = value; }
    public Color SlotEmptyColor { get => _slotEmptyColor ?? Color.FromArgb(100, 20, 20, 30); set => _slotEmptyColor = value; }

    /// <summary>All slots in the panel.</summary>
    public IReadOnlyList<EquipmentSlot> Slots => _slots;

    /// <summary>
    /// Create a panel with the default survival game layout.
    /// Panel size: 200x320. Slot size: 48px. 10 slots.
    /// </summary>
    public UIEquipmentPanel()
    {
        Width = 200;
        Height = 320;
        Padding = 8;
        SetupDefaultLayout();
    }

    private void SetupDefaultLayout()
    {
        float s = 48f;
        float cx = (Width - s) / 2;  // center X
        float gap = 4;

        _slots.Clear();
        _slots.Add(new EquipmentSlot { Name = "Head", SlotType = EquipmentSlotType.Head, X = cx, Y = 8, Size = s });
        _slots.Add(new EquipmentSlot { Name = "Necklace", SlotType = EquipmentSlotType.Necklace, X = cx, Y = 8 + s + gap, Size = s / 1.5f });
        _slots.Add(new EquipmentSlot { Name = "Back", SlotType = EquipmentSlotType.Back, X = Width - s - 8, Y = 8, Size = s });
        _slots.Add(new EquipmentSlot { Name = "Chest", SlotType = EquipmentSlotType.Chest, X = cx, Y = 8 + s + gap + s / 1.5f + gap, Size = s });
        _slots.Add(new EquipmentSlot { Name = "MainHand", SlotType = EquipmentSlotType.MainHand, X = 8, Y = 8 + s + gap + s / 1.5f + gap, Size = s });
        _slots.Add(new EquipmentSlot { Name = "OffHand", SlotType = EquipmentSlotType.OffHand, X = Width - s - 8, Y = 8 + s + gap + s / 1.5f + gap, Size = s });
        _slots.Add(new EquipmentSlot { Name = "Ring1", SlotType = EquipmentSlotType.Ring, X = 8, Y = 8 + s + gap + s / 1.5f + gap + s + gap, Size = s * 0.75f });
        _slots.Add(new EquipmentSlot { Name = "Ring2", SlotType = EquipmentSlotType.Ring, X = Width - s * 0.75f - 8, Y = 8 + s + gap + s / 1.5f + gap + s + gap, Size = s * 0.75f });
        _slots.Add(new EquipmentSlot { Name = "Legs", SlotType = EquipmentSlotType.Legs, X = cx, Y = Height - s * 2 - gap - 8, Size = s });
        _slots.Add(new EquipmentSlot { Name = "Feet", SlotType = EquipmentSlotType.Feet, X = cx, Y = Height - s - 8, Size = s });
    }

    /// <summary>Add a custom equipment slot.</summary>
    public void AddSlot(string name, EquipmentSlotType type, float x, float y, float size = 48f)
    {
        _slots.Add(new EquipmentSlot { Name = name, SlotType = type, X = x, Y = y, Size = size });
    }

    /// <summary>Remove a slot by name.</summary>
    public void RemoveSlot(string name) => _slots.RemoveAll(s => s.Name == name);

    /// <summary>Clear the default layout and start with empty slots.</summary>
    public void ClearSlots() => _slots.Clear();

    /// <summary>Get a slot by name.</summary>
    public EquipmentSlot? GetSlot(string name) => _slots.Find(s => s.Name == name);

    /// <summary>Set an item in a named slot.</summary>
    public bool SetItem(string slotName, string? label, Color? color = null, object? tag = null)
    {
        var slot = GetSlot(slotName);
        if (slot == null) return false;
        slot.ItemLabel = label;
        slot.ItemColor = color;
        slot.Tag = tag;
        return true;
    }

    /// <summary>Clear a slot's item.</summary>
    public bool ClearItem(string slotName)
    {
        var slot = GetSlot(slotName);
        if (slot == null) return false;
        slot.ItemLabel = null;
        slot.ItemColor = null;
        slot.Tag = null;
        return true;
    }

    /// <summary>Check if a slot type accepts a given equipment type.</summary>
    public static bool IsSlotCompatible(EquipmentSlotType slotType, EquipmentSlotType itemType)
    {
        if (slotType == EquipmentSlotType.Any || itemType == EquipmentSlotType.Any) return true;
        return slotType == itemType;
    }

    public override void Update(GameInput input, float dt)
    {
        if (!Visible || !Enabled) return;

        HoveredSlot = null;

        foreach (var pointer in input.Pointers)
        {
            if (!pointer.ScreenPosition.HasValue) continue;
            var mp = pointer.ScreenPosition.Value;
            var bounds = ScreenBounds;

            foreach (var slot in _slots)
            {
                float sx = bounds.X + slot.X;
                float sy = bounds.Y + slot.Y;

                if (mp.X >= sx && mp.X < sx + slot.Size &&
                    mp.Y >= sy && mp.Y < sy + slot.Size)
                {
                    HoveredSlot = slot;

                    if (pointer.WasReleased)
                    {
                        SelectedSlot = slot;
                        OnSlotClicked?.Invoke(slot);
                    }

                    if (pointer.IsSecondaryPressed)
                    {
                        OnSlotSecondary?.Invoke(slot);
                    }

                    break;
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

        // Draw each slot
        foreach (var slot in _slots)
        {
            float sx = bounds.X + slot.X;
            float sy = bounds.Y + slot.Y;

            // Slot background
            Color bgColor;
            if (slot == DropTargetSlot)
                bgColor = Color.FromArgb(100, 100, 200, 255);
            else if (slot == SelectedSlot)
                bgColor = SlotSelectedColor;
            else if (slot == HoveredSlot)
                bgColor = SlotHoverColor;
            else if (slot.IsOccupied)
                bgColor = SlotColor;
            else
                bgColor = SlotEmptyColor;

            renderer.DrawRect(sx, sy, slot.Size, slot.Size, bgColor);

            // Slot border
            renderer.DrawRect(sx, sy, slot.Size, 1, SlotBorderColor);
            renderer.DrawRect(sx, sy, 1, slot.Size, SlotBorderColor);
            renderer.DrawRect(sx + slot.Size - 1, sy, 1, slot.Size, SlotBorderColor);
            renderer.DrawRect(sx, sy + slot.Size - 1, slot.Size, 1, SlotBorderColor);

            // Slot type label (when empty)
            if (!slot.IsOccupied)
            {
                string typeLabel = slot.SlotType switch
                {
                    EquipmentSlotType.Head => "Head",
                    EquipmentSlotType.Chest => "Chest",
                    EquipmentSlotType.Legs => "Legs",
                    EquipmentSlotType.Feet => "Feet",
                    EquipmentSlotType.MainHand => "Main",
                    EquipmentSlotType.OffHand => "Off",
                    EquipmentSlotType.Ring => "Ring",
                    EquipmentSlotType.Necklace => "Neck",
                    EquipmentSlotType.Back => "Back",
                    EquipmentSlotType.Ammo => "Ammo",
                    _ => slot.Name,
                };
                float tw = renderer.MeasureText(typeLabel, FontSize.Caption);
                float th = renderer.GetLineHeight(FontSize.Caption);
                float tx = sx + (slot.Size - tw) / 2f;
                float ty = sy + (slot.Size - th) / 2f;
                renderer.DrawText(typeLabel, tx, ty, FontSize.Caption, Color.FromArgb(80, 255, 255, 255));
            }
            else
            {
                // Item label
                var textColor = slot.ItemColor ?? UITheme.Current.TextPrimary;
                float tw = renderer.MeasureText(slot.ItemLabel!, FontSize.Caption);
                float th = renderer.GetLineHeight(FontSize.Caption);
                float tx = sx + (slot.Size - tw) / 2f;
                float ty = sy + slot.Size - th - 2;
                renderer.DrawText(slot.ItemLabel!, tx, ty, FontSize.Caption, textColor);
            }
        }
    }
}
