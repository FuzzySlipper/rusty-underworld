using AbyssRpg.Kit.Facts;

namespace AbyssRpg.Kit.Combat;

/// <summary>
/// The attack capability actor behaviour consumes: reach, readiness, beginning a
/// creature's attack through the one attack lifecycle, and interrupting it. The
/// avatar's own melee is the ruleset's charge path, not this seam. Kept for the
/// creature behaviour driver (Den #8683), whose ruleset implements it over
/// <see cref="AttackExecution{TFact}"/>; <see cref="Ai.PursuitCoordinator{TFact}"/>
/// is its consumer.
/// </summary>
public interface IAttackCapabilities<TFact> where TFact : IAbyssRpgFact
{
    double? ReachOf(long actorId);
    bool IsReady(long actorId, ulong generation, ulong step);
    bool TryBeginEnemyAttack(long attacker, long target, ulong generation, ulong step, double delta, FactBuffer<TFact> facts);
    void InterruptPendingAttack(long attacker, ulong generation);
}
