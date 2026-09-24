namespace UltimaUnderworld.Import;

/// <summary>
/// Pack output guard: regenerated (Tool-written) packs may only land under
/// the generated root; authored content is never overwritten. Paths are
/// repository-relative with forward slashes.
/// </summary>
public static class GeneratedPackGuard
{
    public const string GeneratedRoot = "generated";

    /// <summary>Resolve a Tool output path, throwing when it escapes the generated root.</summary>
    public static string ResolveOutputPath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        string normalized = relativePath.Replace('\\', '/');
        if (normalized.StartsWith('/') || normalized.Split('/').Any(segment => segment is "" or "." or ".."))
            throw new ArgumentException($"Pack output path '{relativePath}' is invalid.", nameof(relativePath));
        if (normalized != GeneratedRoot && !normalized.StartsWith(GeneratedRoot + "/", StringComparison.Ordinal))
            throw new InvalidOperationException($"Pack output path '{relativePath}' is outside the generated root '{GeneratedRoot}/'.");
        return normalized;
    }
}
