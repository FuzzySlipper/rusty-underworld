using AbyssRpg.Kit.Progression;
using Xunit;

namespace AbyssRpg.Kit.Tests;

public sealed class ProgressionStateTests
{
    [Fact]
    public void Experience_and_level_only_move_forward()
    {
        var progression = new ProgressionState();
        Assert.Equal((0, 1), (progression.Experience, progression.Level));

        progression.Award(40);
        progression.Award(-5); // a negative award is ignored, not subtracted
        Assert.Equal(40, progression.Experience);

        progression.AdvanceTo(60, 2);
        Assert.Equal((60, 2), (progression.Experience, progression.Level));
        Assert.Throws<ArgumentException>(() => progression.AdvanceTo(50, 2));
        Assert.Throws<ArgumentException>(() => progression.AdvanceTo(70, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => progression.AdvanceTo(70, 0));
    }

    [Fact]
    public void Skill_uses_tally_to_their_cap_reset_and_restore()
    {
        var progression = new ProgressionState();
        Assert.Equal(3, progression.TallySkillUse("lore", 3, maximum: 5));
        Assert.Equal(5, progression.TallySkillUse("lore", 4, maximum: 5));
        progression.ResetSkillUse("lore");
        Assert.Equal(0, progression.SkillUses["lore"]);

        progression.RestoreSkillUses([new("lore", 2), new("search", 1)]);
        Assert.Equal(2, progression.SkillUses["lore"]);
        Assert.Equal(1, progression.SkillUses["search"]);
        Assert.Throws<ArgumentException>(() => progression.RestoreSkillUses([new("lore", 1), new("lore", 2)]));
    }
}
