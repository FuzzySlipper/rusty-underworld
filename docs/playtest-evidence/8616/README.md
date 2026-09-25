# Task 8616: Crew GPU run

Observed 2026-09-25 on the travel revision (`8dfd75d`) with the product restarted
on it. Profile `rusty-underworld`, Wolf/Gamescope, session
`5ee07c05-ea37-48c4-b96c-7edde3eac7a4`, 1280×720 stream. The transition was
driven from the product's live-debug lane (`abyss.travel <level>`), which is
operator tooling: the gameplay caller that walks a stairway or falls down a pit is
carried by #8620.

The operator's own import was run for **two** levels before this run
(`scripts/import-level.sh 1` and `scripts/import-level.sh 2`), and the import step
now admits each imported level in the shipped bundle, so both are real operator
content rather than fixtures.

## Result

**A level transition admits the target level's own content and keeps the level
left behind.** Level 1 and level 2 are both imported; each brings its own
collision, geometry, placements and inhabitants, and the level the avatar leaves
keeps its state.

- [Level 1 before the transition](01-level-1.png) — artifact
  `b19d1574-9245-4f05-81e8-4f76d1e991fe`. The entry level, its imported geometry,
  and the HUD. `abyss.status` reads `level=1 actors=71`, and `abyss.where` reads
  `x=44.00 y=0.92 z=36.00` — level 1's own spawn.
- [Level 2 after the transition](02-level-2-after-travel.png) — artifact
  `6b2b69f4-9856-4f50-ba43-cd90fac58e51`. After `abyss.travel 2`: different
  geometry, the HUD outcome `You descend to level 2.`, and the Engine's own
  renderer-metrics widget reporting `triangles: 25508` — level 2's imported mesh,
  where level 1's was 26104. The widget also reports `live resources: geometry 4,
  material 4`: the previous level's resources are retired rather than released
  under a frame already in flight, and are disposed when the session ends.

State read on the live product across the round trip:

- `abyss.travel 2` → `level=2 actors=75`; `abyss.where` reads
  `x=484.00 y=4.92 z=28.00` (level 2's spawn, raised to the capsule's standing
  center) and `abyss.actors` reports a level-2 critter at `d=0.00` — the arrival
  tile's inhabitant, admitted with the level.
- `abyss.travel 1` → `level=1 actors=71`, `x=44.00 …` again, and the outcome
  `You descend to level 1.`
- `abyss.travel 2` while already on level 2 is refused: `The avatar is already on
  level 2.`
- `abyss.travel 7` is refused with the operator step named: the bundle carries no
  imported level 7.

## What this run does not show

Nothing in play triggers a transition yet: this is the mechanism plus an operator
probe (#8620 carries stairways, pits and level-change triggers, including the
clock cost the caller owns). Placed objects and actors are admitted, simulated and
saved but still not drawn (#8592), so the frames show each level's geometry
rather than its inhabitants. The GPU timer is unavailable on this host, so frame
pacing is submission evidence only.
