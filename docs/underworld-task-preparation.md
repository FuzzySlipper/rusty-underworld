# Underworld task-preparation packet

Prepared with the coverage plan. This is the decomposition input for turning
the plan into explicit work — not created tasks. It makes the target finite
enough to plan without requiring a full semantic audit of the game before
implementation. The original game remains the behavioral target, with explicit
Rusty architecture adaptations and recorded differences.

## Start here

| Document | What the task author gets |
| --- | --- |
| [Coverage plan](underworld-coverage-plan.md) | Twelve-area order, ownership, exclusions, execution and drift-review posture. |
| [Feature map](underworld-feature-map.md) | Donor structures with point-in-time coverage (all Absent; no code exists). |
| [Feature ledger](coverage/feature-ledger.md) | Stable F001–F071 plus corpus IDs (M/R/S/T/C) with dispositions. |
| [Magic inventory](coverage/magic-inventory.md) | The transcribed 40-spell grimoire, 24 runes, gates, dispatch mapping. |
| [Trap inventory](coverage/trap-inventory.md) | Dispatch tables, do/hack qualities, trigger codes, UW2 exclusions. |
| [Content scope](coverage/content-scope.md) | Source families, current anchors (none), authored-small target. |
| [Task index](coverage/task-index.md) | Proposed UW-T01…T38 slices with owners and prerequisites. |

Read the applicable rows and current source before drafting each task. Do not
turn each file, row, or ID into exactly one ticket. A coherent behavior can
involve several IDs; a broad ID can require several tasks with stable child
identifiers.

Preparation accounting: 71 feature-map rows, 40 spells, 24 runes, 20 skills,
16 vanilla trap entries + 9 minorclass-1 entries + 37 do/hack qualities, 42
conversation opcodes, ~57 conversation functions, 12 source families, and 38
proposed task slices. These counts include explicitly excluded donor-only
records and are not counts of required tasks, completed features, or usable
runtime records.

## Scope contract

Baseline target: the Avatar's dungeon, avatar development, physical/magic/
social systems, conversations and barter, survival, and the content needed by
those behaviors. Use the operator ISO and the surveyed donors as references.
The target is not only the first-slice level-1 pack.

Authored records are part of the backlog: importer support without published
and consumable records does not finish a content family. Conversely, not every
raw archive entry needs a new gameplay feature. Every discovered source record
gets an honest imported, required-pending, unused, duplicate, excluded, or
unresolved disposition when that family's importer/publication task enumerates
it.

Retain the coverage plan's explicit exclusions: donor scene/prefab/topology,
runtime ISO setup, donor orchestration and bootstrap, donor serialization and
original saves, widget implementation shapes, XMI synthesis, UW2-only systems
(SCD.ARK, timers, spell branches, projectile tables), and debug/achievement
helpers. Ordinary audio/music remains included, with its long-duration
exercise separate.

Do not silently add donor-only records, achievements, debug showcases, or
optional convenience behavior to the baseline. Keep candidate donor-only
records in the inventories so they can be assessed instead of disappearing.
Original-language text and adapted UI functionality are the initial baseline;
binary classic save compatibility is not assumed.

## Current-source notes (no code yet — reuse anchors are upstream)

These shape task drafting. Paths are repository-relative. The first
implementation tasks extend these anchors rather than inventing seams.

| Area / IDs | Existing anchor | Consequence for task drafting |
| --- | --- | --- |
| Composition, A1 | `docs/code-organization.md` §§1–5; Engine pair in `Directory.Build.props` | Reuse the layering; no new product manager, pack loader, or parallel host. |
| Identity/time, A2 | Kit owner map (`code-organization.md` §3: Session, Time, Persistence) | One identity map, one clock; UW1 xclock subset only. |
| Catalogs, A3 | `uu1-data-inventory.md`, donor loader survey | Import decoders first; runtime consumes packs, never ISO shapes. |
| Dungeon, A4 | `tilemap.cs` block evidence (survey §7); T-trap tables | Admission before operations; wall-face addressing from the start. |
| Avatar/objects, A5 | Manual creation/inventory rules (outline §§2, 6) | Creation is a real validated flow; paperdoll/slots as specified. |
| Combat, A6 | Manual charge rules (outline §5); OU accuracy evidence | One charge state; donor numbers adopted only where verified. |
| Magic, A7 | `magic-inventory.md` (grimoire + gates + dispatch) | Families over the same workflow; UW2 branches never. |
| Inhabited dungeon, A8 | Manual survival/map rules (outline §§9–10) | Rest advances the one clock; knowledge separate from world. |
| Dialogue, A9 | 42-opcode table + function survey | Ruleset-owned interpretation; no ported engine. |
| Magic breadth, A10 | Shrine/seed manual facts (outline §§10–11) | Foundations before dependents; conversation-invoked effects break the cycle. |
| Variables, A11 | `playerdatquest.cs` semantics (survey) | UW1 subset; SCD.ARK never. |
| Completion, A12 | Track-slot evidence (OU survey §3) | Ordinary Engine Audio; synth port never. |

## Turning slices into tasks

Each task states the six items in the coverage plan's "Turning families into
autonomous tasks" section. Prefer "implement paperdoll equip restrictions for
the specified slots and persist them through the existing object owner" to
"deliver working inventory." A task implementing a prerequisite contract may
be complete before its later consumer exists; it must actually implement its
full assigned contract. A stub, unconditional success, hardcoded
demonstration, or unsupported no-op cannot count.
