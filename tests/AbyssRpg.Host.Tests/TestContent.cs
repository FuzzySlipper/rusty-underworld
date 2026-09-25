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

    /// <summary>A square floor at y=0 with a wall box: enough geometry to walk and collide.</summary>
    internal static ProductContent Build(bool withLevel = true)
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
                { "id": "abyssrpg.level-1" }
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
              "skillChoiceTable": [1, 7, 2, 11, 12]
            }
            """),
            File("abyss/content-packs/starting-kit.json", """{ "id": "abyssrpg.starting-kit", "items": ["torch"] }"""),
            File("abyss/content-packs/tuning.json", """{ "id": "abyssrpg.stygian-default", "clockTicksPerSecond": 255, "movement": "default" }"""),
        ];

        if (withLevel)
        {
            files.Add(File("abyss/packs/test.level-1.pack.json", Pack("abyssrpg.level-1", LevelPayloadPath)));
            files.Add(File(LevelPayloadPath, LevelManifest()));
            files.Add(File(RenderPath, RenderMesh()));
        }

        return new ProductContent(files.ToArray());
    }

    internal static string LevelManifest() =>
        """
        {
          "schemaVersion": 1,
          "level": 1,
          "collision": { "path": "__COLLISION__", "sha256": "sha256:__SHA__" },
          "render": { "path": "__RENDER__" },
          "spawn": { "tileX": 0, "tileY": 0, "x": 0.0, "y": 0.0, "z": 0.0, "yawRadians": 0.0, "origin": "derived-open-space" },
          "provenance": { "source": "UW1", "origin": "LEV.ARK", "tiles": 1, "objects": 0 }
        }
        """
        .Replace("__COLLISION__", CollisionPath, StringComparison.Ordinal)
        .Replace("__SHA__", Sha256Literal, StringComparison.Ordinal)
        .Replace("__RENDER__", RenderPath, StringComparison.Ordinal);

    /// <summary>
    /// The declared content identity is only carried, never verified by the
    /// product: the Engine checks the staged bytes. A fixed literal keeps the
    /// fixture deterministic.
    /// </summary>
    internal const string Sha256Literal = "0000000000000000000000000000000000000000000000000000000000000000";

    /// <summary>One floor quad plus one wall quad, four vertices each.</summary>
    internal static string RenderMesh() => """
    {
      "schemaVersion": 1,
      "level": 1,
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
