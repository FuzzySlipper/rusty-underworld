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
public sealed class UuSessionDebugModule : IDebugCommandModule
{
    private UuGameSession? _session;

    /// <summary>Points the module at the ruleset's current session, replacing any released one.</summary>
    public void Attach(UuGameSession session) =>
        _session = session ?? throw new ArgumentNullException(nameof(session));

    private UuGameSession? Live => _session is { Disposed: false } session ? session : null;

    [DebugCommand("abyss.status", Description = "Level, vitals, clock, mode and outcome of the live session.")]
    public string Status()
    {
        if (Live is not { } session) return "no live session";
        SessionStatus status = session.Status;
        return string.Create(CultureInfo.InvariantCulture,
            $"level={status.Level} hp={status.Hp}/{status.MaxHp} mana={status.Mana}/{status.MaxMana} "
            + $"charge={status.ChargeFraction:F2} clock={session.ClockTicks} mode={session.Mode} "
            + $"actors={status.PresentActors} defeated={status.Defeated} outcome=\"{status.Outcome}\"");
    }

    [DebugCommand("abyss.pack", Description = "What the avatar carries, by item identity, and how much of it.")]
    public string Pack()
    {
        if (Live is not { } session) return "no live session";
        Rusty.Engine.Mechanics.InventoryView held = session.CarriedItems;
        return string.Create(CultureInfo.InvariantCulture,
            $"carried={held.UniqueItems.Count} items=[{string.Join(",", held.UniqueItems.Select(item => item.Definition.Value))}]");
    }

    [DebugCommand("abyss.where", Description = "Avatar position and heading in Engine units and radians.")]
    public string Where()
    {
        if (Live is not { } session) return "no live session";
        AbyssRpg.Kit.Controls.WorldPoint? position = session.PlayerPosition;
        return position is { } point
            ? string.Create(CultureInfo.InvariantCulture, $"x={point.X:F2} y={point.Y:F2} z={point.Z:F2} yaw={session.PlayerYawRadians:F3}")
            : "position unavailable";
    }

    [DebugCommand("abyss.travel", Description = "Operator probe: travel to another imported level of the dungeon.")]
    public string Travel(int level)
    {
        if (Live is not { } session) return "no live session";
        session.TravelToLevel(level, costTicks: 0);
        return $"level={level}";
    }

    [DebugCommand("abyss.actors", Description = "The nearest placed actors with their distance from the avatar.")]
    public string Actors()
    {
        if (Live is not { } session) return "no live session";
        var nearest = session.NearestActors(5);
        return nearest.Count == 0
            ? "no actors"
            : string.Join("; ", nearest.Select(entry =>
                $"x={entry.Actor.Position.X:F1} z={entry.Actor.Position.Z:F1} d={entry.Distance:F2}"));
    }

    [DebugCommand("abyss.goto", Description = "Operator probe: stand the avatar on a level tile.")]
    public string Goto(int tileX, int tileY)
    {
        if (Live is not { } session) return "no live session";
        session.PlaceOnTile(tileX, tileY);
        return $"tile=({tileX},{tileY})";
    }

    [DebugCommand("abyss.rune", Description = "Collect one rune into the casting shelf by index.")]
    public string CollectRune(int index)
    {
        if (Live is not { } session) return "no live session";
        session.CollectRune(index);
        return $"rune {index} collected; shelf={string.Join(",", session.Casting.Panel().Shelf)}";
    }

    [DebugCommand("abyss.cast", Description = "Attempt a cast from the shelved runes through the casting owner.")]
    public string Cast(int spellId)
    {
        if (Live is not { } session) return "no live session";
        Magic.UuCastingHosting.CastOutcome outcome = session.AttemptCast(spellId);
        return string.Create(CultureInfo.InvariantCulture,
            $"gate={outcome.Gate} backfired={outcome.Backfired} mana={outcome.ManaCost} "
            + $"primed={outcome.PrimedForAim} effect={(outcome.Effect?.SpellId.ToString(CultureInfo.InvariantCulture) ?? "none")} "
            + $"outcome=\"{session.Status.Outcome}\"");
    }

    [DebugCommand("abyss.damage", Description = "Apply harm to the avatar's defeat track.")]
    public string Damage(int amount)
    {
        if (Live is not { } session) return "no live session";
        session.ApplyDefeatDamage(amount, "Harmed by the debug lane.");
        return string.Create(CultureInfo.InvariantCulture, $"hp={session.Status.Hp}/{session.Status.MaxHp}");
    }

    [DebugCommand("abyss.respawn", Description = "Return the avatar to the level anchor and restore vitals.")]
    public string Respawn()
    {
        if (Live is not { } session) return "no live session";
        session.RespawnAtAnchor();
        return session.Status.Outcome;
    }
}
