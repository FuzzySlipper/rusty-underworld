namespace UltimaUnderworld.Import;

/// <summary>
/// Reads UW1 SKILLS.DAT (187 bytes): 8 classes × 4 bytes of base attributes
/// (ST, DX, INT, bonus pool), then variable-length skill-choice records from
/// offset 32 walked as count-prefixed runs.
/// Behavior reference: UnderworldGodot src/player/chargen/chargen.cs
/// (InitChargenFiles, GetSkillChoices, class-attribute reads). No code is
/// shared with the donor.
/// </summary>
public static class SkillsDatReader
{
    public const int ClassCount = 8;
    public const int ClassRecordSize = 4;
    public const int ChoiceTableOffset = 32;

    public sealed record ClassAttributes(int Strength, int Dexterity, int Intelligence, int BonusPool);

    // Choice records from offset 32 are walked per class by creation logic
    // (donor case aids on the count byte), so the importer exposes them raw;
    // segmentation semantics belong to the creation task, not the reader.
    public sealed record SkillsData(IReadOnlyList<ClassAttributes> Classes, byte[] ChoiceTable);

    public static SkillsData Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < ChoiceTableOffset + 1)
            throw new InvalidDataException($"SKILLS.DAT is {data.Length} bytes; class attributes plus a choice table are required.");

        var classes = new ClassAttributes[ClassCount];
        for (int i = 0; i < ClassCount; i++)
        {
            int at = i * ClassRecordSize;
            classes[i] = new ClassAttributes(data[at], data[at + 1], data[at + 2], data[at + 3]);
        }

        return new SkillsData(classes, data[ChoiceTableOffset..].ToArray());
    }
}
