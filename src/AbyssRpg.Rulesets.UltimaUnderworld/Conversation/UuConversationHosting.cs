using AbyssRpg.Rulesets.UltimaUnderworld.Social;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Conversation;

/// <summary>
/// Core conversation imports bound to live session state: quest variables,
/// NPC attitudes, barter offers/demands over a tray, inventory counts.
/// Menu/ask prompts are recorded for the UI (P06) and return neutral.
/// Calling convention: arguments are peeked (top = depth 0), never popped;
/// the VM writes the returned result over the stack top.
/// </summary>
public sealed class UuConversationHosting : IUuConversationImports
{
    public sealed record BarterTray(int NpcValue, int PlayerValue, int CharmSkill, int AppraisalSkill, int MaxPatience);

    public sealed record PanelData(IReadOnlyList<string> Prompts, string? LastTrade, int Attitude);

    public sealed record AvatarPresence(int CharmSkill, int Level, int Hp, int Vitality);

    private readonly Session.UuSession _session;
    private readonly Random _rng;
    private readonly Dictionary<string, int> _attitudes = [];
    private AvatarPresence _avatar = new(0, 1, 10, 30);
    private readonly List<string> _prompts = [];
    private BarterTray _tray = new(0, 0, 0, 0, 3);
    private int _patience;
    private string? _lastTrade;

    public UuConversationHosting(Session.UuSession session, Random? rng = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _rng = rng ?? Random.Shared;
    }

    public void SetAvatar(AvatarPresence avatar)
    {
        ArgumentNullException.ThrowIfNull(avatar);
        _avatar = avatar;
    }

    public void SetTray(BarterTray tray)
    {
        ArgumentNullException.ThrowIfNull(tray);
        _tray = tray;
        _patience = 0;
    }

    public void SetAttitude(string npc, int attitude) => _attitudes[npc] = attitude;

    public int GetAttitude(string npc) =>
        _attitudes.TryGetValue(npc, out int attitude) ? attitude : UuAttitude.Mellow;

    public PanelData Panel(string npc, IReadOnlyList<string> transcript) =>
        new(_prompts.ToArray(), _lastTrade, GetAttitude(npc));

    public sealed record TalkResult(IReadOnlyList<string> Transcript, PanelData Panel);

    /// <summary>
    /// Run a conversation on the VM against live hosted imports. Targeting
    /// (which entity) rides with perception/UI; this hosts the talk.
    /// </summary>
    public static TalkResult Talk(
        Session.UuSession session, UuConversationVm.ConversationScript script,
        Func<int, int, string> strings, string npc, Random? rng = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentException.ThrowIfNullOrWhiteSpace(npc);
        var hosting = new UuConversationHosting(session, rng);
        var vm = new UuConversationVm(script, strings, hosting, new BlankVariables());
        vm.Run();
        return new TalkResult(vm.Transcript, hosting.Panel(npc, vm.Transcript));
    }

    private sealed class BlankVariables : IUuConversationVariables
    {
        public int Read(int address) => 0;
        public void Write(int address, int value) { }
    }

    public int Call(string name, UuConversationVm vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        return name switch
        {
            "get_quest" => _session.Quests.Get(vm.PeekArg(0)),
            "set_quest" => SetQuest(vm),
            "do_offer" => Offer(vm),
            "do_demand" => Demand(vm),
            "babl_ask" or "babl_menu" or "babl_fmenu" => Ask(vm),
            "setup_to_barter" => 1,
            "end_barter" => 1,
            _ => 0,
        };
    }

    private int SetQuest(UuConversationVm vm)
    {
        _session.Quests.Set(vm.PeekArg(1), vm.PeekArg(0));
        return 0;
    }

    private int Offer(UuConversationVm vm)
    {
        _ = vm;
        (UuBarterPolicy.TradeResult result, int patience) = UuBarterPolicy.Offer(
            _tray.NpcValue, _tray.PlayerValue, _tray.CharmSkill, _tray.AppraisalSkill,
            _patience, _tray.MaxPatience, _rng);
        _patience = patience;
        _lastTrade = result.ToString();
        // Scripts treat 1 as leave-barter, 0 as stay.
        return result is UuBarterPolicy.TradeResult.Accepted or UuBarterPolicy.TradeResult.TraderTired ? 1 : 0;
    }

    private int Demand(UuConversationVm vm)
    {
        _ = vm;
        // Presence comes from the bound avatar record; the ask from the tray.
        (UuBarterPolicy.DemandResult result, int shift) = UuBarterPolicy.Demand(
            _avatar.CharmSkill, _avatar.Level, _avatar.Hp, _avatar.Vitality,
            _tray.NpcValue, GetAttitude("talker"), 1, 10, 10);
        _attitudes["talker"] = GetAttitude("talker") + shift;
        return result == UuBarterPolicy.DemandResult.Yielded ? 1 : 0;
    }

    private int Ask(UuConversationVm vm)
    {
        _prompts.Add($"prompt:{vm.PeekArg(0)}");
        return 0;
    }
}
