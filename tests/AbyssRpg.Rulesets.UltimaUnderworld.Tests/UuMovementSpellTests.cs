using AbyssRpg.Rulesets.UltimaUnderworld.Magic;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuMovementSpellTests
{
    [Fact]
    public void Speed_and_gravity_follow_effects()
    {
        Assert.Equal(8.0, UuMovementSpells.MoveSpeed(4.0, true));
        Assert.Equal(4.0, UuMovementSpells.MoveSpeed(4.0, false));
        Assert.Equal(2.5, UuMovementSpells.FallGravity(true, true));
        Assert.Equal(9.81, UuMovementSpells.FallGravity(true, false));
        Assert.Equal(9.81, UuMovementSpells.FallGravity(false, true));
    }

    [Fact]
    public void Gate_travel_returns_to_the_anchor()
    {
        var result = UuMovementSpells.GateTravel(
            new System.Numerics.Vector3(30, 0, 0), 3,
            new System.Numerics.Vector3(0, 5, 0), 1,
            0.0);
        Assert.Equal(1, result.TargetLevel);
        Assert.Equal(new System.Numerics.Vector3(0, 6, 0), result.TargetPosition);
        Assert.True(result.TrackedDistance > 10.0);

        var near = UuMovementSpells.GateTravel(
            new System.Numerics.Vector3(1, 5, 0), 1,
            new System.Numerics.Vector3(0, 5, 0), 1,
            7.0);
        Assert.Equal(7.0, near.TrackedDistance); // under threshold: untracked
    }
}
