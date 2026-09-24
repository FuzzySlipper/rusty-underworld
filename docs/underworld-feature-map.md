# Underworld feature map (donor structures → rusty-underworld coverage)

**Status:** survey complete (point-in-time coverage notes). Scope is code
structures and features that support Ultima Underworld content, not the content
itself.
**Donors:** `/home/research/old-games/UnderworldGodot` (Godot 4 + C#, UW1+UW2;
~270 files under `src/`) and `/home/research/old-games/OpenUnderground`
(Unity 6000; 167 files under `Assets/Game/Scripts/`). Neither is a code donor.
**rusty-underworld:** setup-pass shape per `docs/code-organization.md` —
no C# project exists yet, so every behavior row is Absent by construction and
every Engine row names the upstream owner it will consume.
**Reading rule:** donor paths below are relative to the donor checkout roots.
rusty-underworld paths are relative to this repo. Coverage is a point-in-time
note, not a roadmap promise.

## Scope and method

- This map covers engine-adjacent structures the donors use to run UW1:
  avatar/stats, movement motors, combat, magic/effects, objects/inventory,
  critters/AI, dungeon/tilemaps, conversation/barter, traps/triggers, UI
  screens, saves, audio, rendering/assets, source formats, and utility.
- It deliberately excludes authored content payloads (conversation text, level
  data, textures) except for the code that loads, interprets, or presents them.
- Donor file counts for scale: Godot `src/traps` 61, `src/objects` ~70,
  `src/conversation_functions` ~57, `src/loaders` 27, `src/magic` 15,
  `src/player` 15 (+chargen), `src/World` 14, `src/ui` 33; OU
  `Assets/Game/Scripts` 167 (+28 in subfolders).
- rusty-underworld was assessed from checked planning documents only
  (`AGENTS.md`, `docs/`, `src/*/README.md`, `content/`, `scripts/`); there is
  no C# to count as coverage.
- Three survey slices contributed one row per distinct structure; each row
  keeps one donor reference, one behavior description, and one coverage note.

## Coverage legend

| Label | Meaning |
| --- | --- |
| Covered | A live path owns the behavior. (No behavior rows carry this yet.) |
| Partial | The shape exists but narrowed. (No behavior rows carry this yet.) |
| Import-only | Offline decoding/normalization exists; no live runtime behavior yet. (Nothing yet.) |
| Absent | No live or import path yet; donor references are the material for future work. |
| Engine-owned | Behavior will belong to the Engine boundary (update admission, spatial, rendering, audio, persistence substrate); the product will consume it through named Engine services rather than reimplementing it. |

Engine-boundary reminder: product code will own application/gameplay state and
policy inside one Engine-admitted update; it must not add a second
loop/clock/thread, renderer, scheduler, or downstream-Rust substitute. A
missing safe Engine contract is an upstream request and an honest stop.

## Donor layout (where to look)

