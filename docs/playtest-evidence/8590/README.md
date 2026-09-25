# Task 8590: Crew GPU run

Observed 2026-09-25 on the placement build: frames 01–05 on `8d56d49` in session
`f3ccc39d-bb22-4e29-8841-cbf79cfc3cbf` (slot-1) and frames 06–08 on `293eed0` in
session `0a084aff-a61f-41c9-a6f4-fb08509eb0c7` (slot-2), profile
`rusty-underworld`, Wolf/Gamescope Firefox, 1280×720 stream. The run was
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
names. A swing resolves against one, damages it and can drop it; the use channel
opens and closes a door tile the import placed, through the level state the save
carries.

The last three frames come from the reconciled revision (session
`0a084aff-a61f-41c9-a6f4-fb08509eb0c7`, slot-2), after the review round; the
first five are from the earlier session `f3ccc39d-bb22-4e29-8841-cbf79cfc3cbf`
and show the same behaviour before the review fixes.

- [Final launch: 71 placed actors](06-final-launch.png) — artifact
  `47002c15-3c86-493e-9e52-d85456c3478d`. The same composed slice on the
  reconciled revision.
- [A strike drops the placed critter](07-final-strike-falls.png) — artifact
  `9b594fd0-6d17-4023-808a-22ebeafcc0ef`. Swinging at the critter placed on tile
  (5,6) lands `You strike for 1.` and then `You strike for 5 and it falls.` The
  fall records the placement in the level state the save carries, so the same
  critter is not waiting at full strength after a load.
- [Use opens the placed door](08-final-door-open.png) — artifact
  `f319f5f2-ce1a-4278-b852-b5993a0fe466`. On the placed door tile (9,10), the
  `E` channel reports `You open the door.` A probe onto a solid tile is refused
  on this revision (`Tile (0,0) is not open in the admitted level`) instead of
  leaving the capsule inside geometry, which used to make the Engine refuse the
  next step and taint the runtime.

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
- [Before a fall: the world after Journey Onward](05-door-state-survives-the-save.png)
  — artifact `f88dd6fb-b1f1-4115-8606-42e2d7cc75d0`. `abyss.load` resumes the
  newest autosave, following the product's Journey Onward rule; the autosave is
  written at level change, so it predates the door use and the door is shut again
  (`You open the door.`). Door state and recorded kills do round-trip through a
  save: `UuSessionTests.A_snapshot_round_trip_keeps_the_levels_own_changes` pins
  the opened door and a removed object's entity across capture and restore, and
  `UuCritterAdmissionTests.A_restored_save_removes_the_actor_of_a_placement_it_says_is_gone`
  pins that a critter the save says is gone is not standing after a load.

## What this run does not show

Placed objects and critters are **admitted and simulated but not drawn**: the
frames show the level mesh, and every placed thing is invisible. Presentation of
admitted objects, actors, containers and doors is carried by #8592. A killed
critter is recorded in the level state and does not come back after a load, but
its *wounds* are not saved, so a critter that was hurt and survived is whole
again after a load. Talk, barter and container loot are not dispatched yet: talk
needs imported conversation content and loot needs an item catalog, carried by
#8614 and #8615. Casting is still reachable only through the live-debug lane; a
placed rune that can be picked up is part of #8615. The GPU timer is unavailable
on this host (Radeon HD 3200 with Gamescope), so the metrics widget reports
`GPU timer: unavailable` with an `effective pacing` figure that measures
submission, not completion.
