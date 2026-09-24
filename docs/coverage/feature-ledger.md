# Feature ledger

Stable IDs for every feature-map row, with disposition and remaining behavior.
`implement` = future task material (nothing exists yet); `adapt` = keep the
role, ours the shape; `exclude` = explicitly out with rationale; `engine` =
upstream capability consumed, not built. Coverage-plan areas in brackets.

## F001–F011 · avatar and movement [A2, A5, A6]

| ID | Feature (map §) | Disposition | Remaining behavior |
| --- | --- | --- | --- |
| F001 | Attribute container (§1) | implement | ST/DX/INT + derived maxima; Kit Avatar owner |
| F002 | Skill container (§1) | implement | 20 skills, use-driven gains, mantra sources |
| F003 | XP and level (§1) | implement | Explore/kill/deed XP, auto-16, HP/mana recalc |
| F004 | Character creation (§1) | implement | 8 ordered choices, 8 classes, validation |
| F005 | Difficulty (§1) | implement | Creation-locked scalar in tuning |
| F006 | Shrine advancement (§1) | implement | Ankh mantra effects (3 group + hidden singles) |
| F007 | Locomotion state (§2) | implement | Walk/run/jump/swim/fly over Engine spatial |
| F008 | Collision (§2) | implement + engine | Tile/object collision via Engine; slot-1 rule as import note |
| F009 | Drown timer (§2) | implement | Skill + encumbrance gated timer |
| F010 | Fall/lava injury (§2) | implement | Height/terrain damage rules |
| F011 | Camera/look (§2) | adapt + engine | Engine camera; tilt policy ours |

## F012–F019 · combat [A6]

| ID | Feature | Disposition | Remaining behavior |
| --- | --- | --- | --- |
| F012 | Charge execution | implement | Readiness/charge/release/impact/cooldown state |
| F013 | Swing kinds | implement | Press-position mapping, per-weapon variance |
| F014 | Accuracy/damage | implement | Skill×weapon×charge×range; coverage-soak 2/3/5 |
| F015 | Missile combat | implement | Ammo dispatch, shared resolution path |
| F016 | Weapon readiness/speed | implement | Per-weapon timing + universal raise |
| F017 | Crits | implement | Double-damage rule; punch-from-Unarmed |
| F018 | Enemy retaliation | implement | Senses, pursuit, weighted attacks, retreat |
| F019 | Corpse flow | implement | Persistent searchable corpses |

## F020–F025 · magic foundations [A7]

| ID | Feature | Disposition | Remaining behavior |
| --- | --- | --- | --- |
| F020 | Spell dispatch | implement | UW1 major/minor policy (UW2 branches excluded) |
| F021 | Rune table | implement | 24 runes, shelf state, sequence mapping |
| F022 | Cast gates | implement | 3×Circle mana, level/2, delay, backfire |
| F023 | Active effects | implement | Max-3, stable/unstable, dispel, sleep-fade |
| F024 | Item-borne casting | implement | Scrolls/wands/potions/worn + Lore gate |
| F025 | Spell corpus | implement | 40 content records; see `magic-inventory.md` |

## F026–F033 · objects [A5]

| ID | Feature | Disposition | Remaining behavior |
| --- | --- | --- | --- |
| F026 | Object master table | implement | Import normalization into packs |
| F027 | Object behaviors | implement | Per-family interpretation over content |
| F028 | Interaction verbs | implement | Look/get/use/use-on/talk + Default-Mode detect |
| F029 | Inventory/paperdoll | implement | Slots, bags, rune-bag lock-in |
| F030 | Stacks/quantities | implement | Type+quality identity, how-many flow |
| F031 | Weight/encumbrance | implement | Stones capacity + movement penalties |
| F032 | Theft/attitude acts | implement | Outcomes feeding attitude policy |
| F033 | Repair | implement | Anvil difficulty/ruin/time + NPC path |

## F034–F039 · dungeon [A4]

| ID | Feature | Disposition | Remaining behavior |
| --- | --- | --- | --- |
| F034 | Tile map admission | implement | LEV.ARK blocks, per-level runtime state |
| F035 | Tile geometry | implement | Import geometry; Engine-owned presentation |
| F036 | Textures | implement | Imported rows as Engine content |
| F037 | Lighting/shading | implement | Palette tables + ranges; Engine rendering |
| F038 | Level transitions | implement | Edges with cost; cross-side save capture |
| F039 | Secrets | implement | Search-gated discovery into knowledge |

