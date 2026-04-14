namespace SpawnDev.GameUI.Animation;

/// <summary>
/// Fluent animation extension methods for UIElement.
/// Enable natural animation syntax:
///
///   panel.FadeIn(0.3f);
///   button.SlideIn(SlideDirection.Left, 200, 0.4f, EasingType.EaseOutBack);
///   toast.FadeOut(0.5f, onComplete: () => toast.Parent?.RemoveChild(toast));
///   healthBar.AnimateWidth(newWidth, 0.2f);
/// </summary>
public static class UIAnimationExtensions
{
    /// <summary>Fade element from invisible to fully visible.</summary>
    public static int FadeIn(this UIElement element, float duration = 0.3f,
        EasingType easing = EasingType.EaseOut, float delay = 0f)
    {
        element.Visible = true;
        element.Opacity = 0;
        return TweenManager.Global.Start(v => element.Opacity = v, 0, 1, duration, easing, delay);
    }

    /// <summary>Fade element from visible to invisible, then hide.</summary>
    public static int FadeOut(this UIElement element, float duration = 0.3f,
        EasingType easing = EasingType.EaseIn, float delay = 0f, Action? onComplete = null)
    {
        return TweenManager.Global.Start(v => element.Opacity = v, element.Opacity, 0, duration, easing, delay,
            onComplete: () => { element.Visible = false; onComplete?.Invoke(); });
    }

    /// <summary>Slide element in from a direction.</summary>
    public static int SlideIn(this UIElement element, SlideDirection direction, float distance,
        float duration = 0.4f, EasingType easing = EasingType.EaseOutBack)
    {
        element.Visible = true;
        float targetX = element.X, targetY = element.Y;

        return direction switch
        {
            SlideDirection.Left => TweenManager.Global.Start(
                v => element.X = v, targetX - distance, targetX, duration, easing),
            SlideDirection.Right => TweenManager.Global.Start(
                v => element.X = v, targetX + distance, targetX, duration, easing),
            SlideDirection.Up => TweenManager.Global.Start(
                v => element.Y = v, targetY - distance, targetY, duration, easing),
            SlideDirection.Down => TweenManager.Global.Start(
                v => element.Y = v, targetY + distance, targetY, duration, easing),
            _ => -1,
        };
    }

    /// <summary>Slide element out to a direction, then hide.</summary>
    public static int SlideOut(this UIElement element, SlideDirection direction, float distance,
        float duration = 0.3f, EasingType easing = EasingType.EaseIn, Action? onComplete = null)
    {
        float startX = element.X, startY = element.Y;

        return direction switch
        {
            SlideDirection.Left => TweenManager.Global.Start(
                v => element.X = v, startX, startX - distance, duration, easing,
                onComplete: () => { element.Visible = false; element.X = startX; onComplete?.Invoke(); }),
            SlideDirection.Right => TweenManager.Global.Start(
                v => element.X = v, startX, startX + distance, duration, easing,
                onComplete: () => { element.Visible = false; element.X = startX; onComplete?.Invoke(); }),
            SlideDirection.Up => TweenManager.Global.Start(
                v => element.Y = v, startY, startY - distance, duration, easing,
                onComplete: () => { element.Visible = false; element.Y = startY; onComplete?.Invoke(); }),
            SlideDirection.Down => TweenManager.Global.Start(
                v => element.Y = v, startY, startY + distance, duration, easing,
                onComplete: () => { element.Visible = false; element.Y = startY; onComplete?.Invoke(); }),
            _ => -1,
        };
    }

    /// <summary>Animate element's X position.</summary>
    public static int AnimateX(this UIElement element, float to, float duration = 0.3f,
        EasingType easing = EasingType.EaseOut)
    {
        return TweenManager.Global.Start(v => element.X = v, element.X, to, duration, easing);
    }

    /// <summary>Animate element's Y position.</summary>
    public static int AnimateY(this UIElement element, float to, float duration = 0.3f,
        EasingType easing = EasingType.EaseOut)
    {
        return TweenManager.Global.Start(v => element.Y = v, element.Y, to, duration, easing);
    }

    /// <summary>Animate element's width.</summary>
    public static int AnimateWidth(this UIElement element, float to, float duration = 0.3f,
        EasingType easing = EasingType.EaseOut)
    {
        return TweenManager.Global.Start(v => element.Width = v, element.Width, to, duration, easing);
    }

    /// <summary>Animate element's height.</summary>
    public static int AnimateHeight(this UIElement element, float to, float duration = 0.3f,
        EasingType easing = EasingType.EaseOut)
    {
        return TweenManager.Global.Start(v => element.Height = v, element.Height, to, duration, easing);
    }

    /// <summary>Animate element's opacity.</summary>
    public static int AnimateOpacity(this UIElement element, float to, float duration = 0.3f,
        EasingType easing = EasingType.EaseOut)
    {
        return TweenManager.Global.Start(v => element.Opacity = v, element.Opacity, to, duration, easing);
    }

    /// <summary>Pulse effect: scale up then back to normal. Good for pickup notifications.</summary>
    public static int Pulse(this UIElement element, float scaleAmount = 10f, float duration = 0.4f)
    {
        float startW = element.Width, startH = element.Height;
        float startX = element.X, startY = element.Y;

        // Scale up
        return TweenManager.Global.Start(v =>
        {
            float scale = 1f + scaleAmount * v / 100f;
            element.Width = startW * scale;
            element.Height = startH * scale;
            element.X = startX - (element.Width - startW) / 2;
            element.Y = startY - (element.Height - startH) / 2;
        }, 0, 1, duration / 2, EasingType.EaseOut,
        onComplete: () =>
        {
            // Scale back down
            TweenManager.Global.Start(v =>
            {
                float scale = 1f + scaleAmount * (1 - v) / 100f;
                element.Width = startW * scale;
                element.Height = startH * scale;
                element.X = startX - (element.Width - startW) / 2;
                element.Y = startY - (element.Height - startH) / 2;
            }, 0, 1, duration / 2, EasingType.EaseIn);
        });
    }
}

/// <summary>Direction for slide animations.</summary>
public enum SlideDirection
{
    Left, Right, Up, Down
}
