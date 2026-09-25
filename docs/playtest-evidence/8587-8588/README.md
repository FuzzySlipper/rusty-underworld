# Tasks 8587 and 8588: Crew GPU run

Observed 2026-09-25 on revision `e4fd43f` (frame 05 on `fb203ef`). The commits
after them changed the renderer-metrics wrapper's pre-mount visibility, a
comment, and a refusal message for an unrespawnable session; none of them
changes what these frames show. Profile `rusty-underworld`; Wolf/Gamescope
Firefox; 1280×720 stream; slot-1. Sessions `7e59854e-0725-4f18-9aea-6f8e0eaff527` (frames 01–04,
06, 07) and `599b24d0-56b6-4020-9747-f9c754811b8d` (frame 05), both driven
through the installed Crew `playtest` CLI. Every frame below is a byte-for-byte
copy of an original capture; the original artifact id and directory follow each
one.

## Result

**Both tasks pass on the GPU host.** The launch shows the imported level as
visible geometry with the avatar HUD projecting live session facts, and the
Engine's own renderer-metrics widget reporting the imported mesh as what is
actually drawn.

- [Dungeon, HUD, renderer metrics](01-dungeon-hud-renderer-metrics.png) —
  artifact `a14ddbbf-fcb3-433d-8df0-0d09a146b708`. Floors, walls and a doorway
  are visible; the HUD reads `35/35 hp · 1/1 mana · level 1` from the product
  projection; the metrics widget reports `Draws: 2 | triangles: 26104`, which is
  exactly the imported level mesh (`abyssrpg.level-1-render.json`, 26104
  vertices, 13052 triangles).
- [After look and move](02-after-move-and-look.png) — artifact
  `6fc9cb3c-1ca4-4b77-beff-4f8be9a78281`. Mouse look and a held `W` moved the
  avatar: the doorway shifted and new wall runs entered the frame.
- [Charge held](03-charge-while-held.png) — artifact
  `d07c4b01-e5a4-4b38-bb1e-af0bdeaac962`. Primary held for 2.5 s: the charge bar
  builds from empty to full while the button is down.
- [Menu key opens the Engine console](04-menu-key-opens-engine-console.png) —
  artifact `2b45814b-00b0-4992-a776-91ace060fc95`. `Escape` released the pointer,
  the product paused and published the menu (Save, Journey Onward, three real
  slots, the two tool controls), and `c` opened the packaged Engine live-debug
  panel, which reports `Connected` and lists the product's real command catalog.
- [Engine console command response](05-engine-console-command-response.png) —
  artifact `dc8a2c23-a73e-43e0-8109-a8678990f17e`. A command typed into the
  panel's own input returned `lifecycle=Running mode=Paused` in the panel's
  Responses block, alongside its diagnostics and product/runtime lane readouts.
- [Quicksave through the declared key mapping](06-quicksave-via-key-mapping.png)
  — artifact `9d48de5e-0b3a-4b50-94df-732aeb0d37a2`. `Alt+S`, the physical
  mapping in the staged manifest, wrote a quicksave slot.
- [Journey Onward resumed](07-journey-onward-resumed.png) — artifact
  `496fbd9c-c39e-4aa6-bbb4-b57038204b55`. The menu's Journey Onward button
  replaced the session from the saved payload, rebuilt the world at the anchor
  and re-captured the pointer; the product reported `mode=Playing` with the
  clock restarted.

Live state read from the running product over the Engine's own live-debug route
during the same run: `abyss.product` (bundle `abyssrpg.stygian-abyss`, packs
`abyssrpg.avatar-options, abyssrpg.classes, abyssrpg.starting-kit,
abyssrpg.level-1`, `mode=Playing`), `abyss.status` (level, vitals, charge, clock,
outcome), `abyss.slots` (autosave plus quicksaves), and after a load
`lifecycle=Running mode=Playing` again with the world at `x=44.00 y=0.92
z=36.00`, the imported spawn tile.

## After the review round

A second run on revision `b018566` repeated the launch, look, move, pause and
Journey Onward load after the review's lifetime and save-boundary fixes:

- [Journey Onward after the review fixes](08-after-review-load.png) — artifact
  `15ccd175-2fe4-4fca-9887-04275dd52f95`, session
  `8f09b1bd-2c39-4ad4-8d5e-5c5a0da4a85e`. The menu's Journey Onward replaced the
  world from the autosave, the product reported `mode=Playing` with the clock
  restarted, the pointer was re-captured, and the renderer still reports the
  imported mesh (`triangles: 26104`). The product refused the loads the review
  reproduced as destructive before this revision; its own suite now asserts
  that a corrupt payload, a foreign ruleset's slot and a save of another level
  all leave the live session in place.

## What these captures establish

- The ordinary launch path composes: bundle → packs → tuning → imported level →
  spatial session → scene and camera → admitted updates. No demonstration scene
  is staged and no placeholder HUD text remains.
- Input reaches real owners: Engine first-person look and movement through the
  Engine spatial step, charge/release through the combat owner, `Escape` through
  the Engine pointer-lock-loss fact, the save/load intents through the product's
  slot store.
- The projection is authoritative: every value the HUD shows (vitals, charge,
  outcome, mode, menu availability, slot list) came from the product's one
  stream, and the menu followed the product's mode rather than DOM state.
- The Engine's own diagnostics surfaces are adopted, not imitated: the
  live-debug panel and the renderer-metrics widget are the packaged modules, and
  the metrics widget reports the imported mesh as the drawn geometry.

## Known limits of this evidence

- The renderer reports `GPU timer: unavailable` on this host (Radeon HD 3200 via
  Gamescope); frame pacing was verified from the submission rate and frame
  counts only.
- Judge-reported mouse input did not reliably reach the DOM below roughly the
  middle of the 1280×720 stream, so the two tool controls were exercised with
  the menu's `c` and `m` keys rather than the pointer. That is why the menu
  carries single-key access; the tool buttons themselves are covered by the DOM
  tests, not by these captures.
- Only collision, visible geometry and a spawn are imported today. There are no
  object, critter or conversation placements, so melee resolves against no
  opponent (`Your swing meets empty air.`) and the conversation, barter, loot
  and NPC owners are composed but unreachable in play; Den #8590 carries the
  placement import, the admitted Use/interact dispatch, combat against a real
  opponent, and world-reachable casting. Den #8576 carries the repeat acceptance
  run.
- Frame freshness, GPU adapter identity readback, and pointer-lock state are
  target-reported by the Crew service; none of them is asserted by the product.

## Local verification

`./scripts/verify.sh` passes on this revision: Engine pair verification, `npm
ci`, the TypeScript UI build, the DOM suite, every project's tests including the
ordinary-composition suite, the architecture laws, and CoreCLR staging. The
scripts and the semantic suites are described in
[`docs/gpu-playtesting.md`](../../gpu-playtesting.md); those tests do not
themselves prove GPU output, which is what the captures above are for.
