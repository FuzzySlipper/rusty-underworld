namespace AbyssRpg.Rulesets.UltimaUnderworld.Magic;

/// <summary>
/// The projectile object a spell releases. Objects 16-31 are projectiles (their
/// damage is the object table's projectile row); a cast spell names one by its
/// runes, and a class-5 spell trap by its variant (donor runicmagic.cs and the
/// spell trap's quality/owner). One table, so a cast and a trap that release the
/// same spell release the same object.
/// </summary>
public static class UuSpellProjectiles
{
    public const int FireballItem = 20;
    public const int LightningItem = 21;
    public const int Variant4Item = 22;
    public const int MagicArrowItem = 23;

    /// <summary>The projectile a cast spell releases, or 0 for a spell that releases none.</summary>
    public static int ForRunes(string runes) => runes switch
    {
        "OJ" => MagicArrowItem,
        "OG" => LightningItem,
        "PF" => FireballItem,
        _ => 0,
    };

    /// <summary>The projectile a class-5 spell trap variant releases, or 0.</summary>
    public static int ForTrapVariant(int variant) => variant switch
    {
        1 => MagicArrowItem,
        2 => LightningItem,
        3 => FireballItem,
        4 => Variant4Item,
        _ => 0,
    };
}