| Area | UnderworldGodot location | OpenUnderground location | What lives there |
| --- | --- | --- | --- |
| Bootstrap / game state | `main.cs`, `scenes/Launch.tscn` | `FrontEnd.cs`, `Logos.cs`, `GameDirectoryDialog.cs`, `GameDataPath.cs` | Scene bootstrap, first-run data dialog, front end |
| Avatar / stats | `src/player/playerdat*.cs` (15 partials), `src/player/chargen/` | `PlayerObject.cs`, `PlayerData.cs`, `StatsPanel.cs`, `Skills.cs`, `CreateCharacter.cs` | Attributes, skills, XP, HP/mana, inventory slots, clock, quests |
| Movement | `src/player/playerdatloop.cs` (swim, footsteps), `src/physics/` (13) | `PlayerInput.cs` | Walk/run/jump/swim/fly, collision, drown timer |
| Combat | `src/interaction/combat/` (5 partials), `src/interaction/damage.cs` | `WeaponBase.cs`, `Weapon.cs`, `RangedWeapon.cs`, `Fist.cs`, `Projectile.cs` | Charge buildup, accuracy, swing kinds, missiles, projectiles |
| Magic | `src/magic/` (15: dispatch + per-major classes + runic table + active effects) | `Magic.cs`, `Spell.cs`, `CascadingSpellEffect.cs` | Major/minor dispatch, runes, mana, item-borne casting |
| Objects / inventory | `src/objects/` (~70), `src/objectdata/` (9 table classes), `src/interaction/pickup.cs`, `use.cs`, `useon.cs` | `Inventory.cs`, `Container.cs`, `Chest.cs`, dozens of `UUObject` subclasses | Item behaviors, table offsets, verbs, containers, paperdoll |
| Critters / AI | `src/npc/` (7), critter object data | `Critter.cs`, `CritterLoader.cs`, `CritterVariants/` | Senses, pursuit/attack/retreat, sounds, variants |
| Dungeon | `src/World/tilemap*.cs`, `tileinfo.cs`, `animationoverlay.cs` | `LevelLoader.cs`, `LevelGeometry.cs`, `Tile.cs`, `UUTerrain.cs` | Tile maps, texture maps, overlays, wall faces |
| Conversation / barter | `src/conversation/` (VM + opcodes + ~57 functions) | `Conversations.cs`, `ConversationDecompiler.cs` | Dialogue VM, imported functions, trade |
| Traps / triggers | `src/traps/` (61), `src/triggers/` (5), `src/scd/` (UW2) | `Trap.cs`, `Trigger.cs`, `SwitchBase.cs`, `Switch.cs`, `Lever.cs`, `Moongate.cs` | Dispatch tables, wall controls, moongates |
| UI | `src/ui/` (33), `src/cuts/` (3) | `MapScreen.cs`, `FluteGUI.cs`, `SaveLoadGUI.cs`, panels | HUD, panels, automap, cutscenes, flute |
| Saves | `src/savegame/` (8) | `SaveGameManager.cs`, `SaveGameData.cs` | Slots, XOR, slot-1 detach, autosave/quicksave |
| Audio | `src/audio/` (5), `src/loaders/xmimusic.cs`, `vocloader.cs` | `Music.cs` (+ `XMIPlayer/`), `Sounds/` | XMI themes, VOC speech, track slots |
| Rendering / assets | `tilemaprender.cs`, `AutomapRender`, palette shaders | `Meshes/`, `Prefabs/`, `Textures/`, `Shaders/` | Tile builders, sprites, models, portraits |
| Source formats | `src/loaders/` (27) | `DataLoader.cs`, `StringLoader.cs`, `ObjectsData.cs` | ARK/DAT/GR/BYT/TR/SYS/VOC/XMI readers |
| Survival / rest | `src/World/sleep.cs`, `playerdatloop.cs` | (scattered) | Sleep methods, hunger, dreams, ambush checks |
| Utility | `src/utility/` (19) | `Utils.cs`, `Messages.cs`, `Compass.cs` | Buffers, messages, compass |

## rusty-underworld layout (planned coverage owners)

| Owner | Path | Planned responsibility (per code organization) |
| --- | --- | --- |
| Kit mechanisms | `src/AbyssRpg.Kit/` | Reusable dungeon-RPG mechanisms over Engine services; never names Ultima Underworld |
| Ultima Underworld ruleset | `src/AbyssRpg.Rulesets.UltimaUnderworld/` | UW1 identities, formulas, charge/attack policy, casting gates, conversation interpretation, save behavior, session composition |
| Host | `src/AbyssRpg.Host/` | Product entry, lifecycle, built-in selection, Engine persistence composition |
| Import (offline) | `src/UltimaUnderworld.Import/` + `.Tool` | UW/ format decoding, normalized publication, provenance/differential validation; no runtime authority |
| Content packs | `content/abyss/` (packs, tuning, scenario, imports) | Loaded level publications, tuning profiles, bundle selection |
| UI (thin) | `src/ui/` | DOM presentation of Engine projections; no gameplay state |

> Subsystem feature tables follow. Each table row is: Feature | Donor code ref | Description | rusty-underworld coverage.

## 1. Avatar, attributes, skills, advancement

