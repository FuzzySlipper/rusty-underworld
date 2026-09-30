# tests

Suites mirroring the product graph, each executed by `scripts/verify.sh`.

| Directory | Suite | Answers |
| --- | --- | --- |
| `AbyssRpg.Kit.Tests/` | `AbyssRpg.Kit.Tests` | Do the reusable mechanisms behave as specified, independently of any ruleset? |
| `AbyssRpg.Rulesets.UltimaUnderworld.Tests/` | `AbyssRpg.Rulesets.UltimaUnderworld.Tests` | Do the formulas, gates, advancement rules, and per-system fidelity verdicts match what `docs/gameplay-design.md` and the cited evidence say? |
| `AbyssRpg.Architecture.Tests/` | `AbyssRpg.Architecture.Tests` | Do the ownership laws hold: kit free of ruleset vocabulary, the dependency graph, the importer outside the runtime, the concrete ruleset named only at the Host's built-in seam, no implicit runtime authorities? |
| `AbyssRpg.Host.Tests/` | `AbyssRpg.Host.Tests` | Does the product lifecycle, selection, and session construction work? |
| `UltimaUnderworld.Import.Tests/` | `UltimaUnderworld.Import.Tests` | Do the format readers and normalizers produce the expected packs from synthetic input and, where the operator's data is installed, from the real files? |
| `AbyssRpg.Ui.Tests/` | `AbyssRpg.Ui.Tests` | Do the DOM projections render published state and report intents without owning gameplay state? |

An architecture suite is not ceremony: it is the automated half of the boundary
rules in `AGENTS.md`, and it is the check that fails when a kit file quietly
gains ruleset vocabulary.

A test that reads the operator's game data or imported packs says so in its
attribute (`[OperatorDataFact]`, `[OperatorDataTheory]`, `[ImportedContentFact]`,
from `tests/Shared/OperatorData.cs`) and is reported **Skipped**, with the missing
files named, on a clone without them — never Passed. A test whose expected values
were read off one data release (counts, strings, table rows) is a
`[PinnedReleaseFact]`: it runs only against the release those values came from,
identified by file hash, and skips on another.

Every checked-in suite is executed by `scripts/verify.sh`. Temporary probe files
a review lane creates inside a suite are covered by `.gitignore` and are not a
pattern to imitate in committed code.
