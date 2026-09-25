using System.Buffers.Binary;
using Rusty.Engine;

namespace AbyssRpg.Kit.Controls;

/// <summary>
/// Content hashing shared by artifact producers and the Engine content contract.
/// The Engine carries a content SHA-256 as four words read big-endian from the
/// digest; hashing bytes the other way round resolves nothing.
/// </summary>
public static class ContentHashing
{
    public const int Sha256Bytes = 32;

    /// <summary>Converts a 32-byte SHA-256 digest into the Engine's four big-endian words.</summary>
    public static ContentSha256 FromSha256(ReadOnlySpan<byte> digest)
    {
        if (digest.Length != Sha256Bytes)
            throw new ArgumentOutOfRangeException(nameof(digest), digest.Length, "A content identity requires a 32-byte SHA-256 digest.");
        return new ContentSha256(
            BinaryPrimitives.ReadUInt64BigEndian(digest[..8]),
            BinaryPrimitives.ReadUInt64BigEndian(digest.Slice(8, 8)),
            BinaryPrimitives.ReadUInt64BigEndian(digest.Slice(16, 8)),
            BinaryPrimitives.ReadUInt64BigEndian(digest.Slice(24, 8)));
    }

    /// <summary>Hashes artifact bytes into the Engine's content identity.</summary>
    public static ContentSha256 Of(ReadOnlySpan<byte> bytes) =>
        FromSha256(System.Security.Cryptography.SHA256.HashData(bytes));
}
