namespace AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;

/// <summary>Light identities: major class 2, minor class 1, index 0–7.
/// Donor: UnderworldGodot src/World/uwobject.cs, src/interaction/use.cs
/// (UseMajorClass2), and src/objects/light.cs (LightOn/LightOff).
/// Unlit indices 0–3 become lit indices 4–7 by adding four.</summary>
public static class UuLightSources
{
    public const int LightMajorClass = 2;
    public const int LitClassIndex = 4;
    public const int LitStep = 4;
    public static int MajorClass(int itemId) => itemId >> 6;
    public static int ClassIndex(int itemId) => itemId & 15;
    public static bool IsLight(int itemId) => itemId >= 144 && itemId <= 151;
    public static bool IsLit(int itemId) => IsLight(itemId) && ClassIndex(itemId) >= LitClassIndex;
    public static int Lit(int itemId) => IsLight(itemId) && !IsLit(itemId) ? itemId + LitStep : itemId;
    public static int Unlit(int itemId) => IsLit(itemId) ? itemId - LitStep : itemId;
}
