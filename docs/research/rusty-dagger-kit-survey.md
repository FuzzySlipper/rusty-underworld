# Rusty-Dagger Kit survey (bootstrap-copy source)

Source: `/home/dev/rusty-dagger` (read-only survey; nothing written there).
Purpose: identify `WorldRpg.Kit` code worth one-time bootstrap-copying into
`AbyssRpg.Kit`, per the owner's decision that this copy carries **no
versioning or provenance tracking** — copy once, rename, customize, and the
AbyssRpg copy is the owner from then on. Donor-license posture does not apply
(the same author/house owns both repositories); UW donor rules
(UnderworldGodot/OpenUnderground: read, never copy) are unaffected.

Den note: the `rusty-underworld` Den project exists and its task list is
currently empty (`list_tasks` returns `[]`). No tasks were created by this
survey; UW-T01…T38 in [`coverage/task-index.md`](../coverage/task-index.md)
are the task source when tracking starts.

## 1. What was surveyed (sizes verified by listing)

| Location | Size | Role |
| --- | --- | --- |
| `src/WorldRpg.Kit/` (excl. `obj/`, `bin/`) | 27 files, ~4,770 lines | Reusable world-RPG mechanisms; Engine-only dependency |
| `src/WorldRpg.Rulesets.Daggerfall/Modules/` | 16 files, ~3,360 lines | Dagger policy in modules (a **migration fact**, not a boundary — see §2) |
| `src/Daggerfall.Import/` | 122 files | Arena2/DFU decoders — UW-irrelevant except pipeline shape |
| `tests/WorldRpg.Architecture.Tests/` | 1 file, 184 lines | Ownership laws as executable tests |
| `tests/WorldRpg.Rulesets.Canary.Tests/` | Canary minimal-ruleset smoke test | Kit/host seam validation pattern |

`WorldRpg.Kit.csproj` references **only** `Rusty.Engine`
(`$(RustyEnginePackageVersion)`, `AllowUnsafeBlocks=false`, warnings-as-errors).
A copied file therefore drags in no Dagger dependency by construction — the
only required edit is the namespace rename (`WorldRpg.Kit` → `AbyssRpg.Kit`).

## 2. The Kit vs Daggerfall seam (how to tell them apart)

- **Namespace + forbidden set.** Kit never names Daggerfall/Arena2. This is
  machine-checked: `ArchitectureLawTests.Kit_does_not_encode_reference_ruleset_vocabulary_or_references`
  forbids `Daggerfall`, `Arena2`, `PrivateersHold`, `DFUnity`
  (case-insensitive) in Kit sources and forbids the ruleset reference in the
  Kit project file.
- **Modules/ is Dagger wearing a Kit costume.** Files under
  `WorldRpg.Rulesets.Daggerfall/Modules/` (e.g. `Combat/DaggerCombatRules.cs`
  555 lines, `Loot/DaggerfallCorpseLootModule.cs` 542 lines) are Dagger policy
  in transit from the migration — never copy these as Kit. Their *shape* is
  the thing to imitate: tiny policy seams (`Combat/DaggerTargetingPolicy.cs`
  is 21 lines; `Combat/CombatDefinitions.cs` 41 lines) over Kit machinery.
- **Dependency direction.** Kit ← Ruleset ← Host; importer outside runtime.
  Also machine-checked (`Project_references_follow_the_worldrpg_dependency_graph`),
  along with no-reflection/no-locator/no-bus/no-second-loop/no-unsafe rules
  (`Active_runtime_projects_reject_implicit_runtime_authorities`).

## 3. Copy map (per Kit area → AbyssRpg owner)

Verdicts: **COPY** (rename namespace, keep logic) · **ADAPT** (copy then
reshape for UW) · **SKIP** (do not copy; reason given).

### World — COPY

- `World/EntityDirectory.cs` (85) + `World/DurableIdentity.cs` (344):
  durable↔runtime identity map over `EntityStore`. Game-agnostic; UW needs
  exactly this for level/object/NPC identity (coverage A2, UW-T03).
  `DurableIdentityKind.Actor` fits the single-avatar + critters model.

### Actors — COPY core, ADAPT player

- `Actors/ActorsState.cs` (138): factory over canonical Engine entities
  (`StatsComponent`, `TargetingComponent`, `AttackState`,
  `EffectsComponent`, `ActorVitals`) with durable lookup. COPY — delete
  nothing; UW critter construction rides the same `Construct` path.
- `CreatePlayer` enforces a single player (`"The session already has a
  player."`) — ADAPT into the Avatar owner: one avatar is the UW design, so
  keep the enforcement, add UW creation inputs.