| Feature | Donor code ref | Description | rusty-underworld coverage |
|---|---|---|---|
| Attribute container | Godot `src/player/playerdatskills.cs` — attributes/skills get/set, governing attribute | ST/DX/INT (12–30) plus derived maxima. | Absent — planned ruleset identities + Kit Avatar owner |
| Skill container | Godot `src/player/playerdatskills.cs` — `SkillCheck`, skill/group increases; OU `Skills.cs` | 20 skills with governing attributes, use-driven gains, shrine mantras (SUMM RA/MU AHM/OM CAH + hidden). | Absent — planned Kit Skills + ruleset policy over content |
| XP and level | Godot `src/player/playerdat.cs` — `ChangeExperience`, `AwardXPKill`; OU HP-formula fix | XP for explore/kill/deeds, auto level-ups to 16, HP/mana/weight recalc. | Absent — planned Kit Avatar + ruleset progression policy |
| Character creation | Godot `src/player/chargen/chargen.cs`; OU `CreateCharacter.cs` | 8 ordered choices (sex/handedness/class/skills/portrait/difficulty/name/keep), 8 classes. | Absent — planned Kit Creation flow driven by ruleset tables |
| Difficulty | Manual p. 2 (Standard/Easy, locked after start) | Monster danger scalar chosen at creation. | Absent — planned creation flag + ruleset tuning |
| Shrine advancement | OU CHANGELOG (mantra counts, no phantoms) | Ankh shrines: chant mantra for category/hidden-skill gains. | Absent — planned world-entity training sources |

## 2. Movement and embodiment

| Feature | Donor code ref | Description | rusty-underworld coverage |
|---|---|---|---|
| Locomotion state | Godot `src/physics/` (13 files); OU sprint-on-Acrobat, auto-jump assist | Walk/run/jump/swim/fly with stance and motion state. | Absent — planned Kit spatial stepping over Engine spatial |
| Collision | Godot `showcolliders`, tile collision; `playerdatobject.cs` slot-1 head insert | Tile/object collision incl. player-as-object-chain-head quirk. | Engine-owned (spatial) + Absent (slot-1 rule as import note) |
| Drown timer | Godot `playerdatloop.cs` swim checks; manual p. 21 | Swimming skill + encumbrance gated sink timer. | Absent — planned Survival owner over ruleset rates |
| Fall/lava injury | Manual pp. 20–21 (≤2 ft free, jump/fly above; lava damages) | Height and terrain damage rules. | Absent — planned movement eligibility + costs |
| Camera/look | Godot `playerdatcamera.cs`; OU locked-cursor look | Free look with head tilt. | Engine-owned (camera/view) + Absent (tilt policy) |

## 3. Combat

| Feature | Donor code ref | Description | rusty-underworld coverage |
|---|---|---|---|
| Charge execution | Godot `src/interaction/combat/` (5 partials); OU `WeaponBase.cs` + charge-cursor feedback | Hold builds charge (Power Gem red/yellow/green), release resolves; aim locks at press. | Absent — planned Kit Combat attack state + ruleset formulas |
| Swing kinds | Manual p. 22 (bash/slash/thrust by press position; unarmed jab) | Position-selected swing with per-weapon damage variance. | Absent — planned attack execution input mapping |
| Accuracy/damage | Godot combat partials; OU data-row missiles, full-range rolls, 2/3/5 soaks | Skill×weapon×charge×range resolution; armor coverage per part. | Absent — planned ruleset formulas over content rows |
| Missile combat | Godot `combat_missile.cs`; OU `RangedWeapon.cs`, `Projectile.cs` | Bow/crossbow/sling + ammo, thrown objects, shared damage path. | Absent — planned reuse of the same resolution + loot flow |
| Weapon readiness/speed | OU windup-by-weapon + universal raise (~2:1) | Per-weapon timing. | Absent — planned readiness/cooldown state |
| Crits | OU double-damage-half-the-time; punch-from-Unarmed | Critical and unarmed rules. | Absent — planned ruleset policy |
| Enemy retaliation | Godot `src/npc/` (7); OU weighted 3-attack choice, charge-scaled damage | Senses, pursuit, strikes, retreat. | Absent — planned Kit AI coordination + ruleset policy |
| Corpse flow | Godot corpse/object handling; manual p. 14 (loot drops) | Death → searchable persistent corpse. | Absent — planned corpse/loot machinery |

## 4. Magic and effects

