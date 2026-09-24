namespace AbyssRpg.Kit.Knowledge;

/// <summary>Quill notes: player-placed map annotations (text + tile position) per level page.</summary>
public sealed record MapNote(string Text, int X, int Y);

public sealed class QuillNotes
{
    private readonly Dictionary<int, List<MapNote>> _pages = [];

    public IReadOnlyList<MapNote> Notes(int level) =>
        _pages.TryGetValue(level, out List<MapNote>? notes) ? notes : [];

    /// <summary>Place a note; returns its index on the page.</summary>
    public int Place(int level, string text, int x, int y)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (!_pages.TryGetValue(level, out List<MapNote>? notes))
            _pages[level] = notes = [];
        notes.Add(new MapNote(text, x, y));
        return notes.Count - 1;
    }

    public void Edit(int level, int index, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        List<MapNote> notes = Page(level);
        if ((uint)index >= (uint)notes.Count) throw new ArgumentOutOfRangeException(nameof(index));
        notes[index] = notes[index] with { Text = text };
    }

    public void Delete(int level, int index)
    {
        List<MapNote> notes = Page(level);
        if ((uint)index >= (uint)notes.Count) throw new ArgumentOutOfRangeException(nameof(index));
        notes.RemoveAt(index);
    }

    private List<MapNote> Page(int level) =>
        _pages.TryGetValue(level, out List<MapNote>? notes) ? notes : throw new InvalidOperationException($"No notes on level {level}.");
}
