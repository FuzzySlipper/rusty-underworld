# Code organization: how the repository expresses the game

Status: **design intent. No project exists yet.** This document fixes where
things belong so that the first implementation tasks do not have to invent the
seams. It describes owners and boundaries, not APIs.

Read it with [`gameplay-design.md`](gameplay-design.md), which defines the shape
being expressed, and [`../AGENTS.md`](../AGENTS.md), which owns the vocabulary,
engine, donor, and git rules.

## 1. The layering

```
Rusty.Engine  (immutable package: lifecycle, input, rendering, spatial, services)
       │
AbyssRpg.Kit  (rules-agnostic mechanisms and state shapes)          UltimaUnderworld.Import
       │                                                             (offline: formats, packs,
AbyssRpg.Rulesets.UltimaUnderworld  (policy, definitions, meaning)   provenance; not a runtime
       │                                                              dependency)
AbyssRpg.Host  (one product entry; selects ruleset, bundle, defaults)
       │
TypeScript UI  (DOM presentation of projections; semantic actions back)
```

The rules are one-directional and enforced by an architecture suite once the
projects exist:

- **Kit never names the game.** No `UltimaUnderworld`, no game or place names,
  no attribute/skill/rune/spell/object/critter names, no donor names, no
  source-file names. Kit mechanics take ruleset-supplied policy and
  content-supplied data.
- **The ruleset depends on Kit, never the reverse**, and never on the Host.
- **The Host depends on Kit and the ruleset** and is the only place that names a
  built-in ruleset or bundle.
- **The importer depends on neither** and nothing runtime depends on it.
- **The UI sees projections and actions only** — no product types, no state.

## 2. Where new code goes

| If the task is… | It belongs in… |
| --- | --- |
| A formula, eligibility rule, ceiling, price, or threshold | ruleset policy, over content data or a typed tuning profile |
| A new attribute, skill, rune, spell, object, critter, conversation, trap, or level | content (definitions), not code |
| A mechanism several systems need (recovery, effects, containers, conversations) | Kit |
| A number that designers will retune | a typed tuning profile in content |
| A quirk of the original's files | `UltimaUnderworld.Import` |
| A screen, HUD field, or interaction affordance | Kit/ruleset projection value + UI rendering |
| Original tile maps, sprites, or sounds | importer output under `content/…/imports/` |
| A missing Engine capability | a narrow upstream `rusty-engine` request, then an honest stop |

## 3. Kit owners

Working names; the responsibilities are the contract, the names are not.

| Owner | Owns | Does not own |
| --- | --- | --- |
| Session | The one live session: its clock, avatar, dungeon, and saved state; composed from a compiled ruleset + bundle + content packs | Rules, formulas, content meaning |
| Ruleset contract | The typed seam a ruleset implements: catalogs it supplies, policy it answers, session services it composes | Any concrete rule |
| Avatar | Attributes, resources (health/mana/hunger/fatigue), conditions, skills and progression flags, encumbrance | What an attribute means; how a skill grows |
| Creation | The character-creation flow and its validation, driven by ruleset-supplied choices and budgets | The ruleset's class tables |
| Skills | Skill catalog shape, per-avatar skill entries, use-driven advancement bookkeeping, shrine/training sources as world entities | Which class may learn what, and thresholds |
| Magic | Rune catalog shape, collected runes and shelf state, casting workflow (validate → cost → target → apply), effect instances with game-time duration, item-borne casting | Rune lists, costs, circles, and per-spell effect policy |
| Combat | One real-time attack state over the live dungeon: charge buildup and release, per-actor recovery, attack execution, effect and condition application, NPC AI coordination, corpses and loot | Damage formulas, critter definitions, condition meanings |
| World | The level graph, entry points, transitions with cost, entity population, spatial stepping, per-level runtime state, persistence of unloaded levels | What a level contains (content), how it looks (Engine + media) |
| Knowledge | Discovery state: automap coverage, notes, compass state, quest variables and flags, read flags | Conversation definitions; world state |
| Interaction | Interaction targets (doors, containers, levers, switches, objects, people) and the look/get/use/use-on workflow | Trap and lock policy |
| Dialogue | Conversation state, option lists, topic availability, barter offers, attitude memory | Who says what (content) |
| Objects | Object definitions and instances, inventory, equipment/paperdoll, currency, food, lights, containers and loot, identify and repair state | Object values, combination rules, treasure placement |
| Survival | Hunger, fatigue, light, rest/sleep advancement, poison/disease/condition ticking over game time | Rates and thresholds (ruleset + tuning) |
| Time | The one dungeon clock; discrete advancement; schedule and respawn queries; duration deadlines | Schedules and constants (content and ruleset) |
| Content | Pack loading and validation: definitions, tuning, scenario, imported level data, provenance; bundle resolution | Any meaning of the data |
| Presentation | Projections (HUD and screens) and semantic actions; live-debug diagnostics | DOM, layout, styling, or state |
| Persistence | The current-schema session snapshot and restore; what is saved and what is deliberately dropped | Original save formats (out of scope) |

Two Kit rules that prevent most later refactoring:

- **One owner mutates one state family.** Effects, conversations, and traps
  request changes through the owner that holds the state; they do not reach
  into it.
- **Cross-owner interaction uses typed RuleEvents and typed notifications**, not
  a generic bus and not a proposal/acceptance ceremony.

## 4. Ruleset owners (UltimaUnderworld)

