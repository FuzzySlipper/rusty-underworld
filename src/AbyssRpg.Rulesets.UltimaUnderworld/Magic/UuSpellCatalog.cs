namespace AbyssRpg.Rulesets.UltimaUnderworld.Magic;

/// <summary>
/// Spell catalog, circles 1-4: rune string, name, raw donor cost, maintained
/// icon (-1 = instant), table duration seconds, string index, family, and
/// aim requirement. Circle derives from cost (cost/3, donor SSpell.circle);
/// listed-circle anomalies keep their raw costs (UP 6, IS 12, AS 24, YP 9),
/// so gates and eviction follow the donor code, not the donor list order.
/// </summary>
public static class UuSpellCatalog
{
    public enum Family
    {
        Damage,
        Light,
        Heal,
        Protection,
        Other,
    }

    public sealed record SpellEntry(
        string Runes,
        string Name,
        int Cost,
        int Icon,
        double Duration,
        int StringIndex,
        Family SpellFamily,
        bool NeedsAim)
    {
        public int Circle => Cost / 3;
    }

    public static readonly IReadOnlyList<SpellEntry> Circles1To4 =
    [        new("IMY", "Create Food", 3, -1, 0, 259, Family.Other, false),
        new("UP", "Leap", 6, 0, 60, 261, Family.Other, false),
        new("IL", "Light", 3, 17, 1500, 256, Family.Light, false),
        new("OJ", "Magic Arrow", 3, -1, 0, 258, Family.Damage, true),
        new("BIS", "Resist Blows", 3, 15, 90, 257, Family.Protection, false),
        new("SH", "Stealth", 3, 6, 30, 260, Family.Protection, false),
        new("QC", "Create Fear", 6, -1, 0, 266, Family.Other, false),
        new("AS", "Curse", 24, 5, 0, 262, Family.Other, false),
        new("WM", "Detect Monster", 6, -1, 0, 265, Family.Other, false),
        new("IBM", "Lesser Heal", 6, -1, 0, 264, Family.Heal, false),
        new("IJ", "Rune of Warding", 6, -1, 0, 267, Family.Other, false),
        new("RDP", "Slow Fall", 6, 1, 30, 263, Family.Protection, false),
        new("BSL", "Conceal", 9, 7, 60, 269, Family.Protection, false),
        new("OG", "Lightning", 9, -1, 0, 271, Family.Damage, true),
        new("QL", "Night Vision", 9, 12, 120, 270, Family.Light, false),
        new("RTP", "Speed", 9, 13, 30, 268, Family.Other, false),
        new("SJ", "Strengthen Door", 9, -1, 0, 272, Family.Other, true),
        new("IS", "Thick Skin", 12, 16, 90, 273, Family.Protection, false),
        new("SF", "Flameproof", 12, 10, 90, 278, Family.Protection, false),
        new("IM", "Heal", 12, -1, 0, 275, Family.Heal, false),
        new("NM", "Poison", 12, -1, 0, 277, Family.Damage, false),
        new("AJ", "Remove Trap", 12, -1, 0, 279, Family.Other, false),
        new("YP", "Water Walk", 9, 3, 180, 274, Family.Other, false),
    ];

    public static readonly IReadOnlyList<SpellEntry> Circles5To8 =
    [
        new("AN", "Cure Poison", 15, -1, 0, 285, Family.Heal, false),
        new("PF", "Fireball", 15, -1, 0, 280, Family.Damage, true),
        new("HP", "Levitate", 15, 2, 60, 276, Family.Other, false),
        new("GSP", "Missile Protection", 15, 9, 60, 283, Family.Protection, false),
        new("OWY", "Name Enchantment", 15, -1, 0, 282, Family.Other, true),
        new("EY", "Open", 15, -1, 0, 284, Family.Other, true),
        new("ACM", "Smite Undead", 15, -1, 0, 281, Family.Damage, false),
        new("VIL", "Daylight", 18, 20, 300, 290, Family.Light, false),
        new("VRP", "Gate Travel", 18, -1, 0, 288, Family.Other, false),
        new("VIM", "Greater Heal", 18, -1, 0, 286, Family.Heal, false),
        new("AEP", "Paralyze", 18, -1, 0, 289, Family.Other, false),
        new("VOG", "Sheet Lightning", 18, -1, 0, 287, Family.Damage, false),
        new("OPY", "Telekinesis", 18, 14, 30, 291, Family.Other, false),
        new("IMR", "Ally", 21, -1, 0, 293, Family.Other, false),
        new("VHP", "Fly", 21, 4, 30, 292, Family.Other, false),
        new("VSL", "Invisibility", 21, 8, 120, 295, Family.Protection, false),
        new("VAW", "Confusion", 21, -1, 0, 296, Family.Other, false),
        new("OAQ", "Reveal", 21, -1, 0, 297, Family.Other, false),
        new("KM", "Summon Monster", 21, -1, 0, 294, Family.Other, false),
        new("VKC", "Armageddon", 24, -1, 0, 303, Family.Damage, false),
        new("FH", "Flame Wind", 24, -1, 0, 301, Family.Damage, false),
        new("AT", "Freeze Time", 24, 11, 30, 302, Family.Other, false),
        new("IVS", "Iron Flesh", 24, 18, 120, 298, Family.Protection, false),
        new("OPW", "Roaming Sight", 24, 19, 30, 300, Family.Other, false),
        new("VPY", "Tremor", 24, -1, 0, 299, Family.Other, false),
    ];

    public static SpellEntry? FindByRunes(string runes) =>
        Circles1To4.Concat(Circles5To8).FirstOrDefault(spell => spell.Runes == runes);
}
