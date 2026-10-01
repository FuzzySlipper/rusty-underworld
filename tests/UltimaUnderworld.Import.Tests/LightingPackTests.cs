using System.Text.Json;
using UltimaUnderworld.Import;
using Xunit;

namespace UltimaUnderworld.Import.Tests;
public sealed class LightingPackTests
{
    [Fact]
    public void Operator_light_records_and_palette_remaps_survive_normalization()
    {
        byte[] objects = new byte[ObjectsDatReader.MinimumLength];
        for (int i = 0; i < 16; i++) {
            objects[ObjectsDatReader.LightOffset + i * 2] = (byte)(i + 10);
            objects[ObjectsDatReader.LightOffset + i * 2 + 1] = (byte)(i % 5);
        }
        var source = UwTableProvenance.FromBytes("UW1", "UW/DATA/OBJECTS.DAT", objects);
        var tables = ObjectTablePack.Emit(ObjectsDatReader.Read(objects), source);
        using var objectJson = JsonDocument.Parse(ObjectTablePack.ToJson(tables));
        var lights = objectJson.RootElement.GetProperty("lights");
        Assert.Equal(16, lights.GetArrayLength());
        for (int i = 0; i < 16; i++) {
            Assert.Equal(144 + i, lights[i].GetProperty("itemId").GetInt32());
            Assert.Equal(i + 10, lights[i].GetProperty("duration").GetInt32());
            Assert.Equal(i % 5, lights[i].GetProperty("brightness").GetInt32());
        }
        byte[] pals = new byte[8 * 768], remaps = new byte[16 * 256], shades = new byte[96];
        pals[6] = 63; pals[7] = 32; pals[8] = 1;
        remaps[256 + 1] = 2;
        for (int i = 0; i < 8; i++) shades[i * 12 + 6] = (byte)(0x80 | (i + 1));
        using var json = JsonDocument.Parse(LightingPack.Emit(pals, remaps, shades));
        Assert.Equal(Enumerable.Range(1, 8), json.RootElement.GetProperty("viewingDistances").EnumerateArray().Select(v => v.GetInt32()));
        var rgb = json.RootElement.GetProperty("colors")[1][1];
        Assert.Equal(252 / 255f, rgb[0].GetSingle());
        Assert.Equal(128 / 255f, rgb[1].GetSingle());
        Assert.Equal(4 / 255f, rgb[2].GetSingle());
        Assert.Equal(UwTableProvenance.FromBytes("UW1", "UW/DATA/LIGHT.DAT", remaps).Sha256Hex,
            json.RootElement.GetProperty("sources")[1].GetProperty("Sha256Hex").GetString());
    }
}