## F040–F044 · conversation [A9]

| ID | Feature | Disposition | Remaining behavior |
| --- | --- | --- | --- |
| F040 | Conversation VM | implement | Ruleset-owned 42-opcode interpretation |
| F041 | Imported functions | implement | Ask/menus, inventory, quest-var, world effects |
| F042 | Barter/appraisal | implement | Real-inventory trade + Appraise reading |
| F043 | Attitude/memory | implement | Treatment memory, demands, violence |
| F044 | Doors/moongates | implement | Stateful doors; gate travel endpoints |

## F045–F050 · traps [A4, A11]

| ID | Feature | Disposition | Remaining behavior |
| --- | --- | --- | --- |
| F045 | Vanilla dispatch | implement | UW1-reachable 6-0 set + 6-1-0 text |
| F046 | Do/hack family | implement | UW1 qualities; UW2-only as documentation |
| F047 | Trigger codes | implement | UW1 code paths; splits resolved at import |
| F048 | Wall controls | implement | Wall-face addressing; blocked-door reopen |
| F049 | SCD/xclock note | exclude | UW2 engine; UW1 vars live in A11 tasks |
| F050 | Object physics | implement | Shove/block/fly/land over Engine physics |

## F051–F055 · UI [A5, A7–A9]

| ID | Feature | Disposition | Remaining behavior |
| --- | --- | --- | --- |
| F051 | HUD | adapt | Projection + charge/compass/flask semantics |
| F052 | Panels | adapt | Screen values + semantic actions |
| F053 | Runebag/casting UI | adapt | Shelf state, red/blue cursors, save-carry |
| F054 | Conversation UI | adapt | Options, barter areas, paging, farewell |
| F055 | Map UI | adapt | Coverage, quill notes, level pages |

## F056–F059 · audio [A12]

| ID | Feature | Disposition | Remaining behavior |
| --- | --- | --- | --- |
| F056 | XMI music | exclude synth / implement playback | No synth port; ordinary files over Engine Audio |
| F057 | VOC speech | implement | Admitted speech files |
| F058 | Track slots | implement | Exploring/combat/warning/victory/automap logic |
| F059 | SFX routing | implement | ID-based routing policy over Engine Audio |

## F060–F065 · saves [A2]

| ID | Feature | Disposition | Remaining behavior |
| --- | --- | --- | --- |
| F060 | Write path | implement | Current-schema snapshot/restore |
| F061 | PLAYER.DAT layout | implement as orientation | Informs schema; no format code in runtime |
| F062 | LEV.ARK writing | implement as orientation | Visited-level capture design |
| F063 | Variables | implement | Quest/game vars, xclocks, moonstone, visited |
| F064 | DESC text | implement | Plain description entry |
| F065 | Autosave/quicksave | adapt | Per-level autosave + rolling slots (ours) |

## F066–F071 · bootstrap and options [A12]

| ID | Feature | Disposition | Remaining behavior |
| --- | --- | --- | --- |
| F066 | Bootstrap | exclude | Host/ruleset composition instead |
| F067 | Corruption guards | exclude | Import-time validation instead |
| F068 | Cheat/debug | exclude | Creation flow + diagnostics instead |
| F069 | Control bindings | adapt | Donor-verified bindings over Engine input |
| F070 | Options | adapt | Toggles/detail/pause semantics |
| F071 | Portraits | adapt | Attributed art direction, not asset ports |

## Corpus IDs (detail inventories)

| ID | Corpus | Disposition |
| --- | --- | --- |
| M-01…M-40 | 40 spells by circle (`magic-inventory.md` §1) | implement as content + ruleset policy |
| R-01…R-24 | 24 runes + gates (`magic-inventory.md` §§2–3) | implement as content + casting workflow |
| S-01…S-20 | 20 skills (`magic-inventory.md` §4) | implement as content + advancement |
| T-V00…V0F | Vanilla 6-0 dispatch (`trap-inventory.md` §1a) | implement UW1-reachable; UW2 branches excluded |
| T-W0…W8 | Minorclass-1 (`trap-inventory.md` §1b) | implement W0; rest excluded (UW2-gated) |
| T-D* | Do/hack qualities (`trap-inventory.md` §2) | implement UW1 qualities; UW2-only excluded |
| C-01…C-12 | Source families (`content-scope.md`) | implement per family; UW2/UNDEROM1/donor-data excluded |
