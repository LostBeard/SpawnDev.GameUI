using SpawnDev.GameUI.Elements;
using SpawnDev.GameUI.Input;
using SpawnDev.UnitTesting;

namespace SpawnDev.GameUI.Demo.Shared.UnitTests;

/// <summary>UIHoldButton driven the way a host drives it: real PushedInputProvider, real GameInput, real Update.</summary>
public class HoldButtonTests
{
    private sealed class Rig
    {
        public readonly PushedInputProvider Input = new();
        public readonly GameInput Game = new();
        public readonly UIHoldButton Button = new() { X = 100, Y = 100, Width = 60, Height = 40 };
        public int Holds, Releases;

        public Rig()
        {
            Game.AddProvider(Input);
            Button.OnHold = () => Holds++;
            Button.OnRelease = () => Releases++;
        }

        public void Frame()
        {
            Game.Poll();
            Button.Update(Game, 1f / 60f);
        }
    }

    private static void Check(bool condition, string what)
    {
        if (!condition) throw new Exception(what);
    }

    [TestMethod]
    public void PressOnTheButton_Holds_UntilTheMouseComesUp()
    {
        var r = new Rig();
        r.Input.MouseMove(130, 120);
        r.Frame();
        Check(r.Holds == 0, "hovering is not holding");
        r.Input.MouseDown(0);
        r.Frame();
        Check(r.Holds == 1 && r.Button.Holding, $"pressed: holds {r.Holds}");
        for (int i = 0; i < 10; i++) r.Frame();
        Check(r.Holds == 1 && r.Releases == 0 && r.Button.Holding, $"still held after 10 frames: holds {r.Holds} releases {r.Releases}");
        r.Input.MouseUp(0);
        r.Frame();
        Check(r.Releases == 1 && !r.Button.Holding, $"released: releases {r.Releases}");
        r.Frame();
        Check(r.Holds == 1 && r.Releases == 1, "and nothing more happens");
    }

    [TestMethod]
    public void SlidingOffTheButtonWhilePressed_Releases()
    {
        var r = new Rig();
        r.Input.MouseMove(130, 120);
        r.Input.MouseDown(0);
        r.Frame();
        Check(r.Button.Holding, "held");
        r.Input.MouseMove(300, 300);
        r.Frame();
        Check(r.Releases == 1 && !r.Button.Holding, $"left the button: releases {r.Releases}");
        r.Input.MouseUp(0);
        r.Frame();
        Check(r.Holds == 1 && r.Releases == 1, "the release is reported once");
    }

    [TestMethod]
    public void APressThatStartedElsewhere_AndSlidOntoTheButton_DoesNotHold()
    {
        var r = new Rig();
        r.Input.MouseMove(300, 300);
        r.Input.MouseDown(0);
        r.Frame();
        r.Input.MouseMove(130, 120);
        r.Frame();
        r.Frame();
        Check(r.Holds == 0 && !r.Button.Holding, $"a drag onto the button is not a press: holds {r.Holds}");
        r.Input.MouseUp(0);
        r.Frame();
        Check(r.Holds == 0 && r.Releases == 0, "and nothing was released");
    }

    [TestMethod]
    public void HidingOrDisablingTheButtonWhileHeld_Releases()
    {
        var r = new Rig();
        r.Input.MouseMove(130, 120);
        r.Input.MouseDown(0);
        r.Frame();
        Check(r.Button.Holding, "held");
        r.Button.Visible = false;
        r.Frame();
        Check(r.Releases == 1 && !r.Button.Holding, $"hidden: releases {r.Releases}");
        r.Button.Visible = true;
        r.Input.MouseUp(0);
        r.Frame();
        Check(r.Holds == 1 && r.Releases == 1, "and the release is not repeated");

        var d = new Rig();
        d.Input.MouseMove(130, 120);
        d.Input.MouseDown(0);
        d.Frame();
        d.Button.Enabled = false;
        d.Frame();
        Check(d.Releases == 1, $"disabled: releases {d.Releases}");
    }

    [TestMethod]
    public void ReleaseFromOutside_EndsTheHoldOnce_AndDoesNothingWhenNotHeld()
    {
        var r = new Rig();
        r.Button.Release();
        Check(r.Releases == 0, "releasing a button that is not held does nothing");
        r.Input.MouseMove(130, 120);
        r.Input.MouseDown(0);
        r.Frame();
        r.Button.Release();
        r.Button.Release();
        Check(r.Releases == 1, $"released once: {r.Releases}");
    }

    [TestMethod]
    public void TwoPressesInARow_HoldTwice()
    {
        var r = new Rig();
        r.Input.MouseMove(130, 120);
        for (int i = 0; i < 2; i++)
        {
            r.Input.MouseDown(0);
            r.Frame();
            r.Frame();
            r.Input.MouseUp(0);
            r.Frame();
        }
        Check(r.Holds == 2 && r.Releases == 2, $"holds {r.Holds} releases {r.Releases}");
    }
}