- `Actors/ActorNavigationCoordinator.cs` (79) + `Actors/PassiveTrackRecovery.cs`
  (73): COPY — navigation coordination and Ellison-free recovery ticking fit
  UW schedules and rest recovery.

### Facts — COPY

- `Facts/FactBuffer.cs` (26) + `IWorldRpgFact` marker: COPY verbatim apart
  from namespace (rename marker to the AbyssRpg equivalent). Stable-batch
  delivery with no transaction journal is exactly the UW interaction model
  (coverage plan §"Three ordinary interaction paths" analog).

### Combat — COPY machinery, SKIP formulas

- `Combat/AttackExecution.cs` (111): readiness, pending impact, interruption,
  cooldown over attached `AttackState`. COPY — this is the UW charge state
  (hold → release → impact → cooldown); UW charge levels become ruleset
  policy *over* it, not a rewrite.
- `Combat/CombatResolution.cs` (125) + `Combat/IAttackCapabilities.cs` (15) +
  `Combat/AttackCapabilities.cs` (23): COPY — neutral capabilities keep hit
  math out of input/AI, matching the UW plan (input and AI never calculate).
- SKIP `Modules/Combat/DaggerCombatRules.cs` (555) and siblings — Dagger
  formulas. Imitate the seam: one `*TargetingPolicy` file per domain.

### Targeting — COPY

- `Targeting/TargetingService.cs` (58) + `Targeting/InteractionTargetingService.cs`
  (209): COPY — Engine perception + explicit ruleset policy is precisely the
  UW targeting design (melee aim, Look/Get/Use detection, missile paths).

### Inventory — COPY core, ADAPT slots

- `Inventory/MechanicsInventoryCoordinator.cs` (360): grant/consume/atomic
  grant+consume/split/merge/destroy + revision-tracked equipment reads over
  Engine Mechanics. COPY — UW stacks, how-many splits, container nesting,
  and barter transfers all ride these operations (UW-T11/T12/T28).
- `Inventory/MechanicsInventoryContainerCoordinator.cs` (395): COPY for the
  same reason (corpse/chest/bag containers share one lifecycle).
- `Inventory/InventoryGridLayout.cs` (51): SKIP unless the DOM UI needs it —
  UW drag semantics differ (donor has no drag-and-drop); decide at UI time.
- Equipment slot IDs (`EquipmentSlotId`, paperdoll assignments) are stringly
  typed records — ADAPT the slot catalog to UW wear slots (weapon/off-hand/
  shoulders/head/torso/hands/legs/feet/fingers).

### Loot — COPY

- `Loot/CorpseLootCoordinator.cs` (123): corpse-owned inventories +
  `CorpseLootTransferResult` through the canonical container coordinator;
  rulesets keep identity/generation/eligibility. COPY — UW corpses persist
  and stay searchable, same contract (UW-T15).

### AI — COPY

- `Ai/PursuitCoordinator.cs` (157): COPY pursuit/attack/retreat coordination;
  UW senses and schedules extend it (UW-T15/T29), not replace it.

### Effects — COPY, verify one bound

- `Effects/ActiveEffectLifecycle.cs` (337): source identity, modifier
  removal, conditions, duration/stacking, `IActiveEffectContribution`
  add/remove. COPY for UW active effects (UW-T19). VERIFY at copy time
  whether it assumes a max-concurrent bound — UW allows max 3 with
  stable/unstable time; encode that as ruleset policy over the lifecycle,
  not a Kit fork.

### Progression — COPY

- `Progression/ProgressionState.cs` (62): per-actor progression bookkeeping
  component. COPY; UW XP/level/mantra effects compose over it (UW-T34).

### Controls — COPY systems, ADAPT bindings

- `Controls/SpatialMovementSystem.cs` (462): `Step` with
  `CharacterStepEnvironment`/`CharacterStepControls`, wall-probe/climb
  queries, ray casts, collider projection, continuation capture. COPY — UW
  walk/run/jump + ≤2 ft climb + drown-adjacent motion ride `Step`; VERIFY
  swim coverage at copy time and extend, don't fork.
- `Controls/FirstPersonCameraSystem.cs` (123) + `FirstPersonCameraTuning`
  (validated record): COPY — Engine-owned camera fed by product pose facts
  is the UW view contract too.
- `Controls/PlayerInputSystem.cs` (381) + `Controls/PlayerControlState.cs`
  (174) + `Controls/ControllerInputTuning.cs` (118): COPY the held-input
  machinery (state outliving one slice until release/focus-drop); ADAPT the
  bindings to UW verbs (look/get/use/talk/fight, charge-hold-release) —
  bindings are ruleset-owned records by design (`InputActionBinding`,
  `DirectionalMovementBindings`), so no Kit change is needed.

