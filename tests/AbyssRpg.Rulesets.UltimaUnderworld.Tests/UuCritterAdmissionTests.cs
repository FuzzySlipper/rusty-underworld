using System.Text;
using AbyssRpg.Kit.Actors;
using AbyssRpg.Kit.Controls;
using AbyssRpg.Rulesets.UltimaUnderworld.Content;
using AbyssRpg.Rulesets.UltimaUnderworld.Creation;
using AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;
using AbyssRpg.Rulesets.UltimaUnderworld.Session;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

/// <summary>
/// A placed critter becomes a live actor on its own tile, and the item pass
/// leaves that slot alone so one placement is never both an item and an actor.
/// </summary>
public sealed class UuCritterAdmissionTests
{
    private static readonly CreationTables CreationTables = new(
        [new(20, 16, 12, 12)],
        [1, 7, 2, 11, 12]);

    private static UuSession NewSession(AdmittedLevel level)
    {
        var flow = new UuCreationFlow(CreationTables, new Random(7));
        flow.SubmitGender(0);
        flow.SubmitHandedness(1);
        flow.SubmitClass(0);
        flow.OfferSkillChoices();
        flow.SubmitSkillChoice(0);
        flow.FinishSkills();
        flow.SubmitPortrait(1);
        flow.SubmitDifficulty(0);
        flow.SubmitName("Avatar");
        UuCreationFlow.CreationResult choices = flow.Confirm(true)!;
        var vitals = UuVitalsPolicy.Recalculate(choices.Attributes[0], 1, 0, choices.Attributes[2]);
        return UuSession.NewGame(
            choices, vitals, level, new ActorPose(new WorldPoint(4, 0, 4), 0f), worldSeed: 7);
    }

    private static UuObjectTables Tables() => UuObjectTablesContent.Read(Encoding.UTF8.GetBytes("""
    {
      "schemaVersion": 1,
      "source": { "SourceGame": "UW1" },
      "critters": [
        { "itemId": 64, "level": 1, "avgHp": 12, "strength": 14, "dexterity": 12, "intelligence": 6, "speed": 3, "corpseIndex": 2, "swimmer": false, "flier": false, "faction": 3 }
      ],
      "containers": [
        { "itemId": 128, "capacityTenthStones": 125, "objectsMask": 255, "slots": 255 }
      ]
    }
    """), "object tables");

    private static UuLevelPlacements Placements() => UuLevelContent.ReadPlacements(
        Encoding.UTF8.GetBytes(PlacementJson()), "placements");

    private static string PlacementJson()
    {
        var tiles = new StringBuilder();
        for (int y = 0; y < UuLevelPlacements.TileDimension; y++)
        {
            for (int x = 0; x < UuLevelPlacements.TileDimension; x++)
            {
                if (tiles.Length > 0) tiles.Append(',');
                int head = (x, y) switch { (0, 0) => 500, (2, 3) => 501, (1, 0) => 502, _ => 0 };
                tiles.Append($"[{x},{y},0,{head},1,0]");
            }
        }

        return $$"""
        {
          "schemaVersion": 1, "level": 1, "unitsPerTile": 8.0, "heightUnitsPerStep": 1.0,
          "liveObjects": 2, "mobileObjects": 1,
          "tiles": [{{tiles}}],
          "objects": [
            [500,0,200,0,0,0,0,0,-1,-1,3],
            [501,1,64,0,0,0,0,0,2,3,8],
            [502,0,128,0,0,0,0,503,-1,-1,-1],
            [503,0,200,0,0,0,502,0,-1,-1,-1]
          ]
        }
        """;
    }

    [Fact]
    public void A_placed_critter_becomes_an_actor_on_its_own_tile()
    {
        UuLevelPlacements placements = Placements();
        var level = new AdmittedLevel(1, placements.Tiles, placements.Objects);
        using UuSession session = NewSession(level);

        // The item pass admitted the placed prop and the container's content,
        // and left the critter slot to the actor pass.
        Assert.True(session.Directory.TryResolve(
            AbyssRpg.Rulesets.UltimaUnderworld.Identity.UuIdentityPolicy.LevelObjectIdentity(1, 500), out _));
        Assert.False(session.Directory.TryResolve(
            AbyssRpg.Rulesets.UltimaUnderworld.Identity.UuIdentityPolicy.LevelObjectIdentity(1, 501), out _));

        UuCritterAdmission.Admission admission =
            UuCritterAdmission.AdmitLevel(session, level, placements, Tables());
        ActorState critter = Assert.Single(admission.Actors);
        Assert.True(admission.ByObjectIndex.ContainsKey(501));
        // Tile (2,3) at floor height 1: the world center of that tile.
        Assert.Equal(new WorldPoint(20f, 1f, 28f), critter.Position);
        // Heading 8 of 32 steps is a quarter turn.
        Assert.Equal(Math.PI / 2, critter.HeadingYawRadians, 4);
        Assert.Single(session.Actors.All);
    }

    [Fact]
    public void A_mobile_item_that_is_not_a_critter_stays_an_item()
    {
        UuLevelPlacements placements = Placements();
        var objects = placements.Objects
            .Select(obj => obj.Index == 501 ? obj with { ItemId = 200 } : obj)
            .ToArray();
        var level = new AdmittedLevel(1, placements.Tiles, objects);
        using UuSession session = NewSession(level);

        UuCritterAdmission.Admission admission =
            UuCritterAdmission.AdmitLevel(session, level, placements, Tables());
        Assert.Empty(admission.Actors);
        Assert.Empty(session.Actors.All);
        // A dropped mobile item is an item entity, like any other placed object.
        Assert.True(session.Directory.TryResolve(
            AbyssRpg.Rulesets.UltimaUnderworld.Identity.UuIdentityPolicy.LevelObjectIdentity(1, 501), out _));
    }

    [Fact]
    public void A_critter_object_the_tables_do_not_define_is_not_admitted()
    {
        UuLevelPlacements placements = Placements();
        var objects = placements.Objects
            .Select(obj => obj.Index == 501 ? obj with { ItemId = 99 } : obj)
            .ToArray();
        var level = new AdmittedLevel(1, placements.Tiles, objects);
        using UuSession session = NewSession(level);

        UuCritterAdmission.Admission admission =
            UuCritterAdmission.AdmitLevel(session, level, placements, Tables());
        Assert.Empty(admission.Actors);
        Assert.Empty(session.Actors.All);
    }

    [Fact]
    public void A_placed_critter_removed_from_the_level_is_not_admitted()
    {
        UuLevelPlacements placements = Placements();
        var level = new AdmittedLevel(1, placements.Tiles, placements.Objects);
        using UuSession session = NewSession(level);
        session.Dungeon.Current.RemoveObject(501);

        UuCritterAdmission.Admission admission =
            UuCritterAdmission.AdmitLevel(session, level, placements, Tables());
        Assert.Empty(admission.Actors);
    }
}
