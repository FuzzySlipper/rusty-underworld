using Rusty.Engine.Input;
using Xunit;

namespace AbyssRpg.Host.Tests;

public sealed class OptionsSaveUxTests
{
    [Fact]
    public void Options_build_input_over_standard_bindings()
    {
        AbyssOptions options = AbyssOptions.Defaults;
        Assert.False(options.InvertY);
        Assert.Equal(AbyssDetail.High, options.Detail);

        FpsInputConfig config = options.ToInputConfig();
        Assert.Equal(FpsInputBindings.Standard, config.Bindings); // WASD + standard gamepad
        Assert.False(config.InvertControllerVertical);
        Assert.False(config.PointerLookConfig.InvertVertical);

        FpsInputConfig inverted = (options with { InvertY = true }).ToInputConfig();
        Assert.True(inverted.InvertControllerVertical);
        Assert.True(inverted.PointerLookConfig.InvertVertical);
    }

    [Fact]
    public void Journey_continues_from_newest_autosave_else_anchor()
    {
        var slots = new List<AbyssSaveUx.SlotDescription>
        {
            new(AbyssSaveSlots.AutosaveKey(1), new DateTime(2026, 1, 1), "Level 1"),
            new(AbyssSaveSlots.AutosaveKey(3), new DateTime(2026, 1, 3), "Level 3"),
            new(AbyssSaveSlots.AnchorKey, new DateTime(2026, 1, 2), "Anchor"),
        };
        Assert.Equal(AbyssSaveSlots.AutosaveKey(3), AbyssSaveUx.JourneyOnward(slots));
        Assert.Equal(AbyssSaveSlots.AnchorKey, AbyssSaveUx.JourneyOnward(
            [new(AbyssSaveSlots.AnchorKey, new DateTime(2026, 1, 2), "Anchor")]));
        Assert.Null(AbyssSaveUx.JourneyOnward([]));
    }
}