### Presentation — COPY builders, SKIP sprite adapter

- `Presentation/UiValueBuilder.cs` (80), `Presentation/PresentationState.cs`
  (48), `Presentation/PresentationSlots.cs` (60): COPY — safe structured
  projection values without borrowed storage; the HUD/panel/map/conversation
  projections (F051–F055) build on these.
- `Presentation/SpriteAtlasAdapter.cs` (65): SKIP — Dagger sprite-workbench
  specific; UW media arrives via Import packs.

### Composition — COPY

- `GameComposition.cs` (448): bundle/pack/tuning IDs, `ContentPack`,
  `TuningProfile`, `ResolvedGameComposition` + identity. COPY — UW-T05 is
  this file renamed, including the regeneration/provenance rules it encodes.
- `GameplayServices.cs` (21): COPY the explicit-aggregate pattern; the
  property list will grow UW owners (Avatar, Conversation, Survival) as they
  land.
- `PlayerPreferencesSession.cs` (21): COPY if keyed preferences apply;
  decide at options time (UW-T37).

## 4. Explicitly NOT copied (with what to take instead)

| Dagger location | Why not | Take instead |
| --- | --- | --- |
| `Daggerfall.Import/` (122 files) | Arena2 formats; zero UW overlap | Pipeline shape only: format readers → normalization → publication + `.Tool` separation + provenance records (our `UltimaUnderworld.Import`) |
| `Modules/*` (all 16 files) | Dagger policy, incl. 555-line combat rules + 542-line corpse loot | The seam shape: 21-line policy files over Kit machinery |
| `DaggerActorFactory`, `DaggerSession*`, `DaggerfallSavePayload` | Dagger session/save meaning | Our Avatar factory/session/payload per code-organization map |
| `WorldRpg.Host` (`WorldRpgProduct`, `BuiltInRulesets`, save store) | Dagger selection/lifecycle | Our Host; imitate the built-in-seam + envelope pattern |
| `src/ui`, `src/sprite-ui`, workbench | Dagger DOM + sprite tooling | Our thin DOM companion; UW panels differ |

## 5. Tests worth copying

- `ArchitectureLawTests.cs` (184, self-contained, stdlib + xUnit only):
  COPY FIRST, before any production file. Rename the forbidden set to the
  UU1 list already in our `AGENTS.md`, project names to the AbyssRpg graph,
  and keep the dependency-graph, importer-isolation, host-seam, and
  no-authorities tests. This is the enforcement half of our Kit boundary.
- Canary pattern (`CanaryRulesetTests` + minimal ruleset/session doubles):
  IMITATE — a tiny AbyssRpg ruleset proving hostRunsOneAdmittedUpdate,
  attach-republish, and dispose semantics gives UW-T01/T02 their first
  regression anchor without waiting for UW policy.

## 6. Unity/Godot firewall (what the copy keeps out)

Copying Engine-native Kit code is itself the firewall: every need below is
already met without donor-engine notions.

| Donor notion (never import) | Kit counterpart already covering the need |
| --- | --- |
| MonoBehaviour/prefab/scene wiring, Resources loading | Explicit factories + `GameplayServices` composition; content packs, not scenes |
| Godot nodes/shaders/coroutine VM driver | Engine services (`ISpatialService`, camera, audio) + ruleset-owned VM interpretation |
| Unity/Godot serialization, original save read/write | `ProductStateStore` + source-generated JSON codec; current-schema snapshots |
| Donor GameManager/state-stack orchestration | Host lifecycle + one admitted update; session modes, not scene stacks |
| Unity `Resources`/Godot `BasePath` asset discovery | `GameComposition` bundle/pack/tuning resolution with provenance |
| Donor input singletons | `PlayerInputSystem` held-input + ruleset-owned bindings |

## 7. Suggested copy order (matches UW-T waves)

1. Architecture tests + Kit project shell (namespace rename) — UW-T01/T02.
2. World + Facts + GameComposition/GameplayServices — UW-T02…T05.
3. Controls (movement/camera/input) + Targeting — UW-T08/T10.
4. Inventory + Loot + Actors + AI — UW-T11/T12/T15.
5. Combat execution + Effects + Progression — UW-T13/T19.
6. Presentation builders — UW-T17 as panels land.
7. Canary-imitating smoke suite alongside wave 0.

After each batch: run the architecture suite; reconcile Dagger naming that
survived the rename (`grep -rni daggerfall|worldrpg` must be empty outside
this survey).
