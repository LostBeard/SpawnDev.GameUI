using System.Drawing;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Editable text input field with cursor and selection.
/// Handles keyboard input (typing, backspace, delete, arrows, home/end).
/// Supports placeholder text, max length, and submit callback (Enter key).
/// Built for chat input, search bars, console commands, player name entry.
/// </summary>
public class UITextInput : UIElement
{
    private string _text = "";
    private int _cursorPos;
    private float _cursorBlink;
    private bool _isFocused;

    /// <summary>Current text content.</summary>
    public string Text
    {
        get => _text;
        set { _text = value ?? ""; _cursorPos = Math.Min(_cursorPos, _text.Length); }
    }

    /// <summary>Placeholder text shown when empty and unfocused.</summary>
    public string Placeholder { get; set; } = "";

    /// <summary>Maximum character length. 0 = unlimited.</summary>
    public int MaxLength { get; set; } = 0;

    /// <summary>Font size for the input text.</summary>
    public FontSize FontSize { get; set; } = FontSize.Body;

    /// <summary>Whether the input currently has keyboard focus.</summary>
    public bool IsFocused => _isFocused;

    /// <summary>Called when Enter is pressed.</summary>
    public Action<string>? OnSubmit { get; set; }

    /// <summary>Called when text changes.</summary>
    public Action<string>? OnChanged { get; set; }

    // Theme-aware colors
    private Color? _bgColor, _textColor, _placeholderColor, _cursorColor, _focusBorderColor;
    public Color BackgroundColor { get => _bgColor ?? Color.FromArgb(220, 15, 15, 25); set => _bgColor = value; }
    public Color TextColor { get => _textColor ?? UITheme.Current.TextPrimary; set => _textColor = value; }
    public Color PlaceholderColor { get => _placeholderColor ?? UITheme.Current.TextMuted; set => _placeholderColor = value; }
    public Color CursorColor { get => _cursorColor ?? UITheme.Current.FocusBorder; set => _cursorColor = value; }
    public Color FocusBorderColor { get => _focusBorderColor ?? UITheme.Current.FocusBorder; set => _focusBorderColor = value; }

    private const float PadX = 8f;
    private const float PadY = 4f;

    public override void Update(GameInput input, float dt)
    {
        if (!Visible || !Enabled) return;

        // Click to focus/unfocus
        foreach (var pointer in input.Pointers)
        {
            if (pointer.WasPressed && pointer.ScreenPosition.HasValue)
            {
                var bounds = ScreenBounds;
                var mp = pointer.ScreenPosition.Value;
                bool hit = mp.X >= bounds.X && mp.X < bounds.X + bounds.Width &&
                           mp.Y >= bounds.Y && mp.Y < bounds.Y + bounds.Height;
                _isFocused = hit;

                if (hit)
                {
                    // Place cursor near click position
                    float localX = mp.X - bounds.X - PadX;
                    _cursorPos = TextPositionFromX(input, localX);
                }
            }
        }

        if (!_isFocused) return;

        // Blink cursor
        _cursorBlink += dt;
        if (_cursorBlink > 1f) _cursorBlink -= 1f;

        // Handle keyboard input
        var kb = input.Keyboard;

        // Text input (printable characters)
        if (!string.IsNullOrEmpty(kb.TextInput))
        {
            foreach (char c in kb.TextInput)
            {
                if (MaxLength > 0 && _text.Length >= MaxLength) break;
                _text = _text.Insert(_cursorPos, c.ToString());
                _cursorPos++;
            }
            OnChanged?.Invoke(_text);
            _cursorBlink = 0;
        }

        // Backspace
        if (kb.WasKeyPressed("Backspace") && _cursorPos > 0)
        {
            _text = _text.Remove(_cursorPos - 1, 1);
            _cursorPos--;
            OnChanged?.Invoke(_text);
            _cursorBlink = 0;
        }

        // Delete
        if (kb.WasKeyPressed("Delete") && _cursorPos < _text.Length)
        {
            _text = _text.Remove(_cursorPos, 1);
            OnChanged?.Invoke(_text);
            _cursorBlink = 0;
        }

        // Arrow keys
        if (kb.WasKeyPressed("ArrowLeft") && _cursorPos > 0)
        { _cursorPos--; _cursorBlink = 0; }
        if (kb.WasKeyPressed("ArrowRight") && _cursorPos < _text.Length)
        { _cursorPos++; _cursorBlink = 0; }

        // Home/End
        if (kb.WasKeyPressed("Home")) { _cursorPos = 0; _cursorBlink = 0; }
        if (kb.WasKeyPressed("End")) { _cursorPos = _text.Length; _cursorBlink = 0; }

        // Enter = submit
        if (kb.WasKeyPressed("Enter"))
        {
            OnSubmit?.Invoke(_text);
        }

        // Escape = unfocus
        if (kb.WasKeyPressed("Escape"))
        {
            _isFocused = false;
        }

        base.Update(input, dt);
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        var bounds = ScreenBounds;

        // Background
        renderer.DrawRect(bounds.X, bounds.Y, bounds.Width, bounds.Height, BackgroundColor);

        // Focus border
        if (_isFocused)
        {
            float bw = UITheme.Current.FocusBorderWidth;
            renderer.DrawRect(bounds.X - bw, bounds.Y - bw,
                              bounds.Width + bw * 2, bounds.Height + bw * 2,
                              FocusBorderColor);
            renderer.DrawRect(bounds.X, bounds.Y, bounds.Width, bounds.Height, BackgroundColor);
        }

        // Text or placeholder
        float textY = bounds.Y + (bounds.Height - renderer.GetLineHeight(FontSize)) / 2f;
        if (string.IsNullOrEmpty(_text) && !_isFocused)
        {
            renderer.DrawText(Placeholder, bounds.X + PadX, textY, FontSize, PlaceholderColor);
        }
        else
        {
            renderer.DrawText(_text, bounds.X + PadX, textY, FontSize, TextColor);
        }

        // Cursor (blink every 0.5s)
        if (_isFocused && _cursorBlink < 0.5f)
        {
            string beforeCursor = _text.Substring(0, _cursorPos);
            float cursorX = bounds.X + PadX + renderer.MeasureText(beforeCursor, FontSize);
            float lineH = renderer.GetLineHeight(FontSize);
            renderer.DrawRect(cursorX, textY, 2, lineH, CursorColor);
        }

        base.Draw(renderer);
    }

    private int TextPositionFromX(GameInput input, float localX)
    {
        // Binary search would be better but this is simple and correct
        if (string.IsNullOrEmpty(_text)) return 0;
        // Approximate: walk characters and find where localX falls
        // This is a rough approximation without the renderer - will improve with cached metrics
        float charWidth = (int)FontSize * 0.6f; // rough average
        int pos = (int)(localX / charWidth);
        return Math.Clamp(pos, 0, _text.Length);
    }

    /// <summary>Focus this input programmatically.</summary>
    public void Focus() { _isFocused = true; _cursorBlink = 0; }

    /// <summary>Unfocus this input.</summary>
    public void Blur() => _isFocused = false;

    /// <summary>Select all text and place cursor at end.</summary>
    public void SelectAll() => _cursorPos = _text.Length;
}
