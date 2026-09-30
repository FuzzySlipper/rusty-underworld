using Xunit;

namespace AbyssRpg.TestSupport;

/// <summary>
/// The operator-supplied game data under <c>local/extracted/uw</c> and the packs
/// the operator import writes under <c>content/abyss/imports</c>. Both are
/// git-ignored and never committed, so a clean clone has neither. A test that
/// needs them names what it reads in its attribute, and without it the run
/// reports the test Skipped with the missing files named -- never Passed.
/// </summary>
public static class OperatorData
{
    /// <summary>The repository root: the directory holding AGENTS.md.</summary>
    public static string RepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "AGENTS.md"))) return directory.FullName;
        }

        throw new DirectoryNotFoundException("No repository root (AGENTS.md) above the test binaries.");
    }

    /// <summary>Where the operator's extracted install lives.</summary>
    public static string GameRoot() => System.IO.Path.Combine(RepositoryRoot(), "local", "extracted", "uw");

    /// <summary>Where the operator import writes its packs.</summary>
    public static string ImportsRoot() => System.IO.Path.Combine(RepositoryRoot(), "content", "abyss", "imports");

    /// <summary>The full path of one extracted game file.</summary>
    public static string FilePath(string relative) => System.IO.Path.Combine(GameRoot(), relative);

    /// <summary>
    /// Reads one extracted game file. Tests call this only under an
    /// <see cref="OperatorDataFactAttribute"/> naming the same file, so a missing
    /// file here is a test that did not declare what it reads.
    /// </summary>
    public static byte[] Read(string relative)
    {
        string path = FilePath(relative);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Operator UW1 file missing: {relative}. Name it in the test's [OperatorDataFact] so its absence skips.");
        }

        return File.ReadAllBytes(path);
    }

    /// <summary>The skip reason for the files that are missing, or null when all are present.</summary>
    internal static string? MissingGameFiles(IReadOnlyList<string> relatives)
    {
        string[] missing;
        try
        {
            string root = GameRoot();
            missing = [.. relatives.Where(relative => !File.Exists(System.IO.Path.Combine(root, relative)))];
        }
        catch (DirectoryNotFoundException)
        {
            missing = [.. relatives];
        }

        return missing.Length == 0
            ? null
            : $"Operator UW1 data is not installed under local/extracted/uw: {string.Join(", ", missing)}";
    }

    /// <summary>
    /// The release the golden values in <see cref="PinnedReleaseFactAttribute"/>
    /// tests were read from: the GOG <c>game.gog</c> ISO 9660 image labelled
    /// <c>UW12</c> (33,867,776 bytes; docs/research/uu1-data-inventory.md), by the
    /// sha256 of each file those tests read. Structure checks run on any release;
    /// a count or string read off this one is only asserted against this one.
    /// </summary>
    public const string PinnedRelease = "GOG game.gog 'UW12'";

    private static readonly Dictionary<string, string> PinnedHashes = new(StringComparer.Ordinal)
    {
        ["UW/DATA/LEV.ARK"] = "87e9a6e5d249df273e1964f48ad910afee6f7e073165c00237dfb9a22ae3a121",
        ["UW/DATA/TERRAIN.DAT"] = "8a455b6a873f4af5a1c67a0689c44baf48b728136dc32a100d2beaf701c3e09b",
        ["UW/DATA/STRINGS.PAK"] = "e8c9848a14c49620095ae180bdccbe9c6bf2942e3ad8def4a38a8a7d23004b0d",
        ["UW/DATA/CNV.ARK"] = "495b9023a27e83827040af553d7a03067270a986b2cb67d4cad1f91eddafc09e",
        ["UW/DATA/OBJECTS.DAT"] = "2b0ff63f81fb6f74b21731ac895b54706187b71adc8e0ee92eeeda39a539a509",
        ["UW/DATA/COMOBJ.DAT"] = "deacaafab8b04c283043b7b0615c0595e7bc34ad098133ff753a0cc3057d79b3",
        ["UW/DATA/SKILLS.DAT"] = "861a54443a19ae5fc3d4decd4d234fc7f8f3f9a6c8ccb64d4508bfff905e63b8",
    };

    /// <summary>The skip reason when a file is missing or is not the pinned release's, else null.</summary>
    internal static string? NotPinnedRelease(IReadOnlyList<string> relatives)
    {
        if (MissingGameFiles(relatives) is { } missing) return missing;
        foreach (string relative in relatives)
        {
            if (!PinnedHashes.TryGetValue(relative, out string? expected))
                throw new ArgumentException($"{relative} has no pinned hash; add it to OperatorData.PinnedHashes.");
            string actual = Convert.ToHexStringLower(
                System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(FilePath(relative))));
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                return $"Golden values pin {PinnedRelease}; this install's {relative} is another release (sha256 {actual}).";
        }

        return null;
    }

    internal static string? MissingImports()
    {
        try
        {
            return Directory.Exists(ImportsRoot())
                && Directory.EnumerateFiles(ImportsRoot(), "*.pack.json", SearchOption.AllDirectories).Any()
                ? null
                : "The operator import has not run: content/abyss/imports holds no packs (scripts/import-level.sh).";
        }
        catch (DirectoryNotFoundException)
        {
            return "No repository root to find content/abyss/imports under.";
        }
    }
}

/// <summary>A fact over operator game files; skipped, with the files named, when any is missing.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class OperatorDataFactAttribute : FactAttribute
{
    public OperatorDataFactAttribute(params string[] files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (files.Length == 0) throw new ArgumentException("Name the operator files the test reads.", nameof(files));
        Skip = OperatorData.MissingGameFiles(files);
    }
}

/// <summary>A theory over operator game files; skipped, with the files named, when any is missing.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class OperatorDataTheoryAttribute : TheoryAttribute
{
    public OperatorDataTheoryAttribute(params string[] files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (files.Length == 0) throw new ArgumentException("Name the operator files the test reads.", nameof(files));
        Skip = OperatorData.MissingGameFiles(files);
    }
}

/// <summary>
/// A fact whose expected values were read off the pinned release
/// (<see cref="OperatorData.PinnedRelease"/>); skipped when the files are missing
/// or come from another release, because its counts and strings would then be
/// wrong rather than the reader.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PinnedReleaseFactAttribute : FactAttribute
{
    public PinnedReleaseFactAttribute(params string[] files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (files.Length == 0) throw new ArgumentException("Name the operator files the test reads.", nameof(files));
        Skip = OperatorData.NotPinnedRelease(files);
    }
}

/// <summary>A fact over the packs the operator import wrote; skipped when the import has not run.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ImportedContentFactAttribute : FactAttribute
{
    public ImportedContentFactAttribute() => Skip = OperatorData.MissingImports();
}
