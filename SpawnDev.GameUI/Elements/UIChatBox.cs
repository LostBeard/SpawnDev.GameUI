using System.Drawing;
using SpawnDev.GameUI.Input;

namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Chat message type, determines display color and filtering.
/// </summary>
public enum ChatMessageType
{
    Normal,
    System,
    Whisper,
    Team,
    Error,
    Emote,
}

/// <summary>
/// A single chat message.
/// </summary>
public class ChatMessage
{
    /// <summary>Sender display name (null for system messages).</summary>
    public string? Sender { get; set; }

    /// <summary>Message text.</summary>
    public string Text { get; set; } = "";

    /// <summary>Message type (determines display color).</summary>
    public ChatMessageType Type { get; set; } = ChatMessageType.Normal;

    /// <summary>When the message was received (seconds since chat opened).</summary>
    public float Timestamp { get; set; }

    /// <summary>Custom color override (null = use type default).</summary>
    public Color? Color { get; set; }
}

/// <summary>
/// Chat channel for filtering messages.
/// </summary>
public class ChatChannel
{
    /// <summary>Channel display name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Which message types this channel shows.</summary>
    public HashSet<ChatMessageType> Filter { get; set; } = new();

    /// <summary>Show all message types (no filter).</summary>
    public bool ShowAll { get; set; } = true;
}

/// <summary>
/// Multiplayer chat box with message history, text input, and channel tabs.
///
/// Layout:
///   [All] [Team] [Whisper]              - channel tabs
///   [Player1: Hello everyone!        ]  - message history (scrollable)
///   [Player2: Ready to go            ]
///   [System: Match starting in 10s   ]
///   [Type message...           [Send]]  - input field
///
/// Features:
/// - Message history with configurable max count
/// - Auto-fade old messages (opacity decreases over time)
/// - Channel tabs for filtering (All, Team, Whisper, custom)
/// - Text input with submit on Enter
/// - Message type coloring (system=yellow, whisper=pink, team=cyan, etc.)
/// - Auto-scroll to bottom on new messages
///
/// Usage:
///   var chat = new UIChatBox();
///   chat.OnSend = (text) => networkManager.SendChat(text);
///   chat.AddMessage("Player1", "Hello!", ChatMessageType.Normal);
///   chat.AddMessage(null, "Match starting.", ChatMessageType.System);
/// </summary>
public class UIChatBox : UIPanel
{
    private readonly List<ChatMessage> _messages = new();
    private readonly List<ChatChannel> _channels = new();
    private int _activeChannelIndex;
    private string _inputText = "";
    private float _scrollOffset;
    private float _time;
    private bool _inputFocused;

    /// <summary>Maximum messages in history. Oldest removed when exceeded.</summary>
    public int MaxMessages { get; set; } = 200;

    /// <summary>Height of the input field area.</summary>
    public float InputHeight { get; set; } = 28f;

    /// <summary>Height of the channel tab bar.</summary>
    public float TabHeight { get; set; } = 24f;

    /// <summary>Line height in the message area.</summary>
    public float MessageLineHeight { get; set; } = 18f;

    /// <summary>
    /// Seconds after which messages start to fade. 0 = no fade.
    /// Messages fade from FadeStart to FadeStart + FadeDuration.
    /// </summary>
    public float FadeStart { get; set; }

    /// <summary>Duration of the fade effect in seconds.</summary>
    public float FadeDuration { get; set; } = 5f;

    /// <summary>Show timestamps before messages.</summary>
    public bool ShowTimestamps { get; set; }

    /// <summary>Called when the user submits a message.</summary>
    public Action<string>? OnSend { get; set; }

    /// <summary>Current input text.</summary>
    public string InputText
    {
        get => _inputText;
        set => _inputText = value ?? "";
    }

    /// <summary>Whether the input field is focused.</summary>
    public bool IsInputFocused => _inputFocused;

    /// <summary>Number of messages in history.</summary>
    public int MessageCount => _messages.Count;

    /// <summary>Active channel name.</summary>
    public string ActiveChannel => _channels.Count > 0 ? _channels[_activeChannelIndex].Name : "All";

    /// <summary>All registered channels.</summary>
    public IReadOnlyList<ChatChannel> Channels => _channels;

    // Colors per message type
    public Color NormalColor { get; set; } = Color.White;
    public Color SystemColor { get; set; } = Color.FromArgb(255, 255, 220, 80);
    public Color WhisperColor { get; set; } = Color.FromArgb(255, 255, 150, 200);
    public Color TeamColor { get; set; } = Color.FromArgb(255, 100, 220, 255);
    public Color ErrorColor { get; set; } = Color.FromArgb(255, 255, 80, 80);
    public Color EmoteColor { get; set; } = Color.FromArgb(255, 200, 180, 255);
    public Color SenderColor { get; set; } = Color.FromArgb(255, 180, 200, 255);
    public Color InputBgColor { get; set; } = Color.FromArgb(180, 15, 15, 25);
    public Color InputTextColor { get; set; } = Color.White;

