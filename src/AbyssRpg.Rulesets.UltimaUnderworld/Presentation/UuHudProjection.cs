using AbyssRpg.Kit.Presentation;
using Rusty.Engine.Mechanics;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Presentation;

/// <summary>
/// UW HUD values as plain data plus a thin structured-value mapping for the
/// DOM: vital flasks (current/max), charge-gem fraction, compass heading and
/// eight-wind index, and outcome line. Semantic actions ride with their
/// owning systems (flasks with survival, rest with its panel); the HUD only
/// projects. The DOM stream itself plugs in with the UI shell.
/// </summary>
public sealed record UuHudValues(
    int Hp,
    int MaxHp,
    int Mana,
    int MaxMana,
    float ChargeFraction,
    float YawRadians,
    int WindIndex,
    string Outcome,
    int LightRadius);

public static class UuHudProjection
{
    public static UuHudValues Read(
        StatsComponent stats,
        TrackId hpTrack,
        TrackId manaTrack,
        float chargeFraction,
        float yawRadians,
        string outcome,
        int lightRadius = 0)
    {
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(hpTrack);
        ArgumentNullException.ThrowIfNull(manaTrack);
        ArgumentNullException.ThrowIfNull(outcome);
        Track hp = stats.GetTrack(hpTrack);
        Track mana = stats.GetTrack(manaTrack);
        return new UuHudValues(
            (int)hp.Current, (int)hp.Maximum.Value,
            (int)mana.Current, (int)mana.Maximum.Value,
            Math.Clamp(chargeFraction, 0f, 1f),
            yawRadians,
            WindIndex(yawRadians),
            outcome,
            lightRadius);
    }

    /// <summary>
    /// Eight-wind compass index from heading. Index 0 sits at zero yaw and
    /// steps uniformly; cardinal names bind later to the string table once
    /// the Engine yaw-zero convention is pinned.
    /// </summary>
    public static int WindIndex(float yawRadians)
    {
        if (!float.IsFinite(yawRadians)) throw new ArgumentOutOfRangeException(nameof(yawRadians));
        float turned = -yawRadians; // headings grow counter-clockwise
        float eighths = turned / (MathF.PI * 2f) * 8f;
        int index = ((int)MathF.Round(eighths) % 8 + 8) % 8;
        return index;
    }

    public static uint WriteUi(UiValueBuilder builder, UuHudValues values)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(values);
        return builder.Object(
            ("hp", builder.Number(values.Hp)),
            ("maxHp", builder.Number(values.MaxHp)),
            ("mana", builder.Number(values.Mana)),
            ("maxMana", builder.Number(values.MaxMana)),
            ("charge", builder.Number(values.ChargeFraction)),
            ("yawRadians", builder.Number(values.YawRadians)),
            ("wind", builder.Number(values.WindIndex)),
            ("outcome", builder.String(values.Outcome)),
            // How far the avatar's light reaches: the player reads the darkness
            // rather than discovering it by walking into it.
            ("lightRadius", builder.Number(values.LightRadius)));
    }
}
