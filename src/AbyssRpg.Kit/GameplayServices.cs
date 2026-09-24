using AbyssRpg.Kit.Actors;
using AbyssRpg.Kit.Combat;
using AbyssRpg.Kit.Facts;
using AbyssRpg.Kit.Inventory;
using AbyssRpg.Kit.Targeting;

namespace AbyssRpg.Kit;

/// <summary>Explicit named gameplay composition, with state reached through its canonical owners.</summary>
public sealed class GameplayServices<TFact>(ActorsState actors, TargetingService targeting,
    IAttackCapabilities<TFact> attacks, AttackExecution<TFact> execution, CombatResolution rules,
    MechanicsInventoryCoordinator inventory, MechanicsEquipmentCoordinator equipment) where TFact : IAbyssRpgFact
{
    public ActorsState Actors { get; } = actors;
    public TargetingService Targeting { get; } = targeting;
    public IAttackCapabilities<TFact> Attacks { get; } = attacks;
    public AttackExecution<TFact> AttackExecution { get; } = execution;
    public CombatResolution Rules { get; } = rules;
    public MechanicsInventoryCoordinator Inventory { get; } = inventory;
    public MechanicsEquipmentCoordinator Equipment { get; } = equipment;
}
