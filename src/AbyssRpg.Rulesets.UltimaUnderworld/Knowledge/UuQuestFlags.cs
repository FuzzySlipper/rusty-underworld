namespace AbyssRpg.Rulesets.UltimaUnderworld.Knowledge;

/// <summary>
/// Named quest-flag slots (indices follow the donor flag order).
/// Conversation read/write rides on Kit QuestVariables; names live here.
/// </summary>
public static class UuQuestFlags
{
    public const int MurgoFreed = 0;
    public const int TalkedToHagbard = 1;
    public const int MetDrOwl = 2;
    public const int GazerKilled = 4;
    public const int KnightOfCrux = 32;
    public const int TalismansLeft = 36;
    public const int Dreams = 37;
}
