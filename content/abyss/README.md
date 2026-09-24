# abyss

Planned home for Ultima Underworld loaded content. Nothing is checked in yet —
see [`../README.md`](../README.md) and
[`../../docs/code-organization.md`](../../docs/code-organization.md) for the
content kinds and boundary rules.

Planned layout:

| Path | Holds |
| --- | --- |
| `bundles/` | Game-bundle declarations: which ruleset, content packs, and tuning profiles a launchable product selects. |
| `content-packs/` | Authored definitions, tuning profiles, and scenario state. |
| `imports/<level>/` | Importer-generated per-level packs (tile maps, object lists, media, normalized tables) with provenance. Generated; never hand-edited. |
