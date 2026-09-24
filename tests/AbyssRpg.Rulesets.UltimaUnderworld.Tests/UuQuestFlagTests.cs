using AbyssRpg.Kit.Knowledge;
using AbyssRpg.Rulesets.UltimaUnderworld.Knowledge;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuQuestFlagTests
{
    [Fact]
    public void Named_flags_address_conversation_slots()
    {
        var vars = new QuestVariables();
        vars.Set(UuQuestFlags.MurgoFreed, 1);
        vars.Set(UuQuestFlags.KnightOfCrux, 3);
        Assert.Equal(1, vars.Get(UuQuestFlags.MurgoFreed));
        Assert.Equal(3, vars.Get(UuQuestFlags.KnightOfCrux));
        Assert.InRange(UuQuestFlags.Dreams, 0, QuestVariables.SlotCount - 1);
    }
}
