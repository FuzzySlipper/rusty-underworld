# Underworld coverage plan

Status: planning baseline. This defines scope, ownership, and work ordering;
it is not an executable task list or a claim of completed coverage. The
[task-preparation packet](underworld-task-preparation.md) supplies the feature
dispositions, behavior inventories, content scope, and dependency inputs for
expanding these families into explicit tasks. The current user request and
owning task override older guidance.

## Purpose and references

Bring rusty-underworld to mostly full Ultima Underworld 1 behavior and content
coverage through deliberate, primitive-first implementation. The original game
supplies the product requirements; UnderworldGodot supplies a substantial
reverse-engineered behavioral reference and OpenUnderground a second,
gameplay-accuracy reference. This is an experiment in detailed planning and
long autonomous implementation runs, with narrow drift checks and later
reconciliation. It does not use vertical slices, proof threads, or interactive
demonstrations as the organizing unit of delivery.

- [Feature map](underworld-feature-map.md): donor structures and point-in-time
  implementation inventory. Its rows are not automatically implementation tasks.
- [AGENTS.md](../AGENTS.md): active ownership and execution rules.
- [Gameplay design](gameplay-design.md) and
  [code organization](code-organization.md): the shape being expressed and
  where it belongs.
- Donor references: the `/home/research/old-games/UnderworldGodot` checkout
  (MIT; UW1+UW2 engine recreation in Godot 4 + C#) and the
  `/home/research/old-games/OpenUnderground` checkout (no license file; UW1
  gameplay-accuracy recreation in Unity). Neither is a code donor. Their
  surveyed revisions are logged with the coverage tasks rather than pinned
  here. Recheck relevant source when planning each concrete task; a survey is
  not a permanent API or coverage guarantee.

## Behavioral fidelity and architectural adaptation

Preserve UW1 rules, formulas, state transitions, content meaning, and
interactions. The donors model these behaviors; they are not a coding style to
emulate. Their engine architectures (Godot nodes/shaders/coroutines,
Unity MonoBehaviours/prefabs/scenes) reflect their platforms. Use ordinary
explicit C# composition and existing Rusty owners here.

When original behavior and the donors differ, record the actual difference and
selected behavior in the owning task. Do not silently turn a donor enhancement,
workaround, or incidental bug into a classic requirement. Unresolved
differences remain explicit planning decisions, not permission for a worker to
invent semantics.

| Donor area | Disposition for this campaign |
| --- | --- |
| Godot scene/node/shader/palette-shader topology, `godotscale` builders, `MusicStreamPlayer`/`AudioStreamWav` backends, `FontFile` fill, Peaky coroutine VM driver, collision meshes | Exclude the runtime topology. Decode offline into normalized Rusty-friendly formats; consume admitted packs at runtime; interpret conversation with a ruleset-owned VM, not a ported coroutine engine. |
| Unity MonoBehaviour/prefab/scene wiring, Resources loading, `GameDirectoryDialog` + `LooseData` cache, cursor/prompt art | Exclude the runtime topology and distribution/setup workflow. Decode offline; consume admitted packs at runtime. |
| Top-level bootstrap (`main.cs`, `Launch.tscn`, `World` scene, front-end/logos/credits flow) | Exclude scene wiring. Extend the existing Host/ruleset composition and Engine-admitted update. |
| Original executable model data (`UW.EXE` 3D models) | Mine geometry knowledge through the donor `modelloader`; never load the executable at runtime. |
| Unity/Godot serialization and original save-file read/write | Exclude. Persist product meaning through existing Rusty save ownership. Classic save-file compatibility is not a task requirement. |
| UI | Adapt information, choices, workflows, and semantic actions (panels, runebag, automap, conversation/barter, HUD) to thin DOM UI. Do not emulate pixel geometry, widget hierarchies, or component trees. Gameplay authority remains in C#. |
| XMI realtime synthesis (4 synth engines, ROM-gated) | Excluded. Contextual music and long-running loop playback from an ordinary imported audio file remain in scope. |
| UW2-only systems (SCD.ARK scheduled events, UW2 timer triggers, UW2 spell branches, UW2 projectile tables, UW2 automap block layout) | Not targets. They appear only as divergence notes that keep UW1 imports honest. |
| Other donor infrastructure, achievements, debug showcases, physics sims, editor helpers | Not automatically required by a survey row. Classify explicitly before scheduling; do not recreate it just to turn every row into "Covered." |

Excluding donor import/rendering code does not exclude tile maps, objects,
conversations, textures, sounds, or other game content. Excluding donor
orchestration does not exclude actual game behavior such as charge timing or
conversation consequences.

## Ownership and scheduling rules

**Engine guarantees. Kit shapes. Ruleset decides. Bundle assembles. Host launches.**

Kit owns reusable or placement-uncertain dungeon-RPG mechanisms over verified
Engine APIs. Ultima Underworld owns its identities, formulas, interpretation,
and policy; packs own authored records, and typed tuning owns adjustable
policy. Import owns source formats and normalization. Host owns
selection/lifecycle; TypeScript presents UI. Do not build a universal RPG
framework, reflective registry, generic command bus, new gameplay DSL, second
simulation loop, or duplicate Engine mechanism.

The numbered areas below give preferred order, not twelve all-or-nothing
barriers. Tasks depend on concrete capabilities. Import/catalog work can
proceed alongside runtime work, and an Engine blocker only stops its dependent
tasks. Reuse existing coverage after inspecting it; do not rewrite a subsystem
because it appears below.

Shared identity, state ownership, time advancement, and content contracts
deserve precise early planning. Local organization and refactoring can be
reconciled in larger batches. Do not defer interoperability decisions that
many tasks depend on.

## 1. Coverage target and task inventory

Preparation output:
[task-preparation packet](underworld-task-preparation.md), including the
complete feature-map disposition ledger and specialist behavior/content
inventories. Use those stable IDs when creating tasks; maintain their mapping
to task IDs rather than restarting the survey. Exact formulas and API
contracts are resolved while drafting the affected tasks, not through another
global planning gate.

Breakdown:

- Assign stable feature identifiers and map each to source behavior, current
  implementation, planned family, and disposition: implement, extend/reuse,
  adapt, exclude, or unresolved.
- Separate behavior coverage from authored-content coverage. Inventory the
  supported levels, object catalog, rune/spell corpus, conversation corpus,
  critters, and media; the feature map intentionally does not enumerate these
  payloads.
- Record exact exclusions above, selected donor differences, and explicit
  remaining questions. Deduplicate overlapping map rows rather than scheduling
  them twice.
- For partial coverage, enumerate missing behavior and current limitations
  before defining tasks. "Covered" in a structural survey is not full semantic
  parity.
- Expand families into bounded tasks with concrete prerequisites and named
  owners.

Ownership: planning document and task list; no new runtime framework. This
area organizes the other eleven and continues as discoveries refine the
inventory.

## 2. Durable identity, state, and game time

Breakdown:

- Stable level/tile/object identities and dynamic avatar/object/NPC
  identities, distinguished from transient Engine resource handles.
- Loaded and unloaded level changes, object creation/removal, ownership of
  save records, and reconstruction after level transitions or load.
- One product game-time model advanced inside admitted updates, with explicit
  elapsed-time operations for rest and sleep. Specify ordering and catch-up
  behavior for effect deadlines, hunger/fatigue ticks, and respawn/schedule
  queries; no wall-clock loop.
- Quest-variable and game-variable storage with conversation read/write
  semantics; xclock increment rules (UW1 subset only — UW2 timers excluded).

Ownership: Kit reusable identity/state coordination; Ultima Underworld
time/variable policy and save meaning; Host existing envelope/lifecycle;
Engine persistence. Requires: existing composition. Enables object
persistence, timed effects, survival, and conversations. Each later family
extends its own save meaning as it lands.

## 3. Normalized catalogs, source data, and text

Breakdown:

- Attributes, the 20 skills with governing attributes, classes, the 24 runes
  and 40 spells in 8 circles, objects, critters, traps, conditions.
- String blocks, names, books and readables, and source references needed for
  later conversation and lore work.
- Stable cross-record references and normalized pack publication; distinguish
  authored values from algorithmic constants and adjustable policy.
- Preserve source identity/provenance and useful differential fixtures. Expand
  each corpus alongside its consumers; do not require all assets imported
  first.

Ownership: Import decoders/normalizers, content packs, Ultima Underworld
interpretation; reuse Kit composition. Requires identity conventions from
area 2 for relevant records. No runtime ISO setup, source-reader singleton,
or donor scene bootstrap.

## 4. General dungeon and persistent interactions

Breakdown:

- Multiple tile-mapped levels, entry/dream spaces, stairs/pits/doors/gates,
  and Engine-backed admission/unloading/presentation.
- Transition context, return positions, persistent object state, and dungeon
  changes across unloading/reloading.
- Doors, locks, containers, secret doors, trap/trigger wiring, moving
  platforms, and discovery data, with source semantics normalized offline.
- Automap anchors with stable identities (map coverage blocks, notes), even
  before knowledge features consume them. Do not bind quest variables to
  transient renderer objects.
- Verify Engine capabilities for tile geometry, level origin, spatial changes,
  and presentation as their concrete tasks are planned; route genuine gaps
  upstream.

Ownership: Kit level/lifetime/interaction coordination; Ultima Underworld
layout, placement, trap/trigger, and persistence policy; Import source
records; Engine mechanisms. Requires: relevant area 2 state and area 3
records. Enables dungeon-aware gameplay.

## 5. Avatar and object construction

Breakdown:

- Complete avatar creation from class/attributes/skills with validation,
  per-level advancement, and creation choices that gate starting capability.
- Object instances, variants, quality/condition, identification state,
  equipment restrictions, weight/encumbrance, currency, food, lights, and
  complete ordinary loot categories.
- Expose usable operations for later combat, barter, effects, and
  conversations; persist dynamic state and preserve object identity through
  transfers and container nesting.
- Character creation, sheet, inventory/paperdoll, and equipment semantic UI
  actions and projections accompany their owning behavior tasks, using the
  existing UI path.

Ownership: Kit Engine-backed avatar/inventory coordination; Ultima Underworld
rules, definitions, creation policy, and presentation meaning; packs authored
records. Requires: areas 2–4. Effect-driven modification builds further in
area 7.

## 6. Ordinary physical gameplay

Breakdown:

- Look/get/use/use-on/talk verbs; taking/dropping/throwing/combining;
  lockpicking, door-bashing, theft and its consequences; concrete outcomes
  available to later attitude policy.
- Walk/run/jump/swim/fly movement with explicit eligibility, costs, and
  consequences over Engine spatial APIs (drown timer, fall injury, lava
  damage, encumbrance effects).
- Full melee factors: charge buildup and release, swing kinds by press
  position, accuracy and damage resolution, weapon readiness, equipment wear,
  missile dispatch with ammo, first-person weapon presentation, death and
  corpse flow.
- Critter construction, ordinary senses, pursuit/attack/retreat policy,
  sounds and feedback using existing avatar, targeting, navigation, and
  presentation owners.

Ownership: Kit reusable coordination, Ultima Underworld physical rules and AI
policy, Engine spatial/perception/presentation. Requires relevant areas 4–5.
Later magic extends these same operations; do not create separate magical
movers or hit paths.

## 7. Effects and casting foundations

Breakdown:

- Verify and use existing Engine stat contributions and effect mechanisms;
  coordinate source identity, modifier removal, conditions, duration and
  stacking (max 3 concurrent effects; stable/unstable time).
- UW1 spell keys (major/minor classes), rune-shelf state, caster/target
  identity, cast gates (Mana = 3×Circle, level/2 rule, recast delay), success
  rolls, backfire, item-borne casting (scrolls, wands, potions, worn
  enchantments with Lore identification).
- Persist active effects and define normal-time versus rest-advanced behavior,
  periodic application, cure/expiry (including over-sleep fading), and
  restoration without duplicate application.
- Ordinary damage/heal/light/protection effects first, then poison/disease/
  drunk/drugged mechanics using the common time/lifecycle path.

Ownership: Kit reusable Engine-backed coordination; compiled Ultima
Underworld effect semantics and loaded rune/spell records. Requires areas 2,
3, 5 and relevant targeting operations from 6. No reflective broker or copied
donor spell manager.

## 8. Inhabited dungeon, rest, and maps

Breakdown:

- Rest/sleep advancing game time with restoration, hunger interaction,
  interruption/ambush risk, dream logic, and light burn-down.
- Darkness/light ranges, eye-glow limits, ambient audio, and level danger
  pacing.
- NPC presence, identities, schedules, attitudes, spawning/removal, senses,
  and interactions with dungeon persistence.
- Interior automap with coverage blocks, player notes (place/edit/delete,
  level pages), compass and status readouts, rendered through the appropriate
  Engine/UI surface.

Ownership: Kit reusable dungeon/avatar coordination; Ultima Underworld
simulation rules; Engine rendering/spatial/audio. Requires relevant areas
2–7, not every effect. NPC identity and conversation records can start
earlier than schedule/movement depth.

## 9. Society, dialogue, and barter

Breakdown:

- Attitudes, likes/dislikes, intimidation, demands and their consequences.
- Conversation VM interpretation: 42 opcodes, imported-function semantics
  (ask/menus, inventory functions, quest vars, teleports, attitude changes),
  dialogue flow, free-text prompts, farewell lines.
- Barter with real inventories: offers, trade dots, appraisal scaled by
  Appraise, trader patience/profit/valuations, gifts, showing items.
- Repair at anvils (difficulty, skill, ruin risk, time cost) and via NPCs;
  thin UI for these operations and persisted service state.

Ownership: Kit reusable conversation-state/transaction coordination where
appropriate; Ultima Underworld dialogue semantics, text interpretation, and
presentation; packs conversation/text records. Requires relevant identity,
avatar/object, dungeon, and time operations.

## 10. Magic breadth and special states

Breakdown:

- Remaining spell families grouped by prerequisites, including movement
  (fly, teleport gates), detection, concealment, charming, summoning,
  area effects with friendly-fire consequences.
- Enchanted equipment, scrolls, potions, and rune-ward mechanics; reuse
  equipment/strike/use lifecycle rather than introducing parallel dispatch.
- Shrines and mantras (group mantras SUMM RA / MU AHM / OM CAH plus hidden
  single-skill dungeon mantras) with explicit advancement effects.
- Silver seed/tree respawn-anchor mechanics with explicit interactions with
  death, saves, and world state.
- Each family includes all assigned magnitude/duration/chance and target
  behavior, persistence, cleanup, and caller integration, not one
  demonstrable example.

Ownership: Ultima Underworld semantics/content over Kit and Engine
capabilities. Requires area 7 plus each effect's actual domain operation.
Some transformation behavior needs conversation invocation from area 9: do
not make all of 10 a prerequisite for 9.

## 11. Quest variables and scripted machinery

Breakdown:

- Normalize classic quest-variable/game-variable/bglobal semantics offline
  into Rusty-friendly records; compiled Ultima Underworld code interprets
  supported operations. No new generic gameplay language and no ported
  scheduled-event engine (UW2 SCD.ARK stays excluded).
- Persistent variable instances, xclock increments, moonstone and
  world-visited flags, dream/endgame triggers.
- Bind scripted effects to stable dungeon identities and normalized anchors;
  support object placement/removal and attitude changes with lifecycle and
  cleanup semantics.
- Import and enumerate supported trap/trigger operations, unresolved opcodes
  or functions, and deliberately excluded behavior. Unsupported work remains
  explicit, never a no-op.

Ownership: Ultima Underworld variable/script semantics, Import source
conversion, packs authored variables; Kit reusable identity/facts/state
coordination only. Foundations can begin after areas 2–3. Dungeon binding
needs 4/8; individual operations depend on 5–10 selectively. This breaks the
conversation/effect cycle without stubs.

## 12. Content completion and reconciliation

Breakdown:

- Complete the supported levels, catalogs, books/media, conversations, and
  special spaces. Content work progresses throughout earlier areas; this
  closes the inventory instead of starting bulk import at the end.
- Finish remaining UI workflows, controls/settings, contextual sound/music,
  and presentation details selected by the coverage target.
- Schedule reconciliation after substantial implementation batches and at
  campaign completion: repair cross-system mismatches, consolidate accidental
  duplicates, refactor awkward ownership, and update honest
  coverage/disposition records.
- Broader play sessions can discover integration defects here. They do not
  replace behavioral requirements or retroactively turn every task into a
  demo gate.

Ownership: existing domain owners; no separate completion runtime. Requires
each content family's actual capabilities and records remaining gaps
explicitly.

### Music and long-running audio

No XMI decoder, multi-synth engine, ROM-gated playback requirement, or donor
music-manager port. Plan ordinary music selection/looping over the verified
Engine Audio API (exploring/combat/warning/victory/automap slots per the
OpenUnderground track-slot restoration). A user-supplied audio file under
`local/` can be an offline input, converted if necessary to a supported
admitted format; do not assume direct XMI support or bypass Engine content
admission with a browser audio player. Keep local media out of commits.

Separate the product music behavior task from a bounded long-duration Engine
audio exercise. That exercise should specify input, duration, loop/voice
lifecycle, stop/dispose behavior, and available observations of
errors/resource growth. Report the elapsed duration and observations honestly;
hearing one loop is not evidence of long-run stability. This targeted
experiment does not gate unrelated coverage tasks. No media file or audio run
is required to complete this planning document.

## Turning families into autonomous tasks

Each task should state:

1. Feature IDs and exact behavioral scope, donor files/symbols, selected
   semantics and explicit exclusions. Consult source rather than porting a
   class by name.
2. Existing Engine and local owners to reuse/extend, proposed mutable-state
   owner, and concrete required capabilities. Record why a new mechanism is
   necessary.
3. Inputs, outputs, state transitions, ordering, and relevant failure/cleanup
   rules.
4. Required interoperability: real callers, content references, save/restore
   and UI actions/projections where applicable. Identify separately scheduled
   consumers honestly; a primitive need not manufacture a demo consumer to be
   complete.
5. Hard dependencies, independent work, and exactly which behavior is blocked
   if a prerequisite is absent. Prefer ready tasks over queue-wide waiting.
6. Focused checks of semantic cases and interoperability, plus coverage
   updates. Tests should expose meaningful mistakes, not mirror
   implementation branches.

Prefer "implement paperdoll equip restrictions for the specified slots and
persist them through the existing object owner" to "deliver working
inventory." Do not mandate one class per task or scatter one coherent
operation across tiny tickets. A task implementing a prerequisite contract may
be complete before its later consumer exists; it must actually implement its
full assigned contract. A stub, unconditional success, hardcoded
demonstration, or unsupported no-op cannot count.

## Narrow drift review lanes

Use separately scoped reviewers when they add value. The lane definitions
below are available to the orchestrator; they do not require four reviewers
for every task or create a new approval system. Review a task's change against
its stated contract and relevant existing code, not the entire repository
each time.

The first two lanes run on every task; the remaining lanes are selected per
task to a total of two to four reviewers. The reusable reviewer packets live
in [`docs/agent-review/`](agent-review/README.md).

| Lane | One question | Required basis for an actionable finding |
| --- | --- | --- |
| Engine reuse (always on) | Does this change recreate a mechanism already safely available upstream? | Name the current safe API, local duplicate and concrete replacement/adoption path; distinguish product policy from Engine guarantees. |
| Existing product reuse (always on) | Does this change create a competing mechanism instead of extending this repository's existing owner? | Name both owners and their overlapping state/behavior, relevant callers, and consequence. A new file or similar name alone is not a defect. |
| Ownership and values | Does this change leak Ultima Underworld policy into Kit/Host, source quirks into runtime, or authored/tunable values into incidental code? | Identify the actual assumption/value, current and correct owner, and affected use. Do not demand a universal abstraction or a constant for every literal. |
| Behavior and interoperability | Does this implement the task's full specified behavior through the required shared operations? | Show a concrete missing branch, no-op, ignored input, disconnected caller, incompatible state contract, or donor-semantic mismatch. A passing demonstration does not close the finding. |

Findings should be short and source-backed, distinguish confirmed defects from
uncertainty, and stay within the lane. Do not introduce interactive gates,
broad redesigns, stylistic demands, or invented acceptance criteria. The root
consolidates overlap and judges whether to fix, adapt, or decline findings;
reviewer verdicts do not amend user intent. Deferrable cleanup is explicit,
not falsely marked complete.

## Donor dependency evidence behind this order

Consulted in the surveys (not copied):

- `src/World/tilemap.cs` + `automap.cs`: LEV.ARK block layout and automap
  block indices underpin level admission; dungeon identity and normalized
  anchors precede scripted operations.
- `src/conversation/conversationvm.cs` + `conversation_functions/`: quest
  variables, inventory functions, and teleports can be retained before an NPC
  is spawned; variable identity/lifecycle precede operation breadth.
- `src/player/playerdatclock.cs` + `playerdatloop.cs` + `playerdatquest.cs`:
  elapsed game time drives hunger, regeneration, and effect deadlines. They
  must share product time semantics.
- `src/magic/spellcasting*.cs`: major/minor dispatch and active-effect
  lifecycle precede bulk spells. Adopt behavior while adapting away from
  donor topology.
- Shrine mantras and seed/tree respawn cross-reference advancement, death,
  and saves; schedule their foundations before dependent families.

These are planning dependencies, not an instruction to copy the donor files.
