using Rusty.Engine;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Presentation;

/// <summary>
/// What the avatar's light does to a thing at a distance. Light falls off with
/// distance rather than switching off at the edge of a radius: a thing across the
/// room is drawn dimmer than one at arm's length, and only what is past the light
/// entirely is not drawn at all.
/// </summary>
public static class UuLightBands
{
    /// <summary>The band past the light: nothing is drawn there.</summary>
    public const int Hidden = 4;

    /// <summary>Bands inside the light, from closest to farthest.</summary>
    public const int Bands = 4;

    /// <summary>Which band a distance falls in, given how far the light reaches.</summary>
    public static int Band(float distance, float reach)
    {
        if (!float.IsFinite(distance) || distance < 0f)
            throw new ArgumentOutOfRangeException(nameof(distance));
        if (!float.IsFinite(reach) || reach <= 0f)
            throw new ArgumentOutOfRangeException(nameof(reach));
        if (distance > reach) return Hidden;
        int band = (int)(distance / reach * Bands);
        return band >= Bands ? Bands - 1 : band;
    }

    /// <summary>How much of a colour survives in a band: 1 at arm's length, less farther out.</summary>
    public static float Dimming(int band) => band switch
    {
        0 => 1f,
        1 => 0.72f,
        2 => 0.45f,
        3 => 0.22f,
        _ => 0f,
    };

    /// <summary>A colour as it appears in a band.</summary>
    public static Color Shade(Color color, int band)
    {
        float dimming = Dimming(band);
        return new Color(color.R * dimming, color.G * dimming, color.B * dimming, color.A);
    }
}
