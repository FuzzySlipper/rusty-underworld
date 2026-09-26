using System.Text;
using Rusty.Engine;

namespace AbyssRpg.Host.Tests;

/// <summary>
/// The ordinary composition fixture: a staged content snapshot shaped exactly
/// like the shipped tree (bundle descriptor, pack descriptors, payloads, and one
/// operator-produced level import). Focused tests drive the real product
/// entry over it, so the composition path they exercise is the launch path.
/// </summary>
internal static class TestContent
{
    public const string CollisionPath = "abyss/imports/level-1/test.level-1-collision.json";
    public const string RenderPath = "abyss/imports/level-1/test.level-1-render.json";
    public const string LevelPayloadPath = "abyss/imports/level-1/test.level-1.level.json";
    public const string PlacementsPath = "abyss/imports/level-1/test.level-1-placements.json";

    /// <summary>The same staged shape for any level a fixture admits.</summary>
    public static string CollisionPathFor(int level) => $"abyss/imports/level-{level}/test.level-{level}-collision.json";
    public static string RenderPathFor(int level) => $"abyss/imports/level-{level}/test.level-{level}-render.json";
    public static string LevelPayloadPathFor(int level) => $"abyss/imports/level-{level}/test.level-{level}.level.json";
    public static string PlacementsPathFor(int level) => $"abyss/imports/level-{level}/test.level-{level}-placements.json";

    /// <summary>The placed critter's item id, its object index, and the container's.</summary>
    public const int CritterItemId = 64;
    public const int CritterObjectIndex = 501;
    public const int ContainerObjectIndex = 502;
    public const int ContainerContentIndex = 503;
    public const int PropObjectIndex = 500;

