using SpawnDev.GameUI.Animation;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI;

/// <summary>
/// Manages a stack of UI screens with transitions.
/// Only the top screen receives input. Screens below can optionally render
/// as a dimmed background (for pause menus over gameplay HUD).
///
/// Usage:
///   var screens = new UIScreenManager(viewportW, viewportH);
///   screens.Register("hud", BuildHudScreen());
///   screens.Register("inventory", BuildInventoryScreen());
///   screens.Register("pause", BuildPauseScreen());
///   screens.Register("settings", BuildSettingsScreen());
///
///   screens.Push("hud"); // initial screen
///
///   // Player presses I:
///   screens.Push("inventory"); // inventory slides in over HUD
///
///   // Player presses Escape:
///   screens.Pop(); // back to HUD
///
///   // Player presses Escape again:
///   screens.Push("pause"); // pause menu over HUD
///
///   // Per frame:
///   screens.Update(gameInput, dt);
///   renderer.Begin(w, h);
///   screens.Draw(renderer);
///   renderer.End(encoder, view);
/// </summary>
public class UIScreenManager
{
    private readonly Dictionary<string, UIElement> _screens = new();
    private readonly List<ScreenEntry> _stack = new();
    private float _viewportWidth;
    private float _viewportHeight;

    /// <summary>Whether to dim screens below the top screen.</summary>
    public bool DimBackground { get; set; } = true;

    /// <summary>Dim overlay color.</summary>
    public System.Drawing.Color DimColor { get; set; } = System.Drawing.Color.FromArgb(120, 0, 0, 0);

    /// <summary>Whether to render screens below the top (true = stack visible, false = only top).</summary>
    public bool RenderStack { get; set; } = true;

    /// <summary>Animation duration for push/pop transitions.</summary>
    public float TransitionDuration { get; set; } = 0.3f;

    /// <summary>The currently active (top) screen name, or null.</summary>
    public string? ActiveScreen => _stack.Count > 0 ? _stack[^1].Name : null;

    /// <summary>Number of screens on the stack.</summary>
    public int StackDepth => _stack.Count;

    public UIScreenManager(float viewportWidth, float viewportHeight)
    {
        _viewportWidth = viewportWidth;
        _viewportHeight = viewportHeight;
    }

    /// <summary>Update viewport dimensions (on window resize).</summary>
    public void SetViewport(float width, float height)
    {
        _viewportWidth = width;
        _viewportHeight = height;
        foreach (var screen in _screens.Values)
        {
            screen.Width = width;
            screen.Height = height;
        }
    }

    /// <summary>Register a screen by name. Does not display it.</summary>
    public void Register(string name, UIElement screen)
    {
        screen.Width = _viewportWidth;
        screen.Height = _viewportHeight;
        screen.Visible = false;
        _screens[name] = screen;
    }

    /// <summary>Unregister a screen.</summary>
    public void Unregister(string name)
    {
        _screens.Remove(name);
        _stack.RemoveAll(e => e.Name == name);
    }

    /// <summary>Push a screen onto the stack (makes it active).</summary>
    public void Push(string name)
    {
        if (!_screens.TryGetValue(name, out var screen)) return;
        if (_stack.Any(e => e.Name == name)) return; // already on stack

        screen.Visible = true;
        screen.Opacity = 0;
        _stack.Add(new ScreenEntry { Name = name, Screen = screen });

        // Animate in
        TweenManager.Global.Start(v => screen.Opacity = v, 0, 1, TransitionDuration, EasingType.EaseOut);
    }

    /// <summary>Pop the top screen off the stack.</summary>
    public void Pop()
    {
        if (_stack.Count <= 1) return; // keep at least one screen

        var entry = _stack[^1];
        _stack.RemoveAt(_stack.Count - 1);

        // Animate out
        TweenManager.Global.Start(v => entry.Screen.Opacity = v, 1, 0, TransitionDuration, EasingType.EaseIn,
            onComplete: () => entry.Screen.Visible = false);
    }

    /// <summary>Replace the entire stack with a single screen.</summary>
    public void SetScreen(string name)
    {
        foreach (var entry in _stack)
            entry.Screen.Visible = false;
        _stack.Clear();
        Push(name);
    }

    /// <summary>Toggle a screen: push if not on stack, pop if it's on top.</summary>
    public void Toggle(string name)
    {
        if (_stack.Count > 0 && _stack[^1].Name == name)
            Pop();
        else
            Push(name);
    }

    /// <summary>Check if a screen is currently on the stack.</summary>
    public bool IsOnStack(string name) => _stack.Any(e => e.Name == name);

    /// <summary>Update the active (top) screen.</summary>
    public void Update(GameInput input, float dt)
    {
        // Only the top screen receives input
        if (_stack.Count > 0)
            _stack[^1].Screen.Update(input, dt);
    }

    /// <summary>Draw all visible screens in stack order.</summary>
    public void Draw(UIRenderer renderer)
    {
        if (_stack.Count == 0) return;

        if (RenderStack)
        {
            // Draw all screens bottom-to-top
            for (int i = 0; i < _stack.Count; i++)
            {
                var entry = _stack[i];
                if (!entry.Screen.Visible) continue;

                // Dim background between screens (not on the bottom screen)
                if (i > 0 && DimBackground)
                {
                    renderer.DrawRect(0, 0, _viewportWidth, _viewportHeight, DimColor);
                }

                entry.Screen.Draw(renderer);
            }
        }
        else
        {
            // Only draw top screen
            _stack[^1].Screen.Draw(renderer);
        }
    }

    private struct ScreenEntry
    {
        public string Name;
        public UIElement Screen;
    }
}
