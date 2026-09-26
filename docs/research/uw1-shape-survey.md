# UW1 system shapes (donor survey)

The shape of Ultima Underworld 1's systems as the two donor recreations express
them, for our own composition. This is reference about the donors and the shipped
data, not about this repository's progress. Donor paths are relative to
`/home/research/old-games/UnderworldGodot` (Godot 4 + C#, UW1+UW2; primary) and
`/home/research/old-games/OpenUnderground` (Unity, UW1; secondary); game data is
`/home/research/old-games/game-uu1` (the install tree is partial; content is in
the `game.gog` ISO: `UW/{DATA,CRIT,CUTS,SOUND}` plus a `UW2/` mirror that is never
an extraction source).

Neither donor's engine topology is ours to imitate: Godot scenes, nodes, shaders,
`Peaky.Coroutines`, Unity MonoBehaviours, prefabs, `Resources` loading, colliders
and animators are platform, not game. Their archive and table loaders are importer
material; no donor file layout belongs in runtime types. Two independent findings
are strong evidence of the game's own shape rather than donor invention: both
recreations implement the same conversation bytecode VM, and the same four-outcome
skill test with identical thresholds.

## World and levels

`src/World/tilemap.cs` (`UWTileMap`: 64x64 tiles, `uwObject[1024]` per level, per
tile an object-chain head; 9 levels), `src/loaders/levarkloader.cs`,
`src/utility/teleportation.cs` (level change); OpenUnderground
`Assets/Game/Scripts/LevelLoader.cs`, `Tile.cs`. A level is a tile grid plus a
fixed 1,024-slot object table; the level you leave stays in memory **with its
mutations**, and a transition activates the target level, places the avatar and
runs leave-then-enter events. Preserve: tile grid, per-level object table with
stable index identity, per-tile chain head, level persistence. Simplify: archive
block layout and texture conventions.

## Objects, chains and links

`src/World/uwobject.cs` (`item_id` decomposes as `majorclass = id>>6`,
`minorclass = (id&0x30)>>4`, `classindex = id&0xF`; fields `next`, `link`,
`owner`, `quality`, flags), `src/utility/ObjectCreator.cs` (tile insertion),
`src/utility/objectsearch.cs`; OpenUnderground `UUObject.cs`
(`chainIndex`, `special`, `ownerIndex`). One record per object with **two
orthogonal pointers**: a sibling pointer and a link pointer whose meaning depends
on the type — container contents head, a door's lock, a trigger's trap, an
object's use-trigger. `owner` is the faction that decides theft and key identity.
This one shape explains containers, inventories, doors, traps and the world side
of conversations.

## Containers and inventories

`src/objects/container.cs` (contents = the container's link chain; use in
inventory browses, use in world spills, locked containers refuse),
`src/player/playerdatinventory.cs` (carried weight and maximum),
`src/savegame/PlayerDatWriter.cs` (equipment slots, backpack roots, nested
chains); OpenUnderground `LevelLoader.cs` (`FillContainer`), `Inventory.cs`
(12 paperdoll slots, an 8-slot trade tray). Containment is the same records with
an owner; weight gates pickup. Preserve: contents as a chain of the same records,
nesting, named equipment slots, weight. Our memory model need not be a linked
list, but containment and identity must survive save and load.

## Doors, locks, keys, lockpicking

`src/objects/door.cs`, `a_lock.cs` (locked is one bit on a separate lock record
reached through the door's link; `KeyIndex = link & 0x3F`), `doorkey.cs`,
`lockpick.cs` (`SkillCheck(PickLock + 1, difficulty)`; a critical failure can
destroy the pick), `src/interaction/useon.cs`; OpenUnderground `Lockable.cs`,
`Door.cs`, `Chest.cs`, `Key.cs`. Unlocking is both a state change **and an event**
that runs whatever the lock is linked to, which is why opening a door can fire a
trap. Preserve: the separate lock record, the key id, the skill test and the
event on unlock.

## Traps and triggers

`src/triggers/trigger.cs` (`RunTrigger` fires on a matching type or `ALL`; the
trap is the trigger's link; a one-shot trigger is removed after firing; a link
chain of triggers continues through `next`), `a_pressure_trigger.cs`,
`an_open_trigger.cs`, `a_move_trigger.cs`; `src/traps/trap.cs` (major-class
dispatch, then continues to the next trap or trigger), `hack_trap.cs` and the
`a_*_trap.cs` family; `src/objectdata/triggerobjectdat.cs` (trigger types:
move, step-on, pickup, use, look, enter/unlock, pressure/open, close, timer,
scheduled, exit, pressure-release). OpenUnderground `Trigger.cs`, `Trap.cs`.
A trigger is a placed object watching one condition; firing walks its chain —
linked triggers re-test, linked traps execute. A trap is a parameterised effect
(damage, teleport, terrain change, spawn or delete object, door, spell, variable
arithmetic, inventory) that mutates world state and may chain onward. Preserve
the graph shape; collapse the ~60 concrete trap classes into a small effects
vocabulary with authored parameters. UW1 has **no general event scheduler**:
`SCD.ARK` and `src/scd/*` are UW2-only, and time-based change in UW1 rides xclock
counters (`src/traps/a_hack_trap_castleschedule.cs` moves NPCs by time of day).
A scheduler for UW1 would be an invented mechanism.

## NPCs

`src/World/uwobject.cs` (mobile fields `npc_goal`, `npc_gtarg`, `npc_attitude`,
`npc_home`, `npc_hunger`, `npc_level`, `npc_talkedto`, `npc_whoami`, `npc_hp`),
`src/npc/npc.cs` (16 named goals), `src/npc/npcai.cs` (goal switch ending in a
shared move step), `pathfind.cs`, `src/interaction/thief.cs` (theft lowers
attitude), `npcdeath.cs`; OpenUnderground `Critter.cs` (13 goals, four attitudes,
movement types). An NPC is a mobile record holding its own AI state, with a
durable identity (`whoami`) separate from its runtime index: identity, not index,
selects the conversation, the special death and the schedule entry. Preserve AI
state on the entity, a small goal enum over one movement step, durable identity,
four attitude levels, home tile. Simplify goal numbering and authored special
cases.

## Conversation

`src/conversation/conversationvm.cs` and `conversationvmopcodes.cs` (42 opcodes),
`conversationimports.cs`, `src/loaders/cnvarkloader.cs`,
`conversation_functions/*` (about 57 imports: menus, typed input, quest
variables, inventory, teleports, attitude, barter, experience),
`conversationinitialisation.cs` (conversation number = `whoami`, else
`256 + item_id - 64`; `whoami == 255` answers nothing); OpenUnderground
`Conversations.cs` implements the same VM. A conversation is a program per
conversation id: code, private globals, an import table naming host functions,
and a stack machine that suspends on `say`/`respond`/ask. Preserve the shape — a
script that can inspect and mutate avatar, NPC, world and quest state, with menus,
typed input and suspend/resume. Do not reproduce the opcode set or the archive
format.

## Barter

`src/conversation/conversation_functions/conversationtrade.cs` (threshold,
patience, appraisal inaccuracy, like/dislike classes), `find_barter*.cs`,
`do_offer.cs`, `do_demand.cs`; OpenUnderground `Critter.cs` (tray setup,
stealing from the tray), `Conversations.cs` (the offer/demand verdict).
Trade is a sub-session inside a conversation: both sides have a tray, and an
evaluate import returns a verdict the dialogue reacts to. Preserve two trays, the
verdict, per-NPC threshold and patience, appraisal error, theft consequence.

## Combat

`src/interaction/combat/combat_globals.cs` (stages), `combat_input.cs` (hold to
charge; per-weapon charge rate and minimum), `combat.cs` (one skill test,
critical, one of four body locations, locational protection reduces the attack
roll and armour soaks damage), `combat_missile.cs`, `damage.cs`,
`src/player/playerdatcombat.cs` and `playerdatloop.cs` (armour values per
location), `playerdatdeath.cs`, `npcdeath.cs`, `npcloot.cs`; OpenUnderground
`WeaponBase.cs`, `RangedWeapon.cs`, `Projectile.cs`, `Armour.cs`, `Critter.cs`
(corpse with contents, loot tables). Preserve: charge while held, release above
the minimum, per-weapon charge profile, one test per swing, four hit locations
with per-location armour, a separate missile path, corpses as searchable world
objects holding loot.

## Magic

`src/magic/runicmagic.cs` (spell table, rune sequences, circle), `spellcasting.cs`
(major-class dispatch), `spellcasting_activeeffects.cs`,
`src/player/playerdatmagic.cs` (runes owned, up to three on the shelf),
`src/ui/uimanager_runes.cs`, `src/objects/runestone.cs` (item ids 232-255),
`playerdatstatus.cs` (three effect slots with a stability counter); OpenUnderground
`Magic.cs` (spell table, three-rune shelf, casting test `SkillCheck(Casting + 5,
2*circle)`, level gate), `MagicSaveData`. Casting = match the shelf, one skill
test, spend mana, then an effect, a projectile or a targeted prompt; enchanted
items cast from charge. Preserve runes as collectible gates, the three-rune shelf,
one test per cast, mana, few concurrent timed effects with stability.

## Survival

`src/player/playerdatloop.cs` (one-second accumulator; periodic meters, effect
stability decay, regeneration as periodic skill checks, hunger and fatigue on
five-minute steps), `playerdatstatus.cs` (hunger, fatigue, poison, intoxication,
light level, swim counter, levitation and flight), `src/physics/motion_player.cs`
(fall damage mitigated by a skill; drowning counter), `src/World/sleep.cs`
(bedroll, bed, drunk; interrupted by hostile awareness), `src/objects/light.cs`,
`src/objectdata/lightsourceobjectdat.cs`; OpenUnderground `PlayerData.cs`,
`PlayerObject.cs` (hunger, drowning), `Bedroll.cs`, `LightSource.cs` (fuel
countdown). One clock advances continuous meters; discrete actions (sleep,
incense) jump that clock; regeneration is a periodic skill check, not a constant
rate; light is carried-item state with fuel and it gates perception. OpenUnderground's
sprint stamina is a declared modern convenience, not UW1 fatigue.

## Skills

`src/player/playerdatskills.cs` (`SkillCheck(skill, target)`: `skill - target +
rand(0..30)`, with critical failure at 2 or less, failure at 15, success at 28,
critical success above; governing attribute per skill; advancement by use);
OpenUnderground `Skills.cs` (same roll and thresholds, citing the executable
offset, and mana ceiling `(Mana + 1) * INT / 8`). Every uncertain action is one
shared four-outcome roll; attributes govern advancement rather than the roll.
The donors agree exactly, so the thresholds are cheap tuning rather than
call-site constants.

## Automap

`src/World/automap.cs` (per level, a 64x64 "observed" grid sourced from the level
archive), `automapnotes.cs`, `automaprender.cs`; OpenUnderground `MapScreen.cs`
(per-level observed flags plus notes, run-length encoded in its save),
`Map.cs`. The automap is avatar knowledge: revealed as the avatar sees tiles,
independent of live geometry, and persisted with the game.

## Quest and scenario state

`src/player/playerdatquest.cs` (32 one-bit quest flags, six byte-sized ones,
xclock counters, world-visited flags), `get_quest.cs`/`set_quest.cs`,
`src/conversation/bglobal.cs` (per-conversation globals from `BABGLOBS.DAT`),
`src/traps/a_check_variable_trap.cs` and `a_set_variable_trap.cs` (the same
variable space from traps), `src/World/specificworlds/*.cs` (per-level scenario
code); OpenUnderground `PlayerData.cs` (quest flags and global variables with
named constants at the original indices), `Conversations.cs`. Scenario state is a
global numeric space writable from conversation scripts **and** trap effects,
plus per-conversation private globals, event counters and per-level flags.

## Save contents

`src/savegame/SaveGame.cs`, `PlayerDatWriter.cs`, `BGlobalWriter.cs`,
`LevArkWriter.cs`, `ScdArkWriter.cs` (UW2 only), `docs/save-architecture.md`;
OpenUnderground `SaveGameData.cs` and `SaveGameManager.cs`. What the game
considers necessary to persist: the avatar (identity, attributes, skills,
experience, hit points and mana, hunger, fatigue, poison, intoxication, light
level, level and position and heading, equipment, carried items, item in hand);
magic (runes known, runes on the shelf, active effects with remaining stability);
**every visited level** as its full object set with per-object position, chain
membership, quality, owner, flags, contents, and for mobiles hit points, goal,
attitude, identity and whether it is dead; per-level automap knowledge and notes;
quest variables, per-conversation globals, event counters and world-visited
flags; and in-game time and play time. Ignore the donors' file layouts,
encryption, DOS-canonical ordering and slot tricks. The per-level world diff is
the part easiest to under-build, and the persisted set is the game's own answer
to what matters.

## The player's verb set

`src/ui/uimanager_interaction.cs` (modes options, talk, pickup, look, attack,
use), `src/interaction/use.cs` and `useon.cs` (using a selected item on a
target), `pickup.cs`, `look.cs`, `talk.cs`, `src/ui/uimanager_runes.cs` (cast);
OpenUnderground `UUObject.cs` (`Look`, `Use`, `Pickup`, `Open`, `Unlock`,
`Trigger`), `Interaction.cs` (ray to the centred object). Verbs are modes over a
centred target: look (which also searches), talk, take, drop and throw, use,
**use this on that**, attack by charge and release, cast from the shelf, plus
panel verbs (inventory, runebag, map, stats, sleep, options). Use-on pairing is
what makes the object simulation feel deep; preserving the verb list and the
mode-over-target structure is cheap.

## Unverified

| Item | Status |
| --- | --- |
| Timer and scheduled triggers in UW1 | The types exist in the trigger table, but
`src/World/timers.cs` returns unless the game is UW2 and no UW1 caller of the UW2 script processor
was found. |
| A shield's armour contribution | UW's armour initialisation reads five non-shield slots;
OpenUnderground's README claims a shield counts twice. |
| Rune count and alphabet | 24 slots in UW's rune UI against 25 letter positions in
OpenUnderground. |
| Automap page count in UW1 | One grid per level appears to be the case; OpenUnderground saves
several pages. |
| Multi-trigger link chaining | Both agree on simple trigger-to-trap chains; UW walks `next` within
a link chain where OpenUnderground takes one link hop. |
| OpenUnderground licensing | No license file in the checkout; do not assume reuse terms. |
