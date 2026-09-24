namespace AbyssRpg.Rulesets.UltimaUnderworld.Magic;

/// <summary>
/// Maintained-spell admission gate (max 3): a same-spell cast replaces,
/// a stronger similar-group cast replaces its weaker kin, otherwise the
/// lowest-cost effect is evicted (donor code basis; cost anomalies keep raw
/// costs per the catalog decision). Similar groups are content (donor
/// similarSpells: shield, stealth, float, light, time).
/// Donor: Magic.TryCast admission (UW1 maintained-spell rule).
/// </summary>
public sealed class UuMaintainedSpells
{
    public const int MaxMaintained = 3;

    public sealed record MaintainedSpell(int SpellId, string Runes, int Cost);

    private static readonly string[][] SimilarGroups =
    [
        ["IVS", "IS", "BIS"],
        ["VSL", "BSL", "SH"],
        ["VHP", "HP", "RDP"],
        ["VIL", "IL"],
        ["RTP", "AT"],
    ];

    private readonly List<MaintainedSpell> _spells = [];

    public IReadOnlyList<MaintainedSpell> Spells => _spells;

    /// <summary>Admit a maintained spell; returns the evicted spell, if any.</summary>
    public MaintainedSpell? Admit(int spellId, string runes, int cost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runes);
        MaintainedSpell? evicted = null;

        if (_spells.Count == MaxMaintained)
        {
            int same = _spells.FindIndex(s => s.SpellId == spellId);
            if (same >= 0)
            {
                evicted = _spells[same];
                _spells.RemoveAt(same);
            }
            else
            {
                int weaker = FindWeakerSimilar(runes);
                if (weaker >= 0)
                {
                    evicted = _spells[weaker];
                    _spells.RemoveAt(weaker);
                }
            }
        }

        if (_spells.Count == MaxMaintained)
        {
            int lowest = 0;
            for (int i = 1; i < _spells.Count; i++)
                if (_spells[i].Cost < _spells[lowest].Cost) lowest = i;
            evicted = _spells[lowest];
            _spells.RemoveAt(lowest);
        }

        _spells.Add(new MaintainedSpell(spellId, runes, cost));
        return evicted;
    }

    public bool Dismiss(int spellId)
    {
        int index = _spells.FindIndex(s => s.SpellId == spellId);
        if (index < 0) return false;
        _spells.RemoveAt(index);
        return true;
    }

    private int FindWeakerSimilar(string runes)
    {
        foreach (string[] group in SimilarGroups)
        {
            int rank = Array.IndexOf(group, runes);
            if (rank < 0) continue;
            for (int i = 0; i < _spells.Count; i++)
            {
                int held = Array.IndexOf(group, _spells[i].Runes);
                if (held > rank) return i;
            }
        }

        return -1;
    }
}
