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
    [InlineData(64, false, false)]
    [InlineData(80, false, false)]
    [InlineData(144, true, false)]
    [InlineData(147, true, false)]
    [InlineData(148, true, true)]
    [InlineData(151, true, true)]
    [InlineData(152, false, false)]
    public void Which_items_are_lights_is_read_from_the_id(int itemId, bool isLight, bool lit)
    {
        Assert.Equal(isLight, UuLightSources.IsLight(itemId));
        Assert.Equal(lit, UuLightSources.IsLit(itemId));
    }

    [Fact]
    public void Lighting_and_dousing_are_one_step_apart()
    {
        Assert.Equal(148, UuLightSources.Lit(144));
        Assert.Equal(144, UuLightSources.Unlit(148));
        Assert.Equal(148, UuLightSources.Lit(148));
        Assert.Equal(144, UuLightSources.Unlit(144));
        // A thing that is not a light is left alone by either.
        Assert.Equal(200, UuLightSources.Lit(200));
        Assert.Equal(200, UuLightSources.Unlit(200));
    }
}
