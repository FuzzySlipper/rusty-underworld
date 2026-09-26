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

        UuMaintainedSpells.MaintainedSpell? evicted = null;
        UuCastingWorkflow.SpellEffectInstance? effect = null;
        if (spell.Icon >= 0)
        {
            // Duration-0 maintained (Curse) holds until dismissed: its effect never
            // expires, and the held spell carries no deadline of its own. Anything else
            // ends at its table duration with the game's spread.
            bool held = spell.Duration > 0;
            ulong expires = held
                ? nowTicks + UuCastingWorkflow.DurationTicks(spell.Duration, ticksPerSecond, _rng)
                : ulong.MaxValue;
            evicted = UuSpellEffects.AdmitMaintained(_maintained, spell, spellId, held ? expires : 0);
            (_, effect) = UuCastingWorkflow.Apply(
                false, spellId, expires,
                stability: 1 /* stable class; per-spell classes ride with UuSpellStability */);
            if (effect is not null) _effects.Add(effect);
        }

        return new CastOutcome(UuCastGates.GateResult.Cast, false, 0, spell.Cost, false, effect, evicted);
    }
}