    public UIChatBox()
    {
        Width = 350;
        Height = 250;
        Padding = 4;

        // Default channels
        _channels.Add(new ChatChannel { Name = "All", ShowAll = true });
        _channels.Add(new ChatChannel
        {
            Name = "Team",
            ShowAll = false,
            Filter = { ChatMessageType.Team, ChatMessageType.System }
        });
        _channels.Add(new ChatChannel
        {
            Name = "Whisper",
            ShowAll = false,
            Filter = { ChatMessageType.Whisper }
        });
    }

    /// <summary>Add a chat message.</summary>
    public void AddMessage(string? sender, string text, ChatMessageType type = ChatMessageType.Normal,
        Color? color = null)
    {
        _messages.Add(new ChatMessage
        {
            Sender = sender,
            Text = text,
            Type = type,
            Timestamp = _time,
            Color = color,
        });

        // Trim history
        while (_messages.Count > MaxMessages)
            _messages.RemoveAt(0);

        // Auto-scroll to bottom
        _scrollOffset = float.MaxValue; // will be clamped in Draw
    }

    /// <summary>Add a system message.</summary>
    public void AddSystemMessage(string text)
    {
        AddMessage(null, text, ChatMessageType.System);
    }

    /// <summary>Add a custom channel.</summary>
    public void AddChannel(string name, params ChatMessageType[] filter)
    {
        var ch = new ChatChannel { Name = name, ShowAll = filter.Length == 0 };
        foreach (var f in filter) ch.Filter.Add(f);
        _channels.Add(ch);
    }

    /// <summary>Clear all messages.</summary>
    public void ClearMessages() => _messages.Clear();

    /// <summary>Set focus to the input field.</summary>
    public void FocusInput() => _inputFocused = true;

    /// <summary>Remove focus from the input field.</summary>
    public void BlurInput() => _inputFocused = false;

    /// <summary>Submit the current input text and clear it.</summary>
    public void Submit()
    {
        if (string.IsNullOrWhiteSpace(_inputText)) return;
        OnSend?.Invoke(_inputText.Trim());
        _inputText = "";
    }

    private List<ChatMessage> GetFilteredMessages()
    {
        if (_activeChannelIndex >= _channels.Count) return _messages;
        var channel = _channels[_activeChannelIndex];
        if (channel.ShowAll) return _messages;
        return _messages.FindAll(m => channel.Filter.Contains(m.Type));
    }

    private Color GetMessageColor(ChatMessage msg)
    {
        if (msg.Color.HasValue) return msg.Color.Value;
        return msg.Type switch
        {
            ChatMessageType.System => SystemColor,
            ChatMessageType.Whisper => WhisperColor,
            ChatMessageType.Team => TeamColor,
            ChatMessageType.Error => ErrorColor,
            ChatMessageType.Emote => EmoteColor,
            _ => NormalColor,
        };
    }

    public override void Update(GameInput input, float dt)
    {
        if (!Visible || !Enabled) return;

        _time += dt;

        // Keyboard input for chat
        if (_inputFocused)
        {
            // Handle Enter to submit
            if (input.Keyboard.WasKeyPressed("Enter"))
            {
                Submit();
            }
            // Handle Escape to blur
            else if (input.Keyboard.WasKeyPressed("Escape"))
            {
                _inputFocused = false;
            }
            // Handle Backspace
            else if (input.Keyboard.WasKeyPressed("Backspace"))
            {
                if (_inputText.Length > 0)
                    _inputText = _inputText[..^1];
            }
            // Handle typed text (printable characters accumulated this frame)
            else if (!string.IsNullOrEmpty(input.Keyboard.TextInput))
            {
                _inputText += input.Keyboard.TextInput;
            }
        }

        // Click detection
        foreach (var pointer in input.Pointers)
        {
            if (!pointer.ScreenPosition.HasValue || !pointer.WasReleased) continue;
            var mp = pointer.ScreenPosition.Value;
            var bounds = ScreenBounds;

            // Tab clicks
            float tabX = bounds.X + Padding;
            float tabY = bounds.Y + Padding;
            for (int i = 0; i < _channels.Count; i++)
            {
                float tw = 50f;
                if (mp.X >= tabX && mp.X < tabX + tw &&
                    mp.Y >= tabY && mp.Y < tabY + TabHeight)
                {
                    _activeChannelIndex = i;
                    _scrollOffset = float.MaxValue;
                }
                tabX += tw + 3;
            }

            // Input field click
            float inputY = bounds.Y + bounds.Height - InputHeight - Padding;
            if (mp.X >= bounds.X && mp.X < bounds.X + bounds.Width &&
                mp.Y >= inputY && mp.Y < inputY + InputHeight)
            {
                _inputFocused = true;
            }
            else if (mp.X >= bounds.X && mp.X < bounds.X + bounds.Width &&
                     mp.Y >= bounds.Y && mp.Y < bounds.Y + bounds.Height)
            {
                // Click in message area - don't blur
            }
            else
            {
                _inputFocused = false;
            }

            // Scroll in message area
            float msgTop = bounds.Y + Padding + TabHeight + 2;
            float msgBottom = inputY - 2;
            if (mp.X >= bounds.X && mp.X < bounds.X + bounds.Width &&
                mp.Y >= msgTop && mp.Y < msgBottom)
            {
                // Scroll handled below
            }
        }

        // Scroll wheel
        foreach (var pointer in input.Pointers)
        {
            if (pointer.ScrollDelta != 0)
            {
                _scrollOffset -= pointer.ScrollDelta * MessageLineHeight * 3;
            }
        }

        base.Update(input, dt);
    }

