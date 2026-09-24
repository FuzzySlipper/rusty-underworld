using AbyssRpg.Kit.World;
using Rusty.Engine.Entities;
using Xunit;

namespace AbyssRpg.Kit.Tests;

public sealed class IdentityTests
{
    [Fact]
    public void Allocator_skips_reserved_and_never_reissues_removed()
    {
        var allocator = new DurableIdentityAllocator(DurableIdentityKind.Actor, 2, reserved: [1]);
        Assert.Equal(new DurableIdentityReference(DurableIdentityKind.Actor, 2), allocator.Allocate(DurableIdentityKind.Actor));
        var second = allocator.Allocate(DurableIdentityKind.Actor);
        allocator.Remove(second);
        Assert.Equal((ulong)4, allocator.NextIdentity(DurableIdentityKind.Actor));
        Assert.Contains(3UL, allocator.RemovedIdentities(DurableIdentityKind.Actor));
    }

    [Fact]
    public void Allocator_state_round_trips()
    {
        var allocator = new DurableIdentityAllocator(DurableIdentityKind.Item, 10240);
        allocator.Allocate(DurableIdentityKind.Item);
        DurableIdentityState state = allocator.CaptureState();
        var restored = DurableIdentityAllocator.Restore(state);
        Assert.Equal((ulong)10241, restored.NextIdentity(DurableIdentityKind.Item));
    }

    [Fact]
    public void Directory_create_resolve_destroy_lifecycle()
    {
        using var directory = new EntityDirectory();
        var identity = new DurableIdentityReference(DurableIdentityKind.Actor, 1);
        EntityId entity = directory.Create(identity, new EntityTypeId("test.avatar"));
        Assert.Throws<InvalidOperationException>(() => directory.Create(identity, new EntityTypeId("test.avatar")));
        Assert.Equal(entity, directory.Resolve(identity));
        Assert.Equal(identity, directory.IdentityOf(entity));
        Assert.True(directory.Destroy(identity));
        Assert.False(directory.TryResolve(identity, out _));
        Assert.False(directory.Destroy(identity));
    }

    [Fact]
    public void Classify_distinguishes_unloaded_from_never_issued()
    {
        using var directory = new EntityDirectory();
        var allocator = new DurableIdentityAllocator(DurableIdentityKind.Actor, 2, reserved: [1]);
        var issued = allocator.Allocate(DurableIdentityKind.Actor);
        var never = new DurableIdentityReference(DurableIdentityKind.Actor, 999);
        Assert.Equal(DurableEntityResolution.Unloaded, directory.Classify(issued, allocator));
        Assert.Equal(DurableEntityResolution.NeverIssued, directory.Classify(never, allocator));
    }
}
