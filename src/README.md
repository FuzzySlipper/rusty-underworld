# src

Product graph for Rusty Underworld. `AbyssRpg.Kit` is a working one-time
bootstrap copy of `WorldRpg.Kit` mechanics (see `AbyssRpg.Kit/README.md` and
[`../docs/research/rusty-dagger-kit-survey.md`](../docs/research/rusty-dagger-kit-survey.md));
ruleset, Host, importer and Tool are owned projects with the final
dependency graph, each carrying its own behavior and tests. The owner-level contract is in
[`../docs/code-organization.md`](../docs/code-organization.md).

| Directory | Planned project | Owns |
| --- | --- | --- |
| `AbyssRpg.Kit/` | `AbyssRpg.Kit` | Reusable dungeon-centric, first-person RPG mechanisms: avatar state and attributes, skill and rune catalogs, casting workflows, charge-based attack execution, targeting, NPC presence and AI coordination, corpse and loot machinery, containers and doors, object physics coordination, conversation state and barter, traps and triggers, automap and quest-variable state, survival and time, world and spatial session stepping, structured UI values, typed IDs, compiled ruleset/session contracts, bundle and tuning resolution. It is not a universal RPG framework and must not mention Ultima Underworld vocabulary. |
| `AbyssRpg.Rulesets.UltimaUnderworld/` | `AbyssRpg.Rulesets.UltimaUnderworld` | The compiled Ultima Underworld ruleset: attributes, the 20 skills, classes, the 24 runes and 40 spells in 8 circles, objects, critters, traps, combat charge and damage formulas, casting gates and costs, advancement and mantra policy, barter and repair policy, survival rates, conversation interpretation, presentation meaning, save meaning, and session composition. |
| `AbyssRpg.Host/` | `AbyssRpg.Host` | Product lifecycle, the explicit built-in ruleset and bundle selection, default selection, and the one ordinary product entry. It may select Ultima Underworld; it never interprets Ultima Underworld rules or source files. |
| `UltimaUnderworld.Import/` | `UltimaUnderworld.Import` | Offline knowledge of the original game's data files and of the donor projects that document them: UW/ archive formats (LEV.ARK, CNV.ARK, OBJECTS.DAT, tables, art, sound), conversion quirks, provenance, normalization into packs, and differential validation against the donor recreations. It is not a runtime dependency. |
| `UltimaUnderworld.Import.Tool/` | `UltimaUnderworld.Import.Tool` | The operator-facing command line that drives `UltimaUnderworld.Import` and writes import output. |
| `ui/` | product DOM companion (TypeScript) | Thin DOM presentation of Engine-delivered projections and semantic actions. It owns neither gameplay state nor game-world rendering. |

Runtime code consumes normalized content packs produced by the importer; it
does not read source-shaped game data. The `UW2/` tree is never an input.
