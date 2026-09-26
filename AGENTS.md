# Rusty Underworld / AbyssRpg product guidance

## Direction and authority

Rusty Underworld is the reference repository and proving product for **AbyssRpg**:
an opinionated construction kit and reference host for dungeon-centric,
real-time, first-person systemic RPGs in the Ultima Underworld tradition. The
dungeon is the durable center of gravity: one continuous, tile-mapped underworld
whose levels, objects, inhabitants, and schedules persist around a single
avatar. Combat, magic, conversation, and survival exist to develop that
inhabitation and move it through the dungeon, rather than the dungeon existing
as a backdrop for a storyline.

The durable formula is:

> **Engine guarantees. Kit shapes. Ruleset decides. Bundle assembles. Host launches.**

Ultima Underworld: The Stygian Abyss is the first compiled ruleset, content
source, and game-bundle family, and the only emulated game. It is not implicit
AbyssRpg architecture. Ultima Underworld II shares its engine and remains useful
donor context for formats and divergences; it is not a target, and no code path
may quietly depend on its data.

- Underworld checkout: `/home/dev/rusty-underworld`
- paired Engine checkout: `/home/dev/rusty-engine`
- Underworld Den project: `rusty-underworld`
- primary donor reference (UW1+UW2 engine recreation, Godot 4 + C#):
  `/home/research/old-games/UnderworldGodot`
- secondary donor reference (UW1 gameplay-accuracy recreation, Unity):
  `/home/research/old-games/OpenUnderground`
- sibling-kit reference (Engine-native mechanisms, one-time bootstrap source):
  `/home/dev/rusty-dagger` — see
  `docs/research/rusty-dagger-kit-survey.md` for the copy map; copy once,
  rename, and customize with no provenance tracking
- donor surveys: `docs/research/underworldgodot-survey.md` and
  `docs/research/openunderground-survey.md`
- experience outline with manual citations: `docs/research/uu1-manual-outline.md`
- extracted data inventory with donor citations:
  `docs/research/uu1-data-inventory.md`
- design shape: `docs/gameplay-design.md` and `docs/code-organization.md`
- repository shape, current state, and how to develop: `README.md`

Before substantial work, resolve the current Den task and project guidance. Den
owns live task status and dependencies. If it is unreachable, report the failed
read; do not invent task records or infer dependency completion from source or
Git. Continue work whose scope and authority are already established, pausing
only decisions or actions that depend on unavailable Den information.

## Fidelity: similar, not a remake

The product is a **semi-equivalent recreation of Ultima Underworld 1** — not a
remake and not a compatibility project. The target is that a player who knows
the original recognizes its shape: the Abyss, the avatar, the runic magic, the
conversations, the object-dense simulation, the survival pressure, and the way a
session unfolds. Exact numbers, byte-level formats, and original file
compatibility are not goals.

- **Match the structure.** Single-avatar first-person exploration of a
  persistent multi-level dungeon; charge-based melee and missile combat; runic
  spellcasting from collected runes; conversation with NPC societies; traps,
  triggers, and physics objects; hunger, fatigue, sleep, and light; automap and
  quest variables.
- **Approximate the tuning.** Formulas, stat tables, and object and spell values
  may be adopted from extracted data where that is cheap, retuned, or
  simplified. A task states which values are faithful and which are ours; never
  claim fidelity that was not checked.
- **Out of scope.** Reading or writing original save games, loading the original
  executables, byte-exact tile geometry, and any promise that original mods,
  trainers, or editors keep working.

Donor and extracted data inform the design; they do not bind it. Where a task's
required behavior and the original game disagree, the task and the design
documents decide, and the difference is recorded rather than silently rounded.

## Current state

**The ordinary product entry composes a playable slice of the imported game.**
Launch resolves the bundle, admits an imported level with its geometry, collision,
placements and inhabitants, and runs one admitted update: first-person movement,
charge-based melee against admitted creatures, the use channel (doors,
conversation and barter, looting, taking), runic casting from collected runes,
survival, save and load, and the projection the companion UI renders.

Status is not repository prose. What each system reaches, and what it does not, is
measured in the Den document `coverage-audit` (project `rusty-underworld`), which
also lists the behaviors that exist as policy without a caller. Den owns live
status, tasks and evidence; read it before claiming a system runs.
`scripts/verify.sh` checks the Engine pair, the UI build and DOM suite, every
project's semantic suites, the architecture laws, and CoreCLR staging; it does not
prove GPU output, which `docs/gpu-playtesting.md` and the Den evidence record
cover.

## Current product graph

| Owner | Responsibility |
| --- | --- |
| `AbyssRpg.Kit` | Reusable and reasonably uncertain dungeon-RPG mechanisms: typed IDs, compiled ruleset/session contracts, bundle/content-pack/tuning resolution, avatar state, attributes, skills, runic-magic casting workflows, conditions and recovery, progression bookkeeping, attack execution with charge timing, targeting, NPC presence and AI coordination, corpse and loot machinery, containers and doors, object physics coordination, conversation state, barter, traps and triggers, automap and quest-variable state, world and spatial session stepping, structured UI values. It is not a universal RPG framework. |
| `AbyssRpg.Rulesets.UltimaUnderworld` | Ultima Underworld identities, attributes, skills, rune and spell definitions, object and critter definitions, combat and charge formulas, spell-effect and cost policy, time and schedule policy, conversation and barter policy, trap and lock policy, content interpretation, presentation meaning, save meaning, and session composition. |
| `AbyssRpg.Host` | Product lifecycle, explicit built-in ruleset/bundle selection, product defaults, and the one ordinary product entry. It may select Ultima Underworld; it never interprets Ultima Underworld rules or reads original game data. |
| `UltimaUnderworld.Import` | Offline knowledge of the original game's data files and of the donors that document them: source formats, conversion quirks, provenance, normalization into packs, and differential validation against the donor recreations. Not a runtime dependency. |
| `UltimaUnderworld.Import.Tool` | The operator-facing command line that drives the importer and writes normalized packs. |
| Content packs | Authored avatar options, skills, runes, spells, objects, critters, conversations, traps, levels, placements, assets, and scenario state interpreted by a ruleset. |
| TypeScript UI | Thin DOM presentation of Engine-delivered projections and semantic actions. It owns neither gameplay state nor game-world rendering. |

Rulesets are **compiled** into the SDK-generated product composition. Content
packs, typed tuning profiles, and game bundles are **loaded**. Adding code-bearing
ruleset semantics requires a product rebuild; changing valid content or tuning
does not. Do not add runtime assembly loading, reflection discovery,
`Assembly.Load`, ambient `Resolve<T>()` lookup, generic command buses, a new
gameplay DSL, or a universal plug-in ABI. Named, explicitly composed Kit services
and typed RuleEvents are encouraged where they make gameplay ownership and
contribution discoverable.

The concrete project graph, and the seam between kit and ruleset, are planning
decisions. `src/README.md` and the per-project READMEs record the current
intention.

## Kit, Ultima Underworld, and tuning rules

Reusable mechanisms begin in `AbyssRpg.Kit`; when placement is genuinely
uncertain, prefer Kit and keep concrete Ultima Underworld policy behind
ruleset-owned definitions and configuration. Do not make Kit universal, and do
not move ruleset vocabulary into it merely by renaming it.

`AbyssRpg.Kit` must not contain Ultima Underworld vocabulary. The forbidden set
includes the game and ruleset names (`UltimaUnderworld`, `Ultima`, `Underworld`,
`Stygian`, `Abyss` as a place name, `UW1`, `UW2`, `UU1`), world and level names from the
game, its attribute/skill/rune/spell/object/critter names, donor project names
(`UnderworldGodot`, `OpenUnderground`, `UnityUnderground`), and source file
names (`.ARK`, `.GR`, `.BYT`, `.TR`, `.PAK`, `.DAT` basenames such as `LEV`,
`CNV`, `OBJECTS`, `STRINGS`, `XFER`, `TERRAIN`, `COMOBJ`, `WEAPONS`, `CRIT`,
`CUTS`, `.VOC`, `.N00`). An architecture suite enforces this list once the
projects exist; until then the rule is a review obligation, not a checked one.

Ultima Underworld assumptions are legal only in the ruleset, Ultima Underworld
content packs, Ultima Underworld presentation, and `UltimaUnderworld.Import`. The
Host may select a built-in Ultima Underworld ruleset and bundle only at its
explicit catalog/default composition seam.

**The source game and its provenance are explicit.** The ruleset targets one game
— Ultima Underworld: The Stygian Abyss — and imported packs record which release
and build the data came from, so a pack cannot silently mix UW1 and UW2. UW2
appears only as donor documentation (a format variant, a struct difference, a
divergence note) and never as a supported target. Where a divergence matters to
imported data, record it with the donor path that documents it rather than
guessing. The `UW2/` tree inside the ISO is never an extraction source.

Give each value one honest home:

- Adjustable ruleset values use discoverable, validated, typed tuning handles.
- Avatar, skill, rune, spell, object, critter, conversation, trap, and level
  values belong in content packs.
- Algorithmic invariants stay beside the owning algorithm.
- Source-format quirks stay in `UltimaUnderworld.Import`.
- Product default selection stays in the Host.

Do not solve this with magic numbers hidden in call sites or a const field for
every authored value. Keep compact structural constants local and promote a value
only when it is genuinely adjustable or authored data. The original game keeps
its world in tile-mapped archive records decoded by the donors; that is a donor
fact, not an architecture. Our ruleset owns policy, content packs carry tables,
and source-file layout does not leak into runtime types.

## Gameplay composition direction

Read [`docs/gameplay-design.md`](docs/gameplay-design.md) for the shape being
expressed — the loop, each system's shape with a fidelity verdict, and the
decisions that are expensive to reverse — and
[`docs/code-organization.md`](docs/code-organization.md) for the owner map: which
Kit owner holds which state, what the ruleset supplies, where content and imports
land, and what modes the session has. Both are design intent: what the product is
for and who owns which behavior. They bind new work, and changing a decision they
pin is a deliberate re-plan, not an implementation detail.

Compose Engine `Actor` in Kit/ruleset facades with named properties over the
actual attached components. Explicit factories construct entities; wrapping an
entity never silently creates components. Keep runtime entity identity, kind or
origin type identity, and durable product identity distinct. No reflection
scanning, no duplicate actor graph, no Unity- or Godot-style cache/rebinding
machinery.

Use direct methods for simple reads and actions, typed RuleEvents for interactions
with real participant contributions, and typed notifications for completed
changes. Explicitly compose base rules and contributors. Application rules may
mutate through canonical owners; ordinary gameplay does not require
proposal/acceptance, snapshots, receipts, revision guards, or rollback. Optional
diagnostics must not become mandatory replay or audit work.

Combat is **one real-time state over the live dungeon**: charge builds while held
and resolves on release; there is no second mode, no turn queue, and no separate
battle scene. Preserve that shape — one session, one dungeon clock, one admitted
update — and do not grow a second scheduler.

Engine `ProductStateStore` stores current bytes without product-schema policy;
`JsonProductStateCodec` accepts source-generated `JsonTypeInfo` for AOT-safe JSON.
Capture meaningful state at explicit save boundaries. Only the current product
schema exists during development: no versions, migration branches, historical
readers, or compatibility fingerprints. The original game's save files are out of
scope — the product never reads or writes them, and no knowledge of that binary
layout belongs in runtime types.

## Engine boundary

> The product decides. The Engine guarantees.

The product owns application/gameplay logic, authoritative state, entities,
catalogs, content meaning, policy, and ordering within each Engine-admitted
update. Engine owns reusable host lifecycle and admission, input, rendering and
resources, spatial mechanisms, and published service families.

Use direct, safe, named C# Engine APIs from the installed `Rusty.Engine` package.
The SDK generates CoreCLR and NativeAOT composition beneath ignored `obj` output.
Reverify the actual packaged contract when using a capability; a capability list
in a document is boundary routing, not an API catalog.

Do not write downstream Rust or move product logic into Rust. Ordinary safe
product code must not use `unsafe`, pointers, `Native*`, `GCHandle`, raw statuses,
or handwritten native declarations. Generated `obj/` sources are ignored output:
never edit or commit them.

If a required behavior is absent from the safe API: name the behavior and the
Engine owner, and confirm no safe wrapper already exposes it. If the owning Engine
change is already authorized, implement it upstream and continue the dependent
work. Otherwise file or link one narrow purpose-neutral `rusty-engine` request
when authorized and report the blocked behavior at that boundary. Do not
reimplement Engine machinery in C#, TypeScript, or downstream Rust, and do not
substitute a fake proof path or parallel host.

## Update, donors, and evidence

There is one Engine-admitted update. Host and ruleset code may use the optional
`Rusty.Engine.Application` phases or implement `IEngineProduct.Update` directly;
it must not create a second loop, clock, timer, thread, browser authority,
parallel ECS, scheduler, or renderer. Product game-time advancement inside
admitted updates is allowed and required by this game family (schedules, hunger,
regeneration, effect deadlines); it does not establish an independent clock.

The original game and the donor checkouts are behavior, format, and rules
references — not code-style or architecture templates. Each carries its own
licensing and derivation history, so:

- **UnderworldGodot** (see its `LICENSE.md`; a Godot-based engine recreation
  covering UW1 and UW2) is the primary behavior and format reference. Read it to
  settle what the game does; do not translate its source into C#, and do not
  copy its code. Its Godot scene, node, and shader topology stays behind.
- **OpenUnderground** (Unity-based UW1 recreation; note its removed store-bought
  assets and LFS-hosted textures) is the secondary gameplay-accuracy reference.
  Read it for interaction, combat-feel, and presentation decisions; do not port
  its MonoBehaviours, prefabs, scenes, or Resources loading, and copy nothing
  from it.
- Original game data is **operator-supplied** (the `game.gog` ISO). Never commit
  it, and never copy converted game data out of a donor.

A claim about game behavior, formulas, tables, or formats is checked against a
donor file path or a documented table, not recalled from memory. Record the path
with the claim, as the donor surveys do. Where donors disagree or a divergence is
unverified, say so rather than picking the convenient answer.

Coverage planning — what behavior is in scope for the emulated game, in what
order, and which donor artifact documents it — lives in Den (project
`rusty-underworld`: the `coverage-audit` document and the campaign epics built
from it). The repository keeps durable reference only: the donor surveys and data
inventories under `docs/research/` and `docs/coverage/`, which state what the
donors and the shipped data do, never what this repository has finished.

## Coverage execution and drift

Incremental implementation is fine, but a proof-only slice or demonstration is not
a completed task. Do not leave no-op branches, hardcoded examples, parallel paths,
or partial adapters to satisfy a demonstration. Include the task's real callers,
state changes, content, and save/UI interoperability where applicable.

Before creating a mechanism, check both the current Engine surface and existing
repository owners for reuse or extension. Narrow drift reviews separately check
upstream reinvention, local duplication, ownership and tuning leakage, and
behavior completeness. Review the task's change and its relevant callers; report
concrete source-backed defects, not new scope or stylistic preferences. The root
reconciles findings. The lane model lives in `docs/agent-review/`.

Use focused compilation and semantic checks appropriate to the change. Broader
interactive evaluation can inform later reconciliation without becoming each
task's definition of done. Stop an upstream-blocked task honestly and continue
independent ready work; never invent a substitute to unblock the queue.

## Documentation and check posture

Keep durable repository documents free of commit revisions and pinned versions: a
stale pin in prose invites a later agent to roll the code back to match the
document, and the document is not the owner of that identity. The Engine pair
identity lives in `Directory.Build.props`, where scripts verify it; move it with
`scripts/update-engine-pin.sh` and never hand-edit a version into prose.

A hard failure must name the loss it prevents. Where the consequence is
recoverable, warn and report the actual observed value instead. Keep hard stops
for data loss, an ownership-boundary violation, or a silently wrong artifact. A
concrete collision is a real hard stop; a merely potential one is not.

Rescoping during implementation is expected, but the deferred requirement must
move to a concrete receiving task — that task's required behavior and
verification, or a new follow-up task — and the source task's record points at
it. A note that a task became narrower does not hold the requirement: a concern
is only passed on while some task is still carrying it.

## Git

Commit and push the work of a turn or task directly. This is a solo repository
used for backup and change tracking, so there is no push-approval ceremony and no
blast-radius review. Keep each commit scoped to the work that produced it, and
leave unrelated dirty files alone. `AGENTS.md` is intentionally tracked despite
its `.gitignore` rule; stage it with `git add -f AGENTS.md`.

## Task workflow (Den + commits)

Den (`rusty-underworld`) is the durable status record; Git is the evidence.
For every task, follow this loop:

1. **Start:** move the Den task to `in_progress` when work begins.
2. **Commit per task:** commit and push the task's work (plus its review
   reconciliations) with the task ID in the message, e.g.
   `UW-T06: ...`. One task, one commit series — never batch unrelated tasks.
3. **Attach the commit:** when the task reaches `done` (or `review`/`blocked`),
   append `Delivered in: <hash> (...)` to the task description, naming what
   each commit carried, plus any explicit remainder and its receiving task.
   A task without a commit hash is not done.
4. **Status updates:** post short evidence messages on the task for review
   verdicts, scope decisions, and deferred remainders, so later searches
   recover *why*, not just *what*.

Keep descriptions free of pinned versions and commit revisions *of other
things*; the delivery hashes above are the exception — they are the task's
own audit trail, not prose pins.
