namespace AbyssRpg.Rulesets.UltimaUnderworld.Combat;

/// <summary>
/// UW strike resolution: the attack roll pits an attack score against a
/// defence score on the combat ladder (d31 spread: at most 2 critical
/// failure, below 16 failure, below 29 success, above critical success);
/// damage is the weapon maximum rolled down through d6 dice, scaled by
/// charge, with criticals re-rolled as maximum times 1-2. Armour never
/// deflects — it soaks per struck part, floored at zero.
/// Donor behavior: OpenUnderground Skills.GetResult (UW.EXE 0x3419c),
/// Utils.GetDamageRoll/GetCriticalDamage, Critter.TryDamageTarget,
/// PlayerObject.AbsorbWithArmour (UW.EXE 0x24e14/0x24e22). No code shared.
/// Values are Approximate until verified; skill-index mapping rides with
/// the skill catalog (UW-T18), so resolution takes values, not indices.
/// </summary>
public static class UuStrikeResolution
{
    public enum StrikeResult
    {
        CritFail = -1,
        Fail = 0,
        Success = 1,
        CritSuccess = 2,
    }

    public static StrikeResult RollToHit(int attackScore, int defenceScore, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        int roll = attackScore + rng.Next(0, 31) - defenceScore;
        if (roll <= 2) return StrikeResult.CritFail;
        if (roll < 16) return StrikeResult.Fail;
        if (roll < 29) return StrikeResult.Success;
        return StrikeResult.CritSuccess;
    }

    /// <summary>Defence the original spends on attackers: Defense skill plus half the wielded weapon skill.</summary>
    public static int DefenceScore(int defenseSkill, int wieldedWeaponSkill) =>
        defenseSkill + (wieldedWeaponSkill / 2);

    /// <summary>Damage maximum before dice: weapon modifier plus a fifth of strength, at least 2.</summary>
    public static int DamageMaximum(int weaponModifier, int strength) =>
        Math.Max(2, weaponModifier + (strength / 5));

    public static int RollDamage(int maximum, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
        int dice = maximum / 6;
        int remainder = maximum % 6;
        int total = remainder > 0 ? rng.Next(1, remainder + 1) : 0;
        for (int i = 0; i < dice; i++) total += rng.Next(1, 7);
        return total;
    }

    public static int RollCriticalDamage(int maximum, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
        return maximum * rng.Next(1, 3);
    }

    /// <summary>Charge scaling: fraction of rolled damage lands, at least 1 on a hit.</summary>
    public static int ScaleByCharge(int damage, float chargeFraction) =>
        Math.Max(1, (int)MathF.Round(damage * Math.Clamp(chargeFraction, 0f, 1f)));

    /// <summary>Armour soaks the struck part's protection off the damage, floored at zero.</summary>
    public static int AbsorbWithArmour(int damage, int partProtection) =>
        Math.Max(0, damage - partProtection);
}
