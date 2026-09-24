# UnderworldGodot survey

Source: `/home/research/old-games/UnderworldGodot` (read-only survey; nothing
written). File-path claims were verified by listing and reading headers in that
pass; one-line loader semantics come from class and file-name evidence plus
header greps. Anything not directly confirmed is flagged "not verified".

## 1. README — overview, features, controls, settings

- Overview: engine recreation of Ultima Underworld 1 + 2 in Godot; supersedes
  the author's Unity-based project; rebooted to behave like the originals.
- Status: pre-alpha, "interactive map viewer with a lot of game logic"; requires
  UW1 or UW2 game files.
- Current features (README lines 50-104): map loading; animated doors/sprites;
  looping NPC animations; full NPC conversations; "almost all" triggers/traps
  (including some unused in the originals); barter; palette-based lighting
  (transparencies excepted); inventory including runebag (excluding
  drag-and-drop); pickup/drop without pickup-rule checks; usable switches;
  readables + all game strings; palette cycling/shading; save-file loading;
  stats/HP/mana/compass UI; 3D models; food; near-complete runic spell set with
  original logic; wands/scrolls/potions; partial small-window cutscenes; partial
  game/quest variables; level transitions + persistence; options menu
  (sound/music/low-detail); many object interactions; attack-charge + accuracy;
  missile combat; advancement over time (hunger/fatigue/mana regen); skill
  points; automap (both "read-only automap notes" and "adding and deleting
  automap notes" appear as stated); partial UW2 SCD.ARK events; near-complete
  cutscenes including scrolling bitmaps; sleep/dream logic; character creation;
  mostly-complete object physics; NPC AI/movement/pathfinding/combat; player
  movement/collision ("small amount of jank"); swimming; VOC speech; realtime
  XMI music via 4 synth engines (cm32l/mt32 via Munt.NET, soundfont via
  MeltySynth, opl via AdlMidi.NET); SFX; camera shake/color flash; partial
  saving / full loading; endgame screens; dragon UI; partial
  weight/encumbrance; splash/credits; stealth; death/resurrection cutscenes;
  sprite transparencies.
- Controls (README lines 195-245): W/Shift+W run/walk, S back, A/D strafe, Q/E
  turn, J/Shift+J jumps, R/F fly with levitate, 1/2/3 look, P/;/./mouse-hold
  attacks, F1 options (UW1), F2 talk, F3 pickup, F4 look, F5 attack, F6 use, F7
  panel, F8 cast, F9 track, F10 camp/sleep, F11 position label, Alt+F7 version,
  Alt+F8 dungeon/location, Alt+F12 screenshot, F12 debug-process SCD.ARK; cheat:
  tilde gives runestones + 30 mana + max mage skills.
- Settings (README lines 149-193 + `uwsettings.example.json`): config file in
  the Godot user-data dir; point `pathuw1`/`pathuw2` at the folder containing
  DATA/CRIT/CUTS/SOUND; `gametoload` UW1|UW2; `level`; `FOV` (clamped >= 50);
  `showcolliders`; `shaderbandsize`; `synth`/`synthpath` (see `docs/audio-*`).
  Naming wrinkle (not reconciled): README says `settings.json`, the music
  section says `uwsettings.json`, the repo holds `uwsettings.schema.json` +
  `uwsettings.example.json`.
- Music ROMs (README lines 255-266): cm32l/mt32 need user ROMs
  (CM32L_CONTROL/PCM, MT32_CONTROL/PCM, std + MAME names); silent fallback to
  OPL; synth change needs restart. AI policy: no AI-upscaled/generated assets.
  Credits/appendix cite uw-formats lineage, Ultima Codex internal formats,
  Aigner FAQs, Abysmal/Underworld-Adventures credits.

## 2. Project facts: license, .NET, UW1+UW2

- License: `LICENSE.md` — MIT, (c) 2023 hankmorgan.
- .NET: `Underworld.csproj` — `Godot.NET.Sdk/4.3.0`,
  `<TargetFramework>net9.0</TargetFramework>`, `AllowUnsafeBlocks`; packages
  Munt.NET 0.2.2, ADLMidi.NET 1.2.0, MeltySynth 2.4.1; `project.godot` app name
  "Underworld".
- UW1+UW2: `_RES` GAME_UW1/GAME_UW2/GAME_UWDEMO switches throughout
  loaders/tilemap/spells; `UWTileMap.NO_OF_LEVELS` = 1 demo / 9 UW1 / 80 UW2
  (`src/World/tilemap.cs`); UW2-only SCD.ARK confirmed by
  `src/savegame/SaveGame.cs` ("UW1 saves omit SCD.ARK"),
  `src/savegame/ScdArkWriter.cs` (16-block UW2 container; empty for UW1),
  `src/World/timers.cs` (early-return unless UW2).

## 3. src/loaders — which UW file each loads (27 files)

- `src/loaders/artloader.cs` — base `ArtLoader` (image buffer + palette no); no
  single game file (parent of BYT/GR/CUTS/critter loaders).
- `src/loaders/bytloader.cs` — `BytLoader : ArtLoader`; UI bitmap sets
  BLNKMAP/CHARGEN/CONV/MAIN/OPSCR/PRES1/PRES2/WIN1/WIN2 `.BYT` (constants
  verified; folder not verified).
- `src/loaders/cmbloader.cs` — `DATA/CMB.DAT` (string observed;
  paperdoll/combination data; touches equip/inventory logic).
- `src/loaders/cnvarkloader.cs` — `CNV.ARK` (`LoadCnvArkUW1`; conversation
  headers + imported-function table).
- `src/loaders/comobjloader.cs` — `DATA/COMOBJ.DAT` (code also contains
  "comboj.dat" spelling; common-object data).
- `src/loaders/critterloader.cs` — `CRIT/CR<octal-id>PAGE.N00|N01` (UW1 two
  pages) and `CRIT/CR<octal>.<page>` via PGMP table (UW2); critter sprite
  animations (paths verified in constructor).
- `src/loaders/cutsloader.cs` — `CUTS/<FILE>` cutscene bitmaps
  (`CutsLoader : ArtLoader`; single-frame + basePixels sprite/panorama-scroll
  modes).
- `src/loaders/dataloader.cs` — generic `DataLoader` (UWBlock I/O,
  `getAt`/`getValAtAddress`, `unpackUW2`); no single game file.
- `src/loaders/dldatloader.cs` — `DATA/DL.DAT` (string observed).
- `src/loaders/grloader.cs` — `DATA/*.GR` sprite/UI archives: 3DWIN, ANIMO,
  ARMOR_F/M, BODIES, BUTTONS, CHAINS, CHARHEAD, CHRBTNS, COMPASS, CONVERSE,
  CURSORS, DOORS, DRAGONS, EYES, FLASKS, GENHEAD, HEADS, INV, LFTI, OBJECTS,
  OPBTN, OPTB, OPTBTNS, PANELS, POWER, QUEST, SCRLEDGE, SPELLS, TMFLAT, TMOBJ,
  WEAPONS, GEMPT, GHED (+ `ALLPALS.DAT` aux palettes).
- `src/loaders/installationvalidator.cs` — install-usability checker; loads no
  game data itself.
- `src/loaders/levarkloader.cs` — `LEV.ARK` (DATA/ or SAVE*; UW1 Int16-count +
  Int32 offset table including `lev_ark_block`/`tex_ark_block`; UW2 separate
  header/lengths).
- `src/loaders/loader.cs` — base `Loader` (`BasePath`, `ReadStreamFile`); no
  game file.
- `src/loaders/modelloader.cs` — 3D models from `UW.EXE` / `UW2.EXE` (strings
  observed).
- `src/loaders/objectdatloader.cs` — `DATA/OBJECTS.DAT` (base `objectDat`
  buffer class).
- `src/loaders/paletteloader.cs` — `DATA/PALS.DAT` (8 palettes) +
  `DATA/LIGHT.DAT` + `DATA/MONO.DAT` (paths verified).
- `src/loaders/shadesdatloader.cs` — `SHADES.DAT` shading tables (a
  `System.Dat` string also appears nearby; role not verified).
- `src/loaders/sysfontloader.cs` — `.SYS` font files (parse only, no Godot
  types; headless-tested per docs).
- `src/loaders/sysfontbuilder.cs` — fills Godot `FontFile` from parsed `.SYS`
  (Godot-side).
- `src/loaders/sysfontprovider.cs` — loads all four fonts as a set or fails
  whole; owns metrics.
- `src/loaders/terraindatloader.cs` — `DATA/TERRAIN.DAT` / `DTERRAIN.DAT` (+
  `TerrainTypes.Gr` reference observed).
- `src/loaders/textureloader.cs` — wall/floor textures: UW1 `W64.TR`/`F32.TR`
  (demo `DW64.TR`/`DF32.TR`), UW2 `T64.TR`; floor/wall split index 210 (UW1) /
  48 (demo).
- `src/loaders/uwblock.cs` — `UWBlock` struct (.ark block: Data/Address/DataLen
  + UW2 compression flags); no file.
- `src/loaders/vocloader.cs` — `SOUND/*.VOC` speech → Godot `AudioStreamWav`
  (path `BasePath/SOUND/<file>` verified).
- `src/loaders/weaponloader.cs` — `WEAPONS.DAT` + `WEAPONS.GR`
  (`WEAP.DAT`/`WEAP.GR` strings also appear; exact split not verified).
- `src/loaders/xferloader.cs` — `XFER.DAT` (string observed; `palette.gr` also
  referenced nearby).
- `src/loaders/xmimusic.cs` — `XMIMusic` facade: theme number →
  `UWA<digit><digit>.XMI` theme files (exact MUSIC folder not verified in this
  pass).

## 4. src/player — playerdat partials (15 files) + chargen

All `playerdat : Loader` partials:

- `playerdat.cs` — core: XP (`ChangeExperience`, `AwardXPKill`), HP/mana-max/
  weight recalculation.
- `playerdatainit.cs` — `LoadPlayerDat(folder)` + `InitEmptyPlayer`; save→UI/
  camera wiring.
- `playerdatcamera.cs` — `PositionPlayerCamera`, `SetCameraViewValues` (the
  original engine could attach the camera to non-player mobiles).
- `playerdatclock.cs` — `AdvanceTime` (game clock).
- `playerdatcombat.cs` — combat helpers on player (header-only in this pass;
  specifics not verified).
- `playerdatdeath.cs` — `ResurrectAtSilverTree` (death/resurrection).
- `playerdatinventory.cs` — inventory linked-list slots (heads/offsets),
  backpack, add/remove, `CanCarryWeight`, `ClearInventory`.
- `playerdatloop.cs` — per-tick `PlayerTimedLoop`: HP/mana regen, Killorn/keep
  events, swim checks, light stability, automap update, stealth/sneak,
  footsteps, weapon-sheathing, damage resistance.
- `playerdatmagic.cs` — rune get/set + selected-rune state.
- `playerdatobject.cs` — `PlacePlayerInTile` (inserts player slot-1 at tile
  chain head for collision; save path detaches it for DOS compatibility).
- `playerdatpitsofcarnage.cs` — pit-fighter slots get/set.
- `playerdatquest.cs` — game vars, quest vars, xclocks (+increment), moonstone,
  world-visited flags.
- `playerdatskills.cs` — `SkillCheck`, attributes/skills get/set, HP/mana max,
  level lore, governing attribute, skill/group increases.
- `playerdatstatus.cs` — active spell effects (set/cancel/stability/class) +
  hunger change.
- `playerdatutil.cs` — `pdat` byte buffer + `Get/SetAt[16|32]`; `Load` reads
  `<folder>/PLAYER.DAT` (UW1 bytes 1..210 XOR per save docs).
- `player/chargen/chargen.cs` — character generation (`chargen_dat`/
  `skills_dat` buffers, class-skill rolls, staged UI flow; exact source .DAT
  names not verified).

## 5. src/conversation — VM + opcodes

- `conversationvm.cs` (710 lines) — `ConversationVM : UWClass`;
  `RunConversationVM(talker)` coroutine (Peaky.Coroutines) with
  `instrp`/`basep`/`stackptr`, temp-talker flag (talking doors/UW2 wisp),
  player-teleport request fields.
- `conversationvmopcodes.cs` (56 lines) — 42 opcodes `cnv_NOP`=0 …
  `cnv_OPNEG`=41 (arithmetic OPADD/OPMUL/OPSUB/OPDIV/OPMOD/OPOR/OPAND/OPNOT;
  tests TSTGT/TSTGE/TSTLT/TSTLE/TSTEQ/TSTNE; jumps JMP/BEQ/BNE/BRA/CALL/CALLI/
  RET; stack PUSHI/PUSHI_EFF/POP/SWAP/PUSHBP/POPBP/SPTOBP/BPTOSP/ADDSP;
  FETCHM/STO/OFFSET/START/SAVE_REG/PUSH_REG/STRCMP/EXIT_OP/SAY_OP/RESPOND_OP/
  OPNEG) + import markers `import_function`=0x111 / `import_variable`=0x10F
  and return types void/0x129 int/0x12B string.
- `conversation.cs` (288 lines) — `Conversation` data model;
  `conversationimports.cs` — `ConversationImports` (name, ID/address, import
  type, return type from CNV.ARK); `bglobal.cs` — babl globals
  (`BGLOBALS.DAT`-family; header comment says `bglobal.dat`/`BABGLOBS.DAT` —
  exact retail names not re-verified); `conversationinitialisation.cs`,
  `conversationinstructions.cs`, `conversationio.cs`, `conversationstack.cs` —
  VM support (init/decode/IO/stack; header-only in this pass).
- `conversation_functions/` (~57 files) — imported babl_* implementations:
  ask/menus, barter/trade (`find_barter`, `do_offer`, `do_demand`,
  `conversationtrade`, …), inventory (`find_inv`, `give_to_npc`,
  `take_from_npc`, …), quests/vars (`get_quest`, `set_quest`, `x_clock`,
  `x_exp`, `x_skills`, …), dialogue flow (`say_op`, `do_judgement`, `random`,
  …), world effects (`teleport_player`, `set_attitude`, `place_object`, …).
  See the full file list in the donor checkout; function names are behavior
  evidence, not code to port.

## 6. src/magic — spell classes

- `spellcasting.cs` — `SpellCasting : UWClass`;
  `CastSpell(major, minor, caster, target, tileX, tileY, CastOnEquip)` dispatch:
  majors 0–3 → `CastClass0123_Spells`, 4 heal, 5 projectile, 6
  area-around-player, 7 callback/click-on-object, 8 summon, 9 curse, 10 mana,
  11 misc/special, 12 "not castable here", 13 misc, 14 cutscene; +
  `CastCurrentSpellOnRayCastTarget` / `CastCurrentSpellAtPosition`; deducts
  `RunicMagic.PendingSpellCost` from `playerdat.play_mana`.
- `runicmagic.cs` — `RunicMagic` player-facing table (`SpellList`,
  `PendingSpellCost`, index/rune-sequence/major/minor).
- `MagicEnchantment.cs` — object-cast enchantments/potions (UW2 items 224–231;
  UW1 187–188 when non-UW2-only).
- `spellcasting_class_0123.cs` through `spellcasting_class_13.cs` — per-major
  implementations (motion/misc, heal with minor-0xF special case, magic
  projectile with separate UW1 vs UW2 ID tables, area, targeted callbacks,
  summoning with UW1-vs-UW2 minor-4 branch, curse with on-equip path, mana
  boost, speed/portal, Altaras wand/mind blast/…).
- `spellcasting_activeeffects.cs` — `PlayerActiveStatusEffectSpells(major,
  minor, stabilityclass)`.
- `spellcasting_objects.cs` — casting from enchanted equipment ("not all
  spells will cast from here").

## 7. src/World — tilemap, automap, timers

- `tilemap.cs` — `UWTileMap : Loader`; `lev_ark_block` (tiles+objects; +UW2
  overlays), `tex_ark_block` (texture map), UW1 animation overlays;
  `dungeons[]`, `current_tilemap`; level counts in §2.
- `tilemaprender.cs` — Godot builder; `godotscale` (76.8, 4.8, 76.8) = 64×64
  tiles at 1.2 m, 32 height steps at 0.15 m; TILE_SOLID/OPEN/DIAG_SE/DIAG_SW
  constants.
- `tileinfo.cs` — per-tile properties.
- `automap.cs` — cached `automaps[]`; LEV.ARK block `LevelNo+27` (UW1) /
  `160+LevelNo` (UW2); 64×64 buffer (+blank-map fallback).
- `automaprender.cs`, `automapnotes.cs`, `automaptileinfo.cs` — map rendering,
  notes, per-tile info.
- `timers.cs` — UW2-only timer triggers (block offset 0x7D88, 16-bit ×64,
  `FrameNo`; skips when `FreezeTimeEnchantment`).
- `uwobject.cs` — object model (major/minor/classindex, link, quality,
  `a_name`); `worlds.cs` — world container.
- `animationoverlay.cs` — UW1 animation overlays; `sleep.cs` — sleep methods
  (0 none / 1 bedroll / 2 bed / −2 drunk; hostile-awareness checks; "bizarre
  dream logic"); `specialeffects.cs` — `SpecialEffect(type, param)` (2 sound
  with UW1-TVFX vs UW2-VOC routing by id ≤99/≥100, 4 screenshake, 5–8 color
  flash).
- `specificworlds/` — `academy.cs`, `etherealvoid.cs`, `killorn.cs`,
  `pitsofcarnage.cs`, `prisontower.cs`, `tomb.cs` (per-world logic;
  header-only in this pass).

## 8. src/traps — count and families (61 files)

- Dispatch: `trap.cs` — `ActivateTrap(character, trapObj, ObjectUsed,
  triggerX/Y, objList)`; `majorclass==6/minorclass==0/classindex 0x0–0xF`
  vanilla set (0 damage, 1 teleport, 2 arrow, 3 do/hack entry, 4 pit (UW1) /
  special-effect (UW2), 5 change-terrain, 6 spell, 7 create-object, 8 door,
  0x9–0xF including skill/tell, delete-object, inventory, set/check-variable,
  null/combination); `minorclass==1` variants (text-string … oscillator,
  proximity, pit, bridge; UW2-gated entries noted in code).
- `hack_trap.cs` — `ActivateHackTrap` dispatches on `quality` (do/hack
  family): 8 `a_do_trap_*` (bullfrog, camera, conversation, emeraldpuzzle,
  endgame, platform, quake, trespass) and 29 `a_hack_trap_*` (blyskup,
  changegoal, floorcollapse, forcefield, gemteleport, oscillator, vending,
  visibility, … — full list in the donor checkout).
- Related but NOT in `traps/`: `src/triggers/` (move/collision/open/pressure
  triggers); `src/scd/` (`scd.cs`, `scd_run.cs`, `scd_util.cs`, `functions/`)
  — the UW2 scheduled-event engine driven off xclocks. SCD.ARK is UW2-only
  (see §2); never an extraction source here.

## 9. src/interaction — verbs

- Verbs (F-key modes): look (`look.cs`), talk (`talk.cs`), pickup
  (`pickup.cs`), use (`use.cs` with `UseMajorClass2/3/4/5/6/7`), use-on
  (`useon.cs`), attack (F5 → `interaction/combat/`), cast (F8 → `src/magic/`).
- Support: `damage.cs` (object damage), `enchanting.cs`, `repair.cs`,
  `thief.cs`, `tracking.cs` (ranged monster detection), `trapdisarming.cs`.
- `interaction/combat/` (5 partials of `combat : UWClass`): charge buildup,
  accuracy, and missile paths per README; code-header-only in this pass.

## 10. src/objectdata + src/objects

- Table layer (`src/loaders/objectdatloader.cs` + `src/objectdata/`, all
  `: objectDat`): animation, armour, containers, critter, food, lightsource,
  ranged, trigger (with a `triggertypes` enum: MOVE 0, STEP_ON 1, PICKUP 2,
  USE 4, LOOK 5, ENTER 6 / UNLOCK_UW1 6, PRESSURE 7 / OPEN_UW1 7, OPEN_UW2 8,
  CLOSE 9, TIMER 10, UNLOCK_UW2 11, SCHEDULED 12, EXIT 14, PRESSURE_RELEASE
  15), weapon.
- Behavior layer (`src/objects/`, ~70 files: door, chest, container, potion,
  wand, runestone, runebag, shrine, moongate, fountain, lockpick, readable,
  wearable/armor pieces, food, light, musicalinstrument, key_of_infinity,
  silverseed/silvertree, fishingpole, …): per-item logic — surveyed by
  listing only.

## 11. src/savegame (8 files)

- `SaveGame.cs` — orchestrator; slots 1–4 → `{BasePath}/SAVE{slot}/`; staging
  `SlotTransaction` swap; detach/reattach player slot-1 + `ApplySlot1Markers`
  for DOS compatibility.
- `PlayerDatWriter.cs` — `PLAYER.DAT` (+UW1 XOR bytes 1..210/0xD2).
- `BGlobalWriter.cs` — `BGLOBALS.DAT` (little-endian, no header/footer).
- `LevArkWriter.cs` — `LEV.ARK` (visited levels from
  `UWTileMap.dungeons[i].lev_ark_block.Data`).
- `ScdArkWriter.cs` — `SCD.ARK`, 16 blocks, UW2-only; empty for UW1.
- `SaveDescription.cs` — `DESC` (plain ASCII); `SaveDescriptionPrompt.cs`;
  `SlotTransaction.cs` — atomic slot replace.

## 12. docs summaries

- `docs/audio-architecture.md` — XMI→realtime synth (no pre-render/WAV cache);
  engine table (cm32l/mt32 via Munt.NET, soundfont via MeltySynth + bundled
  Phoenix MT-32 SF2, opl via AdlMidi.NET); `XMIMusic.ChangeTheme` →
  `MusicStreamPlayer` node; UW1 TVFX OPL2 state-machine SFX vs UW2 VOC
  routing; shared Godot audio bus, independent producers.
- `docs/font-architecture.md` — four UI fonts built at runtime from retail
  `.SYS` (replacing clipped TTF conversions); `sysfontloader` (parse,
  headless-tested) / `sysfontbuilder` (fill Godot `FontFile`) /
  `sysfontprovider` (set-or-fail + metrics); placeholder FontFiles + theme
  overrides in `scenes/Underworld.tscn`.
- `docs/save-architecture.md` — write path consolidating at save time (vs DOS
  two-tier continuous writes: bglobals on ExitConversation, SCD roughly every
  20 minutes, level notes on map-close); 5 files; limitations/follow-ups.

## 13. uwsettings.schema.json fields

`pathuw1`, `pathuw2`; `gametoload` UW1|UW2; `level` (0–8 UW1, 0–71 UW2);
`lightlevel` 0–7 (ignored); `levarkfolder` DATA|SAVE1..SAVE4; `shader`
(ignored legacy); `FOV` (default 75.0); `showcolliders`; `shaderbandsize`
(default 8); `synth` cm32l|mt32|soundfont|opl (default soundfont);
`synthpath`; `rompath` (deprecated alias).

## 14. Portable knowledge vs Godot topology

Do NOT port as behavior: `tileMapRender`/`AutomapRender` Node3D builders +
`godotscale`, GRLoader/TextureLoader `ShaderMaterial`/`ImageTexture` + palette
shaders, `MusicStreamPlayer` node, `AudioStreamWav`/SFX backends,
`FontFile` fill/publish + theme overrides, `uimanager` InteractionModes/cursor,
Peaky coroutine VM driver, collision meshes + `showcolliders`, `BasePath`/
user-data save paths, `scenes/Launch.tscn` + `main.cs` bootstrap, Debug-only
scene tests.

Worth mining as behavior/format knowledge: LEV.ARK/CNV.ARK/BGLOBALS/
PLAYER.DAT/SCD.ARK layouts + UW1-vs-UW2 variants, the opcode table (§5) +
imported-function semantics, trap/trigger dispatch tables (§8), spell
major/minor logic including UW1/UW2 projectile tables (§6), OBJECTS.DAT
offsets + trigger-type codes (§10), tile/automap block indices (§7),
timer/xclock/scheduled-event mechanics, TVFX-vs-VOC SFX routing + XMI theme
mapping, DOS-compatibility save details (slot-1 detach, XOR range, DESC/LE
writers).

## 15. Gaps (do not cite without verification)

Exact on-disk folders for CMB/DL/GR/PALS/SHADES/TERRAIN/WEAPON/XFER/XMI files
beyond the filename strings (`DATA/` assumed from neighboring verified paths);
`playerdatcombat.cs` internals; conversation support-file roles
(header-only); trap minorclass 2/3 dispatch tail; per-spell minor-class tables
beyond case comments; `src/objects/` per-item behaviors; `src/npc/`,
`src/physics/`, `src/ui/`, `src/cuts/`, `src/scd/functions/` (out of requested
scope, listed but not read).
