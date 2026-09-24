using Rusty.Engine.Input;

namespace AbyssRpg.Host;

/// <summary>
/// Controls + options over Engine FpsInput: WASD+mouselook and standard
/// gamepad bindings, plus an invert-Y toggle that drives both pointer and
/// controller vertical. Detail is a UI-consumed preference (no Engine
/// mapping in this task). Pause lives in the product lifecycle, never here.
/// </summary>
public sealed record AbyssOptions(bool InvertY, AbyssDetail Detail)
{
    public static AbyssOptions Defaults { get; } = new(false, AbyssDetail.High);

    public FpsInputConfig ToInputConfig() => FpsInputConfig.Standard with
    {
        InvertControllerVertical = InvertY,
        PointerLookConfig = FpsInputConfig.Standard.PointerLookConfig with
        {
            InvertVertical = InvertY,
        },
    };
}

public enum AbyssDetail
{
    Low,
    High,
}

/// <summary>
/// Save UX: slot descriptions plus Journey Onward (newest autosave, else anchor).
/// </summary>
public static class AbyssSaveUx
{
    public sealed record SlotDescription(string Key, DateTime SavedAt, string Label);

    public static string? JourneyOnward(IReadOnlyList<SlotDescription> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        SlotDescription? newest = slots
            .Where(s => s.Key.StartsWith(AbyssSaveSlots.AutosavePrefix, StringComparison.Ordinal))
            .MaxBy(s => s.SavedAt);
        return newest?.Key ?? (slots.Any(s => s.Key == AbyssSaveSlots.AnchorKey) ? AbyssSaveSlots.AnchorKey : null);
    }
}