    public override void Draw(UIRenderer renderer)
    {
        if (!Visible) return;

        var bounds = ScreenBounds;
        var filtered = GetFilteredMessages();

        // Background
        renderer.DrawRect(bounds.X, bounds.Y, bounds.Width, bounds.Height, BackgroundColor);

        // Channel tabs
        float tabX = bounds.X + Padding;
        float tabY = bounds.Y + Padding;
        for (int i = 0; i < _channels.Count; i++)
        {
            float tw = 50f;
            bool active = i == _activeChannelIndex;
            var tabBg = active
                ? Color.FromArgb(180, 60, 60, 90)
                : Color.FromArgb(100, 30, 30, 45);
            renderer.DrawRect(tabX, tabY, tw, TabHeight, tabBg);

            float textW = renderer.MeasureText(_channels[i].Name, FontSize.Caption);
            float textX = tabX + (tw - textW) / 2;
            float textY = tabY + (TabHeight - renderer.GetLineHeight(FontSize.Caption)) / 2;
            renderer.DrawText(_channels[i].Name, textX, textY, FontSize.Caption,
                active ? Color.White : Color.FromArgb(160, 160, 160, 160));

            tabX += tw + 3;
        }

        // Message area
        float msgX = bounds.X + Padding;
        float msgTop = bounds.Y + Padding + TabHeight + 2;
        float inputY = bounds.Y + bounds.Height - InputHeight - Padding;
        float msgH = inputY - msgTop - 2;
        float contentH = filtered.Count * MessageLineHeight;

        // Clamp scroll
        float maxScroll = MathF.Max(0, contentH - msgH);
        _scrollOffset = MathF.Max(0, MathF.Min(_scrollOffset, maxScroll));

        // Draw visible messages
        for (int i = 0; i < filtered.Count; i++)
        {
            float lineY = msgTop + i * MessageLineHeight - _scrollOffset;
            if (lineY + MessageLineHeight < msgTop || lineY > msgTop + msgH) continue;

            var msg = filtered[i];
            var msgColor = GetMessageColor(msg);

            // Fade effect
            if (FadeStart > 0)
            {
                float age = _time - msg.Timestamp;
                if (age > FadeStart)
                {
                    float fade = 1f - MathF.Min((age - FadeStart) / FadeDuration, 1f);
                    if (fade <= 0) continue;
                    msgColor = Color.FromArgb((int)(fade * msgColor.A), msgColor.R, msgColor.G, msgColor.B);
                }
            }

            float tx = msgX;

            // Timestamp
            if (ShowTimestamps)
            {
                int mins = (int)(msg.Timestamp / 60);
                int secs = (int)(msg.Timestamp % 60);
                string ts = $"[{mins:D2}:{secs:D2}] ";
                renderer.DrawText(ts, tx, lineY, FontSize.Caption, Color.FromArgb(80, 180, 180, 180));
                tx += renderer.MeasureText(ts, FontSize.Caption);
            }

            // Sender name
            if (msg.Sender != null)
            {
                string senderStr = msg.Sender + ": ";
                renderer.DrawText(senderStr, tx, lineY, FontSize.Caption, SenderColor);
                tx += renderer.MeasureText(senderStr, FontSize.Caption);
            }

            // Message text
            renderer.DrawText(msg.Text, tx, lineY, FontSize.Caption, msgColor);
        }

        // Input field
        var inputBg = _inputFocused
            ? Color.FromArgb(200, 25, 25, 40)
            : InputBgColor;
        renderer.DrawRect(msgX, inputY, bounds.Width - Padding * 2, InputHeight, inputBg);

        // Input border when focused
        if (_inputFocused)
        {
            renderer.DrawRect(msgX, inputY, bounds.Width - Padding * 2, 1,
                Color.FromArgb(180, 100, 100, 200));
        }

        // Input text or placeholder
        float inputTextY = inputY + (InputHeight - renderer.GetLineHeight(FontSize.Caption)) / 2;
        if (_inputText.Length > 0)
        {
            // Cursor blink
            string displayText = _inputText;
            if (_inputFocused && ((int)(_time * 2) % 2 == 0))
                displayText += "|";
            renderer.DrawText(displayText, msgX + 6, inputTextY, FontSize.Caption, InputTextColor);
        }
        else
        {
            string placeholder = _inputFocused ? "|" : "Type message...";
            renderer.DrawText(placeholder, msgX + 6, inputTextY, FontSize.Caption,
                Color.FromArgb(100, 160, 160, 160));
        }
    }
}
