using AbyssRpg.Rulesets.UltimaUnderworld.Traps;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuDoorTests
{
    [Fact]
    public void Picks_open_hold_break_or_refuse()
    {
        // Dead locks never open (picks can still break: the donor rolls first).
        for (int i = 0; i < 20; i++)
            Assert.NotEqual(UuDoorPolicy.PickResult.Opened, UuDoorPolicy.Pick(0xF, 30, 10, new Random(i)));
        Assert.Equal(UuDoorPolicy.PickResult.BrokePick, UuDoorPolicy.Pick(0xF, 30, 10, new Random(1)));
        // Under-skilled master locks hold or break, never open.
        for (int i = 0; i < 20; i++)
            Assert.NotEqual(UuDoorPolicy.PickResult.Opened, UuDoorPolicy.Pick(0xE, 0x1F, 10, new Random(i)));

        // Master picker opens easy locks on every seed.
        for (int i = 0; i < 20; i++)
            Assert.Equal(UuDoorPolicy.PickResult.Opened, UuDoorPolicy.Pick(2, 30, 10, new Random(i)));
        // Untrained hands never open a hard lock without breaking picks.
        for (int i = 0; i < 20; i++)
            Assert.True(UuDoorPolicy.Pick(10, 0, 10, new Random(i)) is UuDoorPolicy.PickResult.Held or UuDoorPolicy.PickResult.BrokePick);
    }

    [Fact]
    public void Bashing_wears_doors_and_weapons()
    {
        Assert.Equal(0, UuDoorPolicy.BashDoor(10, 0, 20));
        Assert.Equal(10, UuDoorPolicy.BashDoor(10, 3, 99)); // unbreakable holds
        Assert.Equal(4, UuDoorPolicy.BashWeaponWear(5));
        Assert.Equal(0, UuDoorPolicy.BashWeaponWear(0));
    }

    [Fact]
    public void Secrets_open_on_search_and_gates_travel()
    {
        for (int i = 0; i < 20; i++)
            Assert.True(UuDoorPolicy.SearchSecret(30, 5, new Random(i)));
        for (int i = 0; i < 20; i++)
            Assert.False(UuDoorPolicy.SearchSecret(0, 30, new Random(i)));

        var endpoints = new[]
        {
            new UuDoorPolicy.MoongateEndpoint(1, 10, 10),
            new UuDoorPolicy.MoongateEndpoint(5, 20, 20),
        };
        Assert.Equal(endpoints[1], UuDoorPolicy.Travel(endpoints, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => UuDoorPolicy.Travel(endpoints, 2));
    }
}
