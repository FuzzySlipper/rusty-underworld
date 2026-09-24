# UU1 data inventory

Source: the operator's copy at `/home/research/old-games/game-uu1/` — the
`game.gog` ISO 9660 image (label `UW12`, 33,867,776 bytes, listed with
`bsdtar -tf`), the installed `UNDEROM1/` tree, and
`Ultima_Underworld-Manual.pdf`. Counts below were read off that listing, not
recalled. Donor decoding knowledge is cited to
[`underworldgodot-survey.md`](underworldgodot-survey.md) (§N) and
[`openunderground-survey.md`](openunderground-survey.md) (§N).

Original game data is **operator-supplied and never committed here**. Only the
`UW/` tree is ever an extraction source; `UW2/` documents format divergences
and is never imported.

## 1. The ISO layout

```
game.gog (ISO 9660 'UW12')
├── UW/                 Ultima Underworld 1 — THE extraction source
│   ├── DATA/           74 files: archives, tables, art, fonts
│   ├── CRIT/           65 files: critter sprite pages + ASSOC.ANM
│   ├── CUTS/           72 files: cutscene bitmaps
│   └── SOUND/          79 files: speech, music, driver tables
├── UW2/                Ultima Underworld 2 — donor context only
│   ├── DATA/           69 files   CRIT/  CUTS/  DATA/  SOUND/
├── UW/UW.EXE, UW/UWSOUND.EXE, UW/LHA.EXE + docs
└── installers, catalog, DOSBox support (not game data)
```

The root also carries `UW.BAT`/`UW2.BAT`, `ASK.COM`, `CATALOG.EXE`, and the
`DOSBOX/` runtime — installer/support material, not content.

## 2. UW/DATA — the 74 shipped data files

Listed verbatim from the ISO (dotfiles omitted):

```
3DWIN.GR  ALLPALS.DAT  ANIMO.GR  ARMOR_F.GR  ARMOR_M.GR  BABGLOBS.DAT
BLNKMAP.BYT  BODIES.GR  BUTTONS.GR  CHAINS.GR  CHARGEN.BYT  CHARHEAD.GR
CHRBTNS.GR  CHRGEN.DAT  CMB.DAT  CNV.ARK  COMOBJ.DAT  COMPASS.GR  CONV.BYT
CONVERSE.GR  CURSORS.GR  DOORS.GR  DRAGONS.GR  EYES.GR  F16.TR  F32.TR
FLASKS.GR  FONT4X5P.SYS  FONT5X6I.SYS  FONT5X6P.SYS  FONTBIG.SYS
FONTBUTN.SYS  FONTCHAR.SYS  GENHEAD.GR  GRAVE.DAT  HEADS.GR  INV.GR
LEV.ARK  LFTI.GR  LIGHT.DAT  LIGHTS.DAT  MAIN.BYT  MONO.DAT  OBJECTS.DAT
OBJECTS.GR  OPBTN.GR  OPSCR.BYT  OPTB.GR  OPTBTNS.GR  PALS.DAT  PANELS.GR
PLAYER.DAT  POWER.GR  PRES1.BYT  PRES2.BYT  QUESTION.GR  SCRLEDGE.GR
SHADES.DAT  SKILLS.DAT  SPELLS.GR  STRINGS.PAK  TERRAIN.DAT  TMFLAT.GR
TMOBJ.GR  UW.CFG  VIEWS.GR  W16.TR  W64.TR  WEAPONS.CM  WEAPONS.DAT
WEAPONS.GR  WIN1.BYT  WIN2.BYT  XFER.DAT
```

Key records and their donor readers:

| File | Holds | Donor reader |
| --- | --- | --- |
| `LEV.ARK` | Tile-mapped levels + object lists + texture map + automap blocks (block `LevelNo+27` per level, 64×64) | Godot §7 (`levarkloader.cs`); block layout UW1 Int16-count + Int32 offsets |
| `CNV.ARK` | Conversation headers + imported-function table; the full NPC dialogue corpus | Godot §5 (`cnvarkloader.cs`, `conversationvm.cs`, 42 opcodes) |
| `OBJECTS.DAT` | Object master table (per-type offsets for weapons, armour, food, lights, containers, triggers, critters, …) | Godot §10 (`objectdatloader.cs` + `src/objectdata/`) |
| `COMOBJ.DAT` | Common-object data ("comboj.dat" spelling also appears in donor code) | Godot §3 (`comobjloader.cs`) |
| `STRINGS.PAK` | All game strings (readables, UI, dialogue text) | Godot §1 (readables + all strings as a feature) |
| `TERRAIN.DAT` | Terrain types | Godot §3 (`terraindatloader.cs`) |
| `XFER.DAT` | Transfer/palette-crossfade data | Godot §3 (`xferloader.cs`) |
| `PALS.DAT` (8 palettes), `SHADES.DAT`, `LIGHT.DAT`, `MONO.DAT`, `ALLPALS.DAT` | Palette, shading, lighting tables | Godot §3 (`paletteloader.cs`, `shadesdatloader.cs`) |
| `W64.TR` / `F32.TR` (wall/floor, split index 210), `W16.TR` / `F16.TR` | Wall and floor textures | Godot §3 (`textureloader.cs`) |
| `*.GR` (3DWIN, ANIMO, ARMOR_F/M, BODIES, … WEAPONS — ~30 archives) | Sprite/UI art | Godot §3 (`grloader.cs`) |
| `*.BYT` (BLNKMAP, CHARGEN, CONV, MAIN, OPSCR, PRES1/2, WIN1/2) | UI bitmap screens | Godot §3 (`bytloader.cs`) |
| `*.SYS` (FONT4X5P, FONT5X6I/P, FONTBIG, FONTBUTN, FONTCHAR) | UI fonts, built at runtime | Godot §12 (`sysfontloader`, 4-font set-or-fail) |
| `CMB.DAT` | Paperdoll/combination data | Godot §3 (`cmbloader.cs`) |
| `WEAPONS.DAT` + `WEAPONS.GR` (+ `WEAPONS.CM`) | Weapon rows + art | Godot §3 (`weaponloader.cs`); OU §3 (missiles use their own data rows) |
| `SKILLS.DAT`, `CHRGEN.DAT`, `GRAVE.DAT`, `LIGHTS.DAT`, `PLAYER.DAT` | Skills, chargen, graves, lights, default player record | Godot §4 (`playerdat*.cs`; `PLAYER.DAT` bytes 1..210 XOR in saves) |
| `BABGLOBS.DAT` | Conversation global variables | Godot §5 (`bglobal.cs`, `BGLOBALS.DAT`-family in saves) |
| `UW.CFG` | Install config (sound/speech/cuts paths; 48 bytes in the installed copy) | — |
| `DL.DAT` (string observed in donor, NOT in this ISO listing) | — | Godot §15 gap: verify before citing |

