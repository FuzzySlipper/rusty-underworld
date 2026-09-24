using UltimaUnderworld.Import;
using Xunit;

namespace UltimaUnderworld.Import.Tests;

public sealed class GeneratedPackGuardTests
{
    [Fact]
    public void Generated_paths_resolve_and_authored_paths_refuse()
    {
        Assert.Equal("generated/collision/l1.json", GeneratedPackGuard.ResolveOutputPath("generated/collision/l1.json"));
        Assert.Equal("generated/collision/l1.json", GeneratedPackGuard.ResolveOutputPath("generated\\collision\\l1.json"));
        Assert.Throws<InvalidOperationException>(() => GeneratedPackGuard.ResolveOutputPath("abyss/pack.json"));
        Assert.Throws<InvalidOperationException>(() => GeneratedPackGuard.ResolveOutputPath("generatedness/x.json"));
        Assert.Throws<ArgumentException>(() => GeneratedPackGuard.ResolveOutputPath("../generated/x.json"));
        Assert.Throws<ArgumentException>(() => GeneratedPackGuard.ResolveOutputPath("/generated/x.json"));
        Assert.Throws<ArgumentException>(() => GeneratedPackGuard.ResolveOutputPath(""));
    }
}
