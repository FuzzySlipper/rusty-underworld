# Task 8576: Crew GPU run

Observed 2026-09-25. Profile `rusty-underworld`; Wolf/Gamescope Firefox;
session `d8b993af-f87d-4089-bed2-18d6c4f80aba`, slot-1, 1280×720 stream.
The independent playtester operated through the installed Crew CLI.
An initial attempt through retired Den tools failed before creating a session;
it is not the GPU result recorded here.

## Result

**Product fail; service launch/input transport succeeded.** The initial image
and repeated captures showed a dark viewport, a placeholder diagnostic panel,
and three bars. No dungeon geometry or objects appeared. Center focus click,
a one-second W hold, mouse movement in both directions, and primary click
produced no visible world response. Escape opened Resume/Pause controls;
Pause left the menu visible, and Resume closed it and prompted pointer lock.
This verifies DOM response, not authoritative gameplay pause/resume.

- [Initial original capture](initial.png)
- [After W original capture](after-w.png)
- [Escape menu original capture](escape-menu.png)

These are byte-for-byte copies of original captures, not edited screenshots.
Original directory:
`/home/dev/dsh-crew/experiments/wolf-den-srv/controller/state/d8b993af-f87d-4089-bed2-18d6c4f80aba/`.
Their original artifact IDs, respectively:
`3d259012-5be9-45d3-8657-55d820672501`,
`5e36de33-cd7e-4e52-8823-c474b7908401`,
`94e56797-b532-436d-b95a-3c901b96bab2`.
Session receipt:
`/home/agent/.local/state/crew-playtest/session-d8b993af-f87d-4089-bed2-18d6c4f80aba.json`.

Native inputs were target-reported without release errors. Game consumption,
exact frame freshness, GPU adapter identity, and pointer-lock readback were
not established by those receipts. The browser visibly displayed a pointer-lock
notification. Stop succeeded with released=true, local_capture_stopped=true,
and no errors; the service pool subsequently had no occupied slots.

## Fixes and verification

Fixed the invalid default watch root, compiled/staged browser ESM UI, removed
unsupported source map/declaration artifacts, declared the HUD projection, and
corrected the readiness probe. Registered the checked-in Wolf profile in the
installed service. See [setup](../../gpu-playtesting.md).

`./scripts/verify.sh` passed: 156 C# tests, 2 DOM tests, all project builds,
architecture checks, matched pair verification, and CoreCLR staging. Final
CoreCLR staging also passed after adding stale UI artifact cleanup. NativeAOT
was not run. These tests do not prove gameplay composition.

## Remaining required work

Source matches the failed visible result: the ordinary entry has no session
attachment or world rendering, and its staged input manifest has no intents
or mappings. Component helpers alone do not satisfy the playable slice.
Den #8587 carries complete ordinary-entry live integration and its GPU proof.
Den #8588 carries real menu/projection/Engine diagnostics wiring and its GPU
proof. Task #8576 retains the repeat GPU acceptance run after those land.
No unresolved Crew service or upstream Engine blocker was found.
