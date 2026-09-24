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

    public static bool Exists(string relative)
    {
        try
        {
            return File.Exists(Path.Combine(Root(), relative));
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    // Absence is reported loudly, never as a silent pass: xunit v2 has no
    // dynamic-skip API (SkipException exposes no usable constructor), so the
    // gate writes a SKIP line to test output and the test returns. A machine
    // without operator data shows Passed-with-SKIP-notices, and the log names
    // every file it did not check.
    public static string? Optional(string relative, Xunit.Abstractions.ITestOutputHelper output)
    {
        if (!Exists(relative))
        {
            output.WriteLine($"SKIP: operator UW1 file missing, nothing checked: {relative}");
            return null;
        }

        return Find(relative);
    }
}
