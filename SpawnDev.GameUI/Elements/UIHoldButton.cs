using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// A button for press-and-hold (a slew pad, a push-to-talk key, a charge-up): <see cref="OnHold"/> fires when a pointer goes down ON the button and
/// <see cref="OnRelease"/> when it comes up, slides off the button, or the button is hidden or disabled. A press that began somewhere else and slid
/// onto the button does not count, so dragging a map across a pad never triggers it. Anything that must stop when "released" can rely on
/// <see cref="OnRelease"/> being called exactly once per <see cref="OnHold"/>.
/// </summary>
public class UIHoldButton : UIButton
{
    /// <summary>Called when the button is pressed.</summary>
    public Action? OnHold { get; set; }

    /// <summary>Called when the hold ends, for whatever reason.</summary>
    public Action? OnRelease { get; set; }

    /// <summary>True from the press until the release.</summary>
    public bool Holding { get; private set; }

    public override void Update(GameInput input, float dt)
    {
        base.Update(input, dt);
        if (!Holding)
        {
            if (!Visible || !Enabled) return;
            var bounds = ScreenBounds;
            foreach (var p in input.Pointers)
            {
                if (!p.WasPressed || !p.ScreenPosition.HasValue) continue;
                var at = p.ScreenPosition.Value;
                if (at.X >= bounds.X && at.X < bounds.X + bounds.Width && at.Y >= bounds.Y && at.Y < bounds.Y + bounds.Height)
                {
                    Holding = true;
                    OnHold?.Invoke();
                    return;
                }
            }
        }
        else if (!Visible || !Enabled || !IsPressed)
        {
            Release();
        }
    }

    /// <summary>Ends a hold from outside (the window lost focus, the screen is going away). Does nothing when not held.</summary>
    public void Release()
    {
        if (!Holding) return;
        Holding = false;
        OnRelease?.Invoke();
    }
}
