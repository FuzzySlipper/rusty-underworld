# Gameplay design: the shape of the game

Status: **design intent for a product that does not exist yet.** This document
fixes the *shape* of the game — what the player does, what systems must therefore
exist, and how faithful each one is meant to be. It deliberately does not fix
tuning, formulas, or interfaces; those belong to the ruleset, the content, and
later tasks.

Evidence base: [`research/uu1-manual-outline.md`](research/uu1-manual-outline.md)
(experience, cited to manual pages),
[`research/uu1-data-inventory.md`](research/uu1-data-inventory.md) (what the
shipped ISO actually contains), and the donor surveys
([`research/underworldgodot-survey.md`](research/underworldgodot-survey.md),
[`research/openunderground-survey.md`](research/openunderground-survey.md)).
Where this document and the original game disagree, this document decides, and
the difference is recorded rather than silently rounded.

## 1. The game in one page

The player guides **a single avatar** — the Avatar, thrown into the Stygian
Abyss — through a continuous, tile-mapped, eight-level dungeon. There is no
party, no overworld, and no battle screen: exploration, combat, conversation,
and survival all happen in the same first-person place, in real time.

The loop is:

> descend and explore → fight, talk, and take → carry back to safety → convert
> rewards into capability (train, repair, identify, resupply, rest, inscribe) →
> descend further, better equipped.

Three properties bind that loop together and are the reason the shape has to be
decided before code:

- **The dungeon is continuous.** Movement is free and first-person — walk, run,
  jump, swim — across tile-mapped levels joined by stairs, pits, doors, and
  moongates. Position, facing, light, and encumbrance are always live.
- **Combat happens where you stand.** Attacks charge while held and resolve on
  release; there is no mode switch into fighting. The same avatar, the same
  objects, and the same world state serve exploration and combat.
- **Everything interesting is an object.** Weapons, runes, food, lights, keys,
  traps, switches, doors, corpses, and quest tokens share one dense object
  simulation with physics, containers, weight, and condition — hundreds of live
  entries per level.

## 2. Fidelity: what we keep, approximate, and drop

Every system below carries one of three verdicts.

| Verdict | Meaning |
| --- | --- |
| **Match** | The structure and the player-facing behavior are reproduced; a player of the original should recognize it. |
| **Approximate** | The mechanism exists with the same role, but values, breadth, or depth are ours; fidelity is not claimed and not required. |
| **Ours** | We deliberately diverge, or the original feature is out of scope. |

The global stance, from [`AGENTS.md`](../AGENTS.md): a semi-equivalent
recreation. **Out of scope everywhere:** reading or writing original save games,
loading the original executable, byte-exact tile geometry, exact table values,
and any promise that original mods or editors keep working.

## 3. System shapes

Each system states the **shape** (what the player experiences), the **model**
(what the code must therefore represent), and its **fidelity**.

### 3.1 Avatar and character — Match (structure) / Approximate (numbers)

- One avatar, created before descent: appearance and handedness choices plus an
  initial distribution over attributes and skills that gates what the avatar can
  credibly do first.
- A small attribute set (strength, dexterity, intelligence, vitality, and their
  kin) plus a skill list covering weapons, casting, lore, stealth, and craft.
  Skills improve with use and training; shrines and story events grant further
  growth.
- Vital resources: health, mana, hunger, and fatigue. Encumbrance binds inventory
  to strength. Death is answered by resurrection, not by reload-or-quit.

**Model.** An avatar owner (attributes, resources, conditions, skills with
usable values, progression flags) separate from world state (where the avatar
stands, what it carries, what the dungeon currently is). Character creation is a
real product flow with validation, not a debug shortcut. Attribute and skill
identities are ruleset definitions, not enums compiled into the kit.

**Fidelity.** Structure matches. Growth curves, starting budgets, and per-point
numbers are ours until a task verifies them against donor evidence.

### 3.2 Movement and embodiment — Match (structure) / Ours (feel details)

- Free first-person movement over tile-mapped levels: walk and run, jump across
  gaps and down shafts, swim where water demands it, climb where the dungeon
  allows. Falling hurts.
- Collision and physics are always on: the avatar shoves objects, objects block
  passages, projectiles fly, and thrown things land.
- Looking is free; the view carries weapon readiness, the compass, and the
  charge state of a held attack.

**Model.** Kit spatial stepping over Engine spatial mechanisms: one avatar
position with stance and motion state, per-level tile and geometry admission,
object physics coordination, and Engine-backed collision. No second mover, no
parallel physics world.

