using AbyssRpg.Rulesets.UltimaUnderworld.Content;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuProvenanceTests
{
    [Fact]
    public void Only_uw1_sources_admit()
    {
        Assert.Equal("UW1", UuProvenancePolicy.RequireUw1Source("UW1"));
        Assert.Throws<InvalidOperationException>(() => UuProvenancePolicy.RequireUw1Source("UW2"));
        Assert.Throws<InvalidOperationException>(() => UuProvenancePolicy.RequireUw1Source(""));
        Assert.Throws<InvalidOperationException>(() => UuProvenancePolicy.RequireUw1Source(null));
    }
}
