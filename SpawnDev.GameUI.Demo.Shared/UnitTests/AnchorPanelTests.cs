using SpawnDev.GameUI.Elements;
using SpawnDev.GameUI.Input;
using SpawnDev.UnitTesting;

namespace SpawnDev.GameUI.Demo.Shared.UnitTests;

/// <summary>UIAnchorPanel placement, and moving an anchored child with SetAnchor without disturbing the draw order.</summary>
public class AnchorPanelTests
{
    private static void Check(bool condition, string what)
    {
        if (!condition) throw new Exception(what);
    }

    private static void Frame(UIAnchorPanel panel)
    {
        var game = new GameInput();
        game.AddProvider(new PushedInputProvider());
        game.Poll();
        panel.Update(game, 1f / 60f);
    }

    [TestMethod]
    public void SetAnchor_MovesTheChild_AndKeepsTheDrawOrder()
    {
        var panel = new UIAnchorPanel { Width = 400, Height = 300 };
        var a = new UIPanel { Width = 100, Height = 50 };
        var b = new UIPanel { Width = 60, Height = 20 };
        panel.AddAnchored(a, Anchor.BottomRight, -10, -20);
        panel.AddAnchored(b, Anchor.TopLeft, 5, 6);
        Frame(panel);
        Check(a.X == 290 && a.Y == 230, $"bottom right: {a.X},{a.Y}");
        Check(b.X == 5 && b.Y == 6, $"top left: {b.X},{b.Y}");

        panel.SetAnchor(a, Anchor.TopCenter, 0, 5);
        Frame(panel);
        Check(a.X == 150 && a.Y == 5, $"top centre after SetAnchor: {a.X},{a.Y}");
        Check(ReferenceEquals(panel.Children[0], a) && ReferenceEquals(panel.Children[1], b), "the order of the children is unchanged");

        panel.Width = 800; panel.Height = 600;
        Frame(panel);
        Check(a.X == 350, $"still anchored to the centre when the panel grows: {a.X}");
    }

    [TestMethod]
    public void SetAnchor_OnAnElementThatIsNotAnchored_Throws()
    {
        var panel = new UIAnchorPanel { Width = 100, Height = 100 };
        try { panel.SetAnchor(new UIPanel(), Anchor.Center); throw new Exception("accepted a stranger"); }
        catch (ArgumentException) { }
    }
}
