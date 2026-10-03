// Operator-facing importer: emits one level's runtime artifacts (Engine
// collision/navigation artifact, product render mesh, level manifest) and its
// content-pack descriptor, plus the install-global packs (object tables, item
// catalog, strings, conversations) when their source files are given. Every
// output is derived from the operator's own game data, so it goes under the
// git-ignored content/abyss/imports/ tree, which the default bundle admits by
// root. node scripts/import-level.mjs is the ordinary way to run it.
using System.Security.Cryptography;
using System.Text.Json;
using UltimaUnderworld.Import;

if (args.Length == 0 || args[0] != "emit-level")
{
    Console.Error.WriteLine(
        "Usage: emit-level --levark <LEV.ARK> --terrain <TERRAIN.DAT> --level <n> --out <content/abyss/imports/level-n> "
        + "[--objects <OBJECTS.DAT> --packs <content/abyss/imports/object-tables> "
        + "[--common <COMOBJ.DAT>] [--strings <STRINGS.PAK>] [--cnv <CNV.ARK>]]\n"
        + "Outputs are derived game data: keep them under content/abyss/imports/ (git-ignored), never with authored packs.");
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
    string? objectsPath = Array.IndexOf(args, "--objects") >= 0 ? Get("--objects") : null;
    // The object tables are install-global, so they get one imports directory of
    // their own rather than a per-level one; never the authored packs directory.
    string? packsDirectory = Array.IndexOf(args, "--packs") >= 0 ? Get("--packs") : null;
    string? commonDatPath = Array.IndexOf(args, "--common") >= 0 ? Get("--common") : null;
    string? stringsPakPath = Array.IndexOf(args, "--strings") >= 0 ? Get("--strings") : null;
    string? cnvPath = Array.IndexOf(args, "--cnv") >= 0 ? Get("--cnv") : null;

    // One generated pack: its payload beside its descriptor, with the content
    // root-relative payload path the resolver expects and the operator's own
    // file recorded as provenance.
    void WriteGeneratedPack(string directory, string id, string origin, string json)
    {
        Directory.CreateDirectory(directory);
        string file = $"{id}.json";
        File.WriteAllText(Path.Combine(directory, file), json);
        File.WriteAllText(
            Path.Combine(directory, $"{id}.pack.json"),
            JsonSerializer.Serialize(new
            {
                kind = "abyssrpg.content-pack",
                id,
                ruleset = "abyssrpg.ultima-underworld",
                dependencies = Array.Empty<object>(),
                payload = $"{ContentPrefix(directory)}{file}",
                provenance = new { source = "UW1", origin, sha256 = Sha256(json) },
            }, new JsonSerializerOptions { WriteIndented = true }));
    }

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

    // A chain the runtime cannot walk would silently drop the objects behind
    // the break, so the tool refuses to emit a level whose placements are
    // malformed (out-of-range index or cycle). Lava placements occur in the
    // shipped lower levels; report the hazard without changing source content.
    IReadOnlyList<LevArkReader.PlacementIssue> issues = LevArkReader.ValidatePlacement(pack, terrain);
    foreach (LevArkReader.PlacementIssue issue in issues.Take(10))
        Console.Error.WriteLine($"placement {(issue.Kind == "spawn-on-lava" ? "warning" : "issue")}: {issue.Kind} at tile ({issue.TileX},{issue.TileY}) object {issue.ObjectIndex}");
    int malformed = issues.Count(issue => issue.Kind != "spawn-on-lava");
    if (malformed > 0)
    {
        throw new InvalidDataException($"Level {level} has {malformed} malformed placement chain(s); refusing to emit it.");
    }

    LevelPlacements.Placements placements = LevelPlacements.Emit(pack, options);
    string placementsFile = $"{packId}-placements.json";

    string collisionJson = LevelCollisionMesh.ToJson(
        collision, $"uw1/level-{level}/nav", $"uw1/level-{level}/mesh", meshParameters);
    File.WriteAllText(Path.Combine(output, collisionFile), collisionJson);
    File.WriteAllText(
        Path.Combine(output, renderFile),
        LevelRenderMesh.ToJson(render, level, options));
    File.WriteAllText(Path.Combine(output, placementsFile), LevelPlacements.ToJson(placements));

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
        placements = new { path = $"{prefix}{placementsFile}" },
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
            liveObjects = placements.LiveObjects,
            mobileObjects = placements.MobileObjects,
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

    if (objectsPath is not null && packsDirectory is not null)
    {
        // The object tables are install-global, not per level: one pack in the
        // imports tree, admitted by the bundle's imported root like any import.
        string tablesId = "abyssrpg.object-tables";
        string tablesFile = $"{tablesId}.json";
        byte[] objectsBytes = File.ReadAllBytes(objectsPath);
        ObjectTablePack.Tables tables = ObjectTablePack.Emit(
            ObjectsDatReader.Read(objectsBytes),
            UwTableProvenance.FromBytes("UW1", "UW/DATA/OBJECTS.DAT", objectsBytes));
        string tablesJson = ObjectTablePack.ToJson(tables);
        Directory.CreateDirectory(packsDirectory);
        string tablesPrefix = ContentPrefix(packsDirectory);
        File.WriteAllText(Path.Combine(packsDirectory, tablesFile), tablesJson);
        File.WriteAllText(
            Path.Combine(packsDirectory, $"{tablesId}.pack.json"),
            JsonSerializer.Serialize(new
            {
                kind = "abyssrpg.content-pack",
                id = tablesId,
                ruleset = "abyssrpg.ultima-underworld",
                dependencies = Array.Empty<object>(),
                payload = $"{tablesPrefix}{tablesFile}",
                provenance = new
                {
                    source = "UW1",
                    origin = "UW/DATA/OBJECTS.DAT",
                    sha256 = Sha256(tablesJson),
                },
            }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Emitted object tables: {tables.Critters.Count} critters, {tables.Containers.Count} containers.");

        string dataDirectory = Path.GetDirectoryName(objectsPath)!;
        string[] lightingPaths = ["PALS.DAT", "LIGHT.DAT", "SHADES.DAT"];
        if (lightingPaths.All(name => File.Exists(Path.Combine(dataDirectory, name))))
        {
            string lighting = LightingPack.Emit(
                File.ReadAllBytes(Path.Combine(dataDirectory, lightingPaths[0])),
                File.ReadAllBytes(Path.Combine(dataDirectory, lightingPaths[1])),
                File.ReadAllBytes(Path.Combine(dataDirectory, lightingPaths[2])));
            WriteGeneratedPack(packsDirectory, LightingPack.PackId, "UW/DATA/PALS.DAT;LIGHT.DAT;SHADES.DAT", lighting);
            Console.WriteLine("Emitted UW1 lighting distances and palette remaps.");
        }
        else Console.Error.WriteLine("Lighting source files missing: importing without palette remaps and shade distances.");

        // The item catalog joins them: the common object table plus the string
        // archive's item-name block, both install-global.
        if (commonDatPath is not null && stringsPakPath is not null
            && File.Exists(commonDatPath) && File.Exists(stringsPakPath))
        {
            byte[] commonBytes = File.ReadAllBytes(commonDatPath);
            byte[] stringsBytes = File.ReadAllBytes(stringsPakPath);
            ItemCatalogPack.Catalog catalog = ItemCatalogPack.Emit(
                CommonObjDatReader.Read(commonBytes),
                StringsPakReader.Decode(stringsBytes, "UW/DATA/STRINGS.PAK"),
                UwTableProvenance.FromBytes("UW1", "UW/DATA/COMOBJ.DAT", commonBytes),
                UwTableProvenance.FromBytes("UW1", "UW/DATA/STRINGS.PAK", stringsBytes));
            string catalogJson = ItemCatalogPack.ToJson(catalog);
            string catalogFile = $"{ItemCatalogPack.PackId}.json";
            File.WriteAllText(Path.Combine(packsDirectory, catalogFile), catalogJson);
            File.WriteAllText(
                Path.Combine(packsDirectory, $"{ItemCatalogPack.PackId}.pack.json"),
                JsonSerializer.Serialize(new
                {
                    kind = "abyssrpg.content-pack",
                    id = ItemCatalogPack.PackId,
                    ruleset = "abyssrpg.ultima-underworld",
                    dependencies = Array.Empty<object>(),
                    payload = $"{tablesPrefix}{catalogFile}",
                    provenance = new
                    {
                        source = "UW1",
                        origin = "UW/DATA/COMOBJ.DAT + UW/DATA/STRINGS.PAK",
                        sha256 = Sha256(catalogJson),
                    },
                }, new JsonSerializerOptions { WriteIndented = true }));
            int named = catalog.Items.Count(item => item.Name.Length > 0);
            Console.WriteLine($"Emitted item catalog: {catalog.Items.Count} item ids, {named} named.");

            // The conversations are install-global too: the runtime runs the
            // scripts the operator's own game carries, and reads the string
            // blocks they index.
            if (File.Exists(cnvPath))
            {
                byte[] cnvBytes = File.ReadAllBytes(cnvPath);
                ConversationPack.Catalog conversations = ConversationPack.Emit(
                    CnvArkReader.ReadPack(cnvBytes, "UW/DATA/CNV.ARK"));
                ConversationPack.Strings strings = ConversationPack.EmitStrings(
                    conversations,
                    StringsPakReader.Decode(stringsBytes, "UW/DATA/STRINGS.PAK"),
                    UwTableProvenance.FromBytes("UW1", "UW/DATA/STRINGS.PAK", stringsBytes));
                WriteGeneratedPack(
                    packsDirectory, ConversationPack.PackId, "UW/DATA/CNV.ARK", ConversationPack.ToJson(conversations));
                WriteGeneratedPack(
                    packsDirectory, ConversationPack.StringsPackId, "UW/DATA/STRINGS.PAK", ConversationPack.ToJson(strings));
                Console.WriteLine(
                    $"Emitted conversations: {conversations.Conversations.Count} scripts, "
                    + $"{strings.Blocks.Count} string blocks.");
            }
        }
    }

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
