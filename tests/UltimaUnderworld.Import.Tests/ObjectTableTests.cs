using UltimaUnderworld.Import;
using Xunit;
using Xunit.Abstractions;

namespace UltimaUnderworld.Import.Tests;

// Golden values observed from the shipped UW1 tables and cross-checked
// against donor table documentation (objectdata offsets, ObjectsData.cs
// comments, trigger-code enum).
public sealed class ObjectTableTests
{
    private readonly ITestOutputHelper _output;

    public ObjectTableTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Rejects_short_input()
    {
        Assert.Throws<InvalidDataException>(() => ObjectsDatReader.Read(new byte[100]));
    }

    [Fact]
    public void Trigger_table_survives_pack_serialization_with_source_provenance()
    {
        // Distinct values at every slot catch an offset error, a truncated table,
        // or substituting the familiar shipped values for the operator's bytes.
        byte[] raw = new byte[ObjectsDatReader.MinimumLength];
        int[] expected = Enumerable.Range(0, 16).Select(i => i * 17).ToArray();
        for (int i = 0; i < expected.Length; i++)
            raw[ObjectsDatReader.TriggerTypeOffset + i] = (byte)expected[i];
        for (int i = 0; i < expected.Length; i++) raw[ObjectsDatReader.RangedOffset + i * 3] = (byte)expected[i];
        UwTableProvenance provenance = UwTableProvenance.FromBytes("UW1", "UW/DATA/OBJECTS.DAT", raw);
        ObjectTablePack.Tables pack = ObjectTablePack.Emit(ObjectsDatReader.Read(raw), provenance);

        using System.Text.Json.JsonDocument json = System.Text.Json.JsonDocument.Parse(ObjectTablePack.ToJson(pack));
        Assert.Equal(expected, json.RootElement.GetProperty("triggerTypes").EnumerateArray().Select(value => value.GetInt32()));
        Assert.Equal(expected, json.RootElement.GetProperty("projectileDamage").EnumerateArray().Select(value => value.GetInt32()));
        Assert.Equal("UW1", json.RootElement.GetProperty("source").GetProperty("SourceGame").GetString());
        Assert.Equal(provenance.Sha256Hex, json.RootElement.GetProperty("source").GetProperty("Sha256Hex").GetString());
        Assert.Equal("UW/DATA/OBJECTS.DAT", json.RootElement.GetProperty("source").GetProperty("SourceFile").GetString());
    }

    [Fact]
    public void Rejects_short_common_object_and_skill_input()
    {
        Assert.Throws<InvalidDataException>(() => CommonObjDatReader.Read(new byte[10]));
        Assert.Throws<InvalidDataException>(() => SkillsDatReader.Read(new byte[10]));
    }

    [Fact]
    public void Reads_shipped_object_tables()
    {
        if (RequireDataOrSkip("UW/DATA/OBJECTS.DAT") is not byte[] raw) return;
        var tables = ObjectsDatReader.Read(raw);

        Assert.Equal(16, tables.Weapons.Count);
        Assert.Equal(new ObjectsDatReader.WeaponRow(6, 4, 2, 90, 25, 160, 4, 10), tables.Weapons[0]);

        Assert.Equal(16, tables.Ranged.Count);
        Assert.Equal(new ObjectsDatReader.RangedRow(5, 15, 192), tables.Ranged[0]);
        Assert.Equal(new ObjectsDatReader.RangedRow(3, 15, 0), tables.Ranged[8]);

        Assert.Equal(32, tables.Armour.Count);
        Assert.Equal(new ObjectsDatReader.ArmourRow(2, 8, 4, 1), tables.Armour[0]);

        Assert.Equal(64, tables.Critters.Count);
        Assert.Equal(1, tables.Critters[0].Level);
        Assert.Equal(5, tables.Critters[0].AvgHp);
        Assert.Equal(7, tables.Critters[0].Dexterity);
        Assert.Equal(3, tables.Critters[0].Speed);
        Assert.Equal(1, tables.Critters[0].Faction);
        Assert.Equal(20, tables.Critters[10].AvgHp);
        Assert.Equal(3, tables.Critters[10].CorpseIndex);
        Assert.False(tables.Critters[10].IsSwimmer);
        Assert.False(tables.Critters[10].IsFlier);
        Assert.Equal(48, tables.Critters[0].Raw.Length);

        Assert.Equal(16, tables.Containers.Count);
        Assert.Equal(new ObjectsDatReader.ContainerRow(125, 255, 255), tables.Containers[0]);

        Assert.Equal(16, tables.Lights.Count);
        // Byte order per both donors: duration first, brightness second.
        // Brightness 4 on row 4 respects the documented maximum of 4.
        Assert.Equal(new ObjectsDatReader.LightRow(Brightness: 4, Duration: 10), tables.Lights[4]);
        Assert.Equal(new ObjectsDatReader.LightRow(Brightness: 2, Duration: 3), tables.Lights[5]);

        Assert.Equal(new[] { 0, 2, 4, 5, 1, 7, 6, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, tables.TriggerTypes);

        // Food rows are raw unsigned bytes; the ruleset applies the signed
        // reading (donor sbyte cast, negative = intoxicating drink).
        Assert.Equal(16, tables.FoodNutrition.Count);
        Assert.True(tables.FoodNutrition[0] > 0);
        Assert.True(tables.FoodNutrition[10] > 127);
    }

    [Fact]
    public void Reads_shipped_common_object_records()
    {
        if (RequireDataOrSkip("UW/DATA/COMOBJ.DAT") is not byte[] raw) return;
        var rows = CommonObjDatReader.Read(raw);

        Assert.Equal(512, rows.Count);
        Assert.Equal(0, rows[0].Height);
        Assert.Equal(2, rows[0].Radius);
        Assert.Equal(24, rows[0].MassTenthStones);
        Assert.True(rows[0].CanBePickedUp);
    }

    [Fact]
    public void Reads_shipped_skill_tables()
    {
        if (RequireDataOrSkip("UW/DATA/SKILLS.DAT") is not byte[] raw) return;
        var skills = SkillsDatReader.Read(raw);

        Assert.Equal(8, skills.Classes.Count);
        Assert.Equal(new SkillsDatReader.ClassAttributes(20, 16, 12, 12), skills.Classes[0]);
        Assert.Equal(new SkillsDatReader.ClassAttributes(12, 12, 12, 20), skills.Classes[7]);
        Assert.Equal(155, skills.ChoiceTable.Length);
    }

    // Returns null after writing a SKIP notice when operator data is absent;
    // callers return early so the log, not silence, records what was skipped.
    private byte[]? RequireDataOrSkip(string relative) =>
        TestData.Optional(relative, _output) is string path ? File.ReadAllBytes(path) : null;
}
