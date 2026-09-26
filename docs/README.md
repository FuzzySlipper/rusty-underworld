# docs

Durable repository documents for Rusty Underworld. Start here.

| Document | Owns |
| --- | --- |
| [`../AGENTS.md`](../AGENTS.md) | The working contract: direction, fidelity stance, ownership, boundary rules, donor posture, git and documentation conventions. Read it first. |
| [`../README.md`](../README.md) | What the repository is, its current state, and how to develop and verify it. |
| [`gameplay-design.md`](gameplay-design.md) | The shape of the game: the loop, every system with a fidelity verdict, the first coherent slice, non-goals, and the decisions that are expensive to reverse. |
| [`code-organization.md`](code-organization.md) | How the repository expresses that shape: layering, where new code goes, the Kit and ruleset owner maps, content and import shapes, the UI contract, session modes, and persistence. |
| [`research/`](research/) | Donor surveys, the manual-cited experience outline, and the sibling-kit bootstrap map: what the reference recreations, the shipped manual, the ISO data, and `WorldRpg.Kit` say about and for the game. |
| [`coverage/feature-ledger.md`](coverage/feature-ledger.md) | Scope: stable feature IDs with disposition and the behavior required, including the binding exclusions. |
| [`coverage/`](coverage/content-scope.md) | The transcribed magic and trap inventories and the content scope of the shipped data: what the game's data contains, not what this repository imports. |
| [`agent-review/`](agent-review/) | The review lane model and the packets handed to reviewers. |

Documentation posture: a repository document states what is true of the code and
the data — ownership, boundaries, formats, conventions, current behavior. It does
not carry status, roadmaps, coverage ledgers or run logs: those live in Den
(project `rusty-underworld`), whose `coverage-audit`, `coverage-planning`,
`playtest-evidence-log` and task records hold them, because their truth changes as
work lands and a stale copy in the tree is worse than no copy. Durable documents
do not pin engine versions or commit revisions — those live in machine-checked
configuration such as `Directory.Build.props` and are moved by scripts, never by
editing prose. Do not restate a version here.
