namespace SpawnDev.GameUI.Elements;

/// <summary>
/// Windowed data provider for <see cref="UIVirtualList"/>.
/// The list pulls a visible range (plus overscan); the source may fill sync
/// or kick async work and call <see cref="UIVirtualList.NotifyDataChanged"/> when ready.
/// No Task in Update/Draw - async stays in the source/consumer.
/// </summary>
public interface IListDataSource
{
    /// <summary>Total item count (drives content height / scrollbar).</summary>
    int Count { get; }

    /// <summary>
    /// Try to read an item. False means not loaded yet; the list draws PlaceholderText.
    /// </summary>
    bool TryGetItem(int index, out ListItem item);

    /// <summary>
    /// Warm [start, start+count). May fill sync or start async load.
    /// Called every frame for the visible+overscan window.
    /// </summary>
    void EnsureRange(int start, int count);
}
