using System.Text.Json;
using Rusty.Engine;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Content;

/// <summary>Imported palette remaps and viewing distances. Four presentation
/// bands remain product policy; this is not the original game's renderer.</summary>
public sealed class UuLightingContent
{
    public const string PackId = "abyssrpg.lighting";
    private readonly int[] _distances;
    private readonly Color[][] _colors;
    private UuLightingContent(int[] distances, Color[][] colors) => (_distances, _colors) = (distances, colors);
    public int Reach(int brightness) => _distances[Math.Clamp(brightness, 0, _distances.Length - 1)];
    public Color Shade(Color color, int band)
    {
        int nearest = 0;
        float distance = float.MaxValue;
        for (int i = 0; i < 256; i++)
        {
            Color candidate = _colors[0][i];
            float d = MathF.Pow(candidate.R - color.R, 2) + MathF.Pow(candidate.G - color.G, 2) + MathF.Pow(candidate.B - color.B, 2);
            if (d < distance) { nearest = i; distance = d; }
        }
        Color shaded = _colors[Math.Clamp(band, 0, 3) * 5][nearest];
        return new Color(shaded.R, shaded.G, shaded.B, color.A);
    }
    public static UuLightingContent Read(ReadOnlyMemory<byte> payload, string label)
    {
        using JsonDocument document = JsonDocument.Parse(payload);
        JsonElement root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidOperationException($"'{label}' is not the current lighting schema.");
        JsonElement sources = root.GetProperty("sources");
        string[] names = ["UW/DATA/PALS.DAT", "UW/DATA/LIGHT.DAT", "UW/DATA/SHADES.DAT"];
        if (sources.GetArrayLength() != names.Length)
            throw new InvalidOperationException($"'{label}' requires the three UW1 lighting sources.");
        for (int i = 0; i < names.Length; i++)
        {
            JsonElement source = sources[i];
            string? hash = source.GetProperty("Sha256Hex").GetString();
            if (source.GetProperty("SourceGame").GetString() != "UW1"
                || source.GetProperty("SourceFile").GetString() != names[i]
                || hash is null || hash.Length != 64 || hash.Any(c => !Uri.IsHexDigit(c)))
                throw new InvalidOperationException($"'{label}' has invalid UW1 lighting provenance.");
        }
        int[] distances = root.GetProperty("viewingDistances").EnumerateArray().Select(row => row.GetInt32()).ToArray();
        Color[][] colors = root.GetProperty("colors").EnumerateArray().Select(map => map.EnumerateArray().Select(row => {
            if (row.GetArrayLength() != 3) throw new InvalidOperationException($"'{label}' requires RGB triplets.");
            float[] rgb = row.EnumerateArray().Select(channel => channel.GetSingle()).ToArray();
            if (rgb.Any(channel => !float.IsFinite(channel) || channel < 0 || channel > 1))
                throw new InvalidOperationException($"'{label}' contains invalid RGB channels.");
            return new Color(rgb[0], rgb[1], rgb[2], 1);
        }).ToArray()).ToArray();
        if (distances.Length != 8 || distances.Any(distance => distance <= 0 || distance > 15)
            || colors.Length != 16 || colors.Any(map => map.Length != 256))
            throw new InvalidOperationException($"'{label}' requires eight viewing distances and sixteen 256-color remaps.");
        return new UuLightingContent(distances, colors);
    }
}
