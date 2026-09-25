using AbyssRpg.Rulesets.UltimaUnderworld.Conversation;
using AbyssRpg.Rulesets.UltimaUnderworld.Session;

namespace AbyssRpg.Host;

/// <summary>
/// Interaction hosting: runs a conversation on the VM against live hosted
/// imports and returns the transcript plus panel data for the UI.
/// Targeting (which entity) rides with perception/UI; this hosts the talk.
/// </summary>
public static class AbyssInteraction
{
    public sealed record TalkResult(IReadOnlyList<string> Transcript, UuConversationHosting.PanelData Panel);

    public static TalkResult Talk(
        UuSession session, UuConversationVm.ConversationScript script,
        Func<int, int, string> strings, string npc, Random? rng = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentException.ThrowIfNullOrWhiteSpace(npc);
        var hosting = new UuConversationHosting(session, rng);
        var vm = new UuConversationVm(script, strings, hosting, new SessionVariables());
        vm.Run();
        return new TalkResult(vm.Transcript, hosting.Panel(npc, vm.Transcript));
    }

    private sealed class SessionVariables : IUuConversationVariables
    {
        public int Read(int address) => 0;
        public void Write(int address, int value) { }
    }
}
