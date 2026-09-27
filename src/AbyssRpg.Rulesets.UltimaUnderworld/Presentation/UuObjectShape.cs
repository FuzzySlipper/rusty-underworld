namespace AbyssRpg.Rulesets.UltimaUnderworld.Session;

/// <summary>How one admitted record is drawn: a primitive, sized and tinted by class.</summary>
public readonly record struct UuObjectShape(
    UuObjectShapeKind Class,
    float Width,
    float Height,
    float Depth,
    Rusty.Engine.Color Color)
{
    /// <summary>
    /// Whether an item id is a door: the donor's static door, majorclass 5 minorclass 0,
    /// or its moving door, majorclass 7 minorclass 0 classindex 0xF.
    /// </summary>
    public static bool IsDoorItem(int itemId) =>
        (itemId >> 6 == 5 && ((itemId & 0x30) >> 4) == 0)
        || (itemId >> 6 == 7 && ((itemId & 0x30) >> 4) == 0 && (itemId & 0xF) == 0xF);

    /// <summary>A creature that has been struck down: the same creature, lying down.</summary>
    public static UuObjectShape Corpse => new(UuObjectShapeKind.Corpse, 1.2f, 0.25f, 0.7f, new(0.4f, 0.16f, 0.14f, 1f));

    public static UuObjectShape Of(int itemId, bool mobile, bool door)
    {
        if (mobile) return new(UuObjectShapeKind.Creature, 0.7f, 1.5f, 0.7f, new(0.72f, 0.24f, 0.2f, 1f));
        if (door) return new(UuObjectShapeKind.Door, 1.5f, 1.7f, 0.25f, new(0.45f, 0.3f, 0.16f, 1f));
        if (Content.UuObjectTablesContent.IsContainerItem(itemId))
            return new(UuObjectShapeKind.Container, 0.7f, 0.7f, 0.7f, new(0.5f, 0.36f, 0.2f, 1f));
        if (itemId is >= 232 and <= 255) return new(UuObjectShapeKind.Rune, 0.35f, 0.35f, 0.35f, new(0.35f, 0.65f, 0.85f, 1f));
        return new(UuObjectShapeKind.Item, 0.4f, 0.4f, 0.4f, new(0.62f, 0.6f, 0.55f, 1f));
    }
}

/// <summary>The few shapes an admitted record is currently drawn as.</summary>
public enum UuObjectShapeKind
{
    Item,
    Container,
    Creature,
    Door,
    Rune,
    Corpse,
}
