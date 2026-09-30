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

    // Donor src/player/playerdatskills.cs stores skill n at 0x22 + n: ManaSkill
    // at 0x29 (7), Lore at 0x2A (8), Casting at 0x2B (9).
    public const int ManaIndex = 7;
    public const int CastingIndex = 9;
    public const int SearchIndex = 11;
    public const int CharmIndex = 15;
    public const int AppraiseIndex = 18;
    public const int MissileIndex = 6;
    public const int AttackIndex = 0;
    public const int DefenseIndex = 1;
    public const int UnarmedIndex = 2;

    public static string Name(int index) =>
        (uint)index < (uint)Names.Count
            ? Names[index]
            : throw new ArgumentOutOfRangeException(nameof(index), $"Unknown skill index {index}.");
}
