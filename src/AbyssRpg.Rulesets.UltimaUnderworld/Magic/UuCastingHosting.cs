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

    public void Upkeep(ulong nowTicks) => _effects.RemoveAll(e => UuCastingWorkflow.IsExpired(e, nowTicks));

    public CastOutcome AttemptCast(
        string runes, int characterLevel, int mana, int castingSkill,
        bool delayed, ulong nowTicks, double ticksPerSecond, int spellId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runes);
        UuSpellCatalog.SpellEntry? spell = UuSpellCatalog.FindByRunes(runes);
        UuCastGates.GateResult gate = UuCastGates.CheckGates(
            spell is not null, characterLevel, spell?.Circle ?? 0, mana, delayed);
        if (gate != UuCastGates.GateResult.Cast)
            return new CastOutcome(gate, false, 0, null, null);

        UuCastGates.GateResult cast = UuCastGates.RollCast(castingSkill, spell!.Circle, _rng);
        if (cast == UuCastGates.GateResult.Backfire)
            return new CastOutcome(cast, true, UuCastGates.RollBackfire(_rng), null, null);
        if (cast != UuCastGates.GateResult.Cast)
            return new CastOutcome(cast, false, 0, null, null);

        UuMaintainedSpells.MaintainedSpell? evicted = null;
        UuCastingWorkflow.SpellEffectInstance? effect = null;
        if (spell.Icon >= 0)
        {
            evicted = UuSpellEffects.AdmitMaintained(_maintained, spell, spellId);
            ulong expires = nowTicks + UuCastingWorkflow.DurationTicks(spell.Duration, ticksPerSecond, _rng);
            (_, effect) = UuCastingWorkflow.Apply(spell.NeedsAim, spellId, expires, stability: 1);
            if (effect is not null) _effects.Add(effect);
        }

        return new CastOutcome(UuCastGates.GateResult.Cast, false, 0, effect, evicted);
    }
}
