namespace UltimaUnderworld.Import.Tests;

// Operator-supplied UW1 data lives under local/ (git-ignored, never committed).
// Tests that need it skip gracefully when the install is absent.
internal static class TestData
{
    public static string Root()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "local", "extracted", "uw");
            if (Directory.Exists(candidate)) return candidate;
        }

        throw new DirectoryNotFoundException("Operator UW1 data not found under local/extracted/uw.");
    }

    public static string Find(string relative)
    {
        string path = Path.Combine(Root(), relative);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Operator UW1 file missing: {relative}.");
        return path;
    }

    public static bool Exists(string relative) => File.Exists(Path.Combine(Root(), relative));
}
