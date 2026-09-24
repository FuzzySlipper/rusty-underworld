# tests

Planned suites, mirroring the product graph. No suite is checked in yet.

| Directory | Planned suite | Answers |
| --- | --- | --- |
| `AbyssRpg.Kit.Tests/` | `AbyssRpg.Kit.Tests` | Do the reusable mechanisms behave as specified, independently of any ruleset? |
| `AbyssRpg.Rulesets.UltimaUnderworld.Tests/` | `AbyssRpg.Rulesets.UltimaUnderworld.Tests` | Do the formulas, gates, advancement rules, and per-system fidelity verdicts match what `docs/gameplay-design.md` and the cited evidence say? |
| `AbyssRpg.Architecture.Tests/` | `AbyssRpg.Architecture.Tests` | Do the ownership laws hold: kit free of ruleset vocabulary, the dependency graph, the importer outside the runtime, one product project per layer? |
| `AbyssRpg.Host.Tests/` | `AbyssRpg.Host.Tests` | Does the product lifecycle, selection, and session construction work? |
| `UltimaUnderworld.Import.Tests/` | `UltimaUnderworld.Import.Tests` | Do the format readers and normalizers produce the expected packs, including the recorded quirks (UW1-vs-UW2 divergence, installed-subset trap)? |
| `AbyssRpg.Ui.Tests/` | `AbyssRpg.Ui.Tests` | Do the DOM projections render published state and report intents without owning gameplay state? |

An architecture suite is not ceremony: it is the automated half of the boundary
rules in `AGENTS.md`, and it is the check that fails when a kit file quietly
gains ruleset vocabulary.

Every checked-in suite is executed by `scripts/verify.sh`. Temporary probe files
a review lane creates inside a suite are covered by `.gitignore` and are not a
pattern to imitate in committed code.
