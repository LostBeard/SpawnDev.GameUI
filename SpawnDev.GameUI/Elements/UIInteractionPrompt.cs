using System.Drawing;
using SpawnDev.GameUI.Animation;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// "Press F to interact" prompt that appears near the crosshair when
/// looking at an interactive object. Supports hold-to-complete actions.
///
/// Usage:
///   var prompt = new UIInteractionPrompt();
///   root.AddAnchored(prompt, Anchor.Center, offsetY: 50);
///
///   // When looking at a door:
///   prompt.Show("Open Door", "F");
///
///   // When looking at a loot container (hold action):
///   prompt.ShowHold("Search Body", "E", holdDuration: 3f);
///
///   // Per frame while holding:
///   if (input.Keyboard.IsKeyDown("KeyE"))
///       prompt.UpdateHold(dt);
///
///   // When looking away:
///   prompt.Hide();
/// </summary>
public class UIInteractionPrompt : UIElement
{
    private string _actionText = "";
    private string _keyText = "";
    private bool _isHoldAction;
    private float _holdDuration;
    private float _holdProgress;
    private bool _isVisible;
    private float _fadeProgress; // 0 = hidden, 1 = fully visible

    /// <summary>Called when a hold action completes.</summary>
    public Action? OnHoldComplete { get; set; }

    /// <summary>Called when a press action fires.</summary>
    public Action? OnPress { get; set; }

    // Theme-aware colors
    private Color? _bgColor, _textColor, _keyBgColor, _keyTextColor, _holdBarColor;
    public Color BackgroundColor { get => _bgColor ?? Color.FromArgb(180, 15, 15, 25); set => _bgColor = value; }
    public Color TextColor { get => _textColor ?? UITheme.Current.TextPrimary; set => _textColor = value; }
    public Color KeyBgColor { get => _keyBgColor ?? Color.FromArgb(220, 50, 50, 65); set => _keyBgColor = value; }
    public Color KeyTextColor { get => _keyTextColor ?? Color.White; set => _keyTextColor = value; }
    public Color HoldBarColor { get => _holdBarColor ?? UITheme.Current.ButtonNormal; set => _holdBarColor = value; }

    /// <summary>Show an instant-press prompt.</summary>
    public void Show(string action, string key)
    {
        _actionText = action;
        _keyText = key;
        _isHoldAction = false;
        _holdProgress = 0;
        _isVisible = true;
        Visible = true;
    }

    /// <summary>Show a hold-to-complete prompt.</summary>
    public void ShowHold(string action, string key, float holdDuration = 2f)
    {
        _actionText = action;
        _keyText = key;
        _isHoldAction = true;
        _holdDuration = holdDuration;
        _holdProgress = 0;
        _isVisible = true;
        Visible = true;
    }

    /// <summary>Hide the prompt.</summary>
    public void Hide()
    {
        _isVisible = false;
        _holdProgress = 0;
    }

    /// <summary>Update hold progress. Call per frame while the key is held.</summary>
    public bool UpdateHold(float dt)
    {
        if (!_isHoldAction || !_isVisible) return false;

        _holdProgress += dt / _holdDuration;
        if (_holdProgress >= 1f)
        {
            _holdProgress = 1f;
            OnHoldComplete?.Invoke();
            Hide();
            return true; // completed
        }
        return false;
    }

    /// <summary>Reset hold progress (key released before completion).</summary>
    public void ResetHold() => _holdProgress = 0;

    public override void Update(Input.GameInput input, float dt)
    {
        if (!Visible) return;

        // Smooth fade in/out
        float targetFade = _isVisible ? 1f : 0f;
        _fadeProgress += (targetFade - _fadeProgress) * Math.Min(1f, dt * 8f);

        if (_fadeProgress < 0.01f && !_isVisible)
            Visible = false;
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible || _fadeProgress < 0.01f) return;

        var bounds = ScreenBounds;
        float alpha = _fadeProgress;

        // Measure content
        float keyBoxW = 28f;
        float keyBoxH = 24f;
        float actionW = renderer.MeasureText(_actionText, FontSize.Body);
        float gap = 8f;
        float totalW = keyBoxW + gap + actionW + 20;
        float totalH = _isHoldAction ? 44f : 30f;

        Width = totalW;
        Height = totalH;

        float cx = bounds.X; // already anchored by parent
        float cy = bounds.Y;

        // Background
        renderer.DrawRect(cx, cy, totalW, totalH,
            Color.FromArgb((int)(BackgroundColor.A * alpha), BackgroundColor.R, BackgroundColor.G, BackgroundColor.B));

        // Key box
        float keyX = cx + 8;
        float keyY = cy + 4;
        renderer.DrawRect(keyX, keyY, keyBoxW, keyBoxH,
            Color.FromArgb((int)(KeyBgColor.A * alpha), KeyBgColor.R, KeyBgColor.G, KeyBgColor.B));

        // Key letter (centered in box)
        float ktw = renderer.MeasureText(_keyText, FontSize.Body);
        float kth = renderer.GetLineHeight(FontSize.Body);
        renderer.DrawText(_keyText, keyX + (keyBoxW - ktw) / 2, keyY + (keyBoxH - kth) / 2,
            FontSize.Body,
            Color.FromArgb((int)(255 * alpha), KeyTextColor.R, KeyTextColor.G, KeyTextColor.B));

        // Action text
        float textX = keyX + keyBoxW + gap;
        float textY = cy + ((_isHoldAction ? 28 : totalH) - renderer.GetLineHeight(FontSize.Body)) / 2;
        string prefix = _isHoldAction ? "Hold " : "Press ";
        renderer.DrawText(prefix, textX, textY, FontSize.Body,
            Color.FromArgb((int)(180 * alpha), TextColor.R, TextColor.G, TextColor.B));
        float prefixW = renderer.MeasureText(prefix, FontSize.Body);
        renderer.DrawText(_actionText, textX + prefixW, textY, FontSize.Body,
            Color.FromArgb((int)(255 * alpha), TextColor.R, TextColor.G, TextColor.B));

        // Hold progress bar
        if (_isHoldAction)
        {
            float barY = cy + totalH - 8;
            float barW = totalW - 16;
            renderer.DrawRect(cx + 8, barY, barW, 4,
                Color.FromArgb((int)(60 * alpha), 255, 255, 255));
            if (_holdProgress > 0)
            {
                renderer.DrawRect(cx + 8, barY, barW * _holdProgress, 4,
                    Color.FromArgb((int)(HoldBarColor.A * alpha), HoldBarColor.R, HoldBarColor.G, HoldBarColor.B));
            }
        }
    }
}
