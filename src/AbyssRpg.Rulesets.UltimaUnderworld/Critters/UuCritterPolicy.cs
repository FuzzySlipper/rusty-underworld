using AbyssRpg.Kit.Ai;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Critters;

/// <summary>
/// UW critter behavior policy: senses gate pursuit, critical hits received
/// below a third of maximum health trigger retreat, and vertical movement
/// follows locomotion kind — fliers climb freely, swimmers hold water level,
/// walkers stay grounded.
/// Donor structure: retreat-on-crit-below-third (Critter fear branch);
/// corpse item id 0xC0 + table index; swimmer/flier flags from the critter
/// row. Sense radii and the retreat threshold shape are Ours.
/// </summary>
public static class UuCritterPolicy
{
    public sealed record Senses(double SightRadius, double HearingRadius)
    {
        public Senses Validate()
        {
            if (!double.IsFinite(SightRadius) || SightRadius <= 0d)
                throw new ArgumentOutOfRangeException(nameof(SightRadius));
            if (!double.IsFinite(HearingRadius) || HearingRadius <= 0d)
                throw new ArgumentOutOfRangeException(nameof(HearingRadius));
            return this;
        }
    }

    public static readonly Senses DefaultSenses = new(SightRadius: 12.0, HearingRadius: 6.0);

    public enum VerticalKind
    {
        Walker,
        Swimmer,
        Flier,
    }

    public static VerticalKind LocomotionKind(bool isSwimmer, bool isFlier) =>
        isFlier ? VerticalKind.Flier : isSwimmer ? VerticalKind.Swimmer : VerticalKind.Walker;

    public static bool ShouldRetreat(int hp, int maxHp, bool receivedCrit)
    {
        if (maxHp <= 0) throw new ArgumentOutOfRangeException(nameof(maxHp));
        return receivedCrit && (hp * 3) < maxHp;
    }

    public static PursuitState DecideState(bool seesTarget, bool inAttackRange, bool dead, bool retreating) =>
        dead ? PursuitState.Dead
        : retreating ? PursuitState.Retreat
        : !seesTarget ? PursuitState.Idle
        : inAttackRange ? PursuitState.Attack
        : PursuitState.Chase;

    /// <summary>Corpse item id for a table corpse index; 0 means no corpse.</summary>
    public static int CorpseItemId(int corpseIndex) => corpseIndex == 0 ? 0 : 0xC0 + corpseIndex;
}
