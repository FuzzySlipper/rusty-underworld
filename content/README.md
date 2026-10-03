# content

Loaded content for the product. Content is data: changing valid content does not
require a product rebuild, and content never carries code. The four content kinds
and the rule that regeneration never overwrites authored work are fixed in
[`../docs/code-organization.md`](../docs/code-organization.md).

Layout:

| Path | Holds |
| --- | --- |
| `abyss/bundles/` | Game-bundle declarations: which ruleset, which authored content packs by id, which roots of imported packs, and which tuning profile a launchable product selects. |
| `abyss/packs/` | Hand-authored content-pack descriptors, one per authored pack, naming its payload. |
| `abyss/content-packs/` | Authored payloads: avatar options, classes, the starting kit, and the tuning profile's values. |
| `abyss/tuning/` | Tuning-profile descriptors; each names its payload in `content-packs/`. |
| `abyss/imports/` | Git-ignored packs produced offline by `node scripts/import-level.mjs` from the operator's own installation: one directory per level (collision, render mesh, placements, level manifest) and `object-tables/` (object tables, item catalog, strings, conversations). Each generated descriptor records its source game, source file and hash. The default bundle admits every pack under this root, so importing never edits a tracked file. |

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