**Fidelity.** The movement vocabulary matches. Speeds, jump arcs, collision
tolerances, and camera feel are ours. The control scheme is deliberately Ours,
not the original's: the Engine's `FpsInput` (WASD `Standard` bindings,
pointer-lock mouselook, twin-stick gamepad) consumed in ruleset locomotion
policy — the same arrangement rusty-dagger uses (`DaggerfallLocomotionPolicy`
over Engine `FpsInput`, with Kit `SpatialMovementSystem.Step` underneath).
This rejects both the original's cursor-shape View-Window drive (manual
pp. 4–5) and UnderworldGodot's keyboard turning (Q/E turn, A/D strafe,
mouselook code obsolete/commented out). Agent testing rides the Engine's
conveniences (`controller-interaction.md` inspect/use, `--exercise` contract,
`Debugging.Snapshot`), not bespoke hooks. Settled decision, not fidelity debt.

### 3.3 Melee and missile combat — Match (charge structure) / Approximate (numbers)

- Melee is charge-based: holding the attack input readies a swing, the charge
  builds, and release resolves it. Distinct swing kinds (thrust, slash, bash and
  their kin) trade reach, damage, and accuracy.
- Accuracy and damage resolve from skill, weapon, charge, range, and target —
  the donor transcription records the original's to-hit and damage rows, and the
  charge cursor only promises a swing that can land.
- Missile combat (bows, crossbows, slings, thrown objects and spells) shares the
  same targeting and damage path, not a parallel implementation.
- Enemies fight back with their own senses, pursuit, strikes, and retreat; blood
  and feedback mark hits; corpses persist and are searchable.

**Model.** **One** attack execution over the live dungeon: readiness, charge,
release, pending impact, interruption, and cooldown carried as attached state;
ruleset-supplied formulas for cost, hit, and damage; typed RuleEvents where
independent contributors (effects, equipment, traits) genuinely participate.
Ranged dispatch reuses the same resolution and loot flow.

**Fidelity.** The charge-and-release structure and swing vocabulary match;
formulas, ranges, and per-weapon numbers are approximate until verified.

### 3.4 Runic magic — Match (structure) / Approximate (spells)

- Runes are world objects: found, collected, and carried in a runebag. Casting
  combines runes into a spell at cast time against a mana reserve — the
  runebag is inventory with meaning, not a spellbook menu.
- Spells cover the classic families: damage, light, healing, protection,
  movement, detection, and utility. Circle or tier gates what the avatar can
  credibly cast; skill decides reliability.
- Scrolls cast once without skill; wands carry charges; potions are consumed.
  Failure costs mana and can backfire; it never silently succeeds.

**Model.** A rune and spell catalog in content; per-avatar known runes, mana,
and casting state; a casting workflow (validate → cost → target → apply); effect
instances with game-time duration; item-borne casting as item behaviors reusing
the same resolution. Per-spell effect policy is ruleset code over content data.

**Fidelity.** Structure, rune combination, and gating match. Individual spell
effects are approximated by family first, then deepened. Do not claim
per-spell fidelity that was not checked.

### 3.5 Objects, inventory, and encumbrance — Match (structure) / Approximate (breadth)

- Every level is dense with interactable objects — the donor object-data survey
  counts dozens of families (weapons, armor, ranged, food, lights, containers,
  triggers, critters, animated props). Take, drop, use, use-on, eat, read,
  repair, and combine are ordinary verbs.
- The avatar carries a paperdoll plus loose inventory; weight and encumbrance
  bind what can be carried to strength. Containers nest. Items wear, break, and
  can be identified.
- Quest tokens, keys, and story objects are ordinary objects with identity that
  survives a save — a specific artifact stays that artifact.

**Model.** Object definitions in content and object instances in runtime state,
with durable identity; inventory, equipment, container, and loot owners with
clear mutation ownership; identify/repair/combine as object state transitions,
not shop-only side effects. Imported object tables supply breadth.

**Fidelity.** Structure matches; object counts, values, and wear rules are
approximate. Unused-but-present object records are real scope with an unknown
rule set — treat their rules as ours until a task verifies them.

### 3.6 Traps, triggers, doors, and secrets — Match (structure) / Approximate (breadth)

- The dungeon is wired: pressure plates, tripwires, moving walls, secret doors,
  locks and keys, switches and levers, moongates, and level transitions. The
  donor trap survey counts sixty-plus trap behaviors, including records the
  original never places — the family is broader than the shipped placements.
