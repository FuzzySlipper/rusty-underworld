namespace AbyssRpg.Rulesets.UltimaUnderworld.Creation;

/// <summary>
/// Creation inputs as plain data. The importer (or authored content) maps
/// SKILLS.DAT into this shape; the runtime never touches source formats.
/// </summary>
public sealed record CreationTables(IReadOnlyList<CreationTables.ClassRow> Classes, byte[] ChoiceTable)
{
    public sealed record ClassRow(int Strength, int Dexterity, int Intelligence, int BonusPool);
}
