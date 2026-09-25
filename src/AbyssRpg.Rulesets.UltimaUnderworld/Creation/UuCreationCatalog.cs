using System.Text.Json;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Creation;

/// <summary>
/// The authored starting-avatar situation: which creation choices the slice
/// opens with before the character-creation screen exists, and the class rows
/// the creation flow consumes. The pack supplies class ids and numbers; this
/// ruleset maps them onto its compiled creation tables and refuses a pack whose
/// class list does not match its own catalog, so a content edit cannot silently
/// reorder classes.
/// </summary>
public sealed record UuAvatarDefaults(
    int Gender,
    int Handedness,
    int ClassIndex,
    int Portrait,
    int Difficulty,
    string Name);

public static class UuCreationCatalog
{
    /// <summary>The compiled class order this ruleset's creation tables use.</summary>
    public static IReadOnlyList<string> ClassOrder { get; } =
        ["fighter", "mage", "ranger", "bard", "tinker", "druid", "paladin", "shepherd"];

    public sealed record Catalog(CreationTables Tables, UuAvatarDefaults Defaults);

    public static Catalog Read(ReadOnlyMemory<byte> avatarOptions, ReadOnlyMemory<byte> classes, string avatarLabel, string classesLabel)
    {
        using JsonDocument options = Parse(avatarOptions, avatarLabel);
        using JsonDocument classDocument = Parse(classes, classesLabel);
        JsonElement optionsRoot = options.RootElement;
        JsonElement classesRoot = classDocument.RootElement;

        var classRows = new List<CreationTables.ClassRow>();
        foreach (JsonElement row in Array(classesRoot, "classes", classesLabel).EnumerateArray())
        {
            string id = String(row, "id");
            int expected = classRows.Count;
            if (expected >= ClassOrder.Count || !string.Equals(ClassOrder[expected], id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"'{classesLabel}' class {expected} is '{id}'; this ruleset's class order is "
                    + string.Join(", ", ClassOrder) + ".");
            }

            classRows.Add(new CreationTables.ClassRow(
                (int)Number(row, "strength"),
                (int)Number(row, "dexterity"),
                (int)Number(row, "intelligence"),
                (int)Number(row, "bonusPool")));
        }

        if (classRows.Count != ClassOrder.Count)
            throw new InvalidOperationException($"'{classesLabel}' must declare all {ClassOrder.Count} classes.");

        var choiceTable = new List<byte>();
        foreach (JsonElement record in Array(classesRoot, "skillChoiceTable", classesLabel).EnumerateArray())
            choiceTable.Add(checked((byte)record.GetInt32()));

        UuAvatarDefaults defaults = new(
            Gender: Index(Array(optionsRoot, "genders", avatarLabel), "male", avatarLabel),
            Handedness: Index(Array(optionsRoot, "handedness", avatarLabel), "right", avatarLabel),
            ClassIndex: Index(Array(optionsRoot, "classes", avatarLabel), "fighter", avatarLabel),
            Portrait: 0,
            Difficulty: Index(Array(optionsRoot, "difficulties", avatarLabel), "standard", avatarLabel),
            Name: String(optionsRoot, "defaultName"));

        return new Catalog(new CreationTables(classRows, choiceTable.ToArray()), defaults);
    }

    /// <summary>
    /// Runs the creation flow to completion with the authored defaults, taking
    /// the first offered skill option each time. The creation screen owns real
    /// choices later; this is the same flow, driven by content.
    /// </summary>
    public static UuCreationFlow.CreationResult RollDefault(Catalog catalog, Random rng)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(rng);
        UuAvatarDefaults defaults = catalog.Defaults;
        var flow = new UuCreationFlow(catalog.Tables, rng);
        flow.SubmitGender(defaults.Gender);
        flow.SubmitHandedness(defaults.Handedness);
        flow.SubmitClass(defaults.ClassIndex);
        for (int pass = 0; pass <= catalog.Tables.ChoiceTable.Length; pass++)
        {
            int[]? offered = flow.OfferSkillChoices();
            if (offered is null) break;
            flow.SubmitSkillChoice(0);
        }

        flow.FinishSkills();
        flow.SubmitPortrait(defaults.Portrait);
        flow.SubmitDifficulty(defaults.Difficulty);
        flow.SubmitName(defaults.Name);
        return flow.Confirm(true)
            ?? throw new InvalidOperationException("Default avatar creation did not confirm.");
    }

    private static int Index(JsonElement array, string expected, string label)
    {
        int at = 0;
        foreach (JsonElement element in array.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.String && string.Equals(element.GetString(), expected, StringComparison.Ordinal))
                return at;
            at++;
        }

        throw new InvalidOperationException($"'{label}' does not offer '{expected}'.");
    }

    private static JsonDocument Parse(ReadOnlyMemory<byte> payload, string label)
    {
        try
        {
            return JsonDocument.Parse(payload);
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException($"'{label}' is not valid JSON: {error.Message}", error);
        }
    }

    private static JsonElement Array(JsonElement root, string name, string label) =>
        root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.Array
            ? value
            : throw new InvalidOperationException($"'{label}' requires an array '{name}'.");

    private static string String(JsonElement root, string name)
    {
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: > 0 } text)
        {
            return text;
        }

        throw new InvalidOperationException($"'{name}' must be a non-empty string.");
    }

    private static double Number(JsonElement root, string name)
    {
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out double number)
            && double.IsFinite(number))
        {
            return number;
        }

        throw new InvalidOperationException($"'{name}' must be a finite number.");
    }
}
