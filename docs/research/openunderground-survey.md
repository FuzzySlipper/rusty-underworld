# OpenUnderground survey

Source: `/home/research/old-games/OpenUnderground` ("Unity Underground").
Surveyed from README.md, CHANGELOG.md, CONTROLS.md, `Assets/Game/Scripts/*.cs`
(167 files), `Assets/Game/*`, `Assets/StreamingAssets`, `Packages/*`,
`ProjectSettings/ProjectVersion.txt`, `.gitattributes`/`.gitignore`. No LICENSE
or COPYING file exists in this checkout — do not assume reuse terms. Nothing
was written to the surveyed repo.

## 1. What it is (README.md)

- Unity wrapper for Ultima Underworld 1 data files, with gamepad-driven or
  mouse-and-keyboard input and 3D models for objects and critters.
- Stated goal: "gameplay-accurate while adding some modern conveniences":
  full audio, 3D critter/object models, autosave at each Abyss level start +
  five rolling quicksaves (BG3 style), better lighting and particles, tidied
  portraits, achievements, gamepad support.
- Inventory ratings: attack + damage (weapon hand), defence + armour (shield
  hand). Armour rating counts each piece once per covered body part (shield
  counts twice, helmet once).
- Clearer attack feedback: the charge cursor stays dark until a release would
  land, then lights up and fills.
- Sprinting bound to the Acrobat skill, with a warning that dashing can make
  the player miss secret details.
- Bug reports as GitHub issues (a save from just before, and where the bug
  leaves a mark, just after); discussion on the itch.io page.
- Built with Unity 6000.0.68f1. Root scene is `GameDirectoryDialog`; after one
  run the `World` scene can be opened directly.
- Store-bought assets were removed before pushing: some interface-click audio,
  mostly water-splash particles, minor textures and models. Expect sparse sound
  and possible pink textures until replaced.

## 2. Data-files requirement

- Requires data files from a GOG installation of the original game, searched in
  standard locations; the chosen root persists via the PlayerPrefs key
  `GameDataRootPath` (see `GameDataPath.cs`).
- ISO case: `game.gog` is extracted once and reused into a `LooseData` folder
  under the user save-data path (e.g. AppData\LocalLow on Windows), not next
  to the GOG install.
- Optional UW2 audio: `game.gog` is a plain ISO9660 image that 7-Zip opens;
  tracks live under `UW2\SOUND`. Copy the `UWA` + `UWR` versions of the wanted
  tracks (01, 05, 11–17, 30 recommended) into the `LooseData` sound folder.
- Portable UW1 knowledge: archive names, sound/track layout, UWA/UWR pairs.
  Unity-side topology: the `GameDirectoryDialog` scene + `GameDataPath` +
  `LooseData` cache handling.

## 3. CHANGELOG — gameplay-accuracy restores

Current-release section only (tail entry: first public release). These are
behavior facts about the original, established by fixing divergences:

- New characters start at level 1 with one skill point (not leaked level/HP/XP
  or three points).
- Armour coverage counts per body part; shield spells moved from armour into
  defence. Resist Blows / Thick Skin / Iron Flesh soak damage (2 / 3 / 5), not
  to-hit; strongest wins, never stacks.
- Failed repair destroys the item and costs in-game time; the anvil reads the
  weapon-toughness table; bows/slings never wear and are not offered for
  repair; only full-quality items are refused.
- Enchantments: *of Protection* = harder to hit on the covered part (10 placed
  pieces); *of Toughness* = damage soak on the covered part (8 pieces). One
  body-part roll per blow; creatures wear their own data armour.
- Missiles use their own data rows; Missile skill multiplies damage; no skill
  damage bonus on spells/arrows; no level bonus on player projectiles.
- Spell roll = skill + 5 vs twice circle; spells always cost full mana.
- Weapon windup depends on the weapon plus a universal opening raise (~2:1
  heaviest:lightest). Max HP grows with level per the original formula. All
  skill rolls use the full original range. Crits double damage half the time.
- Punch damage comes from Unarmed. Creature attacks are chosen weighted from
  three, with damage scaling on charge. Create Food makes 7 equal fresh foods.
- Shrine group mantras: SUMM RA advances 3 skills, OM CAH 4; no phantom
  improvements; MU AHM favours low Mana; no bonus point on Strength skills;
  mana ceiling uses the game formula.
- Eye-glow limited to own-light range + ~2 paces. Doors reopen when blocked
  (fixes a level-4 tomb-room softlock). Victory fanfare finishes before fight
  music resumes. Combat vs warning music states split on hearing vs swinging.
- Runes on the shelf survive save/reload. Smith-paid repair actually repairs.
  The level-6 jeweled sword no longer spawns in lava.

## 4. CHANGELOG — modern conveniences (OURS, not original behavior)

- F5 / hold-R3 quicksave; five rolling slots; autosave on first entering each
  level; newest-first save/load lists; Journey Onward preselects most recent.
- Pause on focus loss (hunger/fatigue/poison/creatures used to run during
  Alt-Tab). Sprint wind/recovery scales with Acrobat. Auto-jump fires on press
  with gap-edge assist.
- Inventory weapon hand shows attack + max damage; unidentified enchantment
  bonuses hidden until a Lore roll names them. Keys carry found-level in their
  names. Charge cursor lights only when a release would land.

