using System.Drawing;
using SpawnDev.GameUI.Animation;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Stack of animated toast notifications. New notifications slide in from the right,
/// older ones push up, expired ones fade out. Auto-removes after timeout.
///
/// Usage:
///   var notifications = new UINotificationStack { Width = 300 };
///   root.AddAnchored(notifications, Anchor.TopRight, offsetX: -20, offsetY: 80);
///
///   // In gameplay:
///   notifications.Push("Picked up: Iron Axe");
///   notifications.Push("Achievement: First Kill", NotificationType.Achievement);
///   notifications.Push("-25 HP", NotificationType.Damage);
///   notifications.Push("+50 XP", NotificationType.Success);
/// </summary>
public class UINotificationStack : UIElement
{
    private readonly List<Notification> _notifications = new();
    private int _nextId;

    /// <summary>Max visible notifications. Oldest are removed when exceeded.</summary>
    public int MaxVisible { get; set; } = 5;

    /// <summary>Default display duration in seconds.</summary>
    public float DefaultDuration { get; set; } = 4f;

    /// <summary>Height of each notification.</summary>
    public float NotificationHeight { get; set; } = 36f;

    /// <summary>Gap between notifications.</summary>
    public float NotificationGap { get; set; } = 4f;

    /// <summary>Slide-in animation duration.</summary>
    public float SlideInDuration { get; set; } = 0.3f;

    /// <summary>Fade-out animation duration.</summary>
    public float FadeOutDuration { get; set; } = 0.5f;

    /// <summary>Push a new notification.</summary>
    public void Push(string text, NotificationType type = NotificationType.Info, float? duration = null)
    {
        var notification = new Notification
        {
            Id = _nextId++,
            Text = text,
            Type = type,
            RemainingTime = duration ?? DefaultDuration,
            SlideProgress = 0f, // 0 = off-screen right, 1 = fully visible
            FadeProgress = 1f,  // 1 = fully visible, 0 = invisible
            IsNew = true,
        };

        _notifications.Insert(0, notification); // newest at top

        // Remove excess
        while (_notifications.Count > MaxVisible + 2) // +2 for fading out
            _notifications.RemoveAt(_notifications.Count - 1);
    }

    /// <summary>Clear all notifications immediately.</summary>
    public void Clear() => _notifications.Clear();

    public override void Update(Input.GameInput input, float dt)
    {
        if (!Visible) return;

        for (int i = _notifications.Count - 1; i >= 0; i--)
        {
            var n = _notifications[i];

            // Slide in
            if (n.SlideProgress < 1f)
            {
                n.SlideProgress = Math.Min(1f, n.SlideProgress + dt / SlideInDuration);
                n.SlideProgress = Easing.Apply(EasingType.EaseOutBack, Math.Min(1f, n.SlideProgress / 1f) * 1f);
                // Re-apply raw progress for smooth easing
                if (n.IsNew)
                {
                    n.RawSlide += dt / SlideInDuration;
                    n.SlideProgress = Math.Min(1f, Easing.Apply(EasingType.EaseOut, Math.Min(1f, n.RawSlide)));
                    if (n.RawSlide >= 1f) n.IsNew = false;
                }
            }

            // Count down
            n.RemainingTime -= dt;

            // Fade out when expiring
            if (n.RemainingTime <= FadeOutDuration)
            {
                n.FadeProgress = Math.Max(0f, n.RemainingTime / FadeOutDuration);
            }

            // Remove fully faded
            if (n.RemainingTime <= 0)
            {
                _notifications.RemoveAt(i);
                continue;
            }

            _notifications[i] = n;
        }
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible || _notifications.Count == 0) return;

        var bounds = ScreenBounds;
        float y = bounds.Y;

        for (int i = 0; i < _notifications.Count && i < MaxVisible; i++)
        {
            var n = _notifications[i];

            // Slide from right
            float slideOffset = (1f - n.SlideProgress) * (Width + 20);
            float nx = bounds.X + slideOffset;
            float ny = y;

            // Get colors for notification type
            var (bgColor, textColor, accentColor) = GetColors(n.Type);

            // Apply fade
            int alpha = (int)(bgColor.A * n.FadeProgress);
            bgColor = Color.FromArgb(alpha, bgColor.R, bgColor.G, bgColor.B);
            textColor = Color.FromArgb((int)(255 * n.FadeProgress), textColor.R, textColor.G, textColor.B);
            accentColor = Color.FromArgb((int)(accentColor.A * n.FadeProgress), accentColor.R, accentColor.G, accentColor.B);

            // Background
            renderer.DrawRect(nx, ny, Width, NotificationHeight, bgColor);

            // Left accent bar
            renderer.DrawRect(nx, ny, 3, NotificationHeight, accentColor);

            // Text
            float textY = ny + (NotificationHeight - renderer.GetLineHeight(FontSize.Caption)) / 2f;
            renderer.DrawText(n.Text, nx + 10, textY, FontSize.Caption, textColor);

            y += NotificationHeight + NotificationGap;
        }

        Height = y - bounds.Y;
    }

    private static (Color bg, Color text, Color accent) GetColors(NotificationType type) => type switch
    {
        NotificationType.Info => (
            Color.FromArgb(200, 25, 25, 40),
            Color.FromArgb(255, 220, 220, 240),
            Color.FromArgb(255, 100, 140, 220)),
        NotificationType.Success => (
            Color.FromArgb(200, 20, 40, 25),
            Color.FromArgb(255, 180, 240, 180),
            Color.FromArgb(255, 80, 200, 100)),
        NotificationType.Warning => (
            Color.FromArgb(200, 45, 35, 15),
            Color.FromArgb(255, 240, 220, 160),
            Color.FromArgb(255, 220, 180, 50)),
        NotificationType.Damage => (
            Color.FromArgb(200, 45, 15, 15),
            Color.FromArgb(255, 240, 160, 160),
            Color.FromArgb(255, 220, 60, 60)),
        NotificationType.Achievement => (
            Color.FromArgb(200, 35, 20, 45),
            Color.FromArgb(255, 220, 200, 255),
            Color.FromArgb(255, 160, 100, 240)),
        _ => (
            Color.FromArgb(200, 25, 25, 40),
            Color.White,
            Color.Gray),
    };

    private struct Notification
    {
        public int Id;
        public string Text;
        public NotificationType Type;
        public float RemainingTime;
        public float SlideProgress;
        public float FadeProgress;
        public float RawSlide;
        public bool IsNew;
    }
}

/// <summary>Notification visual style.</summary>
public enum NotificationType
{
    /// <summary>General info (blue accent).</summary>
    Info,
    /// <summary>Positive event - XP gain, item pickup (green accent).</summary>
    Success,
    /// <summary>Warning - low ammo, approaching danger (yellow accent).</summary>
    Warning,
    /// <summary>Damage taken, death, loss (red accent).</summary>
    Damage,
    /// <summary>Achievement unlocked, milestone (purple accent).</summary>
    Achievement,
}
