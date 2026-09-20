using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;
using Microsoft.AspNetCore.Components;
using System.Numerics;

namespace SpawnDev.GameUI.Input;

/// <summary>
/// Input provider for DOM touch events.
/// Converts multi-touch into multiple Pointer objects, one per active touch point.
/// Primary touch (first finger) maps to primary action.
///
/// All DOM access via SpawnDev.SpawnJS typed wrappers.
/// </summary>
public class TouchProvider : IInputProvider
{
    private readonly Dictionary<long, TouchState> _activeTouches = new();
    private readonly List<TouchState> _frameSnapshot = new();

    // DOM callbacks
    private ActionCallback<TouchEvent>? _onTouchStart;
    private ActionCallback<TouchEvent>? _onTouchMove;
    private ActionCallback<TouchEvent>? _onTouchEnd;
    private ActionCallback<TouchEvent>? _onTouchCancel;
    private HTMLCanvasElement? _canvas;
    private bool _attached;
    private bool _ownsCanvas;

    private struct TouchState
    {
        public long Id;
        public Vector2 Position;
        public bool IsNew;
        public bool IsEnded;
    }

    /// <summary>
    /// Blazor WASM: ElementReference.As&lt;T&gt;() is correct.
    /// SpawnDomRenderer hosts: resolve with Renderer.GetElement&lt;HTMLCanvasElement&gt; and call Attach(HTMLCanvasElement).
    /// </summary>
    public void Attach(ElementReference canvasRef) => Attach(canvasRef.As<HTMLCanvasElement>(), ownsCanvas: true);

    /// <summary>
    /// Attach to an already-resolved canvas (required under SpawnDomRenderer).
    /// Caller retains ownership of <paramref name="canvas"/>; Dispose will not dispose it.
    /// </summary>
    public void Attach(HTMLCanvasElement canvas) => Attach(canvas, ownsCanvas: false);

    private void Attach(HTMLCanvasElement canvas, bool ownsCanvas)
    {
        if (_attached) return;
        _attached = true;
        _ownsCanvas = ownsCanvas;

        _canvas = canvas;
        _onTouchStart = new ActionCallback<TouchEvent>(OnTouchStart);
        _onTouchMove = new ActionCallback<TouchEvent>(OnTouchMove);
        _onTouchEnd = new ActionCallback<TouchEvent>(OnTouchEnd);
        _onTouchCancel = new ActionCallback<TouchEvent>(OnTouchCancel);

        _canvas.OnTouchStart += _onTouchStart;
        _canvas.OnTouchMove += _onTouchMove;
        _canvas.OnTouchEnd += _onTouchEnd;
        _canvas.OnTouchCancel += _onTouchCancel;
    }

    public void Poll(GameInput gameInput)
    {
        if (!_attached) return;

        _frameSnapshot.Clear();
        _frameSnapshot.AddRange(_activeTouches.Values);

        foreach (var touch in _frameSnapshot)
        {
            var pointer = new Pointer
            {
                Type = PointerType.Touch,
                ScreenPosition = touch.Position,
                IsPressed = !touch.IsEnded,
                WasPressed = touch.IsNew,
                WasReleased = touch.IsEnded,
            };
            gameInput.AddPointer(pointer);
        }

        // Clean up: remove ended touches, clear new flags
        var toRemove = new List<long>();
        foreach (var (id, state) in _activeTouches)
        {
            if (state.IsEnded)
                toRemove.Add(id);
            else if (state.IsNew)
                _activeTouches[id] = state with { IsNew = false };
        }
        foreach (var id in toRemove)
            _activeTouches.Remove(id);
    }

    private void ProcessTouches(TouchEvent e, Action<Touch> handler)
    {
        using var touches = e.ChangedTouches;
        for (int i = 0; i < touches.Length; i++)
        {
            using var t = touches.Items(i);
            handler(t);
        }
    }

    private void OnTouchStart(TouchEvent e)
    {
        ProcessTouches(e, t =>
        {
            _activeTouches[t.Identifier] = new TouchState
            {
                Id = t.Identifier,
                Position = new Vector2((float)t.ClientX, (float)t.ClientY),
                IsNew = true,
            };
        });
    }

    private void OnTouchMove(TouchEvent e)
    {
        ProcessTouches(e, t =>
        {
            if (_activeTouches.TryGetValue(t.Identifier, out var state))
            {
                _activeTouches[t.Identifier] = state with
                {
                    Position = new Vector2((float)t.ClientX, (float)t.ClientY)
                };
            }
        });
    }

    private void OnTouchEnd(TouchEvent e)
    {
        ProcessTouches(e, t =>
        {
            if (_activeTouches.TryGetValue(t.Identifier, out var state))
                _activeTouches[t.Identifier] = state with { IsEnded = true };
        });
    }

    private void OnTouchCancel(TouchEvent e) => OnTouchEnd(e);

    public void Dispose()
    {
        if (!_attached) return;
        _attached = false;

        if (_canvas != null)
        {
            if (_onTouchStart != null) _canvas.OnTouchStart -= _onTouchStart;
            if (_onTouchMove != null) _canvas.OnTouchMove -= _onTouchMove;
            if (_onTouchEnd != null) _canvas.OnTouchEnd -= _onTouchEnd;
            if (_onTouchCancel != null) _canvas.OnTouchCancel -= _onTouchCancel;
        }

        _onTouchStart?.Dispose();
        _onTouchMove?.Dispose();
        _onTouchEnd?.Dispose();
        _onTouchCancel?.Dispose();
        if (_ownsCanvas)
            _canvas?.Dispose();
        _canvas = null;
    }
}
