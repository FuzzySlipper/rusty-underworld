using AbyssRpg.Kit;
using Rusty.Engine;
using Xunit;

namespace AbyssRpg.Host.Tests;

public sealed class SaveSlotsTests
{
    [Fact]
    public void Autosave_quicksave_and_anchor_round_trip()
    {
        IEngineContext engine = EngineContextFake.Create(persistence: new InMemoryPersistenceService());
        using var store = new AbyssSaveStore(engine, "abyssrpg.saves");
        var slots = new AbyssSaveSlots(store);

        slots.Autosave(3, Payload([9, 9]));
        Assert.Equal([9, 9], slots.LoadAutosave(3));
        Assert.Null(slots.LoadAutosave(4));

        slots.PlantAnchor(Payload([7]));
        Assert.Equal([7], slots.LoadAnchor());

        // Rolling ring of three.
        Assert.Equal("quicksave/0", slots.Quicksave(Payload([1])));
        Assert.Equal("quicksave/1", slots.Quicksave(Payload([2])));
        Assert.Equal("quicksave/2", slots.Quicksave(Payload([3])));
        Assert.Equal("quicksave/0", slots.Quicksave(Payload([4])));
        Assert.Equal([4], store.Load("quicksave/0").State!.Payload);
        Assert.Throws<ArgumentOutOfRangeException>(() => AbyssSaveSlots.QuicksaveKey(3));
        // The envelope names the ruleset the payload came from, not one the Host assumes.
        Assert.Equal("abyssrpg.test-ruleset", store.Load("quicksave/0").State!.Ruleset);
    }

    private static RulesetSavePayload Payload(byte[] bytes) => new(new RulesetId("abyssrpg.test-ruleset"), bytes);
}
