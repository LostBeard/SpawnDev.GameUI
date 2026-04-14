using System.Numerics;

namespace SpawnDev.GameUI.Input;

/// <summary>
/// Manages keyboard/gamepad focus navigation across UI elements.
/// Tab cycles through focusable elements. Arrow keys navigate directionally.
/// Enter/Space activates the focused element. Escape unfocuses.
///
/// Gamepad D-pad maps to arrow keys for menu navigation.
/// VR thumbstick maps to directional focus movement.
///
/// Usage:
///   var focus = new UIFocusManager();
///   focus.SetRoot(rootElement);
///
///   // Per frame (before element updates):
///   focus.Update(gameInput);
///
///   // Elements check focus state:
///   if (focus.FocusedElement == myButton) { /* draw focus ring */ }
/// </summary>
public class UIFocusManager
{
    private UIElement? _root;
    private readonly List<UIElement> _focusableElements = new();
    private int _focusIndex = -1;
    private bool _dirty = true;

    /// <summary>Currently focused element, or null.</summary>
    public UIElement? FocusedElement => _focusIndex >= 0 && _focusIndex < _focusableElements.Count
        ? _focusableElements[_focusIndex] : null;

    /// <summary>Whether any element has focus.</summary>
    public bool HasFocus => FocusedElement != null;

    /// <summary>Set the root element to scan for focusable children.</summary>
    public void SetRoot(UIElement root)
    {
        _root = root;
        _dirty = true;
    }

    /// <summary>Mark the focus list as needing rebuild (call when UI tree changes).</summary>
    public void Invalidate() => _dirty = true;

    /// <summary>Focus a specific element.</summary>
    public void Focus(UIElement element)
    {
        RebuildIfDirty();
        int idx = _focusableElements.IndexOf(element);
        if (idx >= 0) _focusIndex = idx;
    }

    /// <summary>Clear focus.</summary>
    public void Blur() => _focusIndex = -1;

    /// <summary>
    /// Process focus navigation input. Call before element updates.
    /// Handles Tab, Shift+Tab, arrow keys, Enter, Escape.
    /// </summary>
    public void Update(GameInput input)
    {
        RebuildIfDirty();
        if (_focusableElements.Count == 0) return;

        var kb = input.Keyboard;

        // Tab / Shift+Tab to cycle
        if (kb.WasKeyPressed("Tab"))
        {
            if (kb.IsKeyDown("ShiftLeft") || kb.IsKeyDown("ShiftRight"))
                MoveFocus(-1);
            else
                MoveFocus(1);
        }

        // Arrow keys for directional navigation
        if (kb.WasKeyPressed("ArrowUp")) MoveFocusDirectional(0, -1);
        if (kb.WasKeyPressed("ArrowDown")) MoveFocusDirectional(0, 1);
        if (kb.WasKeyPressed("ArrowLeft")) MoveFocusDirectional(-1, 0);
        if (kb.WasKeyPressed("ArrowRight")) MoveFocusDirectional(1, 0);

        // Gamepad D-pad
        if (input.Gamepad.Connected)
        {
            if (input.Gamepad.WasButtonPressed(12)) MoveFocusDirectional(0, -1); // D-pad up
            if (input.Gamepad.WasButtonPressed(13)) MoveFocusDirectional(0, 1);  // D-pad down
            if (input.Gamepad.WasButtonPressed(14)) MoveFocusDirectional(-1, 0); // D-pad left
            if (input.Gamepad.WasButtonPressed(15)) MoveFocusDirectional(1, 0);  // D-pad right
        }

        // Escape to unfocus
        if (kb.WasKeyPressed("Escape"))
            Blur();
    }

    /// <summary>Move focus by offset (positive = forward, negative = backward).</summary>
    public void MoveFocus(int direction)
    {
        if (_focusableElements.Count == 0) return;

        if (_focusIndex < 0)
            _focusIndex = direction > 0 ? 0 : _focusableElements.Count - 1;
        else
            _focusIndex = ((_focusIndex + direction) % _focusableElements.Count + _focusableElements.Count) % _focusableElements.Count;
    }

    /// <summary>Move focus to the nearest element in the given direction.</summary>
    private void MoveFocusDirectional(int dx, int dy)
    {
        if (_focusableElements.Count == 0) return;

        var current = FocusedElement;
        if (current == null) { _focusIndex = 0; return; }

        var currentBounds = current.ScreenBounds;
        float currentCX = currentBounds.X + currentBounds.Width / 2;
        float currentCY = currentBounds.Y + currentBounds.Height / 2;

        float bestScore = float.MaxValue;
        int bestIndex = -1;

        for (int i = 0; i < _focusableElements.Count; i++)
        {
            if (i == _focusIndex) continue;
            var el = _focusableElements[i];
            if (!el.Visible || !el.Enabled) continue;

            var bounds = el.ScreenBounds;
            float cx = bounds.X + bounds.Width / 2;
            float cy = bounds.Y + bounds.Height / 2;

            float ddx = cx - currentCX;
            float ddy = cy - currentCY;

            // Check if element is in the desired direction
            bool inDirection = false;
            if (dx > 0 && ddx > 10) inDirection = true;
            if (dx < 0 && ddx < -10) inDirection = true;
            if (dy > 0 && ddy > 10) inDirection = true;
            if (dy < 0 && ddy < -10) inDirection = true;

            if (!inDirection) continue;

            // Score: prefer elements aligned on the perpendicular axis
            float mainDist = dx != 0 ? MathF.Abs(ddx) : MathF.Abs(ddy);
            float crossDist = dx != 0 ? MathF.Abs(ddy) : MathF.Abs(ddx);
            float score = mainDist + crossDist * 3f; // penalize off-axis elements

            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        if (bestIndex >= 0)
            _focusIndex = bestIndex;
    }

    private void RebuildIfDirty()
    {
        if (!_dirty || _root == null) return;
        _dirty = false;
        _focusableElements.Clear();
        CollectFocusable(_root);
    }

    private void CollectFocusable(UIElement element)
    {
        if (!element.Visible || !element.Enabled) return;

        // Elements that are interactive are focusable
        if (IsFocusable(element))
            _focusableElements.Add(element);

        foreach (var child in element.Children)
            CollectFocusable(child);
    }

    private static bool IsFocusable(UIElement element)
    {
        // Focusable element types
        return element is Elements.UIButton
            || element is Elements.UICheckbox
            || element is Elements.UIToggle
            || element is Elements.UISlider
            || element is Elements.UITextInput
            || element is Elements.UIDropdown;
    }
}
