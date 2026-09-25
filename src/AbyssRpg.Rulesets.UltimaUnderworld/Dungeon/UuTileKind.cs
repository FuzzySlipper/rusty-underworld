using AbyssRpg.Rulesets.UltimaUnderworld.Content;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;

/// <summary>
/// The tile kinds the imported level carries. The emulated game stores the kind
/// in the tile record the importer decodes; this ruleset is the runtime owner
/// that decides what a kind means, so nothing else compares the raw number.
/// Solid tiles are the ones the Engine's collision artifact closes off; an open
/// tile is the only place a capsule can stand.
/// </summary>
public static class UuTileKind
{
    /// <summary>A solid tile: the level's collision closes it.</summary>
    public const int Solid = 0;

    /// <summary>An open floor tile, including a door tile, which the record lists as open.</summary>
    public const int Open = 1;

    public static bool IsOpen(AdmittedTile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        return tile.Type == Open;
    }
}
