namespace AbyssRpg.Rulesets.UltimaUnderworld.Session;

/// <summary>How one admitted record is drawn: a primitive, sized and tinted by class.</summary>
public readonly record struct UuObjectShape(
    UuObjectShapeKind Class,
    float Width,
    float Height,
    float Depth,
    Rusty.Engine.Color Color)
{
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
}
