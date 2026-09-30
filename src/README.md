# src

Product graph for Rusty Underworld. `AbyssRpg.Kit` is a working one-time
bootstrap copy of `WorldRpg.Kit` mechanics (see `AbyssRpg.Kit/README.md` and
[`../docs/research/rusty-dagger-kit-survey.md`](../docs/research/rusty-dagger-kit-survey.md));
ruleset, Host, importer and Tool are owned projects with the final
dependency graph, each carrying its own behavior and tests. The owner-level contract is in
[`../docs/code-organization.md`](../docs/code-organization.md).

| Directory | Project | Owns |
| --- | --- | --- |
| `AbyssRpg.Kit/` | `AbyssRpg.Kit` | Reusable dungeon-centric, first-person RPG mechanisms: typed IDs, compiled ruleset/session contracts, bundle and tuning resolution, actor and avatar state, attributes, progression and passive recovery, attack execution, targeting, NPC presence and pursuit coordination, containers, containment and equipment, projectile flights, durable identity, automap, map-note and quest-variable state, the game clock, spatial stepping and the first-person camera, structured UI values. It is not a universal RPG framework and must not mention Ultima Underworld vocabulary. |
| `AbyssRpg.Rulesets.UltimaUnderworld/` | `AbyssRpg.Rulesets.UltimaUnderworld` | The compiled Ultima Underworld ruleset: attributes, the 20 skills, classes, the 24 runes and 40 spells in 8 circles, objects, critters, traps, combat charge and damage formulas, casting gates and costs, advancement and mantra policy, barter and repair policy, survival rates, conversation interpretation, presentation meaning, save meaning, and session composition. |
| `AbyssRpg.Host/` | `AbyssRpg.Host` | Product lifecycle, the explicit built-in ruleset and bundle selection, default selection, and the one ordinary product entry. It may select Ultima Underworld; it never interprets Ultima Underworld rules or source files. |
| `UltimaUnderworld.Import/` | `UltimaUnderworld.Import` | Offline knowledge of the original game's data files and of the donor projects that document them: UW/ archive formats (LEV.ARK, CNV.ARK, OBJECTS.DAT, COMOBJ.DAT, STRINGS.PAK and tables), conversion quirks, provenance, and normalization into packs. It is not a runtime dependency. |
| `UltimaUnderworld.Import.Tool/` | `UltimaUnderworld.Import.Tool` | The operator-facing command line that drives `UltimaUnderworld.Import` and writes import output. |
| `ui/` | product DOM companion (TypeScript) | Thin DOM presentation of Engine-delivered projections and semantic actions. It owns neither gameplay state nor game-world rendering. |

Runtime code consumes normalized content packs produced by the importer; it
does not read source-shaped game data. The `UW2/` tree is never an input.
