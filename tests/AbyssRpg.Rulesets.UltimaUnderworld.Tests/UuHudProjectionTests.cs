using AbyssRpg.Kit.Presentation;
using AbyssRpg.Rulesets.UltimaUnderworld.Presentation;
using Rusty.Engine.Mechanics;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuHudProjectionTests
{
    private static StatsComponent AvatarStats()
    {
        var stats = new StatsComponent();
        stats.AddTrack(TrackId.Parse("abyss.defeat"), new Track(34, current: 20));
        stats.AddTrack(TrackId.Parse("abyss.mana"), new Track(12, current: 12));
        return stats;
    }

    [Fact]
    public void Reads_flasks_charge_compass_and_outcome()
    {
        UuHudValues values = UuHudProjection.Read(
            AvatarStats(),
            TrackId.Parse("abyss.defeat"),
            TrackId.Parse("abyss.mana"),
            chargeFraction: 0.5f,
            yawRadians: 0f,
            outcome: "You see a goblin.");
        Assert.Equal((20, 34, 12, 12), (values.Hp, values.MaxHp, values.Mana, values.MaxMana));
        Assert.Equal(0.5f, values.ChargeFraction);
        Assert.Equal(0, values.WindIndex);
        Assert.Equal("You see a goblin.", values.Outcome);
    }

    [Fact]
    public void Winds_step_uniformly_with_index_zero_at_zero_yaw()
    {
        // Index arithmetic only: cardinal names bind later to the string
        // table once the Engine yaw-zero convention is pinned.
        Assert.Equal(0, UuHudProjection.WindIndex(0f));
        Assert.Equal(6, UuHudProjection.WindIndex(MathF.PI / 2f));
        Assert.Equal(2, UuHudProjection.WindIndex(-MathF.PI / 2f));
        Assert.Equal(4, UuHudProjection.WindIndex(MathF.PI));
        Assert.Equal(0, UuHudProjection.WindIndex(MathF.PI * 2f));
        Assert.Throws<ArgumentOutOfRangeException>(() => UuHudProjection.WindIndex(float.NaN));
    }

    [Fact]
    public void Ui_mapping_builds_an_object_root()
    {
        UuHudValues values = UuHudProjection.Read(
            AvatarStats(), TrackId.Parse("abyss.defeat"), TrackId.Parse("abyss.mana"), 1f, 0f, "");
        var builder = new UiValueBuilder();
        uint root = UuHudProjection.WriteUi(builder, values);
        Assert.NotEqual(0u, root);
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.Build(root + 100));
    }
}