    /// <summary>A square floor at y=0 with a wall box: enough geometry to walk and collide.</summary>
    internal static ProductContent Build(
        bool withLevel = true, int level = 1, bool withPlacements = true, bool withCritter = true,
        bool withSecondLevel = false, bool withRunes = false, string defaultClass = "fighter")
    {
        List<ProductContentFile> files =
        [
            File("abyss/bundles/stygian-abyss.bundle.json", """
            {
              "kind": "abyssrpg.game-bundle",
              "id": "abyssrpg.stygian-abyss",
              "ruleset": "abyssrpg.ultima-underworld",
              "contentPacks": [
                { "id": "abyssrpg.avatar-options" },
                { "id": "abyssrpg.classes" },
                { "id": "abyssrpg.starting-kit" },
                { "id": "abyssrpg.object-tables" },
                { "id": "abyssrpg.item-catalog" },
                { "id": "abyssrpg.level-__LEVEL__" }__SECOND_LEVEL__
              ],
              "tuning": { "id": "abyssrpg.stygian-default" }
            }
            """),
            File("abyss/packs/test.avatar-options.pack.json", Pack("abyssrpg.avatar-options", "abyss/content-packs/avatar-options.json")),
            File("abyss/packs/test.classes.pack.json", Pack("abyssrpg.classes", "abyss/content-packs/classes.json")),
            File("abyss/packs/test.starting-kit.pack.json", Pack("abyssrpg.starting-kit", "abyss/content-packs/starting-kit.json")),
            File("abyss/packs/abyssrpg.item-catalog.pack.json", Pack("abyssrpg.item-catalog", "abyss/imports/object-tables/abyssrpg.item-catalog.json")),
            File("abyss/imports/object-tables/abyssrpg.item-catalog.json", ItemCatalog()),
            File("abyss/tuning/test.tuning.json", """
            {
              "kind": "abyssrpg.tuning-profile",
              "id": "abyssrpg.stygian-default",
              "ruleset": "abyssrpg.ultima-underworld",
              "payload": "abyss/content-packs/tuning.json"
            }
            """),
            File("abyss/content-packs/avatar-options.json", """
            {
              "id": "abyssrpg.avatar-options",
              "genders": ["male", "female"],
              "handedness": ["left", "right"],
              "classes": ["__FIRST_CLASS__", "fighter", "mage", "ranger", "bard", "tinker", "druid", "paladin", "shepherd"],
              "difficulties": ["standard", "easy"],
              "defaultName": "Tester"
            }
            """),
            File("abyss/content-packs/classes.json", """
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
              "skillChoiceTable": [1,0,1,1,2,3,4,1,5,1,2,1,0,1,1,2,3,4,1,5,1,2,1,0,1,1,2,3,4,1,5,1,2,1,0,1,1,2,3,4,1,5,1,2,1,0,1,1,2,3,4,1,5,1,2,1,0,1,1,2,3,4,1,5,1,2,1,0,1,1,2,3,4,1,5,1,2,1,0,1,1,2,3,4,1,5,1,2]
            }
            """),
            File("abyss/content-packs/starting-kit.json", """{ "id": "abyssrpg.starting-kit", "items": ["torch"] }"""),
            File("abyss/packs/test.object-tables.pack.json", Pack("abyssrpg.object-tables", "abyss/content-packs/object-tables.json")),
            File("abyss/content-packs/object-tables.json", ObjectTables()),
            File("abyss/content-packs/tuning.json", """{ "id": "abyssrpg.stygian-default", "clockTicksPerSecond": 255, "movement": "default" }"""),
        ];

        if (withLevel) StageLevel(files, level, withPlacements, withCritter, withRunes);
        if (withSecondLevel) StageLevel(files, SecondLevel, withPlacements: true, withCritter: true);

        // The descriptor grammar names one pack per level, so a bundle that
        // admits another level starts from the same staged tree.
        files = files
            .Select(file => Encoding.UTF8.GetString(file.Path.Span) is { } path
                && path.EndsWith("avatar-options.json", StringComparison.Ordinal)
                    ? File(
                        path,
                        Encoding.UTF8.GetString(file.Bytes.Span)
                            .Replace("__FIRST_CLASS__", defaultClass, StringComparison.Ordinal))
                    : file)
            .ToList();
        files[0] = File(
            "abyss/bundles/stygian-abyss.bundle.json",
            Encoding.UTF8.GetString(files[0].Bytes.Span)
                .Replace("__LEVEL__", level.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace(
                    "__SECOND_LEVEL__",
                    withSecondLevel ? $",\n    {{ \"id\": \"abyssrpg.level-{SecondLevel}\" }}".Replace("\\n", "\n") : "",
                    StringComparison.Ordinal));
        return new ProductContent(files.ToArray());
    }

    /// <summary>The second level a travel fixture admits: its own import, its own critter.</summary>
    public const int SecondLevel = 2;

    /// <summary>The critter item id the second level places, distinct from the first level's.</summary>
    public const int SecondLevelCritterItemId = 66;

    /// <summary>The door tile the second level places, distinct from the first level's.</summary>
    public static readonly (int X, int Y) SecondLevelDoorTile = (1, 2);

    private static void StageLevel(
        List<ProductContentFile> files, int level, bool withPlacements, bool withCritter, bool withRunes = false)
    {
        files.Add(File($"abyss/packs/test.level-{level}.pack.json", Pack($"abyssrpg.level-{level}", LevelPayloadPathFor(level))));
        files.Add(File(LevelPayloadPathFor(level), LevelManifest(level, withPlacements)));
        files.Add(File(RenderPathFor(level), RenderMesh(level)));
        if (withPlacements) files.Add(File(PlacementsPathFor(level), Placements(level, withCritter, withRunes)));
    }

    internal static string LevelManifest(int level = 1, bool withPlacements = true) =>
        $$"""
        {
          "schemaVersion": 1,
          "level": {{level}},
          "collision": { "path": "__COLLISION__", "sha256": "sha256:__SHA__" },
          "render": { "path": "__RENDER__" },__PLACEMENTS__
          "spawn": { "tileX": 0, "tileY": 0, "x": 0.0, "y": 0.0, "z": 0.0, "yawRadians": 0.0, "origin": "derived-open-space" },
          "provenance": { "source": "UW1", "origin": "LEV.ARK", "tiles": 1, "objects": 4, "liveObjects": 4, "mobileObjects": 1 }
        }
        """
        .Replace("__COLLISION__", CollisionPathFor(level), StringComparison.Ordinal)
        .Replace("__SHA__", Sha256Literal, StringComparison.Ordinal)
        .Replace("__RENDER__", RenderPathFor(level), StringComparison.Ordinal)
        .Replace(
            "__PLACEMENTS__",
            withPlacements ? $"\n  \"placements\": {{ \"path\": \"{PlacementsPathFor(level)}\" }}," : "",
            StringComparison.Ordinal);

    /// <summary>
    /// The item catalog the shipped import produces, trimmed to the ids this
    /// fixture places: the prop, the container and its content, and both levels'
    /// critters.
    /// </summary>
    /// <summary>The object the fixture's critter carries, linked from its own record.</summary>
    public const int CarriedObjectIndex = 506;

    /// <summary>The runestone slots, item ids and shelf indices the fixture places.</summary>
    public const int FirstRunestoneObjectIndex = 504;
    public const int SecondRunestoneObjectIndex = 505;
    public const int InStoneItemId = 240;
    public const int LorStoneItemId = 243;

    /// <summary>The shelf indices those stones take, and the spell they spell.</summary>
    public const int InRuneIndex = 8;
    public const int LorRuneIndex = 11;
    public const int LightSpellId = 2;

    internal static string ItemCatalog() => """
    {
      "schemaVersion": 1,
      "source": { "commonObjects": { "SourceGame": "UW1" }, "strings": { "SourceGame": "UW1" } },
      "items": [
        { "itemId": 64, "name": "giant rat", "massTenthStones": 12, "height": 4, "radius": 2, "canPickUp": false, "class": 1, "minorClass": 0, "classIndex": 0 },
        { "itemId": 66, "name": "giant spider", "massTenthStones": 14, "height": 4, "radius": 2, "canPickUp": false, "class": 1, "minorClass": 0, "classIndex": 2 },
        { "itemId": 128, "name": "sack", "massTenthStones": 2, "height": 4, "radius": 2, "canPickUp": true, "class": 2, "minorClass": 0, "classIndex": 0 },
        { "itemId": 176, "name": "piece of meat", "massTenthStones": 7, "height": 3, "radius": 1, "canPickUp": true, "class": 2, "minorClass": 3, "classIndex": 0 },
        { "itemId": 200, "name": "torch", "massTenthStones": 4, "height": 5, "radius": 1, "canPickUp": true, "class": 3, "minorClass": 0, "classIndex": 8 },
        { "itemId": 240, "name": "In stone", "massTenthStones": 0, "height": 2, "radius": 1, "canPickUp": true, "class": 3, "minorClass": 3, "classIndex": 8 },
        { "itemId": 243, "name": "Lor stone", "massTenthStones": 0, "height": 2, "radius": 1, "canPickUp": true, "class": 3, "minorClass": 3, "classIndex": 11 }
      ]
    }
    """;

    /// <summary>
    /// The critter and container tables the shipped import produces, trimmed to
    /// the ids this fixture places.
    /// </summary>
    internal static string ObjectTables() => """
    {
      "schemaVersion": 1,
      "source": { "SourceGame": "UW1", "SourceFile": "UW/DATA/OBJECTS.DAT", "ByteLength": 3554, "Sha256Hex": "00" },
      "critters": [
        { "itemId": 64, "level": 1, "avgHp": 12, "strength": 14, "dexterity": 12, "intelligence": 6, "speed": 3, "corpseIndex": 2, "swimmer": false, "flier": false, "faction": 3 },
        { "itemId": 66, "level": 2, "avgHp": 20, "strength": 18, "dexterity": 10, "intelligence": 4, "speed": 4, "corpseIndex": 4, "swimmer": false, "flier": false, "faction": 2 }
      ],
      "containers": [
        { "itemId": 128, "capacityTenthStones": 125, "objectsMask": 255, "slots": 255 }
      ]
    }
    """;

    /// <summary>
    /// The placement artifact's shape (a full 64x64 tile grid in row-major
    /// order plus object rows) with one prop, one container with a content, and
    /// one critter standing on the spawn tile.
    /// </summary>
    internal static string Placements(int level, bool withCritter = true, bool withRunes = false)
    {
        var tiles = new System.Text.StringBuilder();
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                if (tiles.Length > 0) tiles.Append(',');
                int head = (x, y) switch
                {
                    (0, 0) => withCritter ? CritterObjectIndex : 0,
                    (0, 1) => PropObjectIndex,
                    (1, 0) => ContainerObjectIndex,
                    // The runestones hang on their own tile, chained together.
                    (0, 2) when withRunes => FirstRunestoneObjectIndex,
                    _ => 0,
                };
                // One tile is a door, so the interaction verb has something to
                // use, and one tile is solid, so the operator probe's refusal
                // has a tile to refuse.
                (int doorX, int doorY) = level == SecondLevel ? SecondLevelDoorTile : (1, 1);
                int door = (x, y) == (doorX, doorY) ? 1 : 0;
                int type = (x, y) == (2, 2) ? 0 : 1;
                tiles.Append($"[{x},{y},{type},{head},0,{door}]");
            }
        }

