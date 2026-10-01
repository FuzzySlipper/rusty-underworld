# Rusty Underworld

Rusty Underworld is the reference repository and proving product for **AbyssRpg**.

AbyssRpg is an opinionated construction kit and reference host for
dungeon-centric, real-time, first-person systemic RPGs in the Ultima Underworld
tradition. The dungeon is the durable center of gravity: one continuous,
tile-mapped underworld whose levels, objects, inhabitants, and schedules persist
around a single avatar. Combat, magic, conversation, and survival are mechanisms
for inhabiting and unfolding that place, not a mandatory linear product spine.

Ultima Underworld: The Stygian Abyss is the first compiled ruleset,
compatibility corpus, content source, and game-bundle family. It is not the
implicit AbyssRpg architecture. Ultima Underworld II shares its engine and
remains donor context for formats and divergences; it is not a target.

The working formula is: **Engine guarantees. Kit shapes. Ruleset decides.
Bundle assembles. Host launches.**

> **The ordinary entry composes a playable slice of the imported game.** It
> resolves the shipped bundle, creates the compiled ruleset session over an
> operator-imported level, admits the level's geometry, collision, placements and
> inhabitants, and runs one admitted update: movement and look, charge-based melee,
> the use channel (doors, talking to a creature, looting, taking, picking up
> runestones), survival on the dungeon clock, and save and load, projected to the
> companion UI. One Engine scene snapshot draws admitted props and actors at
> their normalized live poses, including fallen creatures. Ordinary use opens
> door leaves for movement and closes them to block it again.
> Pit and teleport traps move the avatar between imported levels;
> stairs, answering a conversation, and casting from play are not reached yet
> (casting runs through the operator probe `abyss.cast`).
> What that slice reaches, and what it does not, is measured in the Den
> document `coverage-audit`; Den also holds task state and GPU evidence
> (`playtest-evidence-log`), and `docs/gpu-playtesting.md` is the operator setup.
> Saves retain the canonical inventory across travel and fresh loads, including
> the belongings of a fallen creature. A corpse remains its loot owner with
> its saved pose and health. Equipment, lock/key, conversation and progression
> persistence remain tied to their gameplay integrations tracked in Den.
> Start with `scripts/import-level.sh` before launching.

## Licence

The repository's own code, authored content and documents are MIT-licensed
([`LICENSE`](LICENSE)). [`NOTICE`](NOTICE) states what that does not cover: the
original game and its data, which are never committed, and the reference
projects, which keep their own licences.

## Ownership

- Rusty Engine guarantees reusable infrastructure and admitted update services.
- `AbyssRpg.Kit` defines the reusable dungeon-RPG composition grammar and the
  ordinary mechanisms needed to construct one.
- `AbyssRpg.Host` owns the product lifecycle, built-in ruleset registry, shipped
  bundles, launcher, defaults, and session selection.
- `AbyssRpg.Rulesets.UltimaUnderworld` owns all Ultima Underworld semantics,
  formulas, identities, runic-magic policy, conversation meaning, presentation
  meaning, and content interpretation.
- Content packs own authored definitions, assets, levels, placements,
  conversations, and scenario state.
- `UltimaUnderworld.Import` owns source-format knowledge for the original game's
  data files and for the donors that document them.
- `AbyssRpg.Host` is the ordinary product entry. The packaged SDK generates
  CoreCLR and NativeAOT composition beneath ignored `obj` output.

Code-bearing rulesets are compiled into the product. Content packs, validated
typed tuning profiles, and game bundles are loaded at runtime. Do not introduce
dynamic managed plug-in loading, reflection discovery, runtime C# compilation,
generic command buses, ambient dependency lookup, or a replacement gameplay DSL.

Reusable mechanisms, and mechanisms whose placement is genuinely uncertain, begin
in `AbyssRpg.Kit`. Ultima Underworld assumptions are forbidden there and
permitted only in the ruleset, its content packs, its presentation, and
`UltimaUnderworld.Import`; the Host may name a built-in ruleset only at its
explicit composition root.

