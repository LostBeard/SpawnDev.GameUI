using SpawnDev.GameUI.Elements;
using SpawnDev.GameUI.Input;
using SpawnDev.UnitTesting;

namespace SpawnDev.GameUI.Demo.Shared.UnitTests;

/// <summary>
/// UIChatBox typing, driven the way a host drives it: real PushedInputProvider, real GameInput, real Update. The host decides how
/// many events land in one frame; the chat must give the same text whether the keys come one per frame or all at once.
/// </summary>
public class ChatBoxInputTests
{
    private sealed class Rig
    {
        public readonly PushedInputProvider Input = new();
        public readonly GameInput Game = new();
        public readonly UIChatBox Chat = new() { X = 0, Y = 0, Width = 300, Height = 200 };
        public readonly List<string> Sent = new();

        public Rig()
        {
            Game.AddProvider(Input);
            Chat.OnSend = Sent.Add;
            Chat.FocusInput();
        }

        public void Frame()
        {
            Game.Poll();
            Chat.Update(Game, 1f / 60f);
        }

        public void Type(string text)
        {
            foreach (char c in text)
            {
                Input.KeyDown("Key" + char.ToUpperInvariant(c), c.ToString());
                Input.KeyUp("Key" + char.ToUpperInvariant(c));
            }
        }
    }

    private static void Check(bool condition, string what)
    {
        if (!condition) throw new Exception(what);
    }

    [TestMethod]
    public void TextTypedInTheSameFrameAsEnter_IsSentWhole()
    {
        var r = new Rig();
        r.Type("hello from the gate");
        r.Input.KeyDown("Enter");
        r.Frame();
        Check(r.Sent.Count == 1 && r.Sent[0] == "hello from the gate", $"the whole line is sent, got: {string.Join("|", r.Sent)}");
        Check(r.Chat.InputText == "", "and the input is cleared");
    }

    [TestMethod]
    public void TextAndEnterInSeparateFrames_SendTheSameLine()
    {
        var r = new Rig();
        r.Type("hello");
        r.Frame();
        Check(r.Chat.InputText == "hello", $"typed text shows in the input, got '{r.Chat.InputText}'");
        r.Input.KeyDown("Enter");
        r.Frame();
        Check(r.Sent.Count == 1 && r.Sent[0] == "hello", $"got: {string.Join("|", r.Sent)}");
    }

    [TestMethod]
    public void BackspaceInTheSameFrameAsText_RemovesTheLastCharacter()
    {
        var r = new Rig();
        r.Type("abc");
        r.Input.KeyDown("Backspace");
        r.Frame();
        Check(r.Chat.InputText == "ab", $"characters first, then Backspace, got '{r.Chat.InputText}'");
    }

    [TestMethod]
    public void EscapeAfterText_KeepsTheTextAndBlurs()
    {
        var r = new Rig();
        r.Type("keep me");
        r.Input.KeyDown("Escape");
        r.Frame();
        Check(!r.Chat.IsInputFocused, "Escape blurs the input");
        Check(r.Chat.InputText == "keep me", $"the text typed before it stays, got '{r.Chat.InputText}'");
        Check(r.Sent.Count == 0, "and nothing was sent");
    }

    [TestMethod]
    public void NothingIsTypedIntoAnUnfocusedChat()
    {
        var r = new Rig();
        r.Chat.BlurInput();
        r.Type("wasd");
        r.Input.KeyDown("Enter");
        r.Frame();
        Check(r.Chat.InputText == "" && r.Sent.Count == 0, "keys do not leak into a chat that is not focused (they steer the camera)");
    }
}
