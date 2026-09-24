using AbyssRpg.Rulesets.UltimaUnderworld.Social;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuRepairTests
{
    [Fact]
    public void Repair_costs_time_and_rolls_outcomes()
    {
        Assert.Equal(15, UuRepairPolicy.RepairMinutes(5, 10, 20)); // floor
        Assert.Equal(60 - 10 - 10, UuRepairPolicy.RepairMinutes(20, 10, 20)); // above floor

        // Master smith always improves or restores (30 + d - durability >= 16 for durability <= 20).
        for (int i = 0; i < 20; i++)
        {
            var (result, quality) = UuRepairPolicy.Repair(20, 10, 30, new Random(i));
            Assert.True(result is UuRepairPolicy.RepairResult.Improved or UuRepairPolicy.RepairResult.Restored);
            Assert.InRange(quality, 21, 63);
        }

        // Hopeless apprentice on hard steel: never improves.
        for (int i = 0; i < 20; i++)
        {
            var (result, _) = UuRepairPolicy.Repair(10, 30, 0, new Random(i));
            Assert.True(result is UuRepairPolicy.RepairResult.Destroyed
                or UuRepairPolicy.RepairResult.Damaged
                or UuRepairPolicy.RepairResult.Held);
        }
    }

    [Fact]
    public void Caught_theft_costs_attitude()
    {
        Assert.Equal(UuAttitude.Upset, UuRepairPolicy.TheftCaught(UuAttitude.Mellow));
        Assert.Equal(UuAttitude.Hostile, UuRepairPolicy.TheftCaught(UuAttitude.Hostile));
    }
}
