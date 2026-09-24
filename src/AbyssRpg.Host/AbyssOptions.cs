using Rusty.Engine.Input;

namespace AbyssRpg.Host;

/// <summary>
/// Controls + options over Engine FpsInput: WASD+mouselook and standard
/// gamepad bindings, invert-Y toggle, detail level, and pause. The product
/// keeps one explicit selected configuration.
/// </summary>
public sealed record AbyssOptions(bool InvertY, AbyssDetail Detail, bool Paused)
{
    public static AbyssOptions Defaults { get; } = new(false, AbyssDetail.High, false);

    public FpsInputConfig ToInputConfig() => FpsInputConfig.Standard with
    {
        InvertControllerVertical = !InvertY,
    };
}

public enum AbyssDetail
{
    Low,
    High,
}

/// <summary>
/// Save UX: slot descriptions plus Journey Onward (continue from the
/// newest autosave, else the anchor).
/// </summary>
public static class AbyssSaveUx
{
    public sealed record SlotDescription(string Key, DateTime SavedAt, string Label);

    public static string? JourneyOnward(IReadOnlyList<SlotDescription> slots, int currentLevel)
    {
        ArgumentNullException.ThrowIfNull(slots);
        SlotDescription? newest = slots
            .Where(s => s.Key == AbyssSaveSlots.AutosaveKey(currentLevel) || s.Key.StartsWith("autosave/", StringComparison.Ordinal))
            .MaxBy(s => s.SavedAt);
        return newest?.Key ?? (slots.Any(s => s.Key == AbyssSaveSlots.AnchorKey) ? AbyssSaveSlots.AnchorKey : null);
    }
}
