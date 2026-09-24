using AbyssRpg.Rulesets.UltimaUnderworld.Combat;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuChargeTests
{
    // Shipped weapon row 0 (T06 golden): slash 6, bash 4, stab 2, min 90,
    // speed 25 per 10 ticks, max 160, skill 4, durability 10.
    private static readonly UuWeaponChargeRow Dagger = new(6, 4, 2, 90, 25, 160, 4, 10);

    [Fact]
    public void Swing_follows_press_height_with_unarmed_jab()
    {
        Assert.Equal(UuSwingKind.Bash, UuChargePolicy.SwingForPressHeight(0.9f, unarmed: false));
        Assert.Equal(UuSwingKind.Slash, UuChargePolicy.SwingForPressHeight(0.5f, unarmed: false));
        Assert.Equal(UuSwingKind.Thrust, UuChargePolicy.SwingForPressHeight(0.1f, unarmed: false));
        Assert.Equal(UuSwingKind.Thrust, UuChargePolicy.SwingForPressHeight(0.9f, unarmed: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => UuChargePolicy.SwingForPressHeight(2f, false));
    }

    [Fact]
    public void Charge_builds_from_min_and_caps_at_max()
    {
        Assert.Equal(90, UuChargePolicy.ChargeFor(0, Dagger));
        Assert.Equal(115, UuChargePolicy.ChargeFor(10, Dagger));
        Assert.Equal(160, UuChargePolicy.ChargeFor(1_000_000, Dagger)); // no overhold bonus
        Assert.Equal(1f, UuChargePolicy.ChargeFraction(160, Dagger));
        Assert.InRange(UuChargePolicy.ChargeFraction(90, Dagger), 0f, 1f);
    }

    [Fact]
    public void Press_release_cancel_lifecycle()
    {
        var state = new UuChargeState();
        Assert.Throws<InvalidOperationException>(() => state.Release(0, Dagger));
        var aim = new UuAimLock(1.0f, 0.2f);
        state.Press(0.9f, aim, tick: 100, unarmed: false);
        Assert.Throws<InvalidOperationException>(() => state.Press(0.5f, aim, 101, false));
        (int charge, UuSwingKind swing, UuAimLock locked) = state.Release(110, Dagger);
        Assert.Equal(UuSwingKind.Bash, swing);
        Assert.Same(aim, locked); // aim locked at press, not release
        Assert.Equal(UuChargePolicy.ChargeFor(10, Dagger), charge);
        Assert.Throws<InvalidOperationException>(() => state.Cancel());
    }
}
