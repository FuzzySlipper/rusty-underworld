using System.Numerics;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Magic;

/// <summary>
/// Movement/utility spell integration: speed doubling, slow-fall gravity,
/// gate-travel anchor math, telekinesis/open radii. Fly and Levitate hold
/// flight while maintained (callers set session Flying from effect
/// presence); Water Walk lets callers treat water as walkable.
/// Donor numbers: speed x2.0, gravity 9.81→2.5, gate 7.0/level with 10.0
/// threshold, open radius 3 tiles. Telekinesis range is Ours (3 tiles,
/// matching the open radius convention).
/// </summary>
public static class UuMovementSpells
{
    public const double NormalGravity = 9.81;
    public const double SlowFallGravity = 2.5;
    public const double SpeedMultiplier = 2.0;
    public const double GateVerticalPerLevel = 7.0;
    public const double GateDistanceThreshold = 10.0;
    public const double OpenRadiusTiles = 3.0;
    public const double TelekinesisRangeTiles = 3.0;

    public static double MoveSpeed(double baseSpeed, bool speedActive) =>
        speedActive ? baseSpeed * SpeedMultiplier : baseSpeed;

    public static double FallGravity(bool falling, bool slowFallActive) =>
        falling && slowFallActive ? SlowFallGravity : NormalGravity;

    public sealed record GateResult(int TargetLevel, Vector3 TargetPosition, double TrackedDistance);

    public static GateResult GateTravel(
        Vector3 currentPosition, int currentLevel,
        Vector3 anchorPosition, int anchorLevel,
        double trackedDistance)
    {
        double horizontal = Vector3.Distance(currentPosition, anchorPosition);
        double total = horizontal + GateVerticalPerLevel * Math.Abs(currentLevel - anchorLevel);
        return new GateResult(
            anchorLevel,
            anchorPosition + Vector3.UnitY,
            total > GateDistanceThreshold ? trackedDistance + total : trackedDistance);
    }
}
