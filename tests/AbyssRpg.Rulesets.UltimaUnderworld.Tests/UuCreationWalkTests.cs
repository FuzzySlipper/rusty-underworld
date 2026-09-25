using System.Text;
using AbyssRpg.Rulesets.UltimaUnderworld.Creation;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

/// <summary>
/// The skill-choice walk is content, and the flow walks it positionally: a
/// malformed table has to be refused when it is read, not surface as an index
/// overrun in the middle of creation, and a class whose block never rolls attack
/// produces an avatar who cannot land a blow.
/// </summary>
public sealed class UuCreationWalkTests
{
    private const string Classes = """
    {
      "id": "abyssrpg.classes",
      "classes": [
        { "id": "fighter", "strength": 20, "dexterity": 16, "intelligence": 12, "bonusPool": 12 },
        { "id": "mage", "strength": 12, "dexterity": 14, "intelligence": 22, "bonusPool": 12 },
        { "id": "ranger", "strength": 17, "dexterity": 18, "intelligence": 13, "bonusPool": 12 },
        { "id": "bard", "strength": 15, "dexterity": 17, "intelligence": 16, "bonusPool": 12 },
        { "id": "tinker", "strength": 16, "dexterity": 15, "intelligence": 17, "bonusPool": 12 },
        { "id": "druid", "strength": 14, "dexterity": 14, "intelligence": 20, "bonusPool": 12 },
        { "id": "paladin", "strength": 18, "dexterity": 14, "intelligence": 16, "bonusPool": 12 },
        { "id": "shepherd", "strength": 16, "dexterity": 16, "intelligence": 16, "bonusPool": 12 }
      ],
      "skillChoiceTable": [__WALK__]
    }
    """;

    private const string Options = """
    {
      "id": "abyssrpg.avatar-options",
      "genders": ["male", "female"],
      "handedness": ["left", "right"],
      "classes": ["fighter", "mage", "ranger", "bard", "tinker", "druid", "paladin", "shepherd"],
      "difficulties": ["standard", "easy"],
      "defaultName": "Avatar"
    }
    """;

    private static string Walk(int[] block) =>
        string.Join(",", Enumerable.Repeat(block, 8).SelectMany(records => records));

    private static Exception Read(string walk) => Record.Exception(() => UuCreationCatalog.Read(
        Encoding.UTF8.GetBytes(Options),
        Encoding.UTF8.GetBytes(Classes.Replace("__WALK__", walk, StringComparison.Ordinal)),
        "avatar options", "classes"))!;

    [Fact]
    public void A_walk_with_five_records_per_class_is_accepted()
    {
        Assert.Null(Read(Walk([1, 0, 1, 1, 2, 3, 4, 1, 5, 1, 2])));
    }

    [Fact]
    public void A_walk_that_stops_short_of_a_class_is_refused()
    {
        InvalidOperationException error = Assert.IsType<InvalidOperationException>(
            Read(Walk([1, 0, 1, 1, 2, 3, 4, 1, 5, 1, 2])[..^4]));
        Assert.Contains("fewer than 5 skill records", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_record_that_runs_past_the_end_is_refused()
    {
        // A count record promising four options with one byte left.
        InvalidOperationException error = Assert.IsType<InvalidOperationException>(
            Read(Walk([1, 0, 1, 1, 2, 3, 4, 1, 5, 1, 2])[..^2] + "4,1"));
        Assert.Contains("runs past the end", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_record_offering_more_skills_than_the_flow_can_offer_is_refused()
    {
        // Nineteen options is one past what the flow can offer, and the record
        // itself is well formed: the table carries a skill for each option.
        int[] wide = [19, .. Enumerable.Range(1, 19)];
        InvalidOperationException error = Assert.IsType<InvalidOperationException>(
            Read(Walk([.. wide, 1, 0, 1, 0, 1, 0])));
        Assert.Contains("at most 18 are offered", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_record_naming_a_skill_outside_the_skill_list_is_refused()
    {
        InvalidOperationException error = Assert.IsType<InvalidOperationException>(
            Read(Walk([1, 25, 1, 1, 2, 3, 4, 1, 5, 1, 2])));
        Assert.Contains("skills are 0-19", error.Message, StringComparison.Ordinal);
    }
}
