using UltimaUnderworld.Import;
using Xunit;

namespace UltimaUnderworld.Import.Tests;

// Golden values observed from the shipped UW1 tables and cross-checked
// against donor table documentation (objectdata offsets, ObjectsData.cs
// comments, trigger-code enum).
public sealed class ObjectTableTests
{
    [Fact]
    public void Rejects_short_input()
    {
        Assert.Throws<InvalidDataException>(() => ObjectsDatReader.Read(new byte[100]));
    }

    [Fact]
    public void Reads_shipped_object_tables()
    {
        if (!TestData.Exists("UW/DATA/OBJECTS.DAT")) return;
        var tables = ObjectsDatReader.Read(File.ReadAllBytes(TestData.Find("UW/DATA/OBJECTS.DAT")));

        Assert.Equal(16, tables.Weapons.Count);
        Assert.Equal(new ObjectsDatReader.WeaponRow(6, 4, 2, 90, 25, 160, 4, 10), tables.Weapons[0]);

        Assert.Equal(16, tables.Ranged.Count);
        Assert.Equal(new ObjectsDatReader.RangedRow(5, 15, 192), tables.Ranged[0]);
        Assert.Equal(new ObjectsDatReader.RangedRow(3, 15, 0), tables.Ranged[8]);

        Assert.Equal(64, tables.Armour.Count);
        Assert.Equal(new ObjectsDatReader.ArmourRow(2, 8, 4, 1), tables.Armour[0]);

        Assert.Equal(64, tables.Critters.Count);
        Assert.Equal(1, tables.Critters[0].Level);
        Assert.Equal(48, tables.Critters[0].Raw.Length);

        Assert.Equal(16, tables.Containers.Count);
        Assert.Equal(new ObjectsDatReader.ContainerRow(125, 255, 255), tables.Containers[0]);

        Assert.Equal(new[] { 0, 2, 4, 5, 1, 7, 6, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, tables.TriggerTypes);

        // Food rows carry signed nutrition (negative = intoxicating drink).
        Assert.Equal(16, tables.FoodNutrition.Count);
        Assert.True(tables.FoodNutrition[0] > 0);
        Assert.True(tables.FoodNutrition[10] > 127);
    }

    [Fact]
    public void Reads_shipped_common_object_records()
    {
        if (!TestData.Exists("UW/DATA/COMOBJ.DAT")) return;
        var rows = CommonObjDatReader.Read(File.ReadAllBytes(TestData.Find("UW/DATA/COMOBJ.DAT")));

        Assert.Equal(512, rows.Count);
        Assert.Equal(0, rows[0].Height);
        Assert.Equal(2, rows[0].Radius);
        Assert.Equal(24, rows[0].MassTenthStones);
        Assert.True(rows[0].CanBePickedUp);
        Assert.Throws<InvalidDataException>(() => CommonObjDatReader.Read(new byte[10]));
    }

    [Fact]
    public void Reads_shipped_skill_tables()
    {
        if (!TestData.Exists("UW/DATA/SKILLS.DAT")) return;
        var skills = SkillsDatReader.Read(File.ReadAllBytes(TestData.Find("UW/DATA/SKILLS.DAT")));

        Assert.Equal(8, skills.Classes.Count);
        Assert.Equal(new SkillsDatReader.ClassAttributes(20, 16, 12, 12), skills.Classes[0]);
        Assert.Equal(new SkillsDatReader.ClassAttributes(12, 12, 12, 20), skills.Classes[7]);
        Assert.Equal(155, skills.ChoiceTable.Length);
        Assert.Throws<InvalidDataException>(() => SkillsDatReader.Read(new byte[10]));
    }
}
