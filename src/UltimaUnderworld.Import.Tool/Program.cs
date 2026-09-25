// Operator-facing importer: emits per-level bundle files (collision mesh
// JSON + manifest) from operator UW1 data. Usage:
//   emit-level --levark UW/DATA/LEV.ARK --terrain UW/DATA/TERRAIN.DAT --level 1 --out <dir>
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
    LevelCollisionMesh.CollisionMesh mesh = LevelCollisionMesh.Emit(pack);
    string meshJson = LevelCollisionMesh.ToJson(mesh, $"uw1/level-{level}/nav", $"uw1/level-{level}/mesh");

    Directory.CreateDirectory(output);
    string meshFile = $"uw1-level-{level}-collision.json";
    File.WriteAllText(Path.Combine(output, meshFile), meshJson);
    var manifest = new
    {
        bundle = "abyssrpg.stygian-abyss",
        level,
        files = new[] { meshFile },
        provenance = pack.Provenance.SourceGame,
    };
    File.WriteAllText(
        Path.Combine(output, $"uw1-level-{level}-manifest.json"),
        JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Emitted {pack.Tiles.Count} tiles, {pack.Objects.Count} objects for level {level}.");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine($"emit-level failed: {error.Message}");
    return 1;
}
