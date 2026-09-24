namespace AbyssRpg.Kit.Knowledge;

/// <summary>
/// Automap coverage: one 64x64 explored page per level, with the donor's
/// boolean run-length encoding for saves ("0:100,5,50" = 100 unexplored,
/// 5 explored, 50 unexplored).
/// </summary>
public sealed class AutomapPage
{
    public const int Dimension = 64;

    private readonly bool[] _mapped = new bool[Dimension * Dimension];

    public bool IsMapped(int x, int y) =>
        (uint)x < Dimension && (uint)y < Dimension && _mapped[y * Dimension + x];

    public void Reveal(int x, int y)
    {
        if ((uint)x >= Dimension || (uint)y >= Dimension)
            throw new ArgumentOutOfRangeException(nameof(x));
        _mapped[y * Dimension + x] = true;
    }

    public void RevealDisc(int centerX, int centerY, int radius)
    {
        if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
        for (int y = centerY - radius; y <= centerY + radius; y++)
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                int dx = x - centerX, dy = y - centerY;
                if ((uint)x < Dimension && (uint)y < Dimension && dx * dx + dy * dy <= radius * radius)
                    _mapped[y * Dimension + x] = true;
            }
    }

    public int MappedCount => _mapped.Count(mapped => mapped);

    public static string Encode(bool[] mapped)
    {
        ArgumentNullException.ThrowIfNull(mapped);
        if (mapped.Length == 0) return string.Empty; // donor-compatible: no data encodes empty
        var runs = new List<int>();
        bool current = mapped[0];
        int length = 1;
        for (int i = 1; i < mapped.Length; i++)
        {
            if (mapped[i] == current) length++;
            else { runs.Add(length); current = mapped[i]; length = 1; }
        }

        runs.Add(length);
        return (mapped[0] ? "1" : "0") + ":" + string.Join(",", runs);
    }

    /// <summary>
    /// An empty document decodes as all-unexplored (donor-compatible: the
    /// live default of an unsaved page is the empty string).
    /// </summary>
    public static bool[] Decode(string encoded, int expectedLength)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            if (expectedLength < 0) throw new ArgumentOutOfRangeException(nameof(expectedLength));
            return new bool[expectedLength];
        }

        string[] head = encoded.Split(':', 2);
        if (head.Length != 2 || (head[0] != "0" && head[0] != "1"))
            throw new ArgumentException("Invalid RLE head.", nameof(encoded));
        bool value = head[0] == "1";
        var result = new List<bool>();
        foreach (string run in head[1].Split(','))
        {
            if (!int.TryParse(run, out int length) || length <= 0)
                throw new ArgumentException("Invalid RLE run.", nameof(encoded));
            for (int i = 0; i < length; i++) result.Add(value);
            value = !value;
        }

        if (result.Count != expectedLength)
            throw new ArgumentException("RLE length mismatch.", nameof(encoded));
        return result.ToArray();
    }

    public string EncodePage() => Encode(_mapped);

    public void DecodeInto(string encoded) =>
        Array.Copy(Decode(encoded, _mapped.Length), _mapped, _mapped.Length);
}
