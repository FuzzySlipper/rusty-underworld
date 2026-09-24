namespace AbyssRpg.Kit.Knowledge;

/// <summary>
/// Quest and game variables: 64 integer slots with conversation read/write
/// semantics (plain get/set; meaning belongs to content and conversation).
/// </summary>
public sealed class QuestVariables
{
    public const int SlotCount = 64;

    private readonly int[] _slots = new int[SlotCount];

    public int Get(int slot) => _slots[Checked(slot)];

    public void Set(int slot, int value) => _slots[Checked(slot)] = value;

    public void Clear() => Array.Clear(_slots);

    private static int Checked(int slot) =>
        (uint)slot < SlotCount ? slot : throw new ArgumentOutOfRangeException(nameof(slot));
}
