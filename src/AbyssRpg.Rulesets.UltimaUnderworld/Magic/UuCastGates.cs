using AbyssRpg.Rulesets.UltimaUnderworld.Combat;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Magic;

/// <summary>
/// Cast gates in donor order: shelf holds a spell (2+ runes), caster level
/// ((level+1)/2 reaches the circle), mana covers the cost (3 per circle),
/// the cast delay has expired, then Casting+5 rolls against twice the circle
/// on the combat ladder — critical failure backfires 1-5 onto the caster,
/// failure fizzles, success spends mana and casts.
/// Donor: Magic.TryCastFromSpellRunes (UW.EXE 0x78ec8/0x78f4d).
/// </summary>
public static class UuCastGates
{
    public enum GateResult
    {
        Cast,
        NotASpell,
        LevelTooLow,
        NotEnoughMana,
        StillDelayed,
        Fizzle,
        Backfire,
    }

    /// <summary>Mana cost of a circle: three per circle.</summary>
    public static int ManaCost(int circle) => circle <= 0
        ? throw new ArgumentOutOfRangeException(nameof(circle))
        : checked(circle * 3);

    /// <summary>Circle derivation from a spell cost (integer division, donor SSpell.circle).</summary>
    public static int CircleForCost(int cost) => cost < 0
        ? throw new ArgumentOutOfRangeException(nameof(cost))
        : cost / 3;

    /// <summary>
    /// Shelves with fewer than 2 runes never reach the gates (the donor
    /// returns silently); only 2-3-rune shelves that match no spell report
    /// NotASpell.
    /// </summary>
    public static bool CanAttemptCast(int shelfCount) => shelfCount > 1;

    public static bool LevelReaches(int characterLevel, int circle) =>
        (characterLevel + 1) / 2 >= circle;

    public static GateResult CheckGates(bool isSpell, int characterLevel, int circle, int mana, bool delayed) =>
        !isSpell ? GateResult.NotASpell
        : !LevelReaches(characterLevel, circle) ? GateResult.LevelTooLow
        : mana < ManaCost(circle) ? GateResult.NotEnoughMana
        : delayed ? GateResult.StillDelayed
        : GateResult.Cast;
    // NOTE: no-magic zones (level-7 orb, level 9) reject with the mana
    // message in the donor; zone availability rides with a future
    // magic-zone task owning level position state.

    public static GateResult ResolveRoll(UuStrikeResolution.StrikeResult roll) => roll switch
    {
        UuStrikeResolution.StrikeResult.CritFail => GateResult.Backfire,
        UuStrikeResolution.StrikeResult.Fail => GateResult.Fizzle,
        _ => GateResult.Cast,
    };

    /// <summary>Cast roll: Casting skill + 5 against twice the circle.</summary>
    public static GateResult RollCast(int castingSkill, int circle, Random rng) =>
        ResolveRoll(UuStrikeResolution.RollToHit(castingSkill + 5, 2 * circle, rng));

    /// <summary>Backfire injury: 1-5 (Unity Range(1,6) excludes the top).</summary>
    public static int RollBackfire(Random rng) => rng.Next(1, 6);
}
