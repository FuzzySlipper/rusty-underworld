using AbyssRpg.Rulesets.UltimaUnderworld.Traps;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuTrapTests
{
    [Fact]
    public void Classifies_uw1_kinds_and_door_traps()
    {
        Assert.Equal(UuTrapDispatch.TrapKind.Damage, UuTrapDispatch.Classify(6, 0, 0));
        Assert.Equal(UuTrapDispatch.TrapKind.Hack, UuTrapDispatch.Classify(6, 0, 3));
        Assert.Equal(UuTrapDispatch.TrapKind.Pit, UuTrapDispatch.Classify(6, 0, 4));
        Assert.Equal(UuTrapDispatch.TrapKind.Door, UuTrapDispatch.Classify(6, 1, 0));
        Assert.Equal(UuTrapDispatch.TrapKind.Unknown, UuTrapDispatch.Classify(5, 0, 0));
        Assert.Equal(UuTrapDispatch.TrapKind.Unknown, UuTrapDispatch.Classify(6, 0, 99));

        Assert.Equal(UuTrapDispatch.DoorTrapAction.Open, UuTrapDispatch.DoorAction(1));
        Assert.Equal(UuTrapDispatch.DoorTrapAction.Close, UuTrapDispatch.DoorAction(2));
        Assert.Equal(UuTrapDispatch.DoorTrapAction.Toggle, UuTrapDispatch.DoorAction(3));
        Assert.Equal(UuTrapDispatch.DoorTrapAction.None, UuTrapDispatch.DoorAction(0));
    }

    [Fact]
    public void Chains_fire_in_link_order_with_cycle_guard()
    {
        var traps = new Dictionary<int, (UuTrapDispatch.TrapKind, int)>
        {
            [1] = (UuTrapDispatch.TrapKind.Damage, 2),
            [2] = (UuTrapDispatch.TrapKind.Door, 3),
            [3] = (UuTrapDispatch.TrapKind.Teleport, 0),
        };
        var fired = UuTrapDispatch.FireChain(i => traps[i], 1);
        Assert.Equal(
            [UuTrapDispatch.TrapKind.Damage, UuTrapDispatch.TrapKind.Door, UuTrapDispatch.TrapKind.Teleport],
            fired);

        traps[3] = (UuTrapDispatch.TrapKind.Teleport, 1); // cycle
        Assert.Equal(3, UuTrapDispatch.FireChain(i => traps[i], 1).Count);
        Assert.Empty(UuTrapDispatch.FireChain(i => traps[i], 0));
    }
}
