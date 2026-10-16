using System.Drawing;
using SpawnDev.GameUI.Elements;
using SpawnDev.UnitTesting;

namespace SpawnDev.GameUI.Demo.Shared.UnitTests;

/// <summary>A label with no color of its own follows the theme; a label given a color keeps it.</summary>
public class LabelThemeTests
{
    [TestMethod]
    public void ALabelWithoutAColor_FollowsTheTheme_WhenTheThemeChanges()
    {
        var saved = UITheme.Current;
        try
        {
            var label = new UILabel { Text = "x" };
            var first = new UITheme { TextPrimary = Color.FromArgb(255, 255, 0, 0) };
            var second = new UITheme { TextPrimary = Color.FromArgb(255, 0, 255, 0) };
            UITheme.Current = first;
            if (label.Color != first.TextPrimary) throw new Exception($"label color {label.Color}, expected the theme's {first.TextPrimary}");
            UITheme.Current = second;
            if (label.Color != second.TextPrimary) throw new Exception($"after the theme changed the label is still {label.Color}, expected {second.TextPrimary}");
        }
        finally { UITheme.Current = saved; }
    }

    [TestMethod]
    public void ALabelGivenAColor_KeepsItAcrossThemeChanges()
    {
        var saved = UITheme.Current;
        try
        {
            var label = new UILabel { Text = "x", Color = Color.Orange };
            UITheme.Current = new UITheme { TextPrimary = Color.Blue };
            if (label.Color != Color.Orange) throw new Exception($"an explicit color was replaced by the theme's: {label.Color}");
        }
        finally { UITheme.Current = saved; }
    }

    [TestMethod]
    public void ATextBlockWithoutAColor_FollowsTheTheme_AndKeepsAnExplicitOne()
    {
        var saved = UITheme.Current;
        try
        {
            var plain = new UITextBlock { Text = "x" };
            var painted = new UITextBlock { Text = "x", Color = Color.Orange };
            UITheme.Current = new UITheme { TextPrimary = Color.FromArgb(255, 255, 0, 0) };
            if (plain.Color != UITheme.Current.TextPrimary) throw new Exception($"text block color {plain.Color}, expected the theme's");
            UITheme.Current = new UITheme { TextPrimary = Color.FromArgb(255, 0, 255, 0) };
            if (plain.Color != UITheme.Current.TextPrimary) throw new Exception("the text block did not follow the second theme");
            if (painted.Color != Color.Orange) throw new Exception("an explicit color was replaced by the theme's");
        }
        finally { UITheme.Current = saved; }
    }
}
