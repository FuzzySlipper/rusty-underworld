namespace AbyssRpg.Rulesets.UltimaUnderworld.Creation;

/// <summary>
/// UW1 skill-check and creation-roll policy. A check pits a value against a
/// target with a d30 (0-30) spread: at most 2 is a critical failure, at most
/// 15 a failure, at most 28 a success, above that a critical success.
/// New skills start at +3 + governing/9 + d3; existing skills gain +1 +
/// governing/13 + d3; both add three check rolls and cap at 30.
/// Behavior reference: UnderworldGodot
/// src/player/playerdatskills.cs (SkillCheck, GetGoverningAttribute) and
/// src/player/chargen/chargen.cs (RollSkill). No code is shared.
/// </summary>
public static class UuSkillRolls
{
    public const int SkillCount = 20;
    public const int SkillCap = 30;
    public const int FreePickSentinel = 0x14;

    public enum CheckResult
    {
        CritFail = -1,
        Fail = 0,
        Success = 1,
        CritSuccess = 2,
    }

    /// <summary>Governing attribute index: 0-6 strength, 7-9 intelligence, rest dexterity.</summary>
    public static int GoverningAttribute(int skill)
    {
        if (skill < 0 || skill >= SkillCount)
            throw new ArgumentOutOfRangeException(nameof(skill), $"Skills are 0-{SkillCount - 1}.");
        if (skill < 7) return 0;
        if (skill < 10) return 2;
        return 1;
    }

    public static CheckResult SkillCheck(int value, int target, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        int score = (value - target) + rng.Next(0, 31);
        if (score <= 2) return CheckResult.CritFail;
        if (score <= 0xF) return CheckResult.Fail;
        if (score <= 0x1C) return CheckResult.Success;
        return CheckResult.CritSuccess;
    }

    public static int RollNewSkill(int governingValue, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        int value = 3 + (governingValue / 9) + rng.Next(3);
        for (int i = 0; i < 3; i++) value += (int)SkillCheck(governingValue, 0x14, rng);
        return Math.Min(value, SkillCap);
    }

    public static int RollExistingSkill(int current, int governingValue, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        int value = current + 1 + (governingValue / 0xD) + rng.Next(3);
        for (int i = 0; i < 3; i++) value += (int)SkillCheck(governingValue, 0x14, rng);
        return Math.Min(value, SkillCap);
    }
}
