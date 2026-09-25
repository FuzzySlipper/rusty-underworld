using System.Text.Json;
using AbyssRpg.Rulesets.UltimaUnderworld.Movement;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Content;

/// <summary>
/// The ruleset's typed tuning handle: the values the shipped tuning profile
/// adjusts, validated on load so a bad profile fails at composition instead of
/// mid-dungeon. The profile names a movement set rather than inlining one:
/// movement numbers are a compiled ruleset table until playtest calibration
/// promotes them.
/// </summary>
public sealed record UuTuningProfile(double ClockTicksPerSecond, UuMovementTuning Movement)
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
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("clockTicksPerSecond", out JsonElement ticks)
                || ticks.ValueKind != JsonValueKind.Number
                || !ticks.TryGetDouble(out double clockTicks)
                || !double.IsFinite(clockTicks)
                || clockTicks <= 0d)
            {
                throw new InvalidOperationException($"'{payloadLabel}' must declare a positive clockTicksPerSecond.");
            }

            string movement = root.TryGetProperty("movement", out JsonElement movementElement)
                && movementElement.ValueKind == JsonValueKind.String
                ? movementElement.GetString() ?? ""
                : "";
            if (!string.Equals(movement, DefaultMovement, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"'{payloadLabel}' selects movement '{movement}'; this ruleset has only '{DefaultMovement}'.");
            }

            return new UuTuningProfile(clockTicks, UuMovementTuning.Default.Validate());
        }
    }
}
