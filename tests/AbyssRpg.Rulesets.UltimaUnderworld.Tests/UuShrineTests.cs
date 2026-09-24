using AbyssRpg.Rulesets.UltimaUnderworld.Magic;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuShrineTests
{
    [Fact]
    public void Mantras_classify_and_gate_skill_points()
    {
        Assert.Equal(26, UuShrinePolicy.MantraCount);
        Assert.Equal(UuShrinePolicy.MantraKind.SingleSkill, UuShrinePolicy.Classify(0));
        Assert.Equal(UuShrinePolicy.MantraKind.SingleSkill, UuShrinePolicy.Classify(19));
        Assert.Equal(UuShrinePolicy.MantraKind.QuestSecret, UuShrinePolicy.Classify(20));
        Assert.Equal(UuShrinePolicy.MantraKind.Unused, UuShrinePolicy.Classify(22));
        Assert.Equal(UuShrinePolicy.MantraKind.Group, UuShrinePolicy.Classify(23));
        Assert.Equal(UuShrinePolicy.MantraKind.Unknown, UuShrinePolicy.Classify(26));
        Assert.True(UuShrinePolicy.RequiresSkillPoint(5));
        Assert.True(UuShrinePolicy.RequiresSkillPoint(24));
        Assert.False(UuShrinePolicy.RequiresSkillPoint(20));

        Assert.Equal(new UuShrinePolicy.GroupMantra(0, 7, 3), UuShrinePolicy.GroupMantras[23]);
        Assert.Equal(new UuShrinePolicy.GroupMantra(7, 3, 2), UuShrinePolicy.GroupMantras[24]);
        Assert.Equal(new UuShrinePolicy.GroupMantra(10, 10, 4), UuShrinePolicy.GroupMantras[25]);
    }

    [Fact]
    public void Seeds_plant_and_death_routes()
    {
        Assert.True(UuSeedPolicy.CanPlant(3, true));
        Assert.False(UuSeedPolicy.CanPlant(9, true));
        Assert.False(UuSeedPolicy.CanPlant(3, false));
        Assert.True(UuSeedPolicy.RebirthAvailable(true, 3));
        Assert.False(UuSeedPolicy.RebirthAvailable(true, 9));
        Assert.False(UuSeedPolicy.RebirthAvailable(false, 3));

        Assert.Equal(UuDeathPolicy.Respawn.AtTree, UuDeathPolicy.Route(true));
        Assert.Equal(UuDeathPolicy.Respawn.AtAnchor, UuDeathPolicy.Route(false));
    }
}
