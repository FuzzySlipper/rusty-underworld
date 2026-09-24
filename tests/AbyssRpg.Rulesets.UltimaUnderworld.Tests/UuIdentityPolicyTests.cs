using AbyssRpg.Kit.World;
using AbyssRpg.Rulesets.UltimaUnderworld.Identity;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuIdentityPolicyTests
{
    [Fact]
    public void Avatar_is_actor_1_and_level_objects_encode_plainly()
    {
        Assert.Equal(new DurableIdentityReference(DurableIdentityKind.Actor, 1), UuIdentityPolicy.AvatarIdentity);
        Assert.Equal(new DurableIdentityReference(DurableIdentityKind.Item, 1024), UuIdentityPolicy.LevelObjectIdentity(1, 0));
        Assert.Equal(new DurableIdentityReference(DurableIdentityKind.Item, (ulong)(6 * 1024 + 822)), UuIdentityPolicy.LevelObjectIdentity(6, 822));
        Assert.Throws<ArgumentOutOfRangeException>(() => UuIdentityPolicy.LevelObjectIdentity(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => UuIdentityPolicy.LevelObjectIdentity(1, 1024));
    }

    [Fact]
    public void New_game_allocators_reserve_the_avatar_and_content_range()
    {
        var actors = UuIdentityPolicy.NewGameActorAllocator();
        Assert.Equal(new DurableIdentityReference(DurableIdentityKind.Actor, 2), actors.Allocate(DurableIdentityKind.Actor));
        var items = UuIdentityPolicy.NewGameItemAllocator();
        Assert.Equal(new DurableIdentityReference(DurableIdentityKind.Item, 10240), items.Allocate(DurableIdentityKind.Item));
    }
}