## 3. UW/CRIT — 65 critter files

`ASSOC.ANM` plus `CR<octal-id>PAGE.N00|N01` pairs (two pages per critter in
UW1; UW2 uses `CR<octal>.<page>` via a PGMP table): CR00–CR07, CR10–CR17,
CR20–CR27, CR30–CR37 sprite-animation pages. Donor: Godot §3
(`critterloader.cs`; `slots[160,8]` tables in OU `CritterLoader.cs`).

## 4. UW/CUTS — 72 cutscene files

`CS<nnn>.Nxx` cutscene bitmaps (single-frame + sprite/panorama-scroll modes
per Godot §3 `cutsloader.cs`). The installed `UNDEROM1/CUTS/` carries only the
`.N00` first frames (CS000–CS003, CS011–CS015, CS030–CS037, CS040–CS041,
CS400–CS404, CS410); full multi-frame sequences come from the ISO. Donor text
versions exist under OU `Assets/StreamingAssets/Cutscenes/cs*.txt`.

## 5. UW/SOUND — 79 sound files

- Speech/SFX: `00.VOC`–`20.VOC`, `22.VOC`–`39.VOC`, `50.VOC`, `58.VOC`,
  `65.VOC` (note: 21, 40–49, 51–57, 59–64 absent), plus `SOUNDS.DAT`.
- Music: `AW01–07, AW10–13, AW15.XMI` and `UW01–07, UW10–13, UW15.XMI`
  (XMI themes; Godot §12 plays them through 4 realtime synth engines —
  cm32l/mt32/soundfont/opl — with no pre-render; OU §3 restores the original
  combat/warning/victory/automap track slots).
- Driver tables: `ADLIB.ADV`, `MT32MPU.ADV`, `PASDIG.ADV`, `PASFM.ADV`,
  `PCSPKR.ADV`, `SBDIG.ADV`, `SBFM.ADV`, `SBPDIG.ADV`, `SBPFM.ADV`,
  `TANDY.ADV`, `UW.AD`, `UW.MT`.
- UW2-only audio under `UW2/SOUND` (UWA/UWR track pairs) is donor context for
  the ten recommended tracks (OU §2), never an import source.

## 6. The installed UNDEROM1/ tree (operator machine state, not source)

```
UNDEROM1/
├── CUTS/        .N00 first frames only (see §4)
├── DATA/
│   └── UW.CFG   48 bytes: sound/speech/cuts device paths
├── MARKER.FIL
├── SAVE1/..SAVE4/   operator save slots (original-format saves are OUT of scope)
```

The installed tree holds no LEV.ARK/CNV.ARK/OBJECTS.DAT — those live in the
ISO (or a full install) until extraction. The importer reads the ISO tree, not
this directory.

## 7. What the inventory implies for import order

1. **Strings + tables first** (`STRINGS.PAK`, `OBJECTS.DAT`, `COMOBJ.DAT`,
   `SKILLS.DAT`, weapon rows): they give every later family its vocabulary.
2. **Levels** (`LEV.ARK` + `W64/F32.TR` + `SHADES/PALS/LIGHT.DAT`): tile maps,
   object lists, texture map, automap blocks.
3. **Conversations** (`CNV.ARK` + `BABGLOBS.DAT`): dialogue corpus + globals;
   needs the object vocabulary for barter/inventory functions.
4. **Art + critters + sound** (`.GR`, `CRIT/`, `.VOC`, `.XMI`): presentation
   and audio, admitted as Engine content.
5. **Screens + fonts** (`.BYT`, `.SYS`): UI bitmaps and the 4-font set.
6. **Save-shape orientation** (`PLAYER.DAT` default record, SAVE slot layout):
   informs our own save schema; original-format reading/writing stays out of
   scope.

`UW.EXE`/`UW2.EXE` carry 3D model data per Godot §3 (`modelloader.cs`) — an
executable-derived source like any other, mined for geometry knowledge, never
loaded at runtime.
