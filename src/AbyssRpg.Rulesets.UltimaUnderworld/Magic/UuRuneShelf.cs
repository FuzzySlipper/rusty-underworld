namespace AbyssRpg.Rulesets.UltimaUnderworld.Magic;

/// <summary>
/// Rune shelf state: the ordered runes laid out ready to cast (the manual's
/// "rune shelf"). Tracks owned runestones separately from shelf placement.
/// </summary>
public sealed class UuRuneShelf
{
    private readonly List<int> _shelf = [];
    private readonly bool[] _owned = new bool[UuRuneCatalog.Count];

    public IReadOnlyList<int> Shelf => _shelf;

    public void AddRunestone(int index)
    {
        if ((uint)index >= UuRuneCatalog.Count) throw new ArgumentOutOfRangeException(nameof(index));
        _owned[index] = true;
    }

    public bool Owns(int index) => _owned[index];

    public bool OwnsAll(IReadOnlyList<int> indices)
    {
        ArgumentNullException.ThrowIfNull(indices);
        return indices.All(i => (uint)i < UuRuneCatalog.Count && _owned[i]);
    }

    /// <summary>Lay an owned runestone on the shelf. Unowned stones cannot be shelved.</summary>
    public void Place(int index)
    {
        if ((uint)index >= UuRuneCatalog.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (!_owned[index]) throw new InvalidOperationException($"Runestone {UuRuneCatalog.Names[index]} is not owned.");
        _shelf.Add(index);
    }

    public void Clear() => _shelf.Clear();
}
