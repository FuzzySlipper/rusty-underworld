namespace AbyssRpg.Rulesets.UltimaUnderworld.Content;

/// <summary>
/// UW content provenance gate: normalized packs admitted into UW
/// compositions must derive from UW1 sources. UW2 appears only as donor
/// documentation and never as an extraction source.
/// </summary>
public static class UuProvenancePolicy
{
    public const string RequiredSourceGame = "UW1";

    public static string RequireUw1Source(string? sourceGame)
    {
        if (sourceGame != RequiredSourceGame)
            throw new InvalidOperationException($"UW content must derive from {RequiredSourceGame}; saw '{sourceGame}'.");
        return sourceGame;
    }
}
