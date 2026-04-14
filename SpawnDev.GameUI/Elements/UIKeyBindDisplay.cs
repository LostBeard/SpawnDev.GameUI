using System.Drawing;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Displays and optionally allows rebinding of key bindings.
/// Shows action name on the left, bound key on the right.
/// Click a binding to enter rebind mode (next key press assigns).
///
/// Usage:
///   var binds = new UIKeyBindDisplay { Width = 400 };
///   binds.AddBinding("Move Forward", "KeyW");
///   binds.AddBinding("Move Back", "KeyS");
///   binds.AddBinding("Jump", "Space");
///   binds.AddBinding("Interact", "KeyF");
///   binds.AddBinding("Inventory", "KeyI");
///   binds.OnBindingChanged = (action, newKey) => SaveBinding(action, newKey);
/// </summary>
public class UIKeyBindDisplay : UIFlexPanel
{
    private readonly List<KeyBinding> _bindings = new();
    private int _rebindingIndex = -1; // which binding is waiting for a key press
    private int _hoveredIndex = -1;

    /// <summary>Height per binding row.</summary>
    public float RowHeight { get; set; } = 30f;

    /// <summary>Whether bindings can be changed by clicking.</summary>
    public bool AllowRebind { get; set; } = true;

    /// <summary>Called when a binding changes.</summary>
    public Action<string, string>? OnBindingChanged { get; set; }

    // Colors
    private Color? _keyBgColor, _rebindColor;
    public Color KeyBgColor { get => _keyBgColor ?? Color.FromArgb(180, 40, 40, 55); set => _keyBgColor = value; }
    public Color RebindColor { get => _rebindColor ?? Color.FromArgb(200, 200, 140, 40); set => _rebindColor = value; }

    public UIKeyBindDisplay()
    {
        Direction = FlexDirection.Column;
        Gap = 2;
        Padding = 8;
        BackgroundColor = Color.FromArgb(160, 15, 15, 25);
    }

    public void AddBinding(string action, string key)
    {
        _bindings.Add(new KeyBinding { Action = action, Key = key });
    }

    public void SetBinding(string action, string key)
    {
        for (int i = 0; i < _bindings.Count; i++)
        {
            if (_bindings[i].Action == action)
            {
                _bindings[i] = _bindings[i] with { Key = key };
                return;
            }
        }
    }

    public string? GetBinding(string action)
    {
        return _bindings.FirstOrDefault(b => b.Action == action).Key;
    }

    public void ClearBindings() => _bindings.Clear();

    public override void Update(GameInput input, float dt)
    {
        if (!Visible || !Enabled) return;

        // Handle rebind mode - next key press assigns
        if (_rebindingIndex >= 0 && _rebindingIndex < _bindings.Count)
        {
            var pressedKeys = input.Keyboard.KeysPressed;
            if (pressedKeys.Count > 0)
            {
                string newKey = pressedKeys.First();
                if (newKey != "Escape") // Escape cancels rebind
                {
                    var binding = _bindings[_rebindingIndex];
                    _bindings[_rebindingIndex] = binding with { Key = newKey };
                    OnBindingChanged?.Invoke(binding.Action, newKey);
                }
                _rebindingIndex = -1;
                return;
            }
        }

        // Click detection
        _hoveredIndex = -1;
        foreach (var pointer in input.Pointers)
        {
            if (!pointer.ScreenPosition.HasValue) continue;
            var mp = pointer.ScreenPosition.Value;
            var bounds = ScreenBounds;

            float localY = mp.Y - bounds.Y - Padding;
            if (localY >= 0 && mp.X >= bounds.X && mp.X < bounds.X + bounds.Width)
            {
                int idx = (int)(localY / RowHeight);
                if (idx >= 0 && idx < _bindings.Count)
                {
                    _hoveredIndex = idx;
                    if (pointer.WasReleased && AllowRebind)
                        _rebindingIndex = idx;
                }
            }
        }

        base.Update(input, dt);
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        Height = Padding * 2 + _bindings.Count * RowHeight;
        var bounds = ScreenBounds;
        renderer.DrawRect(bounds.X, bounds.Y, Width, Height, BackgroundColor);

        float keyColX = bounds.X + Width - 120; // key column starts here
        float keyColW = 100;

        for (int i = 0; i < _bindings.Count; i++)
        {
            float rowY = bounds.Y + Padding + i * RowHeight;
            var binding = _bindings[i];

            // Hover highlight
            if (i == _hoveredIndex)
                renderer.DrawRect(bounds.X + 2, rowY, Width - 4, RowHeight - 2, Color.FromArgb(30, 255, 255, 255));

            // Action label
            float textY = rowY + (RowHeight - renderer.GetLineHeight(FontSize.Body)) / 2;
            renderer.DrawText(binding.Action, bounds.X + Padding + 4, textY, FontSize.Body, UITheme.Current.TextPrimary);

            // Key binding box
            Color keyBg = i == _rebindingIndex ? RebindColor : KeyBgColor;
            renderer.DrawRect(keyColX, rowY + 3, keyColW, RowHeight - 6, keyBg);

            string keyText = i == _rebindingIndex ? "Press key..." : FormatKeyName(binding.Key);
            float keyTextW = renderer.MeasureText(keyText, FontSize.Caption);
            float keyTextX = keyColX + (keyColW - keyTextW) / 2;
            float keyTextY = rowY + (RowHeight - renderer.GetLineHeight(FontSize.Caption)) / 2;
            Color keyTextColor = i == _rebindingIndex ? Color.Black : UITheme.Current.TextPrimary;
            renderer.DrawText(keyText, keyTextX, keyTextY, FontSize.Caption, keyTextColor);
        }
    }

    private static string FormatKeyName(string? code)
    {
        if (string.IsNullOrEmpty(code)) return "None";
        // Strip "Key" prefix for cleaner display
        if (code.StartsWith("Key") && code.Length == 4) return code[3..];
        if (code.StartsWith("Digit")) return code[5..];
        return code switch
        {
            "Space" => "Space",
            "ShiftLeft" => "L-Shift",
            "ShiftRight" => "R-Shift",
            "ControlLeft" => "L-Ctrl",
            "ControlRight" => "R-Ctrl",
            "AltLeft" => "L-Alt",
            "AltRight" => "R-Alt",
            "ArrowUp" => "Up",
            "ArrowDown" => "Down",
            "ArrowLeft" => "Left",
            "ArrowRight" => "Right",
            "Enter" => "Enter",
            "Escape" => "Esc",
            "Backspace" => "Bksp",
            "Tab" => "Tab",
            _ => code,
        };
    }

    private struct KeyBinding
    {
        public string Action;
        public string Key;
    }
}
