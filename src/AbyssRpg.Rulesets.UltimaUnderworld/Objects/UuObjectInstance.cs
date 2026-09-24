using AbyssRpg.Kit.World;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Objects;

/// <summary>
/// A runtime object instance: durable identity plus the mutable fields play
/// actually changes (quality, quantity). Type meaning (names, sprites, uses)
/// lives in content; placement lives in level state; containment lives in
/// the inventory coordinators.
/// </summary>
public sealed record UuObjectInstance(
    DurableIdentityReference Identity,
    int ItemId,
    int Quality,
    int Quantity,
    bool IsQuant)
{
    /// <summary>Major class is the top two bits of the item id; class 1 is NPCs.</summary>
    public int MajorClass => ItemId >> 6;

    public bool CanBeTaken => MajorClass != 1;
}

/// <summary>
/// UW object verbs as pure policy: take off a level, drop/throw back on,
/// combine stacks, split how-many counts. Identity survives every transfer —
/// the directory entity is never destroyed by a move.
/// Donor structure: quantity lives in the link field when the is_quant bit
/// (header bit 15) is set, capped below 0x200 (uwobject.cs ObjectQuantity);
/// statics and major-class-1 NPCs are not takeable.
/// </summary>
public static class UuObjectVerbs
{
    public const int MaxStackQuantity = 0x200 - 1;

    public static bool CanStack(UuObjectInstance a, UuObjectInstance b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return a.IsQuant && b.IsQuant && a.ItemId == b.ItemId && a.Quality == b.Quality;
    }

    /// <summary>Merge b into a. Returns the merged instance, or null when they do not stack.</summary>
    public static UuObjectInstance? Combine(UuObjectInstance a, UuObjectInstance b)
    {
        if (!CanStack(a, b)) return null;
        return a with { Quantity = Math.Min(a.Quantity + b.Quantity, MaxStackQuantity) };
    }

    /// <summary>
    /// Split count off an instance (the how-many flow). Returns the taken
    /// part; the original keeps the remainder.
    /// </summary>
    public static (UuObjectInstance Kept, UuObjectInstance Taken) Split(UuObjectInstance source, int count)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.IsQuant)
            throw new InvalidOperationException($"Item {source.ItemId} does not stack.");
        if (count <= 0 || count >= source.Quantity)
            throw new ArgumentOutOfRangeException(nameof(count), $"Split count must be within 1-{source.Quantity - 1}.");
        return (
            source with { Quantity = source.Quantity - count },
            source with { Quantity = count });
    }
}
