namespace AbyssRpg.Rulesets.UltimaUnderworld.Magic;

/// <summary>
/// Skill index mapping: SKILLS.DAT order to skill names. Order follows the
/// secondary donor's skill enum (Skills.cs ESkill); Casting sits at index 9.
/// </summary>
public static class UuSkillCatalog
{
    public static readonly IReadOnlyList<string> Names =
    [
        "Attack", "Defense", "Unarmed", "Sword", "Axe", "Mace", "Missile",
        "Mana", "Lore", "Casting", "Traps", "Search", "Track", "Sneak",
        "Repair", "Charm", "Picklock", "Acrobat", "Appraise", "Swimming",
    ];

    public const int CastingIndex = 9;
    public const int MissileIndex = 6;
    public const int AttackIndex = 0;
    public const int DefenseIndex = 1;
    public const int UnarmedIndex = 2;

    public static string Name(int index) =>
        (uint)index < (uint)Names.Count
            ? Names[index]
            : throw new ArgumentOutOfRangeException(nameof(index), $"Unknown skill index {index}.");
}
