namespace AbyssRpg.Rulesets.UltimaUnderworld.Creation;

/// <summary>
/// UW1 avatar creation flow: gender, handedness, class, skill picks, portrait,
/// difficulty, name, confirm — in that order, each validated before the next
/// opens. Class base attributes come from SKILLS.DAT; the bonus pool is dealt
/// in 1-3 point chunks to random attributes; class skills roll on first sight
/// and picked skills roll immediately.
/// Behavior reference: UnderworldGodot
/// src/player/chargen/chargen.cs (PresentChargenOptions, SubmitChargenOption,
/// InitClassAttributes, GetSkillChoices). No code is shared; UI presentation
/// (buttons, portraits, typed input) belongs to the UI task.
/// </summary>
public sealed class UuCreationFlow
{
    public enum Stage
    {
        Gender = 0,
        Handedness = 1,
        Class = 2,
        Skills = 3,
        Portrait = 4,
        Difficulty = 5,
        Name = 6,
        Confirm = 7,
        Complete = 8,
    }

    public sealed record CreationResult(
        bool IsFemale,
        bool IsLeftHanded,
        int ClassIndex,
        int[] Attributes,
        int[] Skills,
        int Portrait,
        int Difficulty,
        string Name);

    private readonly CreationTables _tables;
    private readonly Random _rng;

    public Stage Current { get; private set; } = Stage.Gender;
    public bool IsFemale { get; private set; }
    public bool IsLeftHanded { get; private set; }
    public int ClassIndex { get; private set; } = -1;
    public int[] Attributes { get; } = new int[3];
    public int[] Skills { get; } = new int[UuSkillRolls.SkillCount];
    public int Portrait { get; private set; } = -1;
    public int Difficulty { get; private set; } = -1;
    public string Name { get; private set; } = "";

    private readonly int[] _offeredSkills = new int[CreationTables.MaxOfferedSkills];
    private int _arrayPtr;

