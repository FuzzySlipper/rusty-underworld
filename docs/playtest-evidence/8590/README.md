# Task 8590: Crew GPU run

Observed 2026-09-25 on the placement build (HEAD at the run was `8d56d49`;
frames 01–05 come from session `f3ccc39d-bb22-4e29-8841-cbf79cfc3cbf`, slot-1,
profile `rusty-underworld`, Wolf/Gamescope Firefox, 1280×720 stream). The run was
driven through the installed Crew `playtest` CLI plus the product's own live-debug
lane (`POST /__rusty/product/runtime/debug/execute`), which is an operator
surface, not a gameplay path: the only thing it does here is stand the avatar on
a tile (`abyss.goto`) and read session facts (`abyss.status`, `abyss.actors`,
`abyss.where`). Every prompt, click and key in these frames is the product's
ordinary input path. Each frame below is a byte-for-byte copy of its original
capture in
the session's controller state directory, named with the session id above.

## Result

**The imported level now composes its own placements, and the interaction verb
reaches them.** The launch reports `actors=71`: the operator's level 1 carries 71
placed critters (mobile objects whose item id is the critter class the object
tables define), each admitted as an actor standing on the tile its own record
names. A swing resolves against one and can drop it; the use channel opens and
closes a door tile the import placed, through the level state the save carries.

- [Launch: 71 placed actors composed](01-launch-71-placed-actors.png) — artifact
  `8191397f-5a32-453f-98de-69eb05eab620`. The imported level is drawn, the HUD
  projects live facts, the Engine renderer-metrics widget reports `triangles:
  26104` (the imported mesh), and `abyss.status` reads
  `actors=71 defeated=False`.
- [A strike drops the placed critter](02-strike-drops-the-placed-critter.png) —
  artifact `3ef500e6-32e8-4275-ba33-a633ad0529d4`. Standing on tile (5,6) —
  `abyss.actors` reports the placed critter at `d=0.00` — six
  charge-and-release swings land one blow: `You strike for 6 and it falls.`
  Before this work the same swing reported `Your swing meets empty air.`,
  because no placement was admitted.
- [Use opens the placed door](03-use-opens-the-placed-door.png) — artifact
  `27424061-b2c7-4e6d-afa9-cf20c5d2bfe3`. On tile (9,10), a door tile in the
  import, the `E` channel reports `You open the door.`; `abyss.where` reads
  `x=76.00 y=1.92 z=84.00`, the tile center raised to the capsule's standing
  center.
- [Use closes it again](04-use-closes-it-again.png) — artifact
  `a22cab74-e3c4-4b23-95f5-033ea1541c36`. A second press reports
  `You close the door.`, so the verb toggles real state rather than printing one
  line.
- [The world after Journey Onward](05-door-state-survives-the-save.png) — artifact
  `f88dd6fb-b1f1-4115-8606-42e2d7cc75d0`. `abyss.load` resumes the newest autosave, following the product's
  Journey Onward rule; the autosave is written at level change, so it predates
  the door use and the door is shut again (`You open the door.`). The door state
  does round-trip through a save: the quicksave written here carries it, and
  `UuSessionTests.A_snapshot_round_trip_keeps_the_levels_own_changes` pins the
  opened door and the entity of a removed object across capture and restore.

## What this run does not show

Placed objects and critters are **admitted, simulated and saved but not yet
drawn**: the frames show the level mesh, and every placed thing is invisible.
Presentation of admitted objects, actors, containers and doors is a named
remainder of this work, not a claim of it. Talk, barter and container loot are
likewise not dispatched yet: the conversation owner needs imported conversation
content and the loot path needs an item catalog, and both are carried by their
own tasks. The GPU timer is unavailable on this host (Radeon HD 3200 with
Gamescope), so the metrics widget reports `GPU timer: unavailable` with an
`effective pacing` figure that measures submission, not completion.
