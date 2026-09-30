using AbyssRpg.Rulesets.UltimaUnderworld.Combat;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuMissileTests
{
    [Fact]
    public void Launchers_take_their_single_ammunition()
    {
        const int Arrow = 16, Bolt = 17, Stone = 18;
        Assert.Equal(Stone, UuMissilePolicy.AmmoItemId(UuMissilePolicy.LauncherKind.Sling, Arrow, Bolt, Stone));
        Assert.Equal(Arrow, UuMissilePolicy.AmmoItemId(UuMissilePolicy.LauncherKind.Bow, Arrow, Bolt, Stone));
        Assert.Equal(Arrow, UuMissilePolicy.AmmoItemId(UuMissilePolicy.LauncherKind.JeweledBow, Arrow, Bolt, Stone));
        Assert.Equal(Bolt, UuMissilePolicy.AmmoItemId(UuMissilePolicy.LauncherKind.Crossbow, Arrow, Bolt, Stone));
        Assert.Throws<ArgumentOutOfRangeException>(() => UuMissilePolicy.AmmoItemId((UuMissilePolicy.LauncherKind)99, Arrow, Bolt, Stone));
    }

    [Fact]
    public void Shots_share_the_melee_damage_path()
    {
        var rng = new Random(11);
        // Shipped ranged row 0 (T06 golden): damage 5.
        int maximum = UuMissilePolicy.ProjectileMaximum(5);
        for (int i = 0; i < 100; i++)
        {
            int rolled = UuStrikeResolution.RollDamage(maximum, rng);
            Assert.InRange(UuStrikeResolution.AbsorbWithArmour(rolled, 2), 0, maximum);
        }

        // A shot is the melee to-hit roll: the same inputs and the same draw
        // give the same result.
        for (int seed = 0; seed < 20; seed++)
        {
            Assert.Equal(
                UuStrikeResolution.RollToHit(15, 15, new Random(seed)),
                UuMissilePolicy.RollShot(15, 15, new Random(seed)));
        }

        Assert.Equal(
            UuStrikeResolution.DamageMaximum(2, 20),
            UuMissilePolicy.ThrownMaximum(2, 20));
    }
}
