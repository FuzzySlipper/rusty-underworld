namespace AbyssRpg.Kit.Npc;

// Kept for the creature behaviour driver (Den #8683): NPC schedules station
// NPCs through this roster.
/// <summary>NPC presence roster: who exists and where they are stationed. Schedules and attitudes live ruleset-side.</summary>

public sealed record NpcStation(int Level, int TileX, int TileY);

public sealed class NpcPresence
{
    private readonly Dictionary<string, NpcStation> _stations = [];

    public void Place(string id, NpcStation station)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(station);
        _stations[id] = station;
    }

    public bool Remove(string id) => _stations.Remove(id);

    public NpcStation Station(string id) =>
        _stations.TryGetValue(id, out NpcStation? station) ? station : throw new KeyNotFoundException($"No NPC '{id}'.");
}
