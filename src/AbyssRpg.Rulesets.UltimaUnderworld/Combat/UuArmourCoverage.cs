namespace AbyssRpg.Rulesets.UltimaUnderworld.Combat;

/// <summary>
/// UW armour coverage: worn pieces covering a body part sum, and the total
/// soaks off damage landing there. Head covers head; torso covers torso;
/// hands cover arms; legs and feet both cover legs; a shield in the
/// off-hand covers torso and arms.
/// Donor structure: OpenUnderground Inventory.GetArmourByBodyPart (EXE
/// 0x24dc1/0x24e22, slot table 03 00 01 02 02, shield torso+arms 0x7e512).
/// </summary>
public enum UuBodyPart
{
    Torso,
    Arms,
    Legs,
    Head,
}

public static class UuArmourCoverage
{
    public sealed record WornProtection(int Head, int Torso, int Hands, int Legs, int Feet, int Shield);

    public static int ProtectionForPart(WornProtection worn, UuBodyPart part)
    {
        ArgumentNullException.ThrowIfNull(worn);
        return part switch
        {
            UuBodyPart.Head => worn.Head,
            UuBodyPart.Torso => worn.Torso + worn.Shield,
            UuBodyPart.Arms => worn.Hands + worn.Shield,
            UuBodyPart.Legs => worn.Legs + worn.Feet,
            _ => throw new ArgumentOutOfRangeException(nameof(part)),
        };
    }
}
