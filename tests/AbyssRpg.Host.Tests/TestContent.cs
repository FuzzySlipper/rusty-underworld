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
        bool withSecondLevel = false)
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
                { "id": "abyssrpg.level-__LEVEL__" }__SECOND_LEVEL__
              ],
              "tuning": { "id": "abyssrpg.stygian-default" }
            }
            """),
            File("abyss/packs/test.avatar-options.pack.json", Pack("abyssrpg.avatar-options", "abyss/content-packs/avatar-options.json")),
            File("abyss/packs/test.classes.pack.json", Pack("abyssrpg.classes", "abyss/content-packs/classes.json")),
            File("abyss/packs/test.starting-kit.pack.json", Pack("abyssrpg.starting-kit", "abyss/content-packs/starting-kit.json")),
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
              "classes": ["fighter", "mage", "ranger", "bard", "tinker", "druid", "paladin", "shepherd"],
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

        if (withLevel) StageLevel(files, level, withPlacements, withCritter);
        if (withSecondLevel) StageLevel(files, SecondLevel, withPlacements: true, withCritter: true);

        // The descriptor grammar names one pack per level, so a bundle that
        // admits another level starts from the same staged tree.
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

    private static void StageLevel(List<ProductContentFile> files, int level, bool withPlacements, bool withCritter)
    {
        files.Add(File($"abyss/packs/test.level-{level}.pack.json", Pack($"abyssrpg.level-{level}", LevelPayloadPathFor(level))));
        files.Add(File(LevelPayloadPathFor(level), LevelManifest(level, withPlacements)));
        files.Add(File(RenderPathFor(level), RenderMesh(level)));
        if (withPlacements) files.Add(File(PlacementsPathFor(level), Placements(level, withCritter)));
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
    internal static string Placements(int level, bool withCritter = true)
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

        int critterItem = level == SecondLevel ? SecondLevelCritterItemId : CritterItemId;
        string critterRow = withCritter
            ? $"[{CritterObjectIndex},1,{critterItem},0,0,0,0,0,0,0,0],\n            "
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
            [{{ContainerContentIndex}},0,200,0,0,0,{{ContainerObjectIndex}},0,-1,-1,-1]
          ]
        }
        """;
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
