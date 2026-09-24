using System.Security.Cryptography;

namespace UltimaUnderworld.Import;

/// <summary>Identifies the exact shipped bytes a normalized table came from.</summary>
public sealed record UwTableProvenance(string SourceGame, string SourceFile, int ByteLength, string Sha256Hex)
{
    public static UwTableProvenance FromBytes(string sourceGame, string sourceFile, ReadOnlySpan<byte> data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceGame);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        string hash = Convert.ToHexString(SHA256.HashData(data));
        return new UwTableProvenance(sourceGame, sourceFile, data.Length, hash);
    }
}
