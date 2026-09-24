namespace AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;

/// <summary>
/// Runtime level data as plain records. The importer (or authored content)
/// maps source packs into this shape; the runtime never touches source
/// formats and never references the Import project.
/// </summary>
public sealed record AdmittedTile(int X, int Y, int Type, int ObjectHead);

/// <summary>Placed-object chain links needed at runtime; full definitions live in content.</summary>
public sealed record AdmittedObject(int Index, int ItemId, int Next);

public sealed record AdmittedLevel(int LevelNumber, AdmittedTile[] Tiles, AdmittedObject[] Objects);
