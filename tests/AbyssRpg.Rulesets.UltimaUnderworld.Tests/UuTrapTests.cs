using AbyssRpg.Rulesets.UltimaUnderworld.Traps;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuTrapTests
{
    [Fact]
    public void Classifies_uw1_kinds()
    {
        Assert.Equal(UuTrapDispatch.TrapKind.Damage, UuTrapDispatch.Classify(6, 0, 0));
        Assert.Equal(UuTrapDispatch.TrapKind.Hack, UuTrapDispatch.Classify(6, 0, 3));
        Assert.Equal(UuTrapDispatch.TrapKind.Pit, UuTrapDispatch.Classify(6, 0, 4));
        Assert.Equal(UuTrapDispatch.TrapKind.Door, UuTrapDispatch.Classify(6, 0, 8));
        Assert.Equal(UuTrapDispatch.TrapKind.Ward, UuTrapDispatch.Classify(6, 0, 9));
        Assert.Equal(UuTrapDispatch.TrapKind.Tell, UuTrapDispatch.Classify(6, 0, 0xA));
        Assert.Equal(UuTrapDispatch.TrapKind.DeleteObject, UuTrapDispatch.Classify(6, 0, 0xB));
        Assert.Equal(UuTrapDispatch.TrapKind.Inventory, UuTrapDispatch.Classify(6, 0, 0xC));
        Assert.Equal(UuTrapDispatch.TrapKind.SetVariable, UuTrapDispatch.Classify(6, 0, 0xD));
        Assert.Equal(UuTrapDispatch.TrapKind.CheckVariable, UuTrapDispatch.Classify(6, 0, 0xE));
        Assert.Equal(UuTrapDispatch.TrapKind.Null, UuTrapDispatch.Classify(6, 0, 0xF));
        Assert.Equal(UuTrapDispatch.TrapKind.TextString, UuTrapDispatch.Classify(6, 1, 0));
        Assert.Equal(UuTrapDispatch.TrapKind.TriggerLeg, UuTrapDispatch.Classify(6, 2, 0));
        Assert.Equal(UuTrapDispatch.TrapKind.TriggerLeg, UuTrapDispatch.Classify(6, 3, 5));
        Assert.Equal(UuTrapDispatch.TrapKind.Unknown, UuTrapDispatch.Classify(5, 0, 0));
        Assert.Equal(UuTrapDispatch.TrapKind.Unknown, UuTrapDispatch.Classify(6, 0, 0x10));

        Assert.Equal(UuTrapDispatch.DoorTrapAction.Open, UuTrapDispatch.DoorAction(1));
        Assert.Equal(UuTrapDispatch.DoorTrapAction.Close, UuTrapDispatch.DoorAction(2));
        Assert.Equal(UuTrapDispatch.DoorTrapAction.Toggle, UuTrapDispatch.DoorAction(3));
        Assert.Equal(UuTrapDispatch.DoorTrapAction.None, UuTrapDispatch.DoorAction(0));
    }

    [Fact]
    public void Chains_branch_stop_and_continue()
    {
        var traps = new Dictionary<int, UuTrapDispatch.ChainNode>
        {
            [1] = new(UuTrapDispatch.TrapKind.Damage, 2),
            [2] = new(UuTrapDispatch.TrapKind.CheckVariable, 3, 4),
            [3] = new(UuTrapDispatch.TrapKind.Teleport, 0),
            [4] = new(UuTrapDispatch.TrapKind.Door, 0),
        };
        UuTrapDispatch.ChainNode Resolve(int i) => traps[i];

        // True branch follows the link.
        Assert.Equal(
            [UuTrapDispatch.TrapKind.Damage, UuTrapDispatch.TrapKind.CheckVariable, UuTrapDispatch.TrapKind.Teleport],
            UuTrapDispatch.FireChain(Resolve, 1, _ => true));
        // False branch takes the alt link.
        Assert.Equal(
            [UuTrapDispatch.TrapKind.Damage, UuTrapDispatch.TrapKind.CheckVariable, UuTrapDispatch.TrapKind.Door],
            UuTrapDispatch.FireChain(Resolve, 1, _ => false));

        // Create/delete always stop, even with a nonzero link.
        var terminal = new Dictionary<int, UuTrapDispatch.ChainNode>
        {
            [1] = new(UuTrapDispatch.TrapKind.CreateObject, 2),
            [2] = new(UuTrapDispatch.TrapKind.Damage, 0),
        };
        Assert.Equal([UuTrapDispatch.TrapKind.CreateObject], UuTrapDispatch.FireChain(i => terminal[i], 1));

        terminal[1] = new(UuTrapDispatch.TrapKind.DeleteObject, 2);
        Assert.Equal([UuTrapDispatch.TrapKind.DeleteObject], UuTrapDispatch.FireChain(i => terminal[i], 1));

        // Cycles stop.
        traps[4] = new(UuTrapDispatch.TrapKind.Door, 1);
        Assert.Equal(3, UuTrapDispatch.FireChain(Resolve, 1, _ => false).Count);
    }
}
