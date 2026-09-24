namespace AbyssRpg.Rulesets.UltimaUnderworld.Traps;

/// <summary>
/// UW1 trap/trigger dispatch: traps are objects (major 6) dispatched by
/// (minorclass, classindex); firing follows the link chain to the next
/// trap, with check-variable traps redirecting. Wall faces address
/// wall-mounted triggers (face -1 = floor plate). UW2-only kinds are
/// documented, never dispatched.
/// Donor: trap.ActivateTrap dispatch, a_door_trap addressing.
/// </summary>
public static class UuTrapDispatch
{
    public const int TrapMajorClass = 6;

    public enum TrapKind
    {
        Unknown,
        Damage,      // 6-0-0
        Teleport,    // 6-0-1
        Arrow,       // 6-0-2
        Hack,        // 6-0-3 (do-traps incl. conversation, platform, quake)
        Pit,         // 6-0-4 (UW1; UW2 special effect — documented only)
        ChangeTerrain, // 6-0-5
        Spell,       // 6-0-6
        CreateObject, // 6-0-7
        DeleteObject, // 6-0-8 (via link chains)
        Door,        // 6-1-x door traps
        CheckVariable, // branches the chain
    }

    public static TrapKind Classify(int major, int minor, int classIndex)
    {
        if (major != TrapMajorClass) return TrapKind.Unknown;
        if (minor == 1) return TrapKind.Door;
        if (minor != 0) return TrapKind.Unknown;
        return classIndex switch
        {
            0 => TrapKind.Damage,
            1 => TrapKind.Teleport,
            2 => TrapKind.Arrow,
            3 => TrapKind.Hack,
            4 => TrapKind.Pit,
            5 => TrapKind.ChangeTerrain,
            6 => TrapKind.Spell,
            7 => TrapKind.CreateObject,
            8 => TrapKind.DeleteObject,
            _ => TrapKind.Unknown,
        };
    }

    public sealed record TrapTrigger(int TileX, int TileY, int Face = -1);

    /// <summary>Door-trap action by quality: 1 opens, 2 closes, 3 toggles (blocked doors reopen).</summary>
    public enum DoorTrapAction
    {
        None,
        Open,
        Close,
        Toggle,
    }

    public static DoorTrapAction DoorAction(int quality) => quality switch
    {
        1 => DoorTrapAction.Open,
        2 => DoorTrapAction.Close,
        3 => DoorTrapAction.Toggle,
        _ => DoorTrapAction.None,
    };

    /// <summary>Follow a link chain, invoking each trap; returns the fired kinds in order.</summary>
    public static IReadOnlyList<TrapKind> FireChain(
        Func<int, (TrapKind Kind, int Link)> resolve, int head)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        var fired = new List<TrapKind>();
        var seen = new HashSet<int>();
        int current = head;
        while (current != 0 && seen.Add(current))
        {
            (TrapKind kind, int link) = resolve(current);
            if (kind == TrapKind.Unknown) break;
            fired.Add(kind);
            current = link;
        }

        return fired;
    }
}
