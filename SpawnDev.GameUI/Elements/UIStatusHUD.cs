using System.Drawing;
using SpawnDev.GameUI.Animation;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Composite HUD element for survival game status bars.
/// Combines health, stamina, hunger, thirst, and temperature into
/// a compact vertical or horizontal layout with threshold colors
/// and damage flash animation.
///
/// Designed for Lost Spawns (DayZ-style survival) but usable in
/// any game with status tracking.
///
/// Usage:
///   var hud = new UIStatusHUD { Width = 200 };
///   hud.Health = 0.75f;
///   hud.Stamina = 0.5f;
///   hud.Hunger = 0.3f;  // turns orange
///   hud.Thirst = 0.08f; // turns red (critical)
///   root.AddAnchored(hud, Anchor.BottomLeft, offsetX: 20, offsetY: -20);
///
///   // On damage:
///   hud.FlashDamage();
/// </summary>
public class UIStatusHUD : UIFlexPanel
{
    private readonly UIProgressBar _healthBar;
    private readonly UIProgressBar _staminaBar;
    private readonly UIProgressBar _hungerBar;
    private readonly UIProgressBar _thirstBar;
    private readonly UIProgressBar _tempBar;

    private float _health = 1f;
    private float _stamina = 1f;
    private float _hunger = 1f;
    private float _thirst = 1f;
    private float _temperature = 0.5f; // 0=freezing, 0.5=comfortable, 1=overheating

    /// <summary>Health 0-1.</summary>
    public float Health
    {
        get => _health;
        set { _health = Math.Clamp(value, 0, 1); _healthBar.Value = _health; }
    }

    /// <summary>Stamina 0-1.</summary>
    public float Stamina
    {
        get => _stamina;
        set { _stamina = Math.Clamp(value, 0, 1); _staminaBar.Value = _stamina; }
    }

    /// <summary>Hunger 0-1 (1 = full, 0 = starving).</summary>
    public float Hunger
    {
        get => _hunger;
        set { _hunger = Math.Clamp(value, 0, 1); _hungerBar.Value = _hunger; }
    }

    /// <summary>Thirst 0-1 (1 = full, 0 = dehydrated).</summary>
    public float Thirst
    {
        get => _thirst;
        set { _thirst = Math.Clamp(value, 0, 1); _thirstBar.Value = _thirst; }
    }

    /// <summary>Temperature 0-1 (0=freezing, 0.5=comfortable, 1=overheating).</summary>
    public float Temperature
    {
        get => _temperature;
        set { _temperature = Math.Clamp(value, 0, 1); _tempBar.Value = _temperature; }
    }

    /// <summary>Whether to show the temperature bar.</summary>
    public bool ShowTemperature { get; set; } = true;

    /// <summary>Bar height in pixels.</summary>
    public float BarHeight { get; set; } = 16f;

    public UIStatusHUD()
    {
        Direction = FlexDirection.Column;
        Gap = 4;
        Padding = 8;
        BackgroundColor = Color.FromArgb(160, 10, 10, 15);
        Width = 200;

        _healthBar = new UIProgressBar
        {
            Height = 18,
            Label = "HP",
            ShowPercentage = true,
            FillColor = Color.FromArgb(255, 200, 50, 50),
            LowColor = Color.FromArgb(255, 180, 80, 30),
            CriticalColor = Color.FromArgb(255, 200, 30, 30),
            LowThreshold = 0.35f,
            CriticalThreshold = 0.15f,
        };

        _staminaBar = new UIProgressBar
        {
            Height = 14,
            Label = "STA",
            FillColor = Color.FromArgb(255, 80, 180, 80),
            LowColor = Color.FromArgb(255, 180, 180, 50),
            CriticalColor = Color.FromArgb(255, 180, 80, 30),
        };

        _hungerBar = new UIProgressBar
        {
            Height = 12,
            Label = "Food",
            FillColor = Color.FromArgb(255, 180, 140, 60),
            LowColor = Color.FromArgb(255, 200, 120, 30),
            CriticalColor = Color.FromArgb(255, 200, 50, 30),
        };

        _thirstBar = new UIProgressBar
        {
            Height = 12,
            Label = "Water",
            FillColor = Color.FromArgb(255, 60, 140, 200),
            LowColor = Color.FromArgb(255, 200, 140, 40),
            CriticalColor = Color.FromArgb(255, 200, 50, 30),
        };

        _tempBar = new UIProgressBar
        {
            Height = 10,
            Label = "Temp",
            FillColor = Color.FromArgb(255, 120, 180, 120), // comfortable = green
            MinValue = 0,
            MaxValue = 1,
            Value = 0.5f,
        };

        AddChild(_healthBar);
        AddChild(_staminaBar);
        AddChild(new UISeparator { Height = 4 });
        AddChild(_hungerBar);
        AddChild(_thirstBar);
        AddChild(_tempBar);
    }

    public override void Draw(UIRenderer renderer)
    {
        // Update bar widths to match panel width
        float barW = Width - Padding * 2;
        _healthBar.Width = barW;
        _staminaBar.Width = barW;
        _hungerBar.Width = barW;
        _thirstBar.Width = barW;
        _tempBar.Width = barW;
        _tempBar.Visible = ShowTemperature;

        // Temperature color: blue (cold) -> green (comfortable) -> red (hot)
        if (ShowTemperature)
        {
            _tempBar.FillColor = _temperature < 0.3f
                ? Color.FromArgb(255, 60, 120, 220)  // cold = blue
                : _temperature > 0.7f
                    ? Color.FromArgb(255, 220, 80, 40) // hot = red/orange
                    : Color.FromArgb(255, 80, 180, 80); // comfortable = green
        }

        base.Draw(renderer);
    }

    /// <summary>Flash the health bar red briefly (call on taking damage).</summary>
    public void FlashDamage()
    {
        var originalBg = BackgroundColor;
        BackgroundColor = Color.FromArgb(200, 180, 30, 30);
        TweenManager.Global.Start(_ =>
        {
            // Tween doesn't directly support Color, so we use a timer approach
        }, 0, 1, 0.3f, EasingType.EaseOut,
        onComplete: () => BackgroundColor = originalBg);
    }
}
