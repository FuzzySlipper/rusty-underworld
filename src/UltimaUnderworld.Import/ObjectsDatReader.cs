namespace UltimaUnderworld.Import;

/// <summary>
/// Reads UW1 OBJECTS.DAT: the per-item definition tables indexed by the low
/// bits of the item id. Raw bytes are exposed as ints exactly as stored;
/// sign and game meaning are ruleset policy, not import knowledge.
/// Layout reference: UnderworldGodot src/objectdata/*.cs and
/// OpenUnderground Assets/Game/Scripts/ObjectsData.cs (comments cite
/// UW.EXE offsets for the trickiest rows). No code is shared with the donors.
/// </summary>
public static class ObjectsDatReader
{
    public const int WeaponOffset = 2;
    public const int WeaponCount = 16;
    public const int WeaponRecordSize = 8;

    public const int RangedOffset = 0x82;
    public const int RangedCount = 16;
    public const int RangedRecordSize = 3;

    public const int ArmourOffset = 0xB2;
    public const int ArmourCount = 32;
    public const int ArmourRecordSize = 4;

    public const int CritterOffset = 0x132;
    public const int CritterCount = 64;
    public const int CritterRecordSize = 48;

    public const int ContainerOffset = 0xD32;
    public const int ContainerCount = 16;
    public const int ContainerRecordSize = 3;

    public const int LightOffset = 0xD62;
    public const int LightCount = 16;
    public const int LightRecordSize = 2;

    public const int FoodOffset = 0xD82;
    public const int FoodCount = 16;

    public const int TriggerTypeOffset = 0xD92;
    public const int TriggerTypeCount = 16;

    public const int AnimationOffset = 0xDA2;
    public const int AnimationCount = 16;
    public const int AnimationRecordSize = 4;

    public const int MinimumLength = AnimationOffset + (AnimationCount * AnimationRecordSize);

    public sealed record WeaponRow(int Slash, int Bash, int Stab, int MinCharge, int ChargeSpeed, int MaxCharge, int Skill, int Durability);
    public sealed record RangedRow(int Damage, int AmmoType, int RangedWeaponType);
    public sealed record ArmourRow(int Protection, int Durability, int Unknown, int Slot);
    public sealed record CritterRow(int Level, byte[] Raw);
    // Donors disagree on bytes +1/+2 (Godot reads one u16 object mask,
    // OU reads mask byte + slots byte); the reader follows OU's 3-byte split.
    public sealed record ContainerRow(int CapacityTenthStones, int ObjectsMask, int Slots);
    public sealed record LightRow(int Brightness, int Duration);
    public sealed record AnimationRow(int Type, int Unknown, int StartFrame, int FrameCount);

    public sealed record ObjectTables(
        IReadOnlyList<WeaponRow> Weapons,
        IReadOnlyList<RangedRow> Ranged,
        IReadOnlyList<ArmourRow> Armour,
        IReadOnlyList<CritterRow> Critters,
        IReadOnlyList<ContainerRow> Containers,
        IReadOnlyList<LightRow> Lights,
        IReadOnlyList<int> FoodNutrition,
        IReadOnlyList<int> TriggerTypes,
        IReadOnlyList<AnimationRow> Animations);

    public static ObjectTables Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < MinimumLength)
            throw new InvalidDataException($"OBJECTS.DAT is {data.Length} bytes; tables need at least {MinimumLength}.");

        var weapons = new WeaponRow[WeaponCount];
        for (int i = 0; i < WeaponCount; i++)
        {
            int at = WeaponOffset + (i * WeaponRecordSize);
            weapons[i] = new WeaponRow(
                data[at], data[at + 1], data[at + 2], data[at + 3],
                data[at + 4], data[at + 5], data[at + 6], data[at + 7]);
        }

        var ranged = new RangedRow[RangedCount];
        for (int i = 0; i < RangedCount; i++)
        {
            int at = RangedOffset + (i * RangedRecordSize);
            ranged[i] = new RangedRow(data[at], data[at + 1], data[at + 2]);
        }

        var armour = new ArmourRow[ArmourCount];
        for (int i = 0; i < ArmourCount; i++)
        {
            int at = ArmourOffset + (i * ArmourRecordSize);
            armour[i] = new ArmourRow(data[at], data[at + 1], data[at + 2], data[at + 3]);
        }

        var critters = new CritterRow[CritterCount];
        for (int i = 0; i < CritterCount; i++)
        {
            int at = CritterOffset + (i * CritterRecordSize);
            critters[i] = new CritterRow(data[at], data.Slice(at, CritterRecordSize).ToArray());
        }

        var containers = new ContainerRow[ContainerCount];
        for (int i = 0; i < ContainerCount; i++)
        {
            int at = ContainerOffset + (i * ContainerRecordSize);
            containers[i] = new ContainerRow(data[at], data[at + 1], data[at + 2]);
        }

        var lights = new LightRow[LightCount];
        for (int i = 0; i < LightCount; i++)
        {
            int at = LightOffset + (i * LightRecordSize);
            // Byte order per both donors (Godot lightsourceobjectdat,
            // OU ObjectsData reader): duration first, brightness second.
            lights[i] = new LightRow(Brightness: data[at + 1], Duration: data[at]);
        }

        var food = new int[FoodCount];
        for (int i = 0; i < FoodCount; i++) food[i] = data[FoodOffset + i];

        var triggers = new int[TriggerTypeCount];
        for (int i = 0; i < TriggerTypeCount; i++) triggers[i] = data[TriggerTypeOffset + i];

        var animations = new AnimationRow[AnimationCount];
        for (int i = 0; i < AnimationCount; i++)
        {
            int at = AnimationOffset + (i * AnimationRecordSize);
            animations[i] = new AnimationRow(data[at], data[at + 1], data[at + 2], data[at + 3]);
        }

        return new ObjectTables(weapons, ranged, armour, critters, containers, lights, food, triggers, animations);
    }
}
