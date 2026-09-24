using AbyssRpg.Rulesets.UltimaUnderworld.Equipment;
using Xunit;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Tests;

public sealed class UuEquipmentTests
{
    [Fact]
    public void Paperdoll_accepts_matching_armour_only()
    {
        Assert.True(UuPaperdollPolicy.AcceptsArmour(UuPaperdollSlot.Head, 8));
        Assert.True(UuPaperdollPolicy.AcceptsArmour(UuPaperdollSlot.Torso, 1));
        Assert.True(UuPaperdollPolicy.AcceptsArmour(UuPaperdollSlot.Legs, 3));
        Assert.True(UuPaperdollPolicy.AcceptsArmour(UuPaperdollSlot.Hands, 4));
        Assert.True(UuPaperdollPolicy.AcceptsArmour(UuPaperdollSlot.Feet, 5));
        Assert.True(UuPaperdollPolicy.AcceptsArmour(UuPaperdollSlot.LeftHand, 0));
        Assert.True(UuPaperdollPolicy.AcceptsArmour(UuPaperdollSlot.RightFinger, 9));
        Assert.True(UuPaperdollPolicy.AcceptsArmour(UuPaperdollSlot.LeftFinger, 9));
        Assert.False(UuPaperdollPolicy.AcceptsArmour(UuPaperdollSlot.Head, 1));
        Assert.False(UuPaperdollPolicy.AcceptsArmour(UuPaperdollSlot.RightShoulder, 1));
        Assert.False(UuPaperdollPolicy.AcceptsArmour(UuPaperdollSlot.Torso, 9));
    }

    [Fact]
    public void Containers_gate_type_and_weight()
    {
        // Shipped container row 0: capacity 125, mask 0xFFFF (takes all).
        Assert.True(UuContainerPolicy.AcceptsType(0xFFFF, UuContainerPolicy.ContentRunes, 0));
        Assert.True(UuContainerPolicy.AcceptsType(512 + UuContainerPolicy.ContentArrows, UuContainerPolicy.ContentArrows, 99));
        Assert.False(UuContainerPolicy.AcceptsType(512 + UuContainerPolicy.ContentArrows, UuContainerPolicy.ContentRunes, 99));
        Assert.True(UuContainerPolicy.FitsWeight(125, 100, 24));
        Assert.False(UuContainerPolicy.FitsWeight(125, 100, 26));
        Assert.Throws<ArgumentOutOfRangeException>(() => UuContainerPolicy.FitsWeight(125, 100, -1));
    }

    [Fact]
    public void Encumbrance_gates_lifting_at_the_maximum()
    {
        // MaxWeight = 300 + STR*13; STR 20 -> 560 (0.1-stone units as carried).
        Assert.False(UuEncumbrancePolicy.IsEncumbered(560, 560));
        Assert.True(UuEncumbrancePolicy.IsEncumbered(561, 560));
        Assert.True(UuEncumbrancePolicy.CanLift(500, 560, 60));
        Assert.False(UuEncumbrancePolicy.CanLift(500, 560, 61));
        Assert.Throws<ArgumentOutOfRangeException>(() => UuEncumbrancePolicy.IsEncumbered(-1, 560));
    }
}
