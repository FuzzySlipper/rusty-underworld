using System.Text;
using AbyssRpg.Kit.Controls;
using Rusty.Engine;
using Xunit;

namespace AbyssRpg.Kit.Tests;

/// <summary>
/// The content identity is the value the Engine resolves an artifact by, and it
/// reads the digest big-endian. Hashing it the other way round resolves nothing
/// at launch, so the packed words are pinned against a published digest.
/// </summary>
public sealed class ContentHashingTests
{
    // SHA-256("abc") = ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad
    private const ulong Word0 = 0xba7816bf8f01cfeaUL;
    private const ulong Word1 = 0x414140de5dae2223UL;
    private const ulong Word2 = 0xb00361a396177a9cUL;
    private const ulong Word3 = 0xb410ff61f20015adUL;

    [Fact]
    public void Digest_words_are_read_big_endian()
    {
        ContentSha256 identity = ContentHashing.Of(Encoding.UTF8.GetBytes("abc"));
        Assert.Equal(new ContentSha256(Word0, Word1, Word2, Word3), identity);
    }

    [Fact]
    public void Digest_conversion_agrees_with_hashing()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("abc");
        byte[] digest = System.Security.Cryptography.SHA256.HashData(bytes);
        Assert.Equal(ContentHashing.Of(bytes), ContentHashing.FromSha256(digest));
    }

    [Fact]
    public void A_digest_that_is_not_a_sha256_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ContentHashing.FromSha256(new byte[31]));
        Assert.Throws<ArgumentOutOfRangeException>(() => ContentHashing.FromSha256(new byte[33]));
    }

    [Fact]
    public void Different_bytes_hash_differently()
    {
        Assert.NotEqual(
            ContentHashing.Of(Encoding.UTF8.GetBytes("abc")),
            ContentHashing.Of(Encoding.UTF8.GetBytes("abd")));
    }
}