| Owner | Owns |
| --- | --- |
| Identities and catalogs | Attributes, the 20 skills with governing attributes, classes, the 24 runes and 40 spells in 8 circles, objects, critters, traps, condition meanings |
| Formulas and policy | Attack charge and damage, accuracy, missile behavior, spell costs/gates/effects, advancement thresholds, repair difficulty, barter valuations, trap and lock difficulty, survival rates |
| Interpretation | What a content definition means here: which definitions are legal, how shrines and anvils work, which conversations gate which story, what an imported level is |
| Progression policy | Level and skill advancement requirements, mantra effects, respawn-anchor rules |
| Session composition | Assembling the named Kit services with this game's policy, and this game's save meaning |
| Presentation meaning | Which projection fields exist and what the panels and screens show |
| Provenance rules | What an imported pack must record about the source it came from |

Attributes, skills, runes, spells, objects, critters, conversations, traps, and
levels are **data**. Their numbers live in content packs and tuning profiles;
their interpretation lives here; their names never appear in Kit.

## 5. Host and product composition

One product entry type, declared once, with the product id, title, UI root,
content root, lifecycle mode, fixed step, declared input intents and mappings,
and content bundles. The Host selects a built-in ruleset and default bundle at an
explicit composition seam and otherwise only starts, pauses, resumes, and stops
the session. It never interprets rules and never reads original game data.

## 6. Content shapes

Four kinds of content, kept separate because they change at different rates and
have different authors:

| Kind | Holds | Author |
| --- | --- | --- |
| Definitions | Catalogs with meaning: attributes, skills, classes, runes, spells, objects, critters, conversations, traps, levels | Authored, or generated from imported tables |
| Tuning | One discoverable typed profile tree of adjustable values (curves, costs, rates, coefficients) | Authored |
| Scenario | The starting state: avatar defaults, placements, spawns, quest state, initial flags | Authored |
| Imported world | Tile maps, object lists, texture maps, automap blocks, media, and tables normalized by the importer, with provenance | Offline generation from an operator-supplied install |

Rules:

- **Regeneration never overwrites authored content**, and authored content never
  overwrites an import silently.
- Every imported pack records its source: game, release or build, importer
  revision, and what was transformed.
- Original game data is never committed; extracted tables stay in `local/`.

## 7. Import pipeline

```
operator ISO → readers (LEV.ARK, CNV.ARK, OBJECTS.DAT, tables, art, sound)
             → normalized packs under content/abyss/imports/<level>/
             → provenance record + table dumps used as content sources
```

The tool is its own project and is built and tested by `scripts/verify.sh`, so an
import API change cannot leave it silently broken. Differential checks compare
the importer's output against the donor recreations where a comparison is
possible. Nothing in the runtime path parses source-shaped game data.

Two known traps that shape the importer, both recorded in the research:

- The ISO carries **two games** (`UW/` + `UW2/`). A file must be resolved by an
  explicit UW1 source path, never by "whichever tree opened first" — that
  mistake would silently import UW2 records.
- The installed `UNDEROM1/` tree carries only cutscene first-frames, `UW.CFG`,
  and saves. Full archives come from the ISO; the importer must not treat the
  installed subset as complete.

## 8. UI contract

- The product publishes a HUD projection and per-screen values; the UI renders
  them and returns semantic actions.
- Screens are values, not state holders. Opening one does not create authority,
  and closing one loses nothing.
- The UI never evaluates rules, never advances time, and never starts a loop.
- Input intents are declared by the Host; the UI reports actions, not keys.

## 9. Modes, update, and pause semantics

One Engine-admitted update drives everything. Inside it, the session has exactly
one mode:

| Mode | Stepping |
| --- | --- |
| Character creation | No dungeon stepping; the creation flow is the authority |
| Adventure (real time) | The dungeon steps every admitted update; panels may be open while it does |
| Conversation or barter | The dungeon keeps running unless a task decides otherwise |
| Rest, sleep | Discrete clock advancement through the Survival/Time owners — not a second loop |
| Menu, save, load | Session is quiescent; no dungeon stepping |

Pause is a session concept, not a thread, timer, or scheduler. Charge,
recovery, durations, hunger, and respawns are game-time values, so changing
panels never changes what time means.

## 10. Persistence

A save is a snapshot of the session: avatar (attributes, resources, skills,
runes, equipment, progression), clock, current level and position, per-level
dungeon state, knowledge, quest variables, containers and loose world objects,
and the scenario flags. Transient things — in-flight charge, open panels,
target selections, AI intentions — are deliberately dropped and rebuilt on load.

One current schema during development. No versions, migrations, compatibility
readers, or original-format support. The planted-seed respawn anchor is product
behavior over our own save schema, not original-save compatibility.

## 11. Deliberate non-architecture

Do not build any of these, however convenient they look:

- an ECS/ESS framework, a generic component registry, or a service locator
- a generic event bus, command bus, or message broker
- runtime plugin loading, reflection discovery, C# scripting, or a Lua layer
- a second loop, clock, timer, thread, renderer, or browser authority
- a class per object kind, per spell, per trap, or per NPC
- state held by UI screens, or UI-side rules evaluation
- content that carries code, or code that carries content
- schema versions, save migrations, or original-format readers
- a ported conversation VM engine, Godot node graph, or Unity
  MonoBehaviour/prefab topology — the ruleset interprets authored dialogue

## 12. What would force a re-plan

The nine decisions listed in
[`gameplay-design.md` §7](gameplay-design.md#7-decisions-that-are-expensive-to-reverse)
— one avatar, one real-time charge state, the level graph, knowledge versus
world state, one clock, interpreted conversation, policy over data,
projection-only UI, and one save schema. Changing any of them is a deliberate
re-plan with the user, not an implementation detail.
