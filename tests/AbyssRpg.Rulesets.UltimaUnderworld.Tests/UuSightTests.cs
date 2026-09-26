using AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

/// <summary>
/// Light does not pass through walls: what is drawn and what the map remembers
/// both start from what the avatar can actually see.
/// </summary>
public sealed class UuSightTests
{
    private static Func<int, int, bool> SolidAt(params (int X, int Y)[] solid) =>
        (x, y) => solid.Contains((x, y));

    [Fact]
    public void An_open_stretch_is_seen_along_its_whole_length() =>
        Assert.True(UuSight.HasLineOfSight(0, 0, 6, 0, SolidAt()));

    [Fact]
    public void A_wall_between_two_tiles_hides_what_is_behind_it()
    {
        Assert.False(UuSight.HasLineOfSight(0, 0, 6, 0, SolidAt((3, 0))));
        Assert.False(UuSight.HasLineOfSight(0, 0, 0, 5, SolidAt((0, 2))));
    }

    [Fact]
    public void The_tiles_the_line_starts_and_ends_on_never_block() =>
        Assert.True(UuSight.HasLineOfSight(0, 0, 4, 0, SolidAt((0, 0), (4, 0))));

    [Fact]
    public void A_diagonal_line_is_not_seen_through_a_corner()
    {
        // The diagonal from (0,0) to (2,2) passes between (1,0) and (0,1); a wall on
        // either of them closes the corner rather than being slipped past.
        Assert.False(UuSight.HasLineOfSight(0, 0, 2, 2, SolidAt((1, 0))));
        Assert.False(UuSight.HasLineOfSight(0, 0, 2, 2, SolidAt((0, 1))));
        Assert.True(UuSight.HasLineOfSight(0, 0, 2, 2, SolidAt()));
    }

    [Fact]
    public void Looking_at_your_own_tile_is_always_in_sight() =>
        Assert.True(UuSight.HasLineOfSight(4, 4, 4, 4, SolidAt((4, 4))));

    [Fact]
    public void A_line_with_no_tile_lookup_is_refused() =>
        Assert.Throws<ArgumentNullException>(() => UuSight.HasLineOfSight(0, 0, 1, 0, null!));
}