Adjustable ruleset values belong in discoverable validated typed tuning handles;
authored values belong in content packs; algorithmic invariants stay beside their
algorithms; source-format quirks belong in the importer; and default bundle
selection belongs in the Host.

There is one Rusty Engine-admitted update. AbyssRpg does not create a parallel
loop, clock, timer, thread, browser authority, or renderer.

## The game family

Ultima Underworld: The Stygian Abyss is the game being recreated, and the only
target. Ultima Underworld II shares its engine and remains donor context for
formats and divergences; it is not a target, and no code path may quietly depend
on its data. What this repository knows about the game is recorded, with
citations, in [`docs/research/`](docs/research/):

- Single-avatar first-person play in one continuous tile-mapped dungeon —
  the Stygian Abyss, divided into stories-like levels (manual p. 19) — with
  free movement, jumping, and swimming rather than a party, an overworld, or
  separate battle screens.
- Real-time combat paced by an attack-charge buildup shown on the Power Gem
  (manual pp. 13, 22): holding prepares a swing whose kind follows press
  position, resolving on release; missile combat alongside it.
- Runic magic over 24 runes in 8 circles of 5 spells (manual pp. 26-29, 32):
  runes collected in the world, carried in a runebag, combined into spells at
  cast time against a mana reserve; scrolls, wands, and potions as item-borne
  casting alongside it.
- A conversation system with real NPC societies: full dialogue trees driven by a
  conversation VM, barter, and NPC AI with movement, pathfinding, and combat.
- Object-dense simulation: hundreds of interactable objects per level with
  physics, containers, weight and encumbrance, food, and light sources — plus a
  near-complete trap and trigger family, including records the original never uses.
- Survival pressure: hunger, fatigue, sleep and dreams, light and darkness.
- Knowledge play: an automap the player annotates, a compass, and quest state
  carried in game variables the scripts read and write.

