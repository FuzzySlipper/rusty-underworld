using AbyssRpg.Kit.Controls;
using AbyssRpg.Rulesets.UltimaUnderworld.Movement;
using Rusty.Engine;

namespace AbyssRpg.Host;

/// <summary>
/// Product-owned spatial session: assembles the Kit movement system over
/// Engine spatial/content services and a content artifact, holds the player
/// control state, and steps UW locomotion policy through Engine collision
/// into receipt consequences. Collision content arrives with packs (UW-T05);
/// until then the artifact is a caller-supplied fixture.
/// </summary>
public sealed class AbyssSpatialSession : IDisposable
{
    private readonly SpatialMovementSystem _system;
    private bool _disposed;

    public PlayerControlState Player { get; }

    public AbyssSpatialSession(
        IEngineContext context,
        SpatialContentArtifact artifact,
        SpatialTuning tuning,
        PlayerControlState player)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(player);
        _system = new SpatialMovementSystem(context.Spatial, context.Content, artifact, tuning.Validate());
        Player = player;
    }

    public sealed record LocomotionStepResult(
        UuLocomotionPolicy.UuMoveIntent Intent,
        CharacterStepReceipt? Receipt,
        float FallDamage,
        bool Moved);

    /// <summary>
    /// Consume input through UW policy, step Engine collision, map
    /// consequences. A null receipt (no position) yields intent only.
    /// </summary>
    public LocomotionStepResult StepLocomotion(
        UuLocomotionPolicy policy,
        ReadOnlySpan<ProductInputEvent> inputs,
        ProductUpdateState update,
        UuMovementTuning tuning,
        bool canMove,
        bool swimming,
        bool flying)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(tuning);

        (CharacterStepControls controls, UuLocomotionPolicy.UuMoveIntent intent) =
            policy.BeginStep(inputs, update.DeltaSeconds, canMove, swimming, flying);
        CharacterMotion before = Player.Motion;
        CharacterStepReceipt? receipt = _system.Step(Player, update, null, controls);
        if (receipt is not { } confirmed)
            return new LocomotionStepResult(intent, null, 0f, false);
        return new LocomotionStepResult(
            intent,
            confirmed,
            UuStepConsequences.LandingDamage(before, confirmed, tuning),
            UuStepConsequences.Moved(confirmed));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _system.Dispose();
    }
}
