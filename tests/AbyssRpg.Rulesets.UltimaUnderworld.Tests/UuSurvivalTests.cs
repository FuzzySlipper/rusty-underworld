using AbyssRpg.Rulesets.UltimaUnderworld.Survival;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuSurvivalTests
{
    [Fact]
    public void Hunger_and_fatigue_tick_and_bite()
    {
        var state = new UuSurvivalState();
        var rng = new Random(9);

        SurvivalTickResult quiet = UuSurvivalPolicy.Tick(state, 59.0, rng);
        Assert.Equal((0, 0, 0), (quiet.HungerDamage, quiet.FatigueDamage, quiet.PoisonDamage));
        Assert.Equal(0, state.Hunger);

        SurvivalTickResult fed = UuSurvivalPolicy.Tick(state, 2.0, rng);
        Assert.Equal(1, state.Hunger);
        Assert.Equal(0, fed.HungerDamage);

        var starving = new UuSurvivalState { Hunger = 224, HungerTimer = 0, HungerDamageTimer = 0 };
        SurvivalTickResult bite = UuSurvivalPolicy.Tick(starving, 1.0, rng);
        Assert.True(bite.HungerDamage >= 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => UuSurvivalPolicy.Tick(state, -1.0, rng));

        var tired = new UuSurvivalState { Fatigue = 27, FatigueTimer = 0, FatigueDamageTimer = 0 };
        SurvivalTickResult weary = UuSurvivalPolicy.Tick(tired, 1.0, rng);
        Assert.True(weary.FatigueDamage >= 1);
    }

    [Fact]
    public void Poison_and_drink_wear_off()
    {
        var state = new UuSurvivalState { Poison = 3, PoisonTimer = 0, PoisonDamageTimer = 0, Drunkenness = 2, DrunkTimer = 0 };
        SurvivalTickResult hit = UuSurvivalPolicy.Tick(state, 1.0, new Random(3));
        Assert.Equal(2, state.Poison);
        Assert.True(hit.PoisonDamage >= 1);
        Assert.Equal(1, state.Drunkenness);

        // Protection triples both poison timers: wear quickens, injury slows.
        var warded = new UuSurvivalState { Poison = 3, PoisonTimer = 30.0, PoisonDamageTimer = 17.5 };
        SurvivalTickResult wardedHit = UuSurvivalPolicy.Tick(warded, 10.0, new Random(3), hasPoisonProtection: true);
        Assert.Equal(2, warded.Poison); // 10s x3 exhausts the 30s wear timer
        Assert.Equal(0, wardedHit.PoisonDamage); // 10s /3 leaves the damage timer alive
    }

    [Fact]
    public void Sleep_permission_and_outcome()
    {
        Assert.Equal(UuRestPolicy.SleepPermission.InCombat, UuRestPolicy.CanSleep(true, false, 0));
        Assert.Equal(UuRestPolicy.SleepPermission.EnemiesNear, UuRestPolicy.CanSleep(false, true, 0));
        Assert.Equal(UuRestPolicy.SleepPermission.TooPoisoned, UuRestPolicy.CanSleep(false, false, 18));
        Assert.Equal(UuRestPolicy.SleepPermission.Allowed, UuRestPolicy.CanSleep(false, false, 17));

        UuRestPolicy.SleepOutcome rested = UuRestPolicy.Sleep(100, 0, 20);
        Assert.Equal((0, 10, 180, true), (rested.Fatigue, rested.Heal, rested.Hunger, rested.Rested));

        UuRestPolicy.SleepOutcome uneasy = UuRestPolicy.Sleep(230, 0, 20);
        Assert.Equal((10, 0, 255, false), (uneasy.Fatigue, uneasy.Heal, uneasy.Hunger, uneasy.Rested));

        UuRestPolicy.SleepOutcome poisoned = UuRestPolicy.Sleep(100, 12, 20);
        Assert.False(poisoned.Rested);
    }

    [Fact]
    public void Light_burns_per_quality_step()
    {
        // Torch duration 3 -> 300*3/64 s per step.
        Assert.Equal(300.0 * 3 / 64.0, UuLightPolicy.SecondsPerQualityStep(3));
        double countdown = 100.0;
        Assert.False(UuLightPolicy.TickBurn(ref countdown, 50.0, 3));
        Assert.True(UuLightPolicy.TickBurn(ref countdown, 60.0, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => UuLightPolicy.SecondsPerQualityStep(0));
    }
}
