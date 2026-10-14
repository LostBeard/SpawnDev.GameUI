using SpawnDev.GameUI.Elements;
using SpawnDev.GameUI.Input;
using SpawnDev.UnitTesting;

namespace SpawnDev.GameUI.Demo.Shared.UnitTests;

/// <summary>
/// PushedInputProvider: the host-driven input a UI in a Web Worker (no DOM) runs on, and the buffer MouseKeyboardProvider's
/// DOM listeners push into. Real provider, real GameInput, real UIButton - the click path a visitor's mouse takes, minus
/// the browser's event source.
/// </summary>
public class PushedInputTests
{
    private sealed class Rig
    {
        public readonly PushedInputProvider Input = new();
        public readonly GameInput Game = new();
        public readonly UIPanel Root = new() { X = 0, Y = 0, Width = 400, Height = 300 };
        public readonly UIButton Button = new() { X = 100, Y = 100, Width = 80, Height = 40, Text = "Go" };
        public int Clicks;

        public Rig()
        {
            Game.AddProvider(Input);
            Button.OnClick = () => Clicks++;
            Root.AddChild(Button);
        }

        /// <summary>One frame: what GameUIService.Update does with input.</summary>
        public void Frame()
        {
            Game.Poll();
            Root.Update(Game, 1f / 60f);
        }
    }

    private static void Check(bool condition, string what)
    {
        if (!condition) throw new Exception(what);
    }

    private static readonly (float X, float Y) OnButton = (140, 120);
    private static readonly (float X, float Y) OffButton = (10, 10);

    [TestMethod]
    public void Click_PressThenRelease_OverButton_ClicksOnce_OnTheRelease()
    {
        var r = new Rig();
        r.Input.MouseMove(OnButton.X, OnButton.Y);
        r.Frame();
        Check(r.Button.IsHovered, "hovered after the mouse moved onto it");
        Check(!r.Button.IsPressed && r.Clicks == 0, "not pressed, no click yet");

        r.Input.MouseDown(0);
        r.Frame();
        Check(r.Button.IsPressed, "pressed while the button is down");
        Check(r.Clicks == 0, "a press alone is not a click");

        r.Input.MouseUp(0);
        r.Frame();
        Check(r.Clicks == 1, $"one click on the release, got {r.Clicks}");

        r.Frame();
        Check(r.Clicks == 1, "the release is reported for one frame only");
    }

    [TestMethod]
    public void Click_PressAndReleaseBetweenPolls_StillClicks()
    {
        // A fast click: both events arrive inside one frame. The frame reports it as pressed-and-released.
        var r = new Rig();
        r.Input.MouseMove(OnButton.X, OnButton.Y);
        r.Frame();
        r.Input.MouseDown(0);
        r.Input.MouseUp(0);
        r.Frame();
        Check(r.Clicks == 1, $"a click that fits inside one frame still clicks, got {r.Clicks}");
        Check(!r.Button.IsPressed, "and ends released");
    }

    [TestMethod]
    public void Click_ReleaseOutsideTheButton_DoesNotClick()
    {
        var r = new Rig();
        r.Input.MouseMove(OnButton.X, OnButton.Y);
        r.Input.MouseDown(0);
        r.Frame();
        r.Input.MouseMove(OffButton.X, OffButton.Y);
        r.Input.MouseUp(0);
        r.Frame();
        Check(r.Clicks == 0, $"dragging off before the release cancels the click, got {r.Clicks}");
    }

    [TestMethod]
    public void SecondaryButton_ReportedSeparately_AndNeverClicks()
    {
        var r = new Rig();
        r.Input.MouseMove(OnButton.X, OnButton.Y);
        r.Input.MouseDown(2);
        r.Game.Poll();
        var p = r.Game.PrimaryPointer!;
        Check(p.WasSecondaryPressed && p.IsSecondaryPressed, "right button down reported");
        Check(!p.WasPressed && !p.IsPressed, "and the primary is untouched");
        r.Input.MouseUp(2);
        r.Frame();
        Check(r.Clicks == 0, "a right click does not click a button");
        Check(r.Game.PrimaryPointer!.WasSecondaryReleased, "the right release is reported");
    }

    [TestMethod]
    public void MouseButtons_OutsideTheThree_AreIgnored()
    {
        var r = new Rig();
        r.Input.MouseMove(OnButton.X, OnButton.Y);
        foreach (int b in new[] { 3, 4, -1, int.MaxValue })
        {
            r.Input.MouseDown(b);
            r.Input.MouseUp(b);
        }
        r.Game.Poll();
        var p = r.Game.PrimaryPointer!;
        Check(!p.IsPressed && !p.WasPressed && !p.WasReleased && !p.IsSecondaryPressed, "back/forward/odd buttons change nothing");
    }

    [TestMethod]
    public void Wheel_AccumulatesUntilThePoll_ThenClears()
    {
        var r = new Rig();
        r.Input.Wheel(40);
        r.Input.Wheel(-10);
        r.Game.Poll();
        Check(Math.Abs(r.Game.PrimaryPointer!.ScrollDelta - 30) < 1e-4f, $"wheel deltas add up, got {r.Game.PrimaryPointer!.ScrollDelta}");
        r.Game.Poll();
        Check(r.Game.PrimaryPointer!.ScrollDelta == 0, "and are consumed by the poll");
    }

