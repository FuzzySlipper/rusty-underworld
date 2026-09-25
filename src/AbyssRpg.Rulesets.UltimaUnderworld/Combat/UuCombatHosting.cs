using Rusty.Engine.Mechanics;
using AbyssRpg.Rulesets.UltimaUnderworld.Creation;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Combat;

/// <summary>
/// Combat integration: hold builds charge (full in FullChargeSeconds,
/// Ours — no donor number found), release resolves through the shared
/// strike path with charge-scaled damage on the defeat track, and defeat
/// routes through T34 death (tree/anchor). The Kit attack-execution
/// machine (cooldowns/impacts) integrates when NPC combat lands.
/// </summary>
public sealed class UuCombatHosting
{
    public const double FullChargeSeconds = 1.0;

    private double _heldSeconds;

    public float ChargeFraction => (float)Math.Clamp(_heldSeconds / FullChargeSeconds, 0.0, 1.0);

    public void Hold(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        _heldSeconds += seconds;
    }

    public void Reset() => _heldSeconds = 0;

    public sealed record StrikeOutcome(bool Hit, int Damage, bool TargetDefeated);

    public StrikeOutcome Release(
        StatsComponent attacker, StatsComponent target,
        int attackSkill, int difficulty, int damageSides, Random rng)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(rng);
        float charge = ChargeFraction;
        Reset();
        UuStrikeResolution.StrikeResult roll = UuStrikeResolution.RollToHit(attackSkill, difficulty, rng);
        if (roll is UuStrikeResolution.StrikeResult.Fail or UuStrikeResolution.StrikeResult.CritFail)
            return new StrikeOutcome(false, 0, false);
        int damage = UuStrikeResolution.ScaleByCharge(UuStrikeResolution.RollDamage(damageSides, rng), charge);
        Track targetHp = target.GetTrack(UuAvatarFactory.DefeatTrack);
        targetHp.Current = Math.Max(0, targetHp.Current - damage);
        return new StrikeOutcome(true, damage, targetHp.Current <= 0);
    }

    public static Magic.UuDeathPolicy.Respawn RouteDefeat(bool treeRebirthAvailable) =>
        Magic.UuDeathPolicy.Route(treeRebirthAvailable);
}