- Doors lock, jam, pick, bash, and reopen when blocked. Discovery (secret found,
  map noted, passage opened) is player knowledge, separate from world state.

**Model.** Traps and triggers as content-defined behaviors over the object and
world owners; doors as stateful objects with lock/pick/bash/open semantics;
discovery events published as typed notifications into knowledge state. No class
per trap kind.

**Fidelity.** The trigger/trap structure matches. Per-trap numbers and placement
breadth are approximate; never-placed records are explicitly dispositioned, not
silently cut.

### 3.7 NPCs, conversation, and barter — Match (structure) / Approximate (depth)

- The Abyss is inhabited: the shipped conversation archive carries the full
  NPC dialogue corpus (count confirmed at import against CNV.ARK), with
  dialogue trees, topic keywords, barks, and quest hooks per the manual
  (pp. 14-15, 24-25). NPCs move, keep schedules, react to the avatar, fight,
  flee, and trade.
- Barter is conversation with consequences: offers, counter-offers, and
  reputation effects, resolved against real inventories.
- Some NPCs teach, repair, identify, or advance the story; all of them can be
  fought, robbed, or angered, with lasting faction consequences.

**Model.** Conversation definitions in content plus per-instance dialogue state;
a conversation VM interpretation owned by the ruleset ( opcodes interpreted,
never a ported engine ); NPC presence and AI coordinated by Kit over Engine
spatial and perception; barter as a transaction over the item owners. Quest
effects from dialogue reuse the quest owner, not a second system.

**Fidelity.** The conversation structure and barter role match. Dialogue depth,
AI sophistication, and per-NPC numbers are approximate; quest content starts
authored-small.

### 3.8 The Abyss: levels, places, transitions — Match (structure) / Approximate (breadth)

- Tile-mapped levels plus entry and dream spaces, joined by stairs, pits,
  doors, and teleport gates (the manual describes levels "much the same way
  a building is divided into stories," p. 19; the shipped count is confirmed
  against LEV.ARK at import). Each level has its own population, water and
  lava, light and darkness, and secrets.
- Level transitions preserve the avatar and its following state; levels persist
  while unloaded — killed inhabitants stay dead, taken objects stay taken,
  opened doors stay opened.
- Special spaces (dreams, cutscene rooms, the entry vision) are ordinary levels
  with authored rules, not exceptions.

**Model.** A **graph of levels** with transition edges carrying destination,
entry transform, and cost; per-level runtime state (tile admission, object
population, corpse and container state, respawn policy); transitions as explicit
operations with save capture on both sides. **Avatar knowledge** (maps drawn,
notes, known gates, quest variables) is a separate owner from world state.

**Fidelity.** The eight-level structure and persistence rule match. Geometry is
imported where the importer can carry it and authored otherwise; full breadth is
a target, not a first milestone.

### 3.9 Survival: hunger, fatigue, light, and rest — Match

- The avatar must eat: hunger grows, food is consumed, and starvation weakens.
  Fatigue grows with exertion; rest and sleep restore, with dreams and
  interruptions where the dungeon demands them.
- Light is a resource: torches, spells, and ambient sources push back darkness
  that hides dangers and secrets. Time passes visibly and audibly.
- Poisons, disease, drunkenness, and drug effects modify all of the above
  through the common effect path.

**Model.** **One** dungeon clock owned by the session; hunger, fatigue,
regeneration, and effect deadlines are game-time values advanced inside admitted
updates and by explicit rest operations. No second clock, no frame counting.

**Fidelity.** The survival model matches; rates, thresholds, and durations are
ours until verified.

### 3.10 Automap, compass, and quest state — Match (structure) / Ours (presentation)

- The automap fills in as territory is seen; the player annotates it with notes,
  edits them, and pages across levels. The compass reports facing and status.
- Quest progress lives in game variables and quest flags the dungeon scripts
  read and write — persistent, inspectable, and save-carried.
- Books, scrolls, inscriptions, and cutscene texts are readable in the world.

**Model.** Knowledge state (map coverage, notes, quest variables, read flags) as
a first-class save-carried owner; projections for map, compass, and journal;
semantic actions for annotate, page, and dismiss. Reading is interaction with an
object, not a separate library UI.

**Fidelity.** The information architecture matches; layout, art, and interaction
details are ours.

### 3.11 Interface surfaces — Match (information architecture) / Ours (presentation)

