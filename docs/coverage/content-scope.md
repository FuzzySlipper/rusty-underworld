# Content scope

Status: source-family inventory and authored-content target. Counts are ISO
facts from [`uu1-data-inventory.md`](../research/uu1-data-inventory.md); donor behavior
claims cite the surveys. It states what the game's data contains, not what this
repository imports; imported coverage is measured in the Den document
`coverage-audit`.

## Source families (all under `UW/` in the operator ISO)

| ID | Family | Files | Current anchor | Target |
| --- | --- | --- | --- |
| C-01 Levels + object lists | `LEV.ARK` (+ texture map, automap blocks) | None | Normalized per-level packs with provenance; placement bug-class excluded at import (lava-spawn evidence) |
| C-02 Conversations | `CNV.ARK` + `BABGLOBS.DAT` | None | Normalized dialogue corpus + globals; interpreted by ruleset VM, never ported |
| C-03 Object tables | `OBJECTS.DAT`, `COMOBJ.DAT`, weapon rows | None | Typed definition packs per family |
| C-04 Strings/text | `STRINGS.PAK`, books, inscriptions | None | Loaded text tables; readables as world objects |
| C-05 Runes/spells | Manual grimoire (40/8/24) + `SPELLS.GR` | Authored from manual (see `magic-inventory.md`) | Content records + tuning; donor dispatch as interpretation reference |
| C-06 Skills/classes | Manual (20 skills, 8 classes) + `SKILLS.DAT`, `CHRGEN.DAT` | Authored from manual | Content records; ceilings authored, donor-informed |
| C-07 Traps/triggers | `LEV.ARK` chains + dispatch tables | None | Normalized trap/trigger records; UW1-reachable first |
| C-08 Art | ~30 `.GR` archives, `CRIT/` 65 files, `CUTS/` 72 files | None | Admitted Engine content with provenance |
| C-09 Sound/music | `SOUND/` 79 files (VOC + XMI + driver tables) | None | Ordinary audio files over Engine Audio; no XMI synth port |
| C-10 Fonts/screens | `.SYS` fonts, `.BYT` screens | None | Runtime-built fonts; DOM-adapted screens |
| C-11 Terrain/textures | `TERRAIN.DAT`, `W64/F32.TR` | None | Imported geometry/media packs |
| C-12 Config/saves | `UW.CFG`, `PLAYER.DAT` default, SAVE slots | Orientation only | Informs our schema; original-format support excluded |

Explicitly NOT content sources: the `UW2/` tree (divergence notes only), the
installed `UNDEROM1/` subset (first-frames + config + saves), donor-converted
game data (never copied out of a donor), `UW.EXE` at runtime (geometry
knowledge only).

## Authored-content target

Start authored-small: creation options, level-1 slice definitions, a handful
of spells/runes/objects, one NPC conversation with a barter/quest hook,
automap defaults, and tuning profiles. The importer carries breadth; authoring
fills what it cannot and everything the first slice needs before its importer
family lands. Regeneration never overwrites authored files; every imported
pack records game, release/build, importer revision, and transformation.
