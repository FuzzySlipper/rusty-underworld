using Rusty.Engine;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Movement;

/// <summary>
/// Kit step-receipt consequences: landing detection from before/after motion
/// routed into fall damage, and moved-this-step for skill/run accounting.
/// The live call site (policy → SpatialMovementSystem.Step → here) assembles
/// with the spatial session; this mapping is the session-independent half.
/// </summary>
public static class UuStepConsequences
{
    public static float LandingDamage(CharacterMotion before, CharacterStepReceipt receipt, UuMovementTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        if (before.Grounded || !receipt.Motion.Grounded) return 0f;
        float fellFeet = (before.PeakY - receipt.Transform.Translation.Y) * tuning.FeetPerUnit;
        if (fellFeet <= 0f) return 0f;
        return UuMovementHazards.FallDamage(fellFeet, tuning);
    }

    public static bool Moved(CharacterStepReceipt receipt) =>
        receipt.Displacement.X != 0f || receipt.Displacement.Z != 0f;
}