- One adventure screen: 3D view with weapon hand and charge state, avatar
  portraits and vitals, mana, hunger and fatigue indicators, compass, inventory
  and runebag access, automap corner, and message line.
- Panels for inventory and paperdoll, runebag and casting, conversation and
  barter, automap and notes, character sheet and skills, options, and save/load
  with thumbnails. Cutscenes play in small windows inside the world frame.

**Model.** The product publishes **one HUD projection** plus per-screen values
and receives **semantic actions** back. The DOM companion is thin: it renders
values and reports intents, and owns no state, no rules, and no loop.

**Fidelity.** The information architecture matches; layout, art, and
interaction details are ours.

### 3.12 Rules the shipped data does not carry — Ours

Three rule families a designer would expect in the tables live in the
executable or in donor transcription rather than in the archives:

- **Combat feel constants**: charge timing, swing arcs, and hit feedback tuning.
- **Advancement curves**: skill-use thresholds and training costs per level.
- **Schedule and AI constants**: NPC senses, speeds, and reaction thresholds.

Consequences: these are **authored by us**, informed by donor transcription and
the manual, and recorded as ours rather than presented as extracted facts.
Projects that must be checked against the executable are separate tasks with
their own evidence, not assumptions smuggled into the importer.

## 4. A session's lifecycle

1. Product start → title and load.
2. Character creation (appearance, attributes, skills) → entry vision → level 1.
3. Adventure: the dungeon runs in real time; movement, interaction, combat,
   conversation, and survival advance together.
4. Panels (inventory, runebag, map, character, conversation) open **without
   pausing the world**, matching the original.
5. Rest and sleep advance the clock in discrete steps, with interruption and
   dreams.
6. Save at meaningful boundaries (plus per-level autosave as an explicit
   convenience); loading restores avatar, dungeon, knowledge, and clock.
7. The arc runs from the upper halls through the lower deeps to the final
   confrontation and ascent.

One session, one clock, one admitted update. No mode gets its own loop.

## 5. The first coherent slice

The original ships multiple dungeon levels, hundreds of objects per level, 40
spells in 8 circles from 24 runes (manual pp. 28-29, 32), 20 skills (manual
p. 30), scores of NPCs, and a full conversation corpus. Hand-authoring that is
not the plan; the importer carries tile maps, object tables, conversations, and
media, and content authoring fills what it cannot.

A **first coherent slice** is therefore: creation → level 1 walkable with
collisions and light → real-time movement with jump and swim → charged melee
with one weapon family and one critter kind → take, drop, and container loot →
one rune spell cast from found runes → one NPC conversation with a barter or
quest hook → automap with notes → save and load. Missile combat, traps, survival
depth, and the remaining levels follow before breadth. Breadth (more levels,
more objects, more spells, more conversations), then depth (factions, dreams,
endgame).

## 6. Non-goals

- Original save-file compatibility, in either direction.
- Loading the original executable, its sound drivers, or its mods.
- Byte-exact tile geometry, exact table values, or exact balance.
- Reproducing the original's videos, music, or art distribution; extracted media
  is a local development convenience with recorded provenance, never a shipped
  artifact.
- Multiplayer, dedicated server, UW2 support, or a second product runtime.

## 7. Decisions that are expensive to reverse

These are the seams the rest of the work hangs on. Changing one is a deliberate
re-plan, not an implementation detail.

1. **One avatar, not a party.** Roster, purse-sharing, and multi-actor command
   never enter the design; followers are world entities, not party members.
2. **Combat is one real-time charge state over the live dungeon.** No turn mode,
   no battle scene, no second scheduler.
3. **The dungeon is a graph of persistent levels with costed transitions**, and
   **avatar knowledge is separate from world state**.
4. **One clock, game-time durations.** Hunger, fatigue, schedules, respawns, and
   effect deadlines are game-time values; nothing counts frames.
5. **Conversation is interpreted content.** A ruleset-owned VM interpretation of
   authored dialogue; never a ported conversation engine and never code per NPC.
6. **Ruleset policy over content data.** Formulas, eligibility, costs, and
   advancement live in the compiled ruleset; numbers and definitions live in
   content; neither is scattered through kit mechanisms.
7. **The UI is a projection plus semantic actions.** Screens own no state, and
   the dungeon keeps running behind them.
8. **The importer output shape is a runtime contract.** Content packs, tuning
   profiles, level geometry, media, and provenance are consumed by runtime code
   and only change deliberately.
9. **One current save schema.** No versions, migrations, or compatibility
   readers during development.
