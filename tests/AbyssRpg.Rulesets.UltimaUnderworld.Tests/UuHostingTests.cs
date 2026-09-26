using AbyssRpg.Rulesets.UltimaUnderworld.Conversation;
using AbyssRpg.Rulesets.UltimaUnderworld.Session;
using AbyssRpg.Rulesets.UltimaUnderworld.Social;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuHostingTests
{
    private static UuConversationVm Script(
        UuConversationHosting hosting, string name, int id, params short[] code) =>
        new(new UuConversationVm.ConversationScript(0, code.Length, 0, 4,
                [new UuConversationVm.ScriptImport(name, id, false, 0x129)],
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
        var vm = Script(hosting, "set_quest", 0x99, 22, 0, 22, 2, 20, 0x99, 38);
        vm.Run();
        Assert.Equal(2, session.Quests.Get(0));
    }

    [Fact]
    public void Demand_shifts_the_named_npc_and_empty_trays_refuse()
    {
        using var session = UuSnapshotTests.TestSession();
        var hosting = new UuConversationHosting(session, new Random(3));
        hosting.SetTalker("murgo", new UuConversationHosting.NpcRecord(1, 10, 10));
        hosting.SetAttitude("murgo", Social.UuAttitude.Mellow);
        hosting.SetAvatar(new UuConversationHosting.AvatarPresence(12, 10, 30, 30));
        hosting.SetTray(new UuConversationHosting.BarterTray(20, 0, 0, 30, 3));

        // do_demand with no stack args.
        var demand = Script(hosting, "do_demand", 0x40, 20, 0x40, 38);
        demand.Run();
        // Strong avatar vs weak talker yields (tested path); attitude stepped.
        Assert.Equal(Social.UuAttitude.Upset, hosting.GetAttitude("murgo"));

        // Empty tray: deterministic refuse without calling.
        hosting.SetTray(new UuConversationHosting.BarterTray(0, 0, 0, 0, 3));
        for (int i = 0; i < 10; i++)
        {
            var offer = Script(hosting, "do_offer", 0x41, 20, 0x41, 38);
            offer.Run();
            Assert.Equal(0, offer.Pop());
        }

        // Typed input is asked for, and a menu's options are read out of the
        // conversation's own memory as string indices (donor: babl_menu.cs).
        var ask = Script(hosting, "babl_ask", 0x42, 20, 0x42, 38);
        ask.Run();
        Assert.Equal([">"], hosting.Panel("murgo", []).Prompts);

        // The option list is written into the conversation's own memory (STO
        // takes an address then a value), terminated by an empty entry, and its
        // start address is the call's argument. MemorySlots 4 puts the stack
        // base above the four slots, so 64 is free scratch space.
        short[] menuCode =
        [
            22, 64, 22, 11, 32,   // memory[64] = 11, a string index
            22, 65, 22, 12, 32,   // memory[65] = 12
            22, 66, 22, 0, 32,    // memory[66] = 0, the end of the list
            22, 64,               // the list starts at 64
            20, 0x43,             // CALLI babl_menu
            38,
        ];
        var menu = new UuConversationVm(
            new UuConversationVm.ConversationScript(
                1, menuCode.Length, 7, 4,
                [new UuConversationVm.ScriptImport("babl_menu", 0x43, false, 0)],
                menuCode),
            (block, index) => $"line {block}:{index}",
            hosting,
            new StubVariables());
        menu.Run();
        // A talk's panel accumulates what its script asked for.
        Assert.Equal([">", "1. line 7:11", "2. line 7:12"], hosting.Panel("murgo", menu.Transcript).Prompts);
    }
}
