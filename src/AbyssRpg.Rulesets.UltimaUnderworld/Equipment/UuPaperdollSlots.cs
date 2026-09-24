namespace AbyssRpg.Rulesets.UltimaUnderworld.Equipment;

/// <summary>
/// UW paperdoll slots and what armour categories they accept. Slot order
/// follows the secondary donor (OU EInvSlot); acceptance follows the armour
/// table categories (shield 0, body 1, leggings 3, gloves 4, boots 5, helm 8,
/// ring 9 — armourobjectdat slot list). Shoulder slots carry containers and
/// light sources, not armour; weapons wield in the right hand.
/// </summary>
public enum UuPaperdollSlot
{
    RightShoulder,
    Head,
    LeftShoulder,
    RightHand,
    Torso,
    LeftHand,
    Hands,
    RightFinger,
    Legs,
    LeftFinger,
    Feet,
}

public static class UuPaperdollPolicy
{
    public static bool AcceptsArmour(UuPaperdollSlot slot, int armourCategory) =>
        (slot, armourCategory) switch
        {
            (UuPaperdollSlot.Head, 8) => true,
            (UuPaperdollSlot.Torso, 1) => true,
            (UuPaperdollSlot.Legs, 3) => true,
            (UuPaperdollSlot.Hands, 4) => true,
            (UuPaperdollSlot.Feet, 5) => true,
            (UuPaperdollSlot.LeftHand, 0) => true,
            (UuPaperdollSlot.RightFinger, 9) => true,
            (UuPaperdollSlot.LeftFinger, 9) => true,
            _ => false,
        };
}
