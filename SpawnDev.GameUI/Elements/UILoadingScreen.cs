using System.Drawing;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Fullscreen loading screen with progress bar, status text, and rotating tips.
/// Renders as a dark overlay on top of everything, used during asset loading,
/// world generation, server connection, etc.
///
/// Usage:
///   var loading = new UILoadingScreen();
///   loading.Tips.Add("Press F3 for debug info");
///   loading.Tips.Add("Crouch to move silently");
///
///   // Per frame during loading:
///   loading.Progress = currentBytes / totalBytes;
///   loading.StatusText = "Loading terrain...";
///   loading.Update(dt);
///   loading.Draw(renderer, viewportW, viewportH);
///
///   // When done:
///   loading.Visible = false;
/// </summary>
public class UILoadingScreen
{
    /// <summary>Loading progress 0-1.</summary>
    public float Progress { get; set; }

    /// <summary>Status text shown above the progress bar.</summary>
    public string StatusText { get; set; } = "Loading...";

    /// <summary>Title text shown at top center.</summary>
    public string Title { get; set; } = "";

    /// <summary>Whether the loading screen is visible.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Rotating tips shown below the progress bar.</summary>
    public List<string> Tips { get; } = new();

    /// <summary>Seconds between tip rotations.</summary>
    public float TipInterval { get; set; } = 5f;

    /// <summary>Background color (darkened overlay).</summary>
    public Color BackgroundColor { get; set; } = Color.FromArgb(230, 10, 10, 15);

    /// <summary>Progress bar fill color.</summary>
    public Color BarColor { get; set; } = Color.FromArgb(255, 108, 92, 231);

    /// <summary>Progress bar track color.</summary>
    public Color BarTrackColor { get; set; } = Color.FromArgb(120, 40, 40, 55);

    /// <summary>Width of the progress bar (pixels).</summary>
    public float BarWidth { get; set; } = 400;

    /// <summary>Height of the progress bar (pixels).</summary>
    public float BarHeight { get; set; } = 8;

    /// <summary>Show percentage text above the bar.</summary>
    public bool ShowPercentage { get; set; } = true;

    /// <summary>Show a spinning indicator.</summary>
    public bool ShowSpinner { get; set; } = true;

    private float _time;
    private int _tipIndex;
    private float _tipTimer;

    /// <summary>Update tip rotation and spinner animation. Call per frame.</summary>
    public void Update(float deltaTime)
    {
        _time += deltaTime;

        // Rotate tips
        if (Tips.Count > 1)
        {
            _tipTimer += deltaTime;
            if (_tipTimer >= TipInterval)
            {
                _tipTimer = 0;
                _tipIndex = (_tipIndex + 1) % Tips.Count;
            }
        }
    }

    /// <summary>Draw the loading screen. Call after all other UI.</summary>
    public void Draw(UIRenderer renderer, int viewportWidth, int viewportHeight)
    {
        if (!Visible) return;

        float cx = viewportWidth / 2f;
        float cy = viewportHeight / 2f;

        // Full-screen background
        renderer.DrawRect(0, 0, viewportWidth, viewportHeight, BackgroundColor);

        // Title
        if (!string.IsNullOrEmpty(Title))
        {
            float tw = renderer.MeasureText(Title, FontSize.Title);
            renderer.DrawText(Title, cx - tw / 2, cy - 80, FontSize.Title, Color.White);
        }

        // Status text
        float statusW = renderer.MeasureText(StatusText, FontSize.Body);
        renderer.DrawText(StatusText, cx - statusW / 2, cy - 30, FontSize.Body, Color.FromArgb(200, 220, 220, 220));

        // Spinner (rotating dots)
        if (ShowSpinner)
        {
            int dotCount = 3;
            float dotPhase = _time * 3f;
            for (int i = 0; i < dotCount; i++)
            {
                float alpha = (MathF.Sin(dotPhase - i * 0.8f) + 1) / 2f;
                var dotColor = Color.FromArgb((int)(alpha * 200), BarColor.R, BarColor.G, BarColor.B);
                float dx = cx - (dotCount * 8) / 2f + i * 12;
                renderer.DrawRect(dx, cy - 12, 4, 4, dotColor);
            }
        }

        // Progress bar track
        float barX = cx - BarWidth / 2;
        float barY = cy + 5;
        renderer.DrawRect(barX, barY, BarWidth, BarHeight, BarTrackColor);

        // Progress bar fill
        float fillW = BarWidth * Math.Clamp(Progress, 0, 1);
        if (fillW > 0)
            renderer.DrawRect(barX, barY, fillW, BarHeight, BarColor);

        // Percentage text
        if (ShowPercentage)
        {
            string pctText = $"{(int)(Progress * 100)}%";
            float pctW = renderer.MeasureText(pctText, FontSize.Caption);
            renderer.DrawText(pctText, cx - pctW / 2, barY + BarHeight + 6, FontSize.Caption,
                Color.FromArgb(180, 200, 200, 200));
        }

        // Tip text
        if (Tips.Count > 0)
        {
            string tip = Tips[_tipIndex % Tips.Count];
            float tipW = renderer.MeasureText(tip, FontSize.Caption);
            float tipY = barY + BarHeight + 30;
            renderer.DrawText(tip, cx - tipW / 2, tipY, FontSize.Caption,
                Color.FromArgb(140, 180, 180, 200));
        }
    }
}
