using AbyssRpg.Kit;
using AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Content;

/// <summary>
/// The imported levels a resolved bundle carries, read on demand and kept for
/// the session's lifetime. A level's definition, placements, object tables and
/// visible scene all come from content, so admitting a level the avatar travels
/// to uses exactly the path the entry level used at session creation.
/// </summary>
public sealed class UuLevelCatalog
{
    private readonly ResolvedGameComposition _composition;
    private readonly Dictionary<int, UuLevelDefinition> _definitions = [];
    private readonly Dictionary<int, UuLevelPlacements?> _placements = [];
    private readonly Dictionary<int, UuLevelScene> _scenes = [];

    public UuLevelCatalog(ResolvedGameComposition composition)
    {
        _composition = composition ?? throw new ArgumentNullException(nameof(composition));
        ContentPack? tables = composition.ContentPacks
            .SingleOrDefault(pack => pack.Id.Value == UuObjectTablesContent.PackId);
        Tables = tables is null
            ? null
            : UuObjectTablesContent.Read(tables.Payload, $"content pack '{tables.Id.Value}'");
        Levels = composition.ContentPacks
            .Select(pack => LevelNumber(pack.Id.Value))
            .Where(level => level > 0)
            .OrderBy(level => level)
            .ToArray();
    }

    /// <summary>The object tables every level interprets its placements with, when the bundle carries them.</summary>
    public UuObjectTables? Tables { get; }

    /// <summary>The levels this bundle imported, ascending.</summary>
    public IReadOnlyList<int> Levels { get; }

    /// <summary>The level's imported definition, read once.</summary>
    public UuLevelDefinition Definition(int level)
    {
        if (_definitions.TryGetValue(level, out UuLevelDefinition? cached)) return cached;
        ContentPack pack = _composition.ContentPacks.SingleOrDefault(candidate => LevelNumber(candidate.Id.Value) == level)
            ?? throw new InvalidOperationException(
                $"The bundle carries no imported level {level}. Run the operator import for it "
                + "(scripts/import-level.sh <level>) before launching, then rebuild.");
        UuLevelDefinition definition = UuLevelContent.Read(pack.Payload, $"content pack '{pack.Id.Value}'");
        if (definition.Level != level)
            throw new InvalidOperationException($"Content pack '{pack.Id.Value}' declares level {definition.Level}.");
        _definitions[level] = definition;
        return definition;
    }

    /// <summary>The level's placements, or null when it was imported before they existed.</summary>
    public UuLevelPlacements? Placements(int level)
    {
        if (_placements.TryGetValue(level, out UuLevelPlacements? cached)) return cached;
        UuLevelDefinition definition = Definition(level);
        UuLevelPlacements? placements = definition.PlacementsPath is { Length: > 0 } path
            ? UuLevelContent.ReadPlacements(_composition.Content.ReadBytes(path), $"level {level} placements")
            : null;
        _placements[level] = placements;
        return placements;
    }

    /// <summary>The level's visible scene, read once.</summary>
    public UuLevelScene Scene(int level)
    {
        if (_scenes.TryGetValue(level, out UuLevelScene? cached)) return cached;
        UuLevelDefinition definition = Definition(level);
        UuLevelScene scene = UuLevelSceneContent.Read(
            _composition.Content.ReadBytes(definition.RenderPath), definition.RenderPath);
        _scenes[level] = scene;
        return scene;
    }

    /// <summary>The level as the runtime admits it: its tiles and object slots.</summary>
    public AdmittedLevel Admit(int level)
    {
        UuLevelDefinition definition = Definition(level);
        UuLevelPlacements? placements = Placements(level);
        return placements is null
            ? new AdmittedLevel(level, [], [])
            : new AdmittedLevel(level, placements.Tiles, placements.Objects);
    }

    private static int LevelNumber(string packId)
    {
        const string prefix = "abyssrpg.level-";
        return packId.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(packId[prefix.Length..], out int number)
            && number > 0
            ? number
            : 0;
    }
}
