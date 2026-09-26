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

> **Current state: a playable, imported first slice of the dungeon.**
> The ordinary entry resolves the shipped game bundle, creates the compiled
> ruleset session over an operator-imported level, publishes the visible level
> and a first-person camera, and routes admitted updates into the combat,
> casting, survival, menu, and save/load owners. The companion renders the
> product's one projection and mounts the Engine's own live-debug panel.
> What is imported is the level's collision, visible geometry, a spawn, its
> object placements and the object tables: the placed props become durable
> entities, the placed critters become actors standing on their own tiles, a
> swing damages and can drop one (the kill is recorded in the saved level
> state), and the use channel opens a placed door through that state. Every
> imported level is admitted on arrival when the avatar travels, with its own
> collision, geometry and inhabitants, while the level left behind keeps its
> state. The level's objects are held by owners: a container yields what its own
> record links to it, an object on the floor is taken into the avatar, a fallen
> opponent's carried things are looted from it, and a runestone lands on the
> casting shelf so spells can be cast from stones picked up in play. Placed things
> are simulated but not drawn yet, carried items do not survive a save yet, and a
> conversation is one pass: the imported conversations and the talk and barter
> owners are live — using a creature runs its own script, the panel carries the
> transcript, its speaker's name and the options it offers, and a vendor's script
> trades against the imported values of what each side carries — but the avatar
> cannot yet answer an option, which #8623 carries. Start with
> `scripts/import-level.sh`; the operator step, the Crew profile, and the
> observed limits are in [GPU playtesting](docs/gpu-playtesting.md), the slice
> captures in [the composition evidence](docs/playtest-evidence/8587-8588/README.md),
> the placement captures in
> [the placement evidence](docs/playtest-evidence/8590/README.md), the level
> transition in [the travel evidence](docs/playtest-evidence/8616/README.md), and
> looting and rune pickup in
> [the item evidence](docs/playtest-evidence/8615/README.md), and talking to the
> Abyss' own inhabitants in
> [the conversation evidence](docs/playtest-evidence/8614/README.md).

## Ownership

- Rusty Engine guarantees reusable infrastructure and admitted update services.
- `AbyssRpg.Kit` will define the reusable dungeon-RPG composition grammar and the
  ordinary mechanisms needed to construct one.
- `AbyssRpg.Host` will own the product lifecycle, built-in ruleset registry,
  shipped bundles, launcher, defaults, and session selection.
- `AbyssRpg.Rulesets.UltimaUnderworld` will own all Ultima Underworld semantics,
  formulas, identities, runic-magic policy, conversation meaning, presentation
  meaning, and content interpretation.
- Content packs will own authored definitions, assets, levels, placements,
  conversations, and scenario state.
- `UltimaUnderworld.Import` will own source-format knowledge for the original
  game's data files and for the donors that document them.
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
| [`src/`](src/README.md) | The planned product graph: kit, ruleset, host, importer and its tool, and the product DOM companion. |
| [`tests/`](tests/README.md) | The planned suites, including the architecture suite that will enforce the ownership laws. |
| [`content/`](content/README.md) | Loaded content: bundles, authored content packs, and per-level imports produced offline. |
| [`data/`](data/README.md) | Small checked-in reference tables a person maintains. |
| `scripts/` | Engine pair installation and pin movement, the operator level import, and `verify.sh`. |

For every task, identify:

- the owning layer;
- new assumptions introduced;
- whether Ultima Underworld vocabulary is permitted;
- whether the change is code, tuning, content, import, or infrastructure;
- dependency changes;
- focused proof for the owning mechanism and ruleset policy.

## Develop and verify

The product consumes the immutable `Rusty.Engine` package from the installed
`.runtime/sdk-feed` and the matched `.runtime/runtime-pack`. That pair's identity
belongs in `Directory.Build.props`, where the install and verify scripts check it;
do not restate a version or revision here.

Start a clean checkout with the pinned, noninteractive pair install. It validates
the release checksum, payloads, ABI, package version, and Engine source revision
before atomically replacing the whole ignored pair:

```bash
./scripts/install-engine-pair.sh
```

To take the newest published Engine pair, which is the ordinary way to pick up
newer Engine state:

```bash
./scripts/update-engine-pin.sh
```

It resolves the newest `csharp-sdk` release, rewrites both identities in
`Directory.Build.props`, and installs the pair. `--check` reports what is
available without changing anything.

Routine verification:

```bash
./scripts/verify.sh
```

Today that verifies the installed pair identity, installs the product UI
dependencies, builds the TypeScript companion and runs its DOM suite, then
builds every product project and runs each semantic suite plus the architecture
laws. NativeAOT is a separate fidelity target and stays opt-in with `--aot`. When the
first project lands, add it to `product_projects` and its suite to
`test_projects` in the script — the lists are explicit on purpose, because a
discovery-based loop silently stops covering a project that moved.

Import a level before launching; the product's default bundle selects it and the
launch fails with the command to run when it is absent:

```bash
scripts/import-level.sh          # operator data under local/extracted/uw
```

Start the Engine host after installing the pair and UI dependencies:

```bash
npm ci
den-serve up rusty-underworld -repo "$PWD"
```

The broker serves port 4177; the Host build compiles the browser ESM companion.
See [GPU playtesting](docs/gpu-playtesting.md) to register the Crew Wolf profile
and capture the current product. A successful launch is not gameplay acceptance.

## Guidance and proof

Repository-specific instructions are in [`AGENTS.md`](AGENTS.md). The installed
SDK's C# guidance is the authority on the product/Engine boundary; this repository
does not restate it.

A check that only compiles is not verification, and a demonstration is not
completion. Run the smallest proof that answers the changed seam, and state
plainly what was not run.
