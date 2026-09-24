namespace AbyssRpg.Rulesets.UltimaUnderworld.Magic;

/// <summary>
/// Shrine mantras: 26 chants (strings block 2, indices 51-75). Indices
/// 0-19 advance a single skill greatly (costs a skill point); 20-21 reveal
/// quest secrets; 22 is unused; 23-25 are the group mantras (attack
/// 0/7/3, magic 7/3/2, other 10/10/4) advancing several skills.
/// Donor: shrine.cs mantra dispatch, Skills.cs AdvanceSkills groups.
/// </summary>
public static class UuShrinePolicy
{
    public const int MantraCount = 26;
    public const int GroupMantraFirst = 23;

    public sealed record GroupMantra(int BaseSkill, int GroupWidth, int AdvanceCount);

    public static readonly IReadOnlyDictionary<int, GroupMantra> GroupMantras =
        new Dictionary<int, GroupMantra>
        {
            [23] = new(0, 7, 3),   // SUMM RA (attack)
            [24] = new(7, 3, 2),   // MU AHM (magic)
            [25] = new(10, 10, 4), // OM CAH (other)
        };

    public enum MantraKind
    {
        Unknown,
        SingleSkill,
        QuestSecret,
        Unused,
        Group,
    }

    public static MantraKind Classify(int mantra) => mantra switch
    {
        < 0 or >= MantraCount => MantraKind.Unknown,
        < 20 => MantraKind.SingleSkill,
        20 or 21 => MantraKind.QuestSecret,
        22 => MantraKind.Unused,
        _ => MantraKind.Group,
    };

    public static bool RequiresSkillPoint(int mantra) =>
        Classify(mantra) is MantraKind.SingleSkill or MantraKind.Group;
}

/// <summary>
/// Silver seed/tree: planting needs an open, fitting tile with growing
/// terrain off level 9 (caller supplies terrain/fit reads); the tree is
/// recorded on plant. Rebirth at the tree needs only the planted state
/// (Godot playertdatdeath.cs:40; OU gates death-time level 9 too —
/// disagreement recorded, primary donor followed; planting lives in
/// silverseed.cs, rebirth in playertdatdeath.cs:38-65).
/// Death without a tree resurrects at the T25 anchor slot instead — the
/// anchor leg is task-owned (both donors game-over there).
/// </summary>
public static class UuSeedPolicy
{
    public const int ForbiddenLevel = 9;

    public static bool CanPlant(int level, bool tileOpen, bool terrainGrows, bool fits) =>
        level != ForbiddenLevel && tileOpen && terrainGrows && fits;

    public static bool RebirthAvailable(bool planted) => planted;
}

/// <summary>
/// Death outcome: tree rebirth restores full health at the tree; otherwise
/// the anchor snapshot loads (saved HP). The Host performs the load; this
/// policy only routes.
/// </summary>
public static class UuDeathPolicy
{
    public enum Respawn
    {
        AtTree,
        AtAnchor,
    }

    public static Respawn Route(bool treeRebirthAvailable) =>
        treeRebirthAvailable ? Respawn.AtTree : Respawn.AtAnchor;
}
