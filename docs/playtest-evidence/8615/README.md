# Task 8615: Crew GPU run

Observed 2026-09-25 on the item-catalog and inventory revision (`7c4a1fc`) with
the product restarted on it. Profile `rusty-underworld`, Wolf/Gamescope, session
`0e52dcd3-9677-41e9-b82d-25e9b4ad6284`, 1280×720 stream. The level is the
operator's own import (levels 1 and 2, object tables and item catalog all emitted
from their data by `scripts/import-level.sh`).

Everything gameplay here went through the product's ordinary input channel: the
`E` key is the declared use action, and the two `abyss.goto` calls only stand the
avatar on a tile (documented operator tooling in `docs/gpu-playtesting.md`), so a
container and a runestone that the import placed could be reached without walking
the whole level.

## Result

**The level's own objects are now held, named and reachable.** A container the
import placed yields what its own record links to it, an object on the floor is
taken into the avatar, and a runestone the import placed lands on the casting
shelf, so a spell can be cast from stones picked up in play rather than from the
debug lane.

- [Looting a placed pack](01-loot-a-placed-pack.png) — artifact
  `5811d918-7d5f-4fb8-9b28-c2aed1da2d69` (the frame before it is
  `a21f5a59-29f6-4624-9bf0-df1e7732f7cf`). Standing on tile (23,6), where the
  operator's level places a pack (item 130) whose own link chain holds seven
  objects, the use channel answers `You loot 7 items from the pack.` and the
  seven move into the avatar's inventory in one Engine transfer.
- [Taking a placed runestone](02-take-a-placed-runestone.png) — artifact
  `f8b6abd4-1594-40a4-b742-7c06c32f39d7`. The import places a Jux stone and an Ort
  stone on tiles (28,4) and (28,5); using each answers `You take the runestone and
  lay it on the shelf.` and the HUD's shelf shows them, with the item catalog
  naming both as runestones rather than as unnamed objects.

## What this run does not show

The item catalog's names come from the string archive, so the two runestones are
named by the same content path as any other item; what this run does not show is
the catalog's mass or value being *spent*, because nothing carries weight yet and
no merchant exists. Placed objects are still not drawn (#8592), so the frames
show the level's geometry rather than the pack and the stones themselves. Carried
items do not yet survive a save: the level state and the avatar's state are saved,
the Engine's inventory world is not, and #8621 carries that. Talk and barter are
still absent, and #8614 carries them. The GPU timer is unavailable on this host,
so frame pacing is submission evidence only.
