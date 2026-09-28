namespace AbyssRpg.Rulesets.UltimaUnderworld.Magic;

/// <summary>
/// Casting hosting: shelf runes resolve to catalog spells, then gates,
/// cast roll, effect application, and maintained admission run in one flow
/// with upkeep (expiry) and panel data. Mana deduction rides with the
/// caller (avatar track); aim resolution rides with targeting.
/// </summary>
public sealed class UuCastingHosting
{
    public sealed record CastOutcome(
        UuCastGates.GateResult Gate,
        bool Backfired,
        int BackfireDamage,
        int ManaCost,
        bool PrimedForAim,
        UuCastingWorkflow.SpellEffectInstance? Effect,
        UuMaintainedSpells.MaintainedSpell? Evicted);

    public sealed record MagicPanel(
        IReadOnlyList<int> Shelf,
        IReadOnlyList<UuMaintainedSpells.MaintainedSpell> Maintained,
        IReadOnlyList<UuCastingWorkflow.SpellEffectInstance> Effects);

    private readonly UuRuneShelf _shelf = new();
    private readonly UuMaintainedSpells _maintained = new();
    private readonly List<UuCastingWorkflow.SpellEffectInstance> _effects = [];
    private readonly Random _rng;

    public UuCastingHosting(Random? rng = null)
    {
        _rng = rng ?? Random.Shared;
    }

    public void CollectRune(int index) => _shelf.AddRunestone(index);

    public void ShelfRune(int index) => _shelf.Place(index);

    public void ClearShelf() => _shelf.Clear();

    public sealed record TrapCastOutcome(bool Supported, int Healing, int ProjectileItem = 0, string? MaintainedRunes = null);

    /// <summary>
    /// A trap supplies its spell class directly, without runes, mana or a casting
    /// roll. Donor: src/traps/a_spell_trap.cs and magic/spellcasting_class_4.cs.
    /// Healing is variant d8, except variant 15 restores all missing health.
    /// Other classes need their actual effect owners before they can be accepted.
    /// </summary>
    public TrapCastOutcome CastTrap(int major, int minor, int missingHealth, ulong nowTicks = 0, double ticksPerSecond = 1)
    {
        // UW1 runicmagic.cs maps these class-0 variants to the existing light
        // family. Use our runic duration/admission policy, not donor status rounds.
        string? light = major == 0 ? minor switch { 3 => "IL", 5 => "QL", 6 => "VIL", _ => null } : null;
        if (light is not null)
        {
            Admit(UuSpellCatalog.FindByRunes(light)!, -minor, nowTicks, ticksPerSecond);
            return new(true, 0, MaintainedRunes: light);
        }
        if (major == 5 && minor is >= 1 and <= 4)
            return new(true, 0, minor switch { 1 => 23, 2 => 21, 3 => 20, _ => 22 });
        if (major != 4 || minor < 0 || minor > 63) return new(false, 0);
        int healing = 0;
        if (minor == 15) healing = missingHealth;
        else for (int die = 0; die < minor; die++) healing += _rng.Next(1, 9);
        return new(true, Math.Clamp(healing, 0, Math.Max(0, missingHealth)));
    }

    public MagicPanel Panel() => new(_shelf.Shelf.ToArray(), _maintained.Spells.ToArray(), _effects.ToArray());

    /// <summary>
    /// Whether a spell of a family is being maintained right now. What the session
    /// is holding is the maintained owner's answer, so a caller asking "is the
    /// avatar lit" reads it here rather than keeping a light timer of its own.
    /// </summary>
    public bool MaintainsFamily(UuSpellCatalog.Family family) =>
        _maintained.Spells.Any(maintained =>
            UuSpellCatalog.FindByRunes(maintained.Runes) is { } spell && spell.SpellFamily == family);

    public void Upkeep(ulong nowTicks)
    {
        // A held spell ends at its own deadline, in the owner that holds it, as well
        // as when the effect carrying it expires.
        _maintained.Expire(nowTicks);
        var expired = _effects.Where(e => UuCastingWorkflow.IsExpired(e, nowTicks)).ToList();
        _effects.RemoveAll(expired.Contains);
        foreach (UuCastingWorkflow.SpellEffectInstance e in expired) _maintained.Dismiss(e.SpellId);
    }

    /// <summary>
    /// Cast the shelved runes. The caller deducts ManaCost from the avatar
    /// track on Cast; targeting consumes PrimedForAim.
    /// </summary>
    public CastOutcome AttemptCast(
        int characterLevel, int mana, int castingSkill,
        bool delayed, ulong nowTicks, double ticksPerSecond, int spellId)
    {
        string runes = UuRuneCatalog.SpellLetters(_shelf.Shelf);
        UuSpellCatalog.SpellEntry? spell = UuSpellCatalog.FindByRunes(runes);
        UuCastGates.GateResult gate = UuCastGates.CheckGates(
            spell is not null, characterLevel, spell?.Circle ?? 0, mana, delayed);
        if (gate != UuCastGates.GateResult.Cast)
            return new CastOutcome(gate, false, 0, 0, false, null, null);

        UuCastGates.GateResult cast = UuCastGates.RollCast(castingSkill, spell!.Circle, _rng);
        if (cast == UuCastGates.GateResult.Backfire)
            return new CastOutcome(cast, true, UuCastGates.RollBackfire(_rng), 0, false, null, null);
        if (cast != UuCastGates.GateResult.Cast)
            return new CastOutcome(cast, false, 0, 0, false, null, null);

        // Aimed spells prime for targeting before anything is admitted or applied.
        if (spell.NeedsAim)
            return new CastOutcome(UuCastGates.GateResult.Cast, false, 0, spell.Cost, true, null, null);

        var (effect, evicted) = Admit(spell, spellId, nowTicks, ticksPerSecond);

        return new CastOutcome(UuCastGates.GateResult.Cast, false, 0, spell.Cost, false, effect, evicted);
    }
    private (UuCastingWorkflow.SpellEffectInstance? Effect, UuMaintainedSpells.MaintainedSpell? Evicted)
        Admit(UuSpellCatalog.SpellEntry spell, int spellId, ulong nowTicks, double ticksPerSecond)
    {
        if (spell.Icon < 0) return (null, null);
        ulong expires = spell.Duration > 0
            ? nowTicks + UuCastingWorkflow.DurationTicks(spell.Duration, ticksPerSecond, _rng)
            : ulong.MaxValue;
        var evicted = UuSpellEffects.AdmitMaintained(_maintained, spell, spellId,
            spell.Duration > 0 ? expires : 0);
        if (evicted is not null) _effects.RemoveAll(e => e.SpellId == evicted.SpellId);
        var effect = new UuCastingWorkflow.SpellEffectInstance(spellId, expires, 1);
        _effects.Add(effect);
        return (effect, evicted);
    }

    public sealed record SavedState(UuMaintainedSpells.MaintainedSpell[] Maintained,
        UuCastingWorkflow.SpellEffectInstance[] Effects);

    public SavedState Capture() => new(_maintained.Spells.ToArray(), _effects.ToArray());

    public void Restore(SavedState state)
    {
        _maintained.Restore(state.Maintained);
        _effects.Clear();
        _effects.AddRange(state.Effects);
    }

}
