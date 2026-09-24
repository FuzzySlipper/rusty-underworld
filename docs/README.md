# docs

Durable repository documents for Rusty Underworld. Start here.

| Document | Owns |
| --- | --- |
| [`../AGENTS.md`](../AGENTS.md) | The working contract: direction, fidelity stance, ownership, boundary rules, donor posture, git and documentation conventions. Read it first. |
| [`../README.md`](../README.md) | What the repository is, its current state, and how to develop and verify it. |
| [`gameplay-design.md`](gameplay-design.md) | The shape of the game: the loop, every system with a fidelity verdict, the first coherent slice, non-goals, and the decisions that are expensive to reverse. |
| [`code-organization.md`](code-organization.md) | How the repository expresses that shape: layering, where new code goes, the Kit and ruleset owner maps, content and import shapes, the UI contract, session modes, and persistence. |
| [`research/`](research/) | Donor surveys and the manual-cited experience outline: what the reference recreations, the shipped manual, and the ISO data say about the game. |
| [`underworld-coverage-plan.md`](underworld-coverage-plan.md) | Scope, ownership, work ordering, and review posture for reaching UW1 coverage. |
| [`underworld-feature-map.md`](underworld-feature-map.md) | Donor structures with point-in-time coverage notes. |
| [`underworld-task-preparation.md`](underworld-task-preparation.md) | Task-creation packet: ledger, inventories, and proposed slices. |
| [`coverage/`](coverage/feature-ledger.md) | Stable feature/corpus IDs, the transcribed magic and trap inventories, content scope, and the proposed task index. |
| [`agent-review/`](agent-review/) | The review lane model and the packets handed to reviewers. |

Documentation posture: durable documents state ownership, boundaries, and
current behavior. They do not pin engine versions or commit revisions — those
live in machine-checked configuration such as `Directory.Build.props` and are
moved by scripts, never by editing prose. Do not restate a version here.
