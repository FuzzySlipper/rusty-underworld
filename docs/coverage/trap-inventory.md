# Trap inventory

Status: inventory baseline, not tasks. Factual donor survey only; nothing is
implemented here. Primary donor: UnderworldGodot (`src/traps/trap.cs`,
`src/traps/hack_trap.cs`, `src/objectdata/triggerobjectdat.cs`), via
[`underworldgodot-survey.md`](../research/underworldgodot-survey.md) §§8, 10, 15.
Manual trap material is thin (no dedicated section in the guide); behavior
evidence from [`openunderground-survey.md`](../research/openunderground-survey.md) §3
and the donor `CHANGELOG.md` is cited per item. Anything not directly
confirmed is flagged "not verified".

## 1. Vanilla dispatch table (majorclass 6)

Dispatcher: `trap.cs`
`ActivateTrap(character, trapObj, ObjectUsed, triggerX/Y, objList)`. Chain
rule: the next object is `trapObj.link` by default; check-variable traps may
redirect it; create-object and delete-object traps always stop the chain.
Chained minorclass 0/1 objects re-enter `ActivateTrap`; chained minorclass
2/3 objects go to `trigger.RunTrigger` with `triggertypes.ALL` (tail past
that call is not verified). Related but NOT traps: `src/triggers/`
(move/collision/open/pressure triggers).

### 1a. minorclass 0, classindex 0x0–0xF

| classindex | trap file | meaning |
| --- | --- | --- |
| 0x0 | `a_damagetrap.cs` | damage trap |
| 0x1 | `a_teleport_trap.cs` | teleport trap (destination level + tile from trap fields) |
| 0x2 | `an_arrow_trap.cs` | arrow trap (spawns an object at the trigger tile; code marks `implemented ...-ish`) |
| 0x3 | `hack_trap.cs` | do/hack entry: dispatches on `quality` (see §2) |
| 0x4 | — / `a_specialeffect_trap.cs` | pit trap in UW1 (sets `implemented = true` to continue the chain — no effect); special-effect trap in UW2 |
| 0x5 | `a_change_terrain_trap.cs` | change-terrain trap |
| 0x6 | `a_spell_trap.cs` | spell trap |
| 0x7 | `a_create_object_trap.cs` | create-object trap; always stops chain |
| 0x8 | `a_door_trap.cs` | door trap |
| 0x9 | `a_ward_trap.cs` | ward trap (returns the next index) |
| 0xA | `a_skill_trap.cs` / ward | skill trap in UW2; tell trap in UW1 ("Works the same as a Ward Trap") |
| 0xB | `a_delete_object_trap.cs` | delete-object trap; always stops chain |
| 0xC | `an_inventory_trap.cs` | inventory trap (returns the next index) |
| 0xD | `a_set_variable_trap.cs` | set-variable trap (quest/game variable) |
| 0xE | `a_check_variable_trap.cs` | check-variable trap (test selects the next link) |
| 0xF | — | null trap / combination trap (does nothing; `implemented = true`) |

### 1b. minorclass 1, classindex 0–8 (all but classindex 0 UW2-gated)

| classindex | trap file | meaning |
| --- | --- | --- |
| 0 | `a_text_string_trap.cs` | text-string trap (both games) |
| 1 | `an_experience_trap.cs` | experience trap (UW2-only dispatch) |
| 2 | `a_jump_trap.cs` | jump trap (UW2-only dispatch) |
| 3 | `a_change_from_trap.cs` | change-from trap (UW2-only dispatch) |
| 4 | — | change-to trap (UW2-only dispatch; does nothing) |
| 5 | `an_oscillator_trap.cs` | oscillator (UW2-only dispatch) |
| 6 | `a_proximity_trap.cs` | proximity trap (UW2-only dispatch; returns next index) |
| 7 | `a_pit_trap.cs` | pit trap, UW2 ("In UW1 Pit trap is 6,0,4 and does nothing") |
| 8 | `a_bridge_trap.cs` | bridge trap (UW2-only dispatch) |

Minorclass 2/3 on a chain are triggers, not traps — no 6-2/6-3 trap meanings
are claimed here.

## 2. The do/hack family (6-0-3, dispatched on `quality`)

Glosses come ONLY from `hack_trap.cs` case comments; everything else is
"behavior unverified, name only". No behaviors invented.

