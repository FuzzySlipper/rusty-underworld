using AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

/// <summary>
/// A light on the floor lights the room. Which items are lights is read from the
/// item id's class fields, the layout the item catalog documents, and the lit
/// variant is one classindex step above the unlit one.
/// </summary>
public sealed class UuLightSourcesTests
{
    [Theory]
    // majorclass 1 holds lights; the first four classindex values are unlit and the
    // next four are the same lights burning.
    [InlineData(64, true, false)]
    [InlineData(68, true, false)]
    [InlineData(76, true, false)]
    [InlineData(80, true, true)]
    [InlineData(92, true, true)]
    // Another majorclass is not a light whatever its classindex, and past the
    // eighth classindex the majorclass holds things that are not lights.
    [InlineData(128, false, false)]
    [InlineData(96, false, false)]
    public void Which_items_are_lights_is_read_from_the_id(int itemId, bool isLight, bool lit)
    {
        Assert.Equal(isLight, UuLightSources.IsLight(itemId));
        Assert.Equal(lit, UuLightSources.IsLit(itemId));
    }

    [Fact]
    public void Lighting_and_dousing_are_one_step_apart()
    {
        Assert.Equal(80, UuLightSources.Lit(76));
        Assert.Equal(76, UuLightSources.Unlit(80));
        Assert.Equal(80, UuLightSources.Unlit(UuLightSources.Lit(80)));
        // A thing that is not a light is left alone by either.
        Assert.Equal(200, UuLightSources.Lit(200));
        Assert.Equal(200, UuLightSources.Unlit(200));
    }
}
