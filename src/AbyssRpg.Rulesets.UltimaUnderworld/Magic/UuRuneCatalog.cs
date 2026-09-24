namespace AbyssRpg.Rulesets.UltimaUnderworld.Magic;

/// <summary>
/// The 24 runes: index, short name, and shelf letter. Letters run A-X by
/// index with one quirk — index 23 ('X') is written and read as 'Y' —
/// matching the donor shelf encoding (RuneStone.cs, Magic.TryCastFromSpellRunes).
/// </summary>
public static class UuRuneCatalog
{
    public static readonly IReadOnlyList<string> Names =
    [
        "An", "Bet", "Corp", "Des", "Ex", "Flam", "Grav", "Hur", "In", "Jux", "Kal", "Lor",
        "Mani", "Nox", "Ort", "Por", "Quas", "Rel", "Sanct", "Tym", "Uus", "Vas", "Wis", "Ylem",
    ];

    public const int Count = 24;

    public static char Letter(int index)
    {
        if ((uint)index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
        int mapped = index == 23 ? 24 : index;
        return (char)('A' + mapped);
    }

    public static int Index(char letter)
    {
        int mapped = letter == 'Y' ? 23 : letter - 'A';
        if ((uint)mapped >= Count) throw new ArgumentOutOfRangeException(nameof(letter));
        return mapped;
    }

    public static string SpellLetters(IReadOnlyList<int> shelf)
    {
        ArgumentNullException.ThrowIfNull(shelf);
        return string.Concat(shelf.Select(Letter));
    }
}