    [TestMethod]
    public void Keys_PressHoldRepeatRelease_FollowTheFrame()
    {
        var r = new Rig();
        var kb = r.Game.Keyboard;

        r.Input.KeyDown("KeyW", "w");
        r.Game.Poll();
        Check(kb.WasKeyPressed("KeyW") && kb.IsKeyDown("KeyW"), "pressed and down on the first frame");
        Check(kb.TextInput == "w", $"typed text reported, got '{kb.TextInput}'");

        r.Input.KeyDown("KeyW", "w"); // OS auto-repeat
        r.Game.Poll();
        Check(!kb.WasKeyPressed("KeyW"), "auto-repeat is not a new press");
        Check(kb.IsKeyDown("KeyW"), "still held");
        Check(kb.TextInput == "w", "but a held letter keeps typing");

        r.Game.Poll();
        Check(kb.TextInput == "", "text is per frame");
        Check(kb.IsKeyDown("KeyW"), "a quiet frame leaves the key held");

        r.Input.KeyUp("KeyW");
        r.Game.Poll();
        Check(kb.WasKeyReleased("KeyW") && !kb.IsKeyDown("KeyW"), "released and no longer down");

        r.Input.KeyDown("KeyW", "w");
        r.Game.Poll();
        Check(kb.WasKeyPressed("KeyW"), "a key can be pressed again after its release");
    }

    [TestMethod]
    public void Keys_ThatTypeNothing_AddNoText()
    {
        var r = new Rig();
        r.Input.KeyDown("ControlLeft");
        r.Input.KeyDown("F3");
        r.Game.Poll();
        Check(r.Game.Keyboard.TextInput == "", "modifier and function keys type nothing");
        Check(r.Game.Keyboard.WasKeyPressed("F3"), "F3 is still a press (the debug overlay toggle reads it)");
    }

    [TestMethod]
    public void Text_AccumulatesInOrder_WithinOneFrame()
    {
        var r = new Rig();
        r.Input.KeyDown("KeyH", "H");
        r.Input.KeyDown("KeyI", "i");
        r.Game.Poll();
        Check(r.Game.Keyboard.TextInput == "Hi", $"typed in order, got '{r.Game.Keyboard.TextInput}'");
    }

    [TestMethod]
    public void PointerActive_Off_HidesThePointer_ButKeysStillFlow_AndNoStaleClickLaterOn()
    {
        var r = new Rig();
        r.Input.MouseMove(OnButton.X, OnButton.Y);
        r.Frame();
        r.Input.PointerActive = false;

        r.Input.MouseDown(0);
        r.Input.MouseUp(0);
        r.Input.KeyDown("Space");
        r.Frame();
        Check(r.Game.Pointers.Count == 0, "no pointer is reported while the host owns the mouse");
        Check(!r.Button.IsHovered, "so nothing hovers");
        Check(r.Clicks == 0, "and nothing clicks");
        Check(r.Game.Keyboard.WasKeyPressed("Space"), "keys flow regardless");

        r.Input.PointerActive = true;
        r.Frame();
        Check(r.Game.Pointers.Count == 1, "the pointer is back");
        Check(r.Clicks == 0, "the click that happened while it was off does not fire when it comes back");
        Check(r.Button.IsHovered, "and the button is hovered again under the last mouse position");
    }

    [TestMethod]
    public void ReleaseAll_ReleasesHeldKeys_AndCancelsAHeldMouseButtonWithoutAClick()
    {
        var r = new Rig();
        r.Input.MouseMove(OnButton.X, OnButton.Y);
        r.Input.MouseDown(0);
        r.Input.KeyDown("KeyA", "a");
        r.Frame();
        Check(r.Button.IsPressed && r.Game.Keyboard.IsKeyDown("KeyA"), "held before the focus is lost");

        r.Input.ReleaseAll(); // the host lost focus: the up events will never arrive
        r.Frame();
        Check(r.Game.Keyboard.WasKeyReleased("KeyA") && !r.Game.Keyboard.IsKeyDown("KeyA"), "the key is released");
        Check(!r.Button.IsPressed, "the mouse button is no longer held");
        Check(r.Clicks == 0, "and the cancelled press is not a click");

        r.Input.KeyDown("KeyA", "a");
        r.Frame();
        Check(r.Game.Keyboard.WasKeyPressed("KeyA"), "after ReleaseAll a key presses again (it is not stuck in the held set)");
    }

    [TestMethod]
    public void ReleaseAll_DropsAPressThatHadNotBeenPolledYet()
    {
        var r = new Rig();
        r.Input.MouseMove(OnButton.X, OnButton.Y);
        r.Input.MouseDown(0);
        r.Input.ReleaseAll();
        r.Frame();
        Check(!r.Game.PrimaryPointer!.WasPressed && !r.Game.PrimaryPointer!.IsPressed, "a press the host cancelled before the poll never shows");
    }
}