`a_do_trap_*` (8): bullfrog (q24, UW1 — name only), camera (q2 — name only),
conversation (q42, UW1 — "a talking door!"), emeraldpuzzle (q40, UW1 — exact
effect unverified), endgame (q63, UW1), platform (q3 UW1+UW2, q4 "uw1
alternate behaviour" — exact motion unverified), quake (q60/61/62, UW1 —
"not actually used in game but present in game code"), trespass (q5 — name
only).

`a_hack_trap_*` (29): blyskup (q25 — name only), castleschedule (q36 — name
only), changegoal (q62, UW2 — effectiveness unverified per comment),
changegoaltarget (q43, UW2-only — name only), classitem (q10, UW2-only), coward (q30,
pits of carnage — name only), floorcollapse (q17, UW2-only), forcefield
(q11, UW2-only), gemrotate (q54, UW2-only — name only), gemteleport (q55,
UW2-only — exact effect unverified), godry (q34 — name only), graffiti (q24,
UW2 — name only), oscillator (q12, UW2-only), owner (q23/q28, UW2-only),
platformreset (q19 — scintillus academy 7), qbert (q32 — name only), quality
(q27 — change quality), rechargelightsphere (q35, UW2-only), recycler (q33,
UW2-only), resetzpos (q21/22 — change object zpos, two modes),
switchflicker (q29 — exact effect unverified), teleport_pitwarriors (q31 —
MovePitWarriors), terraformplatforms (q20 — rising platforms),
texturecycle (q14, UW2-only), toggleforcefield (q26, UW2-only),
transformpotion (q38, UW2-only — exact effect unverified), usebutton (q18 —
name only), vending (q40/41/42, UW2), visibility (q39, UW2-only — name only).

## 3. Trigger-type codes (`triggerobjectdat.cs`, table offset 0xd92)

MOVE 0, STEP_ON 1, PICKUP 2, (3 unused), USE 4, LOOK 5, ENTER 6 / UNLOCK_UW1
6 (shared), PRESSURE 7 / OPEN_UW1 7 (shared), OPEN_UW2 8, CLOSE 9, TIMER 10,
UNLOCK_UW2 11, SCHEDULED 12, (13 unused), EXIT 14, PRESSURE_RELEASE 15;
ALL -1 is the chain sentinel. UW1-vs-UW2 splits resolve at runtime
(`UNLOCK_TRIGGER_TYPE` / `OPEN_TRIGGER_TYPE`). Lookup: `triggertype(item_id)`
reads `buffer[offset + (item_id & 0xf)]`.

## 4. SCD.ARK / xclock note (UW2-only, out of scope)

The UW2 scheduled-event engine (`src/scd/`, driven off xclocks; SCD.ARK is
UW2-only) is never an extraction source. UW1 quest-variable/xclock/moonstone/
world-visited state lives in `playerdatquest.cs` semantics, which area 11 of
the coverage plan carries.

## 5. OpenUnderground behavior evidence

- Blocked-door reopen (level-4 tomb room): original doors reopen when blocked
  by body, lever action, or sleeping spell; a closed-on-occupant door with
  controls outside is a softlock (donor `CHANGELOG.md` line 39).
- Lever/wall mismatch (level-3 secret door): wall controls are wall-face
  addressed, not tile addressed; same-tile door faces must not capture them
  (donor `CHANGELOG.md` line 121).
- Jeweled sword in lava (level 6): placement validation at import — spawned
  objects must not land in damaging terrain (donor `CHANGELOG.md` line 81).

## 6. Grouping notes (inventory lens, not tasks)

- By trigger: tile-presence (MOVE/STEP_ON/ENTER/EXIT), verb-on-object
  (USE/LOOK/PICKUP), door/container (OPEN/CLOSE/UNLOCK), held-weight
  (PRESSURE/PRESSURE_RELEASE), clock-driven (TIMER/SCHEDULED — UW2, excluded).
- By effect: harm, relocation, world-change, spawn/remove, gating/chain,
  message/special, UW2-only world-script hooks.
- Read order for tasking: dispatch + chain rule (§1), trigger codes (§3),
  UW1-reachable traps first, then door/pressure/lever evidence (§5), then the
  UW2-only remainder as documentation only.
