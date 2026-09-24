namespace AbyssRpg.Rulesets.UltimaUnderworld.Combat;

/// <summary>
/// UW charge-attack policy: hold to build charge from the weapon's minimum
/// at its speed (per 10 clock ticks) up to its maximum; release to strike
/// with no bonus for holding past full (the power-gem rule); aim locks at
/// press, not release. Swing kind comes from press height: top third
/// overhead bash, middle sideways slash, bottom thrust; unarmed always jabs.
/// Structure per the manual outline (attack verbs); weapon rows are the
/// OBJECTS.DAT melee table (T06).
/// </summary>
public enum UuSwingKind
{
    Bash,
    Slash,
    Thrust,
}

/// <summary>Melee charge row as plain data (mapped from the import table by content).</summary>
public sealed record UuWeaponChargeRow(
    int Slash,
    int Bash,
    int Stab,
    int MinCharge,
    int ChargeSpeed,
    int MaxCharge,
    int Skill,
    int Durability);

public static class UuChargePolicy
{
    public static UuSwingKind SwingForPressHeight(float height01, bool unarmed)
    {
        if (!float.IsFinite(height01) || height01 < 0f || height01 > 1f)
            throw new ArgumentOutOfRangeException(nameof(height01));
        if (unarmed) return UuSwingKind.Thrust;
        if (height01 >= 2f / 3f) return UuSwingKind.Bash;
        if (height01 >= 1f / 3f) return UuSwingKind.Slash;
        return UuSwingKind.Thrust;
    }

    /// <summary>Charge after heldTicks of holding (10 ticks per speed step).</summary>
    public static int ChargeFor(ulong heldTicks, UuWeaponChargeRow weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        ulong steps = heldTicks / 10;
        ulong charge = (ulong)weapon.MinCharge + (steps * (ulong)weapon.ChargeSpeed);
        return (int)Math.Min(charge, (ulong)weapon.MaxCharge);
    }

    public static float ChargeFraction(int charge, UuWeaponChargeRow weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        if (weapon.MaxCharge <= 0) throw new ArgumentOutOfRangeException(nameof(weapon), "Max charge must be positive.");
        return Math.Clamp((float)charge / weapon.MaxCharge, 0f, 1f);
    }
}

/// <summary>Aim captured at press time; release resolves against this, not the release aim.</summary>
public sealed record UuAimLock(float YawRadians, float PitchRadians);

/// <summary>
/// One attacker's charge state. Press opens charging with locked aim and
/// swing; release settles the charge value; cancel drops it.
/// </summary>
public sealed class UuChargeState
{
    public bool IsCharging { get; private set; }
    public UuSwingKind Swing { get; private set; }
    public UuAimLock Aim { get; private set; } = new(0f, 0f);

    private ulong _pressTick;

    public void Press(float height01, UuAimLock aim, ulong tick, bool unarmed)
    {
        ArgumentNullException.ThrowIfNull(aim);
        if (IsCharging) throw new InvalidOperationException("Already charging; release or cancel first.");
        Swing = UuChargePolicy.SwingForPressHeight(height01, unarmed);
        Aim = aim;
        _pressTick = tick;
        IsCharging = true;
    }

    public (int Charge, UuSwingKind Swing, UuAimLock Aim) Release(ulong tick, UuWeaponChargeRow weapon)
    {
        ArgumentNullException.ThrowIfNull(weapon);
        if (!IsCharging) throw new InvalidOperationException("Not charging.");
        if (tick < _pressTick) throw new ArgumentOutOfRangeException(nameof(tick));
        IsCharging = false;
        return (UuChargePolicy.ChargeFor(tick - _pressTick, weapon), Swing, Aim);
    }

    public void Cancel()
    {
        if (!IsCharging) throw new InvalidOperationException("Not charging.");
        IsCharging = false;
    }
}
