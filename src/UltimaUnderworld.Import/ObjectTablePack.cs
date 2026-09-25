using System.Text.Json;

namespace UltimaUnderworld.Import;

/// <summary>
/// Emits the runtime-facing slice of UW1 OBJECTS.DAT as a content pack: the
/// critter table indexed by item id 64-127 and the container table indexed by
/// item id 128-143. The tables carry source numbers; their game meaning is
/// ruleset policy (see the class rule in the donor's <c>src/World/uwobject.cs</c>:
/// majorclass is <c>item_id &gt;&gt; 6</c>).
/// </summary>
public static class ObjectTablePack
{
    public const int SchemaVersion = 1;

    public sealed record CritterRow(
        int ItemId,
        int Level,
        int AvgHp,
        int Strength,
        int Dexterity,
        int Intelligence,
        int Speed,
        int CorpseIndex,
        bool Swimmer,
        bool Flier,
        int Faction);

    public sealed record ContainerRow(int ItemId, int CapacityTenthStones, int ObjectsMask, int Slots);

    public sealed record Tables(
        IReadOnlyList<CritterRow> Critters,
        IReadOnlyList<ContainerRow> Containers,
        UwTableProvenance Provenance);

    public static Tables Emit(ObjectsDatReader.ObjectTables tables, UwTableProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(provenance);

        // Item id 64-127 is majorclass 1 (NPC): the table index is the low six
        // bits, so item id 64 + index.
        CritterRow[] critters = tables.Critters
            .Select((row, index) => new CritterRow(
                64 + index, row.Level, row.AvgHp, row.Strength, row.Dexterity, row.Intelligence,
                row.Speed, row.CorpseIndex, row.IsSwimmer, row.IsFlier, row.Faction))
            .ToArray();

        // Item id 128-143 is majorclass 2 with minorclass 0: the class index is
        // the low four bits, so item id 128 + index.
        ContainerRow[] containers = tables.Containers
            .Select((row, index) => new ContainerRow(128 + index, row.CapacityTenthStones, row.ObjectsMask, row.Slots))
            .ToArray();

        return new Tables(critters, containers, provenance);
    }

    public static string ToJson(Tables tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        var document = new
        {
            schemaVersion = SchemaVersion,
            source = tables.Provenance,
            critters = tables.Critters.Select(critter => new
            {
                itemId = critter.ItemId,
                level = critter.Level,
                avgHp = critter.AvgHp,
                strength = critter.Strength,
                dexterity = critter.Dexterity,
                intelligence = critter.Intelligence,
                speed = critter.Speed,
                corpseIndex = critter.CorpseIndex,
                swimmer = critter.Swimmer,
                flier = critter.Flier,
                faction = critter.Faction,
            }),
            containers = tables.Containers.Select(container => new
            {
                itemId = container.ItemId,
                capacityTenthStones = container.CapacityTenthStones,
                objectsMask = container.ObjectsMask,
                slots = container.Slots,
            }),
        };
        return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = false });
    }
}
