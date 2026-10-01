using System.Text.Json;
using AbyssRpg.Rulesets.UltimaUnderworld.Movement;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Content;

/// <summary>
/// The ruleset's typed tuning handle: the values the shipped tuning profile
/// adjusts, validated on load so a bad profile fails at composition instead of
/// mid-dungeon. The profile names a movement set rather than inlining one:
/// movement numbers are a compiled ruleset table until playtest calibration
/// promotes them. Every value here is ours unless its record says otherwise; none
/// claims donor fidelity.
/// </summary>
public sealed record UuTuningProfile(
    double ClockTicksPerSecond,
    UuMovementTuning Movement,
    float ProjectileSpeed,
    float ProjectileLifetimeSeconds,
    UuCombatTuning Combat,
    UuLightTuning Light,
    UuBarterTuning Barter,
    UuCameraTuning Camera)
{
    public const string DefaultMovement = "default";

    public static UuTuningProfile Read(ReadOnlyMemory<byte> payload, string payloadLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadLabel);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException($"'{payloadLabel}' is not valid JSON: {error.Message}", error);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException($"'{payloadLabel}' must be an object.");
            double clockTicks = Positive(root, "clockTicksPerSecond", payloadLabel);

            string movement = root.TryGetProperty("movement", out JsonElement movementElement)
                && movementElement.ValueKind == JsonValueKind.String
                ? movementElement.GetString() ?? ""
                : "";
            if (!string.Equals(movement, DefaultMovement, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"'{payloadLabel}' selects movement '{movement}'; this ruleset has only '{DefaultMovement}'.");
            }

            JsonElement combat = Section(root, "combat", payloadLabel);
            JsonElement light = Section(root, "light", payloadLabel);
            JsonElement barter = Section(root, "barter", payloadLabel);
            JsonElement camera = Section(root, "camera", payloadLabel);
            return new UuTuningProfile(
                clockTicks,
                UuMovementTuning.Default.Validate(),
                (float)Positive(root, "projectileSpeed", payloadLabel),
                (float)Positive(root, "projectileLifetimeSeconds", payloadLabel),
                new UuCombatTuning(
                    Whole(combat, "meleeDifficulty", payloadLabel),
                    Whole(combat, "meleeDamageSides", payloadLabel),
                    Positive(combat, "fullChargeSeconds", payloadLabel)),
                new UuLightTuning(
                    Whole(light, "baseRadiusTiles", payloadLabel),
                    Whole(light, "spellBonusTiles", payloadLabel),
                    Whole(light, "placedLightReachTiles", payloadLabel),
                    PositiveFloat(light, "rendererIntensityPerSquareUnit", payloadLabel)),
                new UuBarterTuning(Whole(barter, "maxPatience", payloadLabel)),
                new UuCameraTuning(
                    Positive(camera, "fieldOfViewDegrees", payloadLabel),
                    Positive(camera, "nearPlane", payloadLabel),
                    Positive(camera, "farPlane", payloadLabel)));
        }
    }

    private static JsonElement Section(JsonElement root, string name, string label) =>
        root.TryGetProperty(name, out JsonElement section) && section.ValueKind == JsonValueKind.Object
            ? section
            : throw new InvalidOperationException($"'{label}' must declare the '{name}' tuning.");

    private static double Positive(JsonElement section, string name, string label) =>
        section.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out double number)
        && double.IsFinite(number)
        && number > 0d
            ? number
            : throw new InvalidOperationException($"'{label}' must declare a positive {name}.");

    private static float PositiveFloat(JsonElement section, string name, string label)
    {
        float value = (float)Positive(section, name, label);
        if (!float.IsFinite(value)) throw new InvalidOperationException($"'{label}' {name} exceeds a finite renderer value.");
        return value;
    }

    private static int Whole(JsonElement section, string name, string label) =>
        section.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out int number)
        && number > 0
            ? number
            : throw new InvalidOperationException($"'{label}' must declare a positive whole {name}.");
}

/// <summary>
/// The avatar's melee: the to-hit difficulty a swing rolls its attack skill
/// against, the damage die, and how long a hold takes to reach full charge.
/// Ours; no donor number was established for any of them.
/// </summary>
public sealed record UuCombatTuning(int MeleeDifficulty, int MeleeDamageSides, double FullChargeSeconds);

/// <summary>
/// How far light reaches, in tiles: the light the avatar always carries, what a
/// light spell adds while it holds, and what a burning light the item catalog
/// gives no radius reaches. Renderer intensity per squared range scales the
/// Engine point lights to these reaches; attenuation is an approximation. Ours.
/// </summary>
public sealed record UuLightTuning(int BaseRadiusTiles, int SpellBonusTiles, int PlacedLightReachTiles, float RendererIntensityPerSquareUnit);

/// <summary>How many refused offers a merchant tolerates before breaking off. Ours.</summary>
public sealed record UuBarterTuning(int MaxPatience);

/// <summary>The first-person view: vertical field of view and clip planes, in Engine units. Ours.</summary>
public sealed record UuCameraTuning(double FieldOfViewDegrees, double NearPlane, double FarPlane);
