# Task index

Implementation tasks derived from the ledger. IDs are stable (`UW-T01…`)
and each has a Den record (`rusty-underworld`) carrying status, delivery
hashes, and review evidence; Den is the durable status source, this file the
scope source. Order follows the coverage plan's dependency evidence; tasks
within a wave are independent unless prerequisites say otherwise.

## Delivery status (waves 0–5)

Waves 0–5 tasks (UW-T01…UW-T38) carry Den records with per-task delivery
commits. Single schema, no versions: saves persist one current snapshot;
reviews reconcile as follow-up commits on the same task series. Known
receivers: trap-executor conditional continuations (from T30), death-path
anchor loading (from T25, routed in T34), trigger RunTrigger executor
(from T30).

## Wave 0 — composition and identity (unblocks everything)

| ID | Task | Owner | Prereqs | Ledger |
| --- | --- | --- | --- | --- |
| UW-T01 | Host entry + built-in UW selection + save envelope | Host | Engine pair | F066→adapted |
| UW-T02 | Ruleset/session contract + composition root | Kit + ruleset | T01 | A1 |
| UW-T03 | Durable↔runtime identity map + avatar/object IDs | Kit | T02 | A2 |
| UW-T04 | Game-time model + rest advancement + save capture | Kit + ruleset | T02 | A2 |
| UW-T05 | Content pack + tuning + bundle resolution + provenance record | Kit | T02 | C-01…C-12, A1 |

## Wave 1 — strings, tables, first level (importers + first geometry)

| ID | Task | Owner | Prereqs | Ledger |
| --- | --- | --- | --- | --- |
| UW-T06 | STRINGS.PAK + table readers (objects/skills/weapons) | Import | T05 | F026, C |
| UW-T07 | LEV.ARK decoding: tiles, objects, texture map, automap blocks | Import | T06 | F034–F037, C |
| UW-T08 | Level admission + Engine presentation + transitions | Kit + ruleset | T03, T04, T07 | F034, F035, F038 |
| UW-T09 | Avatar creation flow (8 choices, validation) | Kit + ruleset | T03, F004 content | F001, F004, F005 |
| UW-T10 | Movement: walk/run/jump/swim + collision + costs | Kit + ruleset | T08 | F007–F011 |

## Wave 2 — objects and combat (first coherent slice core)

| ID | Task | Owner | Prereqs | Ledger |
| --- | --- | --- | --- | --- |
| UW-T11 | Object instances + take/drop/throw/combine + stacks | Kit + ruleset | T03, T06 | F027–F031 |
| UW-T12 | Paperdoll + containers + encumbrance | Kit + ruleset | T11 | F029, F031 |
| UW-T13 | Charge attack execution + swing kinds + readiness | Kit + ruleset | T10 | F012, F013, F016 |
| UW-T14 | Hit/damage formulas + armor coverage + wear | Ruleset + tuning | T13 | F014, F017 |
| UW-T15 | Critter construction + senses + retaliation + corpses | Kit + ruleset | T08, T14 | F018, F019 |
| UW-T16 | Missile dispatch + projectiles + ammo | Kit + ruleset | T13 | F015 |
| UW-T17 | HUD + panels + weapon presentation projections | Ruleset + UI | T13 | F051, F052 |

## Wave 3 — magic, survival, knowledge

| ID | Task | Owner | Prereqs | Ledger |
| --- | --- | --- | --- | --- |
| UW-T18 | Rune catalog + shelf state + cast gates | Ruleset + content | T06 | F021, F022, R |
| UW-T19 | Casting workflow + effect instances (max-3, stable/unstable) | Kit + ruleset | T18 | F020, F023 |
| UW-T20 | Damage/light/heal/protection families | Ruleset | T19 | M (circles 1–4) |
| UW-T21 | Movement/utility families (fly, gate, telekinesis, open) | Ruleset + Kit stepping | T19, T10 | M (circles 3–6) |
| UW-T22 | Item-borne casting + Lore identification | Ruleset | T11, T19 | F024 |
| UW-T23 | Hunger/fatigue/light/rest/sleep + poison/disease | Kit + ruleset | T04 | A8 (survival) |
| UW-T24 | Automap + notes + compass + quest variables | Kit + ruleset | T08 | A8 (knowledge), F063 |
| UW-T25 | Save/load of the slice (avatar, level, knowledge, clock) | Host + ruleset | T04, T24 | F060–F065 |

## Wave 4 — society and traps

| ID | Task | Owner | Prereqs | Ledger |
| --- | --- | --- | --- | --- |
| UW-T26 | CNV.ARK normalization + dialogue corpus packs | Import | T06 | C, F040 |
| UW-T27 | Conversation VM (42 opcodes, core imports) | Ruleset | T26 | F040, F041 |
| UW-T28 | Barter + appraisal + gifts + demands | Ruleset | T11, T27 | F042, F043 |
| UW-T29 | NPC presence + schedules + attitudes | Kit + ruleset | T08, T27 | A8 (NPC) |
| UW-T30 | Trap/trigger dispatch + UW1 chains + wall controls | Ruleset + Kit | T08 | F045–F048, T-V/T-W/T-D |
| UW-T31 | Doors: lock/pick/bash + blocked-reopen + secrets | Ruleset | T30 | F039, F044 |
| UW-T32 | Repair (anvil + NPC) + theft consequences | Ruleset | T11, T28 | F032, F033 |

## Wave 5 — breadth and completion

| ID | Task | Owner | Prereqs | Ledger |
| --- | --- | --- | --- | --- |
| UW-T33 | Remaining spell families (summon, curse, detection, 7–8th) | Ruleset | T19 | M (circles 5–8) |
| UW-T34 | Shrines/mantras + seed/tree respawn | Ruleset | T25 | F006, A10 |
| UW-T35 | Remaining levels + content completion | Import + ruleset | T08 | C, A12 |
| UW-T36 | Track slots + speech + SFX routing | Ruleset + Engine Audio | T19 | F056–F059 |
| UW-T37 | Controls + options + save UX (slots, autosave) | Host + UI | T25 | F065, F069–F071 |
| UW-T38 | Reconciliation + coverage record updates | All owners | Waves 0–4 | A12 |

Wave 5 tasks expand as the ledger's remaining `implement` rows demand; T38
repeats after each wave per the coverage plan's reconciliation rule.