        // Two runestones on tile (0,2), chained: the pair a first-circle spell
        // needs, so casting is reachable by picking them up in play.
        string runeRows = withRunes
            ? $",\n            [{FirstRunestoneObjectIndex},0,{InStoneItemId},0,0,{SecondRunestoneObjectIndex},0,0,-1,-1,-1]"
              + $",\n            [{SecondRunestoneObjectIndex},0,{LorStoneItemId},0,0,0,0,0,-1,-1,-1]"
            : "";
        int critterItem = level == SecondLevel ? SecondLevelCritterItemId : CritterItemId;
        // The critter's own record links what it carries.
        string critterRow = withCritter
            ? $"[{CritterObjectIndex},1,{critterItem},0,0,0,0,{CarriedObjectIndex},0,0,0],\n            "
            : "";
        return $$"""
        {
          "schemaVersion": 1,
          "level": {{level}},
          "unitsPerTile": 8.0,
          "heightUnitsPerStep": 1.0,
          "liveObjects": 4,
          "mobileObjects": 1,
          "tiles": [{{tiles}}],
          "objects": [
            {{critterRow}}[{{PropObjectIndex}},0,200,0,0,0,0,0,-1,-1,5],
            [{{ContainerObjectIndex}},0,128,0,0,0,0,{{ContainerContentIndex}},-1,-1,-1],
            [{{ContainerContentIndex}},0,200,0,0,0,{{ContainerObjectIndex}},0,-1,-1,-1]__RUNE_ROWS____CARRIED_ROW__
          ]
        }
        """
        .Replace("__RUNE_ROWS__", runeRows, StringComparison.Ordinal)
        .Replace(
            "__CARRIED_ROW__",
            withCritter
                ? $",\n            [{CarriedObjectIndex},0,200,0,0,0,{CritterObjectIndex},0,-1,-1,-1]"
                : "",
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The declared content identity is only carried, never verified by the
    /// product: the Engine checks the staged bytes. A fixed literal keeps the
    /// fixture deterministic.
    /// </summary>
    internal const string Sha256Literal = "0000000000000000000000000000000000000000000000000000000000000000";

    /// <summary>One floor quad plus one wall quad, four vertices each.</summary>
    internal static string RenderMesh(int level = 1) => $$"""
    {
      "schemaVersion": 1,
      "level": {{level}},
      "unitsPerTile": 8.0,
      "heightUnitsPerStep": 1.0,
      "bounds": { "min": [0, 0, 0], "max": [8, 8, 8] },
      "positions": [
        [0,0,0],[0,0,8],[8,0,8],[8,0,0],
        [0,0,0],[8,0,0],[8,8,0],[0,8,0]
      ],
      "normals": [
        [0,1,0],[0,1,0],[0,1,0],[0,1,0],
        [0,0,-1],[0,0,-1],[0,0,-1],[0,0,-1]
      ],
      "colors": [
        [0.3,0.3,0.3,1],[0.3,0.3,0.3,1],[0.3,0.3,0.3,1],[0.3,0.3,0.3,1],
        [0.5,0.5,0.5,1],[0.5,0.5,0.5,1],[0.5,0.5,0.5,1],[0.5,0.5,0.5,1]
      ],
      "indices": [0,1,2,0,2,3,4,5,6,4,6,7]
    }
    """;

    private static string Pack(string id, string payload) => $$"""
    {
      "kind": "abyssrpg.content-pack",
      "id": "{{id}}",
      "ruleset": "abyssrpg.ultima-underworld",
      "dependencies": [],
      "payload": "{{payload}}"
    }
    """;

    private static ProductContentFile File(string path, string json) =>
        new(Encoding.UTF8.GetBytes(path), Encoding.UTF8.GetBytes(json));
}
