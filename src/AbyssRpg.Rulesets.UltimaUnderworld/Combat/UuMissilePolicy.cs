namespace AbyssRpg.Rulesets.UltimaUnderworld.Combat;

/// <summary>
/// UW missile policy: each launcher takes exactly one ammunition kind
/// (sling→stone, bow/jewelled bow→arrow, crossbow→bolt); a shot rolls on the
/// Missile skill alone against a fixed difficulty — no Attack, Dexterity,
/// weapon-enchantment, or level bonuses; damage shares the melee path
/// (rolled down, armour soaks). Thrown objects resolve through the same
/// shared path off their stab modifier.
/// Donor behavior: OpenUnderground RangedWeapon.GetAmmoType,
/// GetAttackScore (Missile-alone roll), shared damage routine
/// (UW.EXE 0x2527e/0x259e7). No code shared. Ammunition item ids and the
/// fixed difficulty are content/tuning (Approximate until verified).
///
/// DELIBERATE DIVERGENCE (task-mandated): the donor scales a player-owned
/// physical missile's table damage by Missile skill — (192 + 8*Missile)/256
/// with crit adjustments off a Missile-vs-10 roll, gated on the 0xC0 marker
/// byte that keeps skill out of spell damage (Projectile.GetMaxDamage,
/// Assets/Game/Scripts/Projectile.cs:254-286; ObjectsData.cs:41-44,
/// UW.EXE 0x2b2e6). UW-T16 acceptance requires NO skill/level projectile
/// bonuses, so the policy omits the scale and its 0xC0/owner gates, and
/// RollShot crits do not feed damage. Revisit only if a task reinstates it.
/// </summary>
public static class UuMissilePolicy
{
    public enum LauncherKind
    {
        Sling,
        Bow,
        JeweledBow,
        Crossbow,
    }

    public static int AmmoItemId(LauncherKind launcher, int arrowId, int boltId, int stoneId) =>
        launcher switch
        {
            LauncherKind.Sling => stoneId,
            LauncherKind.Bow or LauncherKind.JeweledBow => arrowId,
            LauncherKind.Crossbow => boltId,
            _ => throw new ArgumentOutOfRangeException(nameof(launcher)),
        };

    /// <summary>Missile hit roll: Missile skill vs the fixed difficulty on the combat ladder.</summary>
    public static UuStrikeResolution.StrikeResult RollShot(int missileSkill, int difficulty, Random rng) =>
        UuStrikeResolution.RollToHit(missileSkill, difficulty, rng);

    /// <summary>Projectile damage maximum: the ranged-row damage, rolled down shared.</summary>
    public static int ProjectileMaximum(int rowDamage) => Math.Max(2, rowDamage);

    /// <summary>Thrown damage maximum off the object's stab modifier (Approximate).</summary>
    public static int ThrownMaximum(int stabModifier, int strength) =>
        UuStrikeResolution.DamageMaximum(stabModifier, strength);
}
