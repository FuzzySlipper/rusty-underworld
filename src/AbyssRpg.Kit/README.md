# AbyssRpg.Kit

One-time bootstrap copy of `WorldRpg.Kit` mechanics (namespace-renamed, no
provenance tracking) — see
[`docs/research/rusty-dagger-kit-survey.md`](../../docs/research/rusty-dagger-kit-survey.md)
for the copy map and the deliberate deviations (avatar construction,
avatar-aim-only targeting, retreat/vertical AI policy, excluded initial-round
method). Owner contract: [`../../docs/code-organization.md`](../../docs/code-organization.md).

Reusable dungeon-centric, first-person RPG mechanisms over Engine services.
Must not mention Ultima Underworld vocabulary (see `AGENTS.md` for the
forbidden set). `tests/AbyssRpg.Architecture.Tests` enforces the game, ruleset
and donor names, the game's place, creature, skill, rune and object names that
cannot be mistaken for ordinary words, and the original data file names; generic
words the game also uses (attack, mana, avatar, the short runes) and game facts
stated in neutral words remain a review obligation.
