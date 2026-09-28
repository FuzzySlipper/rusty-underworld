namespace AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;

/// <summary>
/// Runtime level data as plain records. The importer (or authored content)
/// maps source packs into this shape; the runtime never touches source
/// formats and never references the Import project.
/// </summary>
public sealed record AdmittedTile(
    int X, int Y, int Type, int ObjectHead, int FloorHeight = 0, bool Door = false);

/// <summary>
/// One placed object slot: its chain link, the container/link fields that carry
/// contents, and, for a mobile, the tile its own record positions it on.
/// </summary>
public sealed record AdmittedObject(
    int Index,
    int ItemId,
    int Next,
    int Owner = 0,
    int Link = 0,
    int Quality = 0,
    bool Mobile = false,
    int HomeTileX = -1,
    int HomeTileY = -1,
    int Heading = -1,
    int WhoAmI = 0,
    int Quantity = 1,
    double Height = 0,
    int PressureThreshold = 4,
    int DestinationLevel = 0,
    double TileOffsetX = 0.5,
    double TileOffsetY = 0.5,
    int Flags = 6,
    bool LinkIsQuantity = false,
    int SearchDifficulty = 0)
{
    // UW1 trigger.cs: avatar enable and repeat are ruleset meanings of pack flags.
    public bool AvatarTriggerEnabled => (Flags & 4) != 0;
    public bool RepeatsTrigger => (Flags & 2) != 0;
}

public sealed record AdmittedLevel(int LevelNumber, AdmittedTile[] Tiles, AdmittedObject[] Objects);
