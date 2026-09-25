using AbyssRpg.Rulesets.UltimaUnderworld.Conversation;
using AbyssRpg.Rulesets.UltimaUnderworld.Session;
using AbyssRpg.Rulesets.UltimaUnderworld.Social;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuHostingTests
{
    private static UuConversationVm Script(UuConversationHosting hosting, params short[] code) =>
        new(new UuConversationVm.ConversationScript(0, code.Length, 0, 4,
                [new UuConversationVm.ScriptImport("set_quest", 0x99, false, 0x129)],
                code),
            (block, index) => "", hosting, new StubVariables());

    private sealed class StubVariables : IUuConversationVariables
    {
        public int Read(int address) => 0;
        public void Write(int address, int value) { }
    }

    [Fact]
    public void Quest_offer_and_menu_calls_flow()
    {
        using var session = UuSnapshotTests.TestSession();
        var hosting = new UuConversationHosting(session, new Random(3));
        hosting.SetTray(new UuConversationHosting.BarterTray(10, 30, 5, 30, 3));

        // set_quest(0, 2): PUSHI value, PUSHI slot... convention: top=depth0.
        // set_quest reads slot=Peek(1), value=Peek(0): push slot then value.
        var vm = Script(hosting, 22, 0, 22, 2, 20, 0x99, 38);
        vm.Run();
        Assert.Equal(2, session.Quests.Get(0));
    }
}
