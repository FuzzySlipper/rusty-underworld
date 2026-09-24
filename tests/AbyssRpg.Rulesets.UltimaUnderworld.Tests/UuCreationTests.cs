using AbyssRpg.Kit.Actors;
using AbyssRpg.Kit.Controls;
using AbyssRpg.Kit.World;
using AbyssRpg.Rulesets.UltimaUnderworld.Creation;
using Rusty.Engine;
using Rusty.Engine.Entities;
using Rusty.Engine.Mechanics;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuCreationTests
{
    private static readonly CreationTables Tables = new(
        [
            new(20, 16, 12, 12), new(12, 16, 20, 12), new(14, 20, 14, 12), new(18, 18, 12, 12),
            new(18, 12, 18, 12), new(12, 18, 18, 12), new(16, 16, 16, 12), new(12, 12, 12, 20),
        ],
        // Minimal choice table: class 0 gets one fixed skill then options.
        [1, 7, 2, 11, 12]);

    [Fact]
    public void Full_flow_completes_in_order_with_validation()
    {
        var flow = new UuCreationFlow(Tables, new Random(7));
        Assert.Throws<InvalidOperationException>(() => flow.SubmitClass(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => flow.SubmitGender(5));

        flow.SubmitGender(1);
        flow.SubmitHandedness(0);
        flow.SubmitClass(0);
        Assert.True(flow.Attributes[0] >= 20); // base 20 plus bonus share

        int[]? offered = flow.OfferSkillChoices();
        Assert.NotNull(offered);
        Assert.Empty(offered); // case 1: fixed skill assigned automatically
        offered = flow.OfferSkillChoices();
        Assert.NotNull(offered);
        Assert.Equal(2, offered!.Length);
        flow.SubmitSkillChoice(0);
        Assert.Throws<ArgumentOutOfRangeException>(() => flow.SubmitSkillChoice(9));
        flow.FinishSkills();

        flow.SubmitPortrait(2);
        flow.SubmitDifficulty(1);
        Assert.Throws<ArgumentException>(() => flow.SubmitName("  "));
        flow.SubmitName("Avatar");
        UuCreationFlow.CreationResult? result = flow.Confirm(true);
        Assert.NotNull(result);
        Assert.Equal("Avatar", result!.Name);
        Assert.True(result.IsFemale);
        Assert.True(result.IsLeftHanded);
        Assert.Equal(UuCreationFlow.Stage.Complete, flow.Current);
    }

    [Fact]
    public void Confirm_no_restarts_from_gender()
    {
        var flow = new UuCreationFlow(Tables, new Random(7));
        flow.SubmitGender(0);
        flow.SubmitHandedness(1);
        flow.SubmitClass(1);
        flow.FinishSkills();
        flow.SubmitPortrait(0);
        flow.SubmitDifficulty(0);
        flow.SubmitName("X");
        Assert.Null(flow.Confirm(false));
        Assert.Equal(UuCreationFlow.Stage.Gender, flow.Current);
    }

    [Fact]
    public void Skill_rolls_respect_cap_and_governing_bands()
    {
        var rng = new Random(1234);
        for (int i = 0; i < 200; i++)
            Assert.InRange(UuSkillRolls.RollNewSkill(20, rng), 1, 30);
        Assert.Equal(0, UuSkillRolls.GoverningAttribute(3));
        Assert.Equal(2, UuSkillRolls.GoverningAttribute(8));
        Assert.Equal(1, UuSkillRolls.GoverningAttribute(15));
        Assert.Throws<ArgumentOutOfRangeException>(() => UuSkillRolls.GoverningAttribute(20));
    }

    [Fact]
    public void Vitals_follow_the_donor_formulas()
    {
        // HP = 30 + STR*level/5; mana = (ManaSkill+1)*INT>>3; weight = 300+STR*13.
        Assert.Equal(new UuVitalsPolicy.Vitals(30 + (20 * 1) / 5, ((0 + 1) * 12) >> 3, 300 + 20 * 13),
            UuVitalsPolicy.Recalculate(strength: 20, level: 1, manaSkill: 0, intelligence: 12));
    }

    [Fact]
    public void Avatar_factory_finishes_the_kit_player()
    {
        using var actors = new ActorsState();
        var flow = new UuCreationFlow(Tables, new Random(7));
        flow.SubmitGender(0);
        flow.SubmitHandedness(1);
        flow.SubmitClass(0);
        flow.FinishSkills();
        flow.SubmitPortrait(1);
        flow.SubmitDifficulty(0);
        flow.SubmitName("Avatar");
        UuCreationFlow.CreationResult result = flow.Confirm(true)!;
        var vitals = UuVitalsPolicy.Recalculate(flow.Attributes[0], 1, 0, flow.Attributes[2]);

        PlayerActorState player = UuAvatarFactory.CreateAvatar(
            actors, new ActorPose(new WorldPoint(0, 0, 0), 0f), result, vitals);

        // The Kit constructor alone leaves these absent (kit-survey F1).
        Assert.NotNull(player.Actor.Get<ActorBody>());
        Assert.NotNull(player.Inventory);
        Assert.NotNull(player.Equipment);
        Assert.Equal(1L, player.DurableId);
    }
}
