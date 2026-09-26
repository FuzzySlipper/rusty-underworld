# content

Loaded content for the product. Content is data: changing valid content does not
require a product rebuild, and content never carries code. The four content kinds
and the rule that regeneration never overwrites authored work are fixed in
[`../docs/code-organization.md`](../docs/code-organization.md).

Layout:

| Path | Holds |
| --- | --- |
| `abyss/bundles/` | Game-bundle declarations: which ruleset, content packs, and tuning profiles a launchable product selects. |
| `abyss/packs/` | Generated content-pack descriptors, one per pack, naming the payload and its provenance. |
| `abyss/content-packs/` | Authored **definitions** (attributes, skills, classes, runes, spells, objects, critters, conversations, traps, levels) and **scenario** state. |
| `abyss/tuning/` | Typed tuning profiles: the adjustable values a launch selects. |
| `abyss/imports/<level>/` | Imported dungeon content produced offline from an operator-supplied installation: tile maps, object lists, texture maps, automap blocks, media, normalized tables, and the provenance that records game, build, source file, and transformation. Imports are generated; their sources stay outside the repository. |

Boundary rules:

- Original Ultima Underworld data is operator-supplied (the `game.gog` ISO,
  `UW/` tree only). Never commit it, and never copy converted game data out of
  a donor. Preserve attribution, licensing, and provenance for anything checked
  in.
- Runtime code consumes normalized packs, not source-shaped game data. Format
  knowledge belongs in `src/UltimaUnderworld.Import/`.
- Imported and authored content stay separate: regeneration must not overwrite
  authored files.
- Definitions are data, not code: if content seems to need behavior, the
  behavior belongs in the ruleset and the content should carry the values.

Generated imports and authored content stay separate; regeneration never
overwrites authored work.
