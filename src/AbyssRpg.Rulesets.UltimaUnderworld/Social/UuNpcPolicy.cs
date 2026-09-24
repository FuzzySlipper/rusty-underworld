namespace AbyssRpg.Rulesets.UltimaUnderworld.Social;

/// <summary>
/// Attitude ladder (Hostile 0 .. Friendly 3): provocations step down,
/// soothing steps up, demands cost a step (T28). Hostile NPCs attack.
/// Donor: Critter EAttitude order.
/// </summary>
public static class UuAttitude
{
    public const int Hostile = 0;
    public const int Upset = 1;
    public const int Mellow = 2;
    public const int Friendly = 3;

    public static int Provoke(int attitude) => Math.Max(Hostile, attitude - 1);

    public static int Soothe(int attitude) => Math.Min(Friendly, attitude + 1);

    public static bool IsHostile(int attitude) => attitude <= Hostile;
}

/// <summary>
/// Time-keyed station schedule: NPCs hold stations by game hour, content
/// assigns the stations. Movement between stations rides with pathfinding.
/// </summary>
public sealed class UuNpcSchedule
{
    private readonly SortedDictionary<int, string> _stations = [];

    public void Assign(int hour, string stationId)
    {
        if (hour is < 0 or > 23) throw new ArgumentOutOfRangeException(nameof(hour));
        ArgumentException.ThrowIfNullOrWhiteSpace(stationId);
        _stations[hour] = stationId;
    }

    public string StationAt(int hour)
    {
        string? current = null;
        foreach ((int key, string value) in _stations)
        {
            if (key > hour) break;
            current = value;
        }

        return current ?? throw new InvalidOperationException("Schedule is empty.");
    }
}

/// <summary>
/// Fear-flight check: a critical fear result against a hurt (hp below a
/// third), outleveled foe routs it half the time. Donor: damage-response
/// flee branch. Flee pathfinding rides with movement.
/// </summary>
public static class UuFleeCheck
{
    public static bool ShouldFlee(bool criticalFear, int hp, int maxHp, int playerLevel, int npcLevel, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return criticalFear
            && hp < maxHp / 3.0
            && playerLevel >= npcLevel
            && rng.NextDouble() < 0.5;
    }
}
