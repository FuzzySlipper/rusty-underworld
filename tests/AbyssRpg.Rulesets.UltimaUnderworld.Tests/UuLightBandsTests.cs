using AbyssRpg.Rulesets.UltimaUnderworld.Presentation;
using Rusty.Engine;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

/// <summary>
/// Light falls off with distance rather than switching off at the edge of a
/// radius: a thing across the room is dimmer than one at arm's length, and only
/// what is past the light is not drawn at all.
/// </summary>
public sealed class UuLightBandsTests
{
    [Theory]
    [InlineData(0f, 100f, 0)]
    [InlineData(10f, 100f, 0)]
    [InlineData(30f, 100f, 1)]
    [InlineData(55f, 100f, 2)]
    [InlineData(80f, 100f, 3)]
    [InlineData(100f, 100f, 3)]
    public void A_distance_falls_in_a_band_inside_the_light(float distance, float reach, int expected) =>
        Assert.Equal(expected, UuLightBands.Band(distance, reach));

    [Fact]
    public void Past_the_light_nothing_is_drawn()
    {
        Assert.Equal(UuLightBands.Hidden, UuLightBands.Band(100.1f, 100f));
        Assert.Equal(0f, UuLightBands.Dimming(UuLightBands.Hidden));
    }

    [Fact]
    public void Each_band_farther_out_shows_less_of_the_colour()
    {
        var full = new Color(1f, 0.8f, 0.6f, 1f);
        float previous = float.MaxValue;
        for (int band = 0; band < UuLightBands.Bands; band++)
        {
            Color shaded = UuLightBands.Shade(full, band);
            Assert.True(shaded.R < previous, $"band {band} is dimmer than the one inside it");
            Assert.Equal(full.A, shaded.A);
            previous = shaded.R;
        }

        Assert.Equal(full, UuLightBands.Shade(full, 0));
    }

    [Fact]
    public void A_light_that_reaches_nothing_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => UuLightBands.Band(1f, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => UuLightBands.Band(-1f, 10f));
    }
}