    public UuCreationFlow(CreationTables tables, Random rng)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(rng);
        _tables = tables;
        _rng = rng;
        Array.Fill(_offeredSkills, -1);
    }

    public void SubmitGender(int choice)
    {
        RequireStage(Stage.Gender);
        if (choice is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(choice));
        IsFemale = choice == 1;
        Current = Stage.Handedness;
    }

    public void SubmitHandedness(int choice)
    {
        RequireStage(Stage.Handedness);
        if (choice is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(choice));
        IsLeftHanded = choice == 0;
        Current = Stage.Class;
    }

    public void SubmitClass(int choice)
    {
        RequireStage(Stage.Class);
        if (choice < 0 || choice >= _tables.Classes.Count)
            throw new ArgumentOutOfRangeException(nameof(choice));
        ClassIndex = choice;
        CreationTables.ClassRow baseAttributes = _tables.Classes[choice];
        Attributes[0] = baseAttributes.Strength;
        Attributes[1] = baseAttributes.Dexterity;
        Attributes[2] = baseAttributes.Intelligence;
        int bonus = baseAttributes.BonusPool;
        while (bonus > 0)
        {
            int chunk = Math.Min(1 + _rng.Next(3), bonus);
            Attributes[_rng.Next(3)] += chunk;
            bonus -= chunk;
        }

        Current = Stage.Skills;
    }

    /// <summary>
    /// Advances the skill walk. Consecutive automatic records are consumed
    /// inside the call; it returns the next player options, or null when the
    /// walk is exhausted (five picks or end of table).
    /// </summary>
    public int[]? OfferSkillChoices()
    {
        RequireStage(Stage.Skills);
        Array.Fill(_offeredSkills, -1);
        // Bounded by the table itself: each pass consumes one record.
        for (int pass = 0; pass <= _tables.ChoiceTable.Length; pass++)
        {
            int record = SeekChoiceRecord();
            if (record < 0 || _arrayPtr >= CreationTables.RecordsPerClass) return null;

            byte[] table = _tables.ChoiceTable;
            switch (table[record])
            {
                // Case 0 (free-pick sentinel) never occurs in the shipped
                // table (record-type census of SKILLS.DAT[32..]: no zeroes),
                // but the slot is honored so the walk cannot stall.
                case 0:
                    _arrayPtr++;
                    break;
                // Case 1 (fixed skill) rolls exactly once, here. The donor
                // batches these into a base-skills pass; rolling at record
                // time gives each auto one application either way.
                case 1:
                    RollSkill(table[record + 1]);
                    _arrayPtr++;
                    break;
                default:
                {
                    int count = table[record];
                    var options = new int[count];
                    for (int i = 0; i < count; i++)
                    {
                        options[i] = table[record + 1 + i];
                        _offeredSkills[i] = options[i];
                    }

                    return options;
                }
            }
        }

        // Unreachable: every pass consumes a record or returns, and passes
        // outnumber records. Present so the walk stays a bounded for-loop.
        return null;
    }

    public void SubmitSkillChoice(int optionIndex)
    {
        RequireStage(Stage.Skills);
        if (optionIndex < 0 || optionIndex >= _offeredSkills.Length || _offeredSkills[optionIndex] < 0)
            throw new ArgumentOutOfRangeException(nameof(optionIndex));
        RollSkill(_offeredSkills[optionIndex]);
        Array.Fill(_offeredSkills, -1);
        _arrayPtr++;
    }

    public void SubmitPortrait(int choice)
    {
        RequireStage(Stage.Portrait);
        if (choice is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(choice));
        Portrait = choice;
        Current = Stage.Difficulty;
    }

    public void SubmitDifficulty(int choice)
    {
        RequireStage(Stage.Difficulty);
        if (choice < 0) throw new ArgumentOutOfRangeException(nameof(choice));
        Difficulty = choice;
        Current = Stage.Name;
    }

    public void SubmitName(string name)
    {
        RequireStage(Stage.Name);
        // The original stores typed input raw, empty allowed; the UI task
        // decides prompting, so creation trims but never rejects.
        ArgumentNullException.ThrowIfNull(name);
        Name = name.Trim();
        Current = Stage.Confirm;
    }

    /// <summary>Confirms (true) or restarts (false) creation.</summary>
    public CreationResult? Confirm(bool keep)
    {
        RequireStage(Stage.Confirm);
        if (!keep)
        {
            UuCreationFlow fresh = new(_tables, _rng);
            IsFemale = fresh.IsFemale;
            IsLeftHanded = fresh.IsLeftHanded;
            ClassIndex = fresh.ClassIndex;
            Array.Clear(Attributes);
            Array.Clear(Skills);
            Portrait = fresh.Portrait;
            Difficulty = fresh.Difficulty;
            Name = fresh.Name;
                Array.Fill(_offeredSkills, -1);
            _arrayPtr = 0;
            Current = Stage.Gender;
            return null;
        }

        Current = Stage.Complete;
        return new CreationResult(IsFemale, IsLeftHanded, ClassIndex, (int[])Attributes.Clone(), (int[])Skills.Clone(), Portrait, Difficulty, Name);
    }

    /// <summary>Moves past the skill stage once the walk is exhausted.</summary>
    public void FinishSkills()
    {
        RequireStage(Stage.Skills);
        Current = Stage.Portrait;
    }

    private void RollSkill(int skill)
    {
        int governing = Attributes[UuSkillRolls.GoverningAttribute(skill)];
        Skills[skill] = Skills[skill] == 0
            ? UuSkillRolls.RollNewSkill(governing, _rng)
            : UuSkillRolls.RollExistingSkill(Skills[skill], governing, _rng);
    }

    private int SeekChoiceRecord()
    {
        // Walk count-prefixed records to the (class * 5 + arrayPtr)-th one,
        // mirroring the donor's record hunt.
        int need = (ClassIndex * 5) + _arrayPtr;
        int at = 0;
        byte[] table = _tables.ChoiceTable;
        for (int seen = 0; seen < need; seen++)
        {
            if (at >= table.Length) return -1;
            int step = table[at] switch { 0 => 1, 1 => 2, _ => table[at] + 1 };
            at += step;
        }

        return at < table.Length ? at : -1;
    }

    private void RequireStage(Stage stage)
    {
        if (Current != stage)
            throw new InvalidOperationException($"Creation is at {Current}, not {stage}.");
    }
}
