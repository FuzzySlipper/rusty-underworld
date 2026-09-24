using Xunit;

namespace AbyssRpg.Host.Tests;

public sealed class OptionsSaveUxTests
{
    [Fact]
    public void Options_build_input_and_journey_continues()
    {
        AbyssOptions options = AbyssOptions.Defaults;
        Assert.False(options.InvertY);
        Assert.Equal(AbyssDetail.High, options.Detail);
        Assert.NotNull(options.ToInputConfig());

        var slots = new List<AbyssSaveUx.SlotDescription>
        {
            new(AbyssSaveSlots.AutosaveKey(1), new DateTime(2026, 1, 1), "Level 1"),
            new(AbyssSaveSlots.AutosaveKey(3), new DateTime(2026, 1, 3), "Level 3"),
            new(AbyssSaveSlots.AnchorKey, new DateTime(2026, 1, 2), "Anchor"),
        };
        Assert.Equal(AbyssSaveSlots.AutosaveKey(3), AbyssSaveUx.JourneyOnward(slots, 3));
        Assert.Equal(AbyssSaveSlots.AnchorKey, AbyssSaveUx.JourneyOnward(
            [new(AbyssSaveSlots.AnchorKey, new DateTime(2026, 1, 2), "Anchor")], 9));
        Assert.Null(AbyssSaveUx.JourneyOnward([], 1));
    }
}
