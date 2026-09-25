using System.Globalization;
using AbyssRpg.Kit;
using Rusty.Engine.Debugging;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Session;

/// <summary>
/// The session's live-debug commands. They exist so an operator can reach real
/// session owners (the cast flow, the rune shelf, the clock, the save payload)
/// from the Engine's live-debug panel without a parallel transport or a second
/// set of rules: each command calls the same method ordinary play calls.
/// </summary>
public sealed class UuSessionDebugModule(UuGameSession session) : IDebugCommandModule
{
    private readonly UuGameSession _session = session ?? throw new ArgumentNullException(nameof(session));

    [DebugCommand("abyss.status", Description = "Level, vitals, clock, mode and outcome of the live session.")]
    public string Status()
    {
        SessionStatus status = _session.Status;
        return string.Create(CultureInfo.InvariantCulture,
            $"level={status.Level} hp={status.Hp}/{status.MaxHp} mana={status.Mana}/{status.MaxMana} "
            + $"charge={status.ChargeFraction:F2} clock={_session.ClockTicks} mode={_session.Mode} "
            + $"defeated={status.Defeated} outcome=\"{status.Outcome}\"");
    }

    [DebugCommand("abyss.where", Description = "Avatar position and heading in Engine units and radians.")]
    public string Where()
    {
        AbyssRpg.Kit.Controls.WorldPoint? position = _session.PlayerPosition;
        return position is { } point
            ? string.Create(CultureInfo.InvariantCulture, $"x={point.X:F2} y={point.Y:F2} z={point.Z:F2} yaw={_session.PlayerYawRadians:F3}")
            : "position unavailable";
    }

    [DebugCommand("abyss.rune", Description = "Collect one rune into the casting shelf by index.")]
    public string CollectRune(int index)
    {
        _session.CollectRune(index);
        return $"rune {index} collected; shelf={string.Join(",", _session.Casting.Panel().Shelf)}";
    }

    [DebugCommand("abyss.cast", Description = "Attempt a cast from the shelved runes through the casting owner.")]
    public string Cast(int spellId)
    {
        Magic.UuCastingHosting.CastOutcome outcome = _session.AttemptCast(spellId);
        return string.Create(CultureInfo.InvariantCulture,
            $"gate={outcome.Gate} backfired={outcome.Backfired} mana={outcome.ManaCost} "
            + $"primed={outcome.PrimedForAim} effect={(outcome.Effect?.SpellId.ToString(CultureInfo.InvariantCulture) ?? "none")} "
            + $"outcome=\"{_session.Status.Outcome}\"");
    }

    [DebugCommand("abyss.damage", Description = "Apply harm to the avatar's defeat track.")]
    public string Damage(int amount)
    {
        _session.ApplyDefeatDamage(amount, "Harmed by the debug lane.");
        return string.Create(CultureInfo.InvariantCulture, $"hp={_session.Status.Hp}/{_session.Status.MaxHp}");
    }

    [DebugCommand("abyss.respawn", Description = "Return the avatar to the level anchor and restore vitals.")]
    public string Respawn()
    {
        _session.RespawnAtAnchor();
        return _session.Status.Outcome;
    }
}
