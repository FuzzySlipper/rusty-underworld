using System.Text;
using AbyssRpg.Rulesets.UltimaUnderworld.Content;
using UltimaUnderworld.Import;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

/// <summary>
/// The imported item catalog: an item id's name, mass and pickup flag, read from
/// content the operator's own data produced.
/// </summary>
public sealed class UuItemCatalogTests
{
    private const string Catalog = """
    {
      "schemaVersion": 1,
      "source": { "commonObjects": { "SourceGame": "UW1" }, "strings": { "SourceGame": "UW1" } },
      "items": [
        { "itemId": 128, "name": "sack", "massTenthStones": 2, "height": 4, "radius": 2, "canPickUp": true, "class": 2, "minorClass": 0, "classIndex": 0 },
        { "itemId": 176, "name": "piece of meat", "massTenthStones": 7, "height": 3, "radius": 1, "canPickUp": true, "class": 2, "minorClass": 3, "classIndex": 0 },
        { "itemId": 320, "name": "door", "massTenthStones": 0, "height": 8, "radius": 4, "canPickUp": false, "class": 5, "minorClass": 0, "classIndex": 0 }
      ]
    }
    """;

    [Fact]
    public void A_catalog_row_carries_the_items_name_mass_and_pickup_flag()
    {
        UuItemCatalog catalog = UuItemCatalogContent.Read(Encoding.UTF8.GetBytes(Catalog), "item catalog");

        Assert.Equal(3, catalog.Items.Count);
        Assert.Equal("sack", catalog.Find(128)!.Name);
        Assert.Equal(7, catalog.Find(176)!.MassTenthStones);
        Assert.False(catalog.Find(320)!.CanPickUp);
        Assert.Null(catalog.Find(999));
    }

    [Fact]
    public void An_item_defined_twice_is_refused()
    {
        string duplicate = Catalog.Replace(
            """{ "itemId": 176, "name": "piece of meat", "massTenthStones": 7, "height": 3, "radius": 1, "canPickUp": true, "class": 2, "minorClass": 3, "classIndex": 0 }""",
            """{ "itemId": 128, "name": "sack", "massTenthStones": 2, "height": 4, "radius": 2, "canPickUp": true, "class": 2, "minorClass": 0, "classIndex": 0 }""",
            StringComparison.Ordinal);
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => UuItemCatalogContent.Read(Encoding.UTF8.GetBytes(duplicate), "item catalog"));
        Assert.Contains("twice", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_raw_name_is_reduced_to_its_noun()
    {
        // The archive's entries carry an article or a quantity placeholder; the
        // catalog keeps the noun (donor: src/utility/StringLoader.cs
        // GetSimpleObjectNameUW, which splits on '_' first and then on '&').
        Assert.Equal("torch", ItemCatalogPack.CleanName("a_torch"));
        Assert.Equal("torch", ItemCatalogPack.CleanName("torch&a"));
        Assert.Equal("sack", ItemCatalogPack.CleanName("sack"));
        Assert.Equal("", ItemCatalogPack.CleanName(null));
        Assert.Equal("", ItemCatalogPack.CleanName("   "));
    }
}