## 5. Controls

Gamepad (Xbox names; left-handed swaps attack/stats triggers): left stick move,
right stick look, L3 sprint (forward, stamina), A jump, X examine, Y use/pick
up on yellow, B cancel charge / clear cursor, hold-release attack trigger =
charge + swing, LB magic, RB inventory, non-attack trigger stats, Select map
(needs map item), Start save/load/options, hold R3 quicksave. Inventory, magic,
map, how-many, flute, conversation, and on-screen-keyboard sub-bindings are
spelled out in `CONTROLS.md` — consult it when planning input tasks.

Keyboard/mouse: WASD/arrows + locked-cursor look, Left Shift sprint, Space
jump; left-click examine, left hold/drag use/pick up, right hold-release
charge + swing, X cancel; Q magic, E inventory, R stats, Tab map, Esc panels
then save/load/options; F5 quicksave; rune typing A–W + Y; Enter/C cast;
1–9 conversation replies; left-click cutscene advance, hold right-click skip.

## 6. Scripts — core systems (one line each)

- `DataLoader.cs` — original-data loading MonoBehaviour (palette; static
  `sDataLoader`). `CritterLoader.cs` — critter sprite/slot/frame tables.
- `Conversations.cs` + `ConversationDecompiler.cs` — conversation runtime +
  import records. `CutscenePlayer.cs` — cutscene event/page player.
- `Magic.cs` — spell definitions, active/permanent spells, `TryCast`, save/
  load. `Spell.cs` — in-world spell object hook.
- `WeaponBase.cs` / `Weapon.cs` / `RangedWeapon.cs` / `Fist.cs` — charge-state
  melee base, animated melee, ranged draw/spawn, unarmed. `Projectile.cs` —
  projectile flight/hit + save data.
- `Interaction.cs` — crosshair centered-object, click/hold timing.
- `Inventory.cs` — paperdoll/stuff-list/equip/carry. `Container.cs`,
  `Chest.cs` — containers.
- `PlayerObject.cs` + `PlayerData.cs`, `PlayerInput.cs`,
  `PlayerPanelState.cs`, `PlayerEffectsController.cs` — avatar slices.
- `LevelLoader.cs` / `LevelGeometry.cs` / `Tile.cs` / `UUTerrain.cs` — level
  build, wall faces, tiles/terrain.
- `StringLoader.cs`, `ObjectsData.cs`, `ObjectTypes.cs`, `ComObjProps.cs`,
  `ObjectModelMapping.cs` — strings, melee rows, object typing.
- `SaveGameManager.cs` / `SaveGameData.cs` / `SaveLoadGUI.cs` — compressed
  saves, autosave/quicksave lists, UI.
- `Critter.cs` (+ `CritterVariants/`, sounds, `CritterViewer.cs`) — creature
  runtime/HP/sound/variants.
- `Door.cs` / `Lockable.cs` / `Key.cs` — doors, locks, keys. `Trap.cs` /
  `Trigger.cs` — hazards/triggers. `SwitchBase.cs` / `Switch.cs` / `Lever.cs` /
  `RotaryLever.cs` / `Moongate.cs` — wall controls, moongates.
- `Shrine.cs` / `Anvil.cs` / `RepairDialog.cs` — mantra shrines, anvil repair.
- `Map.cs` / `MapScreen.cs` — automap data + screen. `Music.cs` (+ `XMIPlayer/`)
  — music states/rotation. `Flute.cs` — instrument. `Book.cs` / `Writing.cs` —
  books/inscriptions. `HowMany.cs` / `KeyboardGUI.cs` — quantity/text entry.
- `GameDataPath.cs`, `GameDirectoryDialog.cs`, input routing, `FrontEnd.cs`,
  `Logos.cs`, `Credits.cs`, `EndGame.cs`, `CreateCharacter.cs`.
- Dozens of small `UUObject` subclasses (Armour, Food, LightSource, RuneStone,
  SilverSeed, Wand, …) plus effects and debug utilities — full list in the
  donor checkout.

## 7. Asset/scene topology (Unity-side, NOT portable UW1 facts)

`Meshes/` (14 groups), `Prefabs/` (33 top entries including numbered FX and
critter folders), `Scenes/` (`World`, `GameDirectoryDialog`, `Logos`,
showcases/sims), `Sounds/` (52 top entries + Critters/Books/Cutscenes/Dreams/
Graves), `Textures/` (Portraits, Map, panels), `UI/` prompts, `Fonts/`,
`Shaders/`, `StreamingAssets/` (`Cutscenes/cs*.txt`, `PVS/`, `Soundfonts/`).
UW1 facts (damage rows, circles, mantras, item identity) live in data + loader/
script logic; Unity facts (which prefab/scene/material presents them) live here
— do not cite the latter as original-game behavior.

## 8. Packages / LFS

`ProjectSettings/ProjectVersion.txt`: 6000.0.68f1. Input System 1.18.0,
PostProcessing 3.5.0, Shader/VFX Graph 17.0.4. LFS required for character
textures (`.gitattributes` puts `*.tga` under LFS). `.gitignore` drops
Library/Temp/Obj/Builds/Logs/UserSettings plus generated art folders.
