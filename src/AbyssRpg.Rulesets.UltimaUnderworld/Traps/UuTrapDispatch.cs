namespace AbyssRpg.Rulesets.UltimaUnderworld.Traps;

/// <summary>
/// UW1 trap/trigger dispatch: traps are objects (major 6) dispatched by
/// (minorclass, classindex). Minor 0 holds the 6-0-0..F chain kinds;
/// minor 1 holds text and miscellaneous traps; minor 2/3 continue chains
/// as trigger legs. Firing follows links; check-variable traps branch to
/// the linked object's next on false; create/delete object traps always
/// stop. UW2-only kinds are documented, never dispatched.
/// Donor: trap.ActivateTrap, a_door_trap, a_check_variable_trap.
/// </summary>
public static class UuTrapDispatch
{
    public const int TrapMajorClass = 6;

    public enum TrapKind
    {
        Unknown,
        Damage,        // 6-0-0
        Teleport,      // 6-0-1
        Arrow,         // 6-0-2
        Hack,          // 6-0-3 (do-traps incl. conversation, platform, quake)
        Pit,           // 6-0-4 (UW1; UW2 special effect — documented only)
        ChangeTerrain, // 6-0-5
        Spell,         // 6-0-6
        CreateObject,  // 6-0-7 (always stops)
        Door,          // 6-0-8
        Ward,          // 6-0-9
        Tell,          // 6-0-A (UW1; UW2 skill trap — documented only)
        DeleteObject,  // 6-0-B (always stops)
        Inventory,     // 6-0-C
        SetVariable,   // 6-0-D
        CheckVariable, // 6-0-E (branches on false)
        Null,          // 6-0-F (does nothing, chain continues)
        TextString,    // 6-1-0
        TriggerLeg,    // 6-2-x, 6-3-x (trigger continuations)
    }

    /// <summary>Whether an item id is a trap or trigger record at all (majorclass 6).</summary>
    public static bool IsTrap(int itemId) => itemId >> 6 == TrapMajorClass;

    /// <summary>Classifies an item id the way the donor's records split it.</summary>
    public static TrapKind ClassifyItem(int itemId) =>
        Classify(itemId >> 6, (itemId & 0x30) >> 4, itemId & 0xF);

    public static TrapKind Classify(int major, int minor, int classIndex)
    {
        if (major != TrapMajorClass) return TrapKind.Unknown;
        if (minor is 2 or 3) return TrapKind.TriggerLeg;
        if (minor == 1) return classIndex == 0 ? TrapKind.TextString : TrapKind.Unknown;
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
            8 => TrapKind.Door,
            9 => TrapKind.Ward,
            0xA => TrapKind.Tell,
            0xB => TrapKind.DeleteObject,
            0xC => TrapKind.Inventory,
            0xD => TrapKind.SetVariable,
            0xE => TrapKind.CheckVariable,
            0xF => TrapKind.Null,
            _ => TrapKind.Unknown,
        };
    }

    public sealed record TrapTrigger(int TileX, int TileY);

    /// <summary>Door-trap action by quality: 1 opens, 2 closes, 3 toggles.</summary>
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

    public sealed record ChainNode(TrapKind Kind, int Link, int AltLink = 0);

    /// <summary>
    /// Follow a link chain, invoking each trap; check-variable nodes take
    /// the alt link when branch returns false; create/delete stop the chain.
    /// Returns the fired kinds in order.
    /// </summary>
    public static IReadOnlyList<TrapKind> FireChain(
        Func<int, ChainNode> resolve, int head, Func<int, bool>? branch = null)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        var fired = new List<TrapKind>();
        var seen = new HashSet<int>();
        int current = head;
        while (current != 0 && seen.Add(current))
        {
            ChainNode node = resolve(current);
            if (node.Kind == TrapKind.Unknown) break;
            fired.Add(node.Kind);
            // Trigger legs run as triggers (their internal chaining is the
            // trigger's business); create/delete always stop.
            if (node.Kind is TrapKind.TriggerLeg or TrapKind.CreateObject or TrapKind.DeleteObject) break;
            current = node.Kind == TrapKind.CheckVariable && branch is not null && !branch(current)
                ? node.AltLink
                : node.Link;
        }

        return fired;
    }
}
