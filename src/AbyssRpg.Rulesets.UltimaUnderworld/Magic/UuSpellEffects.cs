namespace AbyssRpg.Rulesets.UltimaUnderworld.Magic;

/// <summary>
/// Core family effects (circles 1-4): heal amounts (vitality fractions),
/// maintained light/protection admission through the max-3 gate, and damage
/// projectiles through the shared melee/missile path. Damage strikes
/// whatever it hits — no faction filter — so friendly fire is inherent.
/// Donor magnitudes: Lesser Heal vitality/4, Heal 5/8 vitality; projectiles
/// use missile-table damage; protections are presence effects.
/// </summary>
public static class UuSpellEffects
{
    public static int HealAmount(string runes, int vitality) => runes switch
    {
        "IBM" => vitality / 4,
        "IM" => 5 * vitality / 8,
        _ => throw new ArgumentOutOfRangeException(nameof(runes), $"No heal amount for '{runes}'."),
    };

    /// <summary>Admit a maintained family spell; returns the evicted spell, if any.</summary>
    public static UuMaintainedSpells.MaintainedSpell? AdmitMaintained(
        UuMaintainedSpells maintained, UuSpellCatalog.SpellEntry spell, int spellId)
    {
        ArgumentNullException.ThrowIfNull(maintained);
        ArgumentNullException.ThrowIfNull(spell);
        if (spell.Icon < 0) throw new ArgumentOutOfRangeException(nameof(spell), "Instant spells are not maintained.");
        return maintained.Admit(spellId, spell.Runes, spell.Cost);
    }
}
