namespace SpawnDev.GameUI.Elements;

/// <summary>
/// In-memory <see cref="IListDataSource"/> for tests, Gallery, and consumers that
/// already hold the full item list (e.g. loaded project metadata).
/// EnsureRange is a no-op; TryGetItem always succeeds for valid indices.
/// </summary>
public class MemoryListDataSource : IListDataSource
{
    private readonly List<ListItem> _items = new();

    public int Count => _items.Count;

    public MemoryListDataSource() { }

    public MemoryListDataSource(IEnumerable<ListItem> items)
    {
        _items.AddRange(items);
    }

    public MemoryListDataSource(IEnumerable<string> texts)
    {
        foreach (var t in texts)
            _items.Add(new ListItem { Text = t });
    }

    /// <summary>Create from string labels (Project 0, Project 1, ...).</summary>
    public static MemoryListDataSource FromCount(int count, Func<int, string>? label = null)
    {
        label ??= i => $"Item {i}";
        var src = new MemoryListDataSource();
        for (int i = 0; i < count; i++)
            src._items.Add(new ListItem { Text = label(i), Tag = i });
        return src;
    }

    public void Add(string text, object? tag = null)
    {
        _items.Add(new ListItem { Text = text, Tag = tag });
    }

    public void Add(ListItem item) => _items.Add(item);

    public void Clear() => _items.Clear();

    public bool TryGetItem(int index, out ListItem item)
    {
        if (index < 0 || index >= _items.Count)
        {
            item = new ListItem();
            return false;
        }
        item = _items[index];
        return true;
    }

    public void EnsureRange(int start, int count)
    {
        // All items already resident.
    }
}
