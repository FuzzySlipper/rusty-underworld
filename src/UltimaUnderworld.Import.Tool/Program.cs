// Operator-facing importer: emits one level's runtime artifacts (Engine
// collision/navigation artifact, product render mesh, level manifest) and the
// content-pack descriptor that admits them into the default bundle. Usage:
//   emit-level --levark UW/DATA/LEV.ARK --terrain UW/DATA/TERRAIN.DAT --level 1 --out <content/abyss/imports/level-1>
using System.Security.Cryptography;
using System.Text.Json;
using UltimaUnderworld.Import;

if (args.Length == 0 || args[0] != "emit-level")
{
    Console.Error.WriteLine("Usage: emit-level --levark <LEV.ARK> --terrain <TERRAIN.DAT> --level <n> --out <dir>");
    return 2;
}

string Get(string name)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : throw new ArgumentException($"Missing {name}.");
}

try
{
    string levArk = Get("--levark");
    string terrainPath = Get("--terrain");
    int level = int.Parse(Get("--level"));
    string output = Get("--out");

    byte[] archive = File.ReadAllBytes(levArk);
    byte[] terrain = File.ReadAllBytes(terrainPath);
    LevArkReader.LevelPack pack = LevArkReader.ReadLevel(archive, level, terrain);
    var options = new LevelRenderMesh.Options();
    var meshParameters = new LevelCollisionMesh.MeshParameters(
        options.UnitsPerTile, options.HeightUnitsPerStep);
    LevelCollisionMesh.CollisionMesh collision = LevelCollisionMesh.Emit(pack, meshParameters);
    LevelRenderMesh.RenderMesh render = LevelRenderMesh.Emit(pack, options);
    LevelSpawn.Spawn spawn = LevelSpawn.Choose(pack, options);

    Directory.CreateDirectory(output);
    string packId = $"abyssrpg.level-{level}";
    string collisionFile = $"{packId}-collision.json";
    string renderFile = $"{packId}-render.json";
    string manifestFile = $"{packId}.level.json";

    string collisionJson = LevelCollisionMesh.ToJson(
        collision, $"uw1/level-{level}/nav", $"uw1/level-{level}/mesh", meshParameters);
    File.WriteAllText(Path.Combine(output, collisionFile), collisionJson);
    File.WriteAllText(
        Path.Combine(output, renderFile),
        LevelRenderMesh.ToJson(render, level, options));

    // Paths in the manifest and descriptor are content-root-relative ('/'
    // separators); the tool derives the 'abyss/…' prefix from the output path
    // so the staged tree resolves them without hand editing.
    string prefix = ContentPrefix(output);
    var manifest = new
    {
        schemaVersion = 1,
        level,
        collision = new { path = $"{prefix}{collisionFile}", sha256 = Sha256(collisionJson) },
        render = new { path = $"{prefix}{renderFile}" },
        spawn = new
        {
            tileX = spawn.TileX,
            tileY = spawn.TileY,
            x = spawn.X,
            y = spawn.Y,
            z = spawn.Z,
            yawRadians = spawn.YawRadians,
            origin = "derived-open-space",
        },
        provenance = new
        {
            source = "UW1",
            origin = pack.Provenance.SourceFile,
            tiles = pack.Tiles.Count,
            objects = pack.Objects.Count,
        },
    };
    string manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(Path.Combine(output, manifestFile), manifestJson);

    // The pack descriptor is generated here because the level payload is
    // operator-produced: an authored descriptor would point at a file that only
    // exists after an import, and this one exists exactly when the import does.
    var descriptor = new
    {
        kind = "abyssrpg.content-pack",
        id = packId,
        ruleset = "abyssrpg.ultima-underworld",
        dependencies = Array.Empty<object>(),
        payload = $"{prefix}{manifestFile}",
        provenance = new
        {
            source = "UW1",
            origin = pack.Provenance.SourceFile,
            sha256 = Sha256(manifestJson),
        },
    };
    File.WriteAllText(
        Path.Combine(output, $"{packId}.pack.json"),
        JsonSerializer.Serialize(descriptor, new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine(
        $"Emitted level {level}: {pack.Tiles.Count} tiles, {pack.Objects.Count} objects, "
        + $"{render.Positions.Length} render vertices, {render.Indices.Length / 3} triangles, "
        + $"spawn tile ({spawn.TileX},{spawn.TileY}) with {spawn.OpenNeighbors} open neighbors.");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine($"emit-level failed: {error.Message}");
    return 1;
}

static string Sha256(string text) =>
    "sha256:" + Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));

// The descriptor and manifest name paths relative to the content root, which is
// the repository's content/ directory: everything from "abyss/" onward.
static string ContentPrefix(string output)
{
    string normalized = Path.GetFullPath(output).Replace('\\', '/').TrimEnd('/');
    int at = normalized.IndexOf("/abyss/", StringComparison.Ordinal);
    return at < 0 ? "" : normalized[(at + 1)..] + "/";
}