The donors are the engine recreation at
`/home/research/old-games/UnderworldGodot` (Godot 4 + C#, UW1 and UW2) and the
gameplay-accuracy recreation at `/home/research/old-games/OpenUnderground`
(Unity 6000, UW1). Their licenses differ and neither of them is a code donor —
read the donor posture in [`AGENTS.md`](AGENTS.md) before using either of them.
The sibling kit at `/home/dev/rusty-dagger` is a third, special source: its
`WorldRpg.Kit` mechanisms are Engine-native and available for one-time
bootstrap copying (no provenance tracking) per
[`docs/research/rusty-dagger-kit-survey.md`](docs/research/rusty-dagger-kit-survey.md).
The operator's own copy of the game is the ISO at
`/home/research/old-games/game-uu1/game.gog` (ISO 9660 `UW12`, carrying both
`UW/` and `UW2/` trees); only `UW/` is ever an extraction source. Original game
data is never committed here.

## Design shape

Two documents fix the shape before implementation starts:

- [Gameplay design](docs/gameplay-design.md) — the loop, every system's shape
  with a fidelity verdict, the first coherent slice, and the decisions that are
  expensive to reverse.
- [Code organization](docs/code-organization.md) — the layering, where new code
  goes, the Kit and ruleset owner maps, content and import shapes, the UI
  contract, session modes, and persistence.

## Repository layout

| Path | Holds |
| --- | --- |
| [`AGENTS.md`](AGENTS.md) | The working contract: direction, ownership, boundary rules, donor posture, git and documentation conventions. |
| [`docs/`](docs/README.md) | Durable documents: the [gameplay design](docs/gameplay-design.md), the [code organization](docs/code-organization.md), the [research notes](docs/research/), and the [review lane model](docs/agent-review/README.md). |
| [`src/`](src/README.md) | The product graph: kit, ruleset, host, importer and its tool, and the product DOM companion. |
| [`tests/`](tests/README.md) | The suites, including the architecture suite that enforces the ownership laws. |
| [`content/`](content/README.md) | Loaded content: bundles, authored content packs, and per-level imports produced offline. |
| `configs/` | Local service profiles (the Crew playtest registration). |
| `local/` | Operator-only and git-ignored: the extracted game data the import reads, and research notes. Never committed. |
| `scripts/` | The operator level import, the UI build the Host's staging runs, and `verify.sh`. The Engine pair is installed and moved by the Engine's `rusty` command. |

For every task, identify:

- the owning layer;
- new assumptions introduced;
- whether Ultima Underworld vocabulary is permitted;
- whether the change is code, tuning, content, import, or infrastructure;
- dependency changes;
- focused proof for the owning mechanism and ruleset policy.

## Develop and verify

The product consumes one immutable Engine SDK/runtime pair, pinned by
`<RustyEnginePackageVersion>` in `Directory.Build.props`; do not restate a
version or revision here. The Engine's `rusty` command installs, updates and
runs it. Get `rusty` once with the Engine bootstrap
(`curl -fsSL https://raw.githubusercontent.com/FuzzySlipper/rusty-engine/main/scripts/install-rusty.sh | bash`),
then start a clean checkout with:

```bash
rusty status
rusty install
```

To take the newest published Engine pair, which is the ordinary way to pick up
newer Engine state:

```bash
rusty update
```

It installs the pair, rewrites the pin, and lists the release notes to read.
`rusty update --check` reports what is available without changing anything.

Routine verification:

```bash
./scripts/verify.sh
```

It checks the installed pair and project shape (`rusty status`), installs the UI
dependencies, builds every product project, stages the Host (which compiles the
TypeScript companion through the SDK's UI build), then runs the DOM suite and
every .NET suite, architecture laws included, reporting every failed stage
before it exits. NativeAOT is a separate fidelity target and stays opt-in with
`--aot`. Projects are listed in `product_projects` and .NET suites in
`test_projects`, the DOM suite by its glob; keep those lists explicit when adding
one, because a discovery-based loop silently stops covering a project that
moved. Tests that need the operator's game data report **Skipped** without it.

Import a level before launching. The game's data is operator-supplied (extract
the `UW/` tree of the `game.gog` ISO to `local/extracted/uw/UW`), and every
import lands in the git-ignored `content/abyss/imports/`, which the default
bundle admits by root without being edited. Level 1 is enough to launch; the
launch fails with this command when no level is imported:

```bash
scripts/import-level.sh
```

Import further levels to make them reachable by travel (a pit or teleport to a
level that is not imported says so rather than moving the avatar):

```bash
scripts/import-level.sh 2
```

Start the Engine host after installing the pair and UI dependencies:

```bash
npm ci
den-serve up rusty-underworld -repo "$PWD"
```

The broker serves port 4177; staging the Host compiles the browser ESM companion
through the SDK's UI build.
See [GPU playtesting](docs/gpu-playtesting.md) to register the local Crew profile
and capture the current product. A successful launch is not gameplay acceptance.

## Guidance and proof

Repository-specific instructions are in [`AGENTS.md`](AGENTS.md). The installed
SDK's C# guidance is the authority on the product/Engine boundary; this repository
does not restate it.

A check that only compiles is not verification, and a demonstration is not
completion. Run the smallest proof that answers the changed seam, and state
plainly what was not run.

Lighting imports include UW1 light brightness and duration, shade viewing distances,
and palette remaps with per-file provenance. Placed burning lights use those
distances; four distance bands and nearest-palette primitive colors remain our
presentation approximation. Physical item collision radius does not set light
reach. Fuel consumption and equipping remain equipment policy work.

Door leaves use the same normalized pose for presentation and the Engine
character obstacle environment. Closed leaves block movement and projectiles;
ordinary use opens them from outside the leaf. Open/closed state is restored
from the dungeon save. Object offsets and static/mobile headings are admitted
from content, and actor presentation follows live pose and facing.
Our instantaneous door toggle refuses closing when the Engine capsule query
reports the avatar obstructing the leaf. Step clear and use it again. This avoids
inserting a solid obstacle around the avatar; movement recovery stays Engine-owned.

Dungeon geometry is lit by Engine point lights derived from the same avatar and
floor-light sources as object visibility and the automap. The default global
light rig is disabled. Renderer intensity is our typed tuning; point attenuation
approximates the imported viewing distances. Wall shadows are not enabled in the
current Engine surface, so the tile line-of-sight policy remains authoritative
for objects and the automap; geometry does not claim matching shadow occlusion.
