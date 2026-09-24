namespace AbyssRpg.Rulesets.UltimaUnderworld.Survival;

/// <summary>
/// Rest and sleep: permission (no combat, no enemies near, poison below
/// serious), and the 8-hour sleep outcome — rested (fatigue 0, heal half
/// vitality) unless starving or badly poisoned (fatigue 10), hunger +80,
/// poison cleared, shoulder lights doused by the caller.
/// Donor: Bedroll equip/sleep sequence, CanSleepHere gates. No ambush
/// interruption is evidenced in the donor (nearby enemies refuse sleep
/// instead); the acceptance line's ambush risk is recorded as unevidenced
/// and not modeled.
/// </summary>
public static class UuRestPolicy
{
    public const int SleepHours = 8;
    public const int SleepHungerGain = 80;
    public const int UneasyFatigue = 10;

    /// <summary>Poison rating bands (poison/6, clamped 0-5): sleep allowed through Badly.</summary>
    public static int PoisonRating(int poison) => Math.Clamp(poison / 6, 0, 5);

    public const int RefusePoisonRating = 3; // Seriously and up
    public const int UneasyPoisonRating = 2; // Badly and up

    public enum SleepPermission
    {
        Allowed,
        InCombat,
        EnemiesNear,
        TooPoisoned,
    }

    public static SleepPermission CanSleep(bool inCombat, bool enemiesNear, int poison) =>
        inCombat ? SleepPermission.InCombat
        : enemiesNear ? SleepPermission.EnemiesNear
        : PoisonRating(poison) >= RefusePoisonRating ? SleepPermission.TooPoisoned
        : SleepPermission.Allowed;

    public sealed record SleepOutcome(int Fatigue, int Heal, int Hunger, bool Rested);

    /// <summary>
    /// The 8-hour outcome carries the resultant hunger already clamped to
    /// the cap (donor applies min(hunger+80, 255)); callers store Hunger.
    /// </summary>
    public static SleepOutcome Sleep(int hunger, int poison, int vitality)
    {
        bool uneasy = hunger >= UuSurvivalState.StarvingAt || PoisonRating(poison) >= UneasyPoisonRating;
        int rested = Math.Min(hunger + SleepHungerGain, UuSurvivalState.HungerCap);
        return uneasy
            ? new SleepOutcome(UneasyFatigue, 0, rested, false)
            : new SleepOutcome(0, vitality / 2, rested, true);
    }
}