| Feature | Donor code ref | Description | rusty-underworld coverage |
|---|---|---|---|
| Spell dispatch | Godot `src/magic/spellcasting.cs` — majors 0–14 | Major/minor class routing (heal, projectile, area, targeted, summon, curse, mana, misc). | Absent — planned ruleset per-major policy (UW1 branches only) |
| Rune table | Godot `src/magic/runicmagic.cs` — `SpellList`, `PendingSpellCost` | 24 runes, shelf state, sequence→spell mapping. | Absent — planned content catalog + Kit casting workflow |
| Cast gates | Manual p. 26 (3×Circle mana, level/2, recast delay); OU skill+5 vs 2×circle, full mana | Eligibility, cost, reliability, backfire. | Absent — planned casting validation over tuning |
| Active effects | Godot `spellcasting_activeeffects.cs` (major/minor/stability) | Max 3 concurrent, stable/unstable time, dispel, over-sleep fading. | Absent — planned Kit effect instances with game-time duration |
| Item-borne casting | Godot `MagicEnchantment.cs`, `spellcasting_objects.cs` (UW1 187–188) | Scrolls/wands/potions/worn enchantments + Lore ID. | Absent — planned item behaviors reusing resolution |
| Spell corpus | Manual pp. 28–29 (40 spells) | Per-spell formulas and behaviors. | Absent — planned content records; see `coverage/magic-inventory.md` |

## 5. Objects, inventory, encumbrance

| Feature | Donor code ref | Description | rusty-underworld coverage |
|---|---|---|---|
| Object master table | Godot `src/loaders/objectdatloader.cs` + `src/objectdata/` (9 classes) | Per-type offsets (weapon/armour/ranged/food/lights/containers/triggers/critters/animation). | Absent — planned Import normalization into packs |
| Object behaviors | Godot `src/objects/` (~70); OU `UUObject` subclasses | Per-item use/combine/repair/read logic. | Absent — planned ruleset interpretation over content defs |
| Interaction verbs | Godot `look.cs`, `talk.cs`, `pickup.cs`, `use.cs` (MajorClass2–7), `useon.cs`; OU `Interaction.cs` | Look/get/use/use-on/talk with mode keys. | Absent — planned Kit Interaction targets + workflow |
| Inventory/paperdoll | Godot `playerdatinventory.cs` (linked slots, `CanCarryWeight`); OU `Inventory.cs` | Wear slots, shoulder slots, rings-only-when-worn, bags nesting, rune-bag lock-in. | Absent — planned Kit Objects owner |
| Stacks/quantities | Manual p. 10 (type+quality stacks, how-many dialog); OU `HowMany.cs` | Stack identity and split/merge. | Absent — planned object state transitions |
| Weight/encumbrance | Godot partial; OU stones capacity | Stones capacity, movement/jump/swim penalties. | Absent — planned Avatar encumbrance + movement costs |
| Theft/attitude acts | Godot `thief.cs`; OU demands/barter | Stealing and threats with lasting consequences. | Absent — planned interaction outcomes feeding attitude |
| Repair | Godot `repair.cs`; OU anvil rules (toughness table, ruin risk, time cost) | Self (anvil) and NPC repair. | Absent — planned ruleset policy + transaction |

## 6. Dungeon, levels, persistence

| Feature | Donor code ref | Description | rusty-underworld coverage |
|---|---|---|---|
| Tile map admission | Godot `src/World/tilemap.cs` (`lev_ark_block`, `tex_ark_block`, overlays) | Levels + object lists + texture map; UW1 Int16-count/Int32 offsets. | Absent — planned Import + Kit World admission |
| Tile geometry | Godot `tilemaprender.cs` (64×64 @1.2 m, 32 heights @0.15 m; SOLID/OPEN/DIAG) | Tile constants and scale. | Absent — planned import geometry; Engine-owned presentation |
| Textures | Godot `textureloader.cs` (W64/F32, split 210) | Wall/floor texture rows. | Absent — planned importer output as Engine content |
| Lighting/shading | Godot palette lighting + cycling (`PALS/SHADES/LIGHT.DAT`); OU torch/eye-glow ranges | Palette tables, light ranges. | Absent — planned media + Engine rendering |
| Level transitions/persist
...[truncated 6151 chars]