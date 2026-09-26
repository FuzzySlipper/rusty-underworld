# Rusty-Dagger Kit survey (bootstrap-copy source)

Source: `/home/dev/rusty-dagger` (read-only survey; nothing written there).
Purpose: identify `WorldRpg.Kit` code worth one-time bootstrap-copying into
`AbyssRpg.Kit`, per the owner's decision that this copy carries **no
versioning or provenance tracking** — copy once, rename, customize, and the
AbyssRpg copy is the owner from then on. Donor-license posture does not apply
(the same author/house owns both repositories); UW donor rules
(UnderworldGodot/OpenUnderground: read, never copy) are unaffected.

Den note: task records live in Den (project `rusty-underworld`); this survey
creates none.

## 1. What was surveyed (sizes verified by listing)

| Location | Size | Role |
| --- | --- | --- |
| `src/WorldRpg.Kit/` (excl. `obj/`, `bin/`) | 31 .cs files (32 incl. csproj), ~4,770 lines | Reusable world-RPG mechanisms; Engine-only dependency |
| `src/WorldRpg.Rulesets.Daggerfall/Modules/` | 16 files, ~3,360 lines | Dagger policy in modules (a **migration fact**, not a boundary — see §2) |
| `src/Daggerfall.Import/` | 122 .cs files (123 incl. csproj) | Arena2/DFU decoders — UW-irrelevant except pipeline shape |
| `tests/WorldRpg.Architecture.Tests/` | 1 source file, 184 lines | Ownership laws as executable tests |
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

### Actors — ADAPT (player construction is unfinished in Kit)

- `Actors/ActorsState.cs` (138): COPY the `Construct` core (canonical Engine
  entity + stats/targeting/attack/effects/vitals) and the durable lookup
  (`Get`/`TryGet`/identity map). But ADAPT avatar construction: `Construct`
  never attaches `ActorBody`, `InventoryComponent`, or `EquipmentComponent`,
  and `CreatePlayer` adds only `ProgressionState` — while `PlayerActorState`
  exposes `Inventory`/`Equipment` accessors over components that do not exist
  and carries no pose/position (only `ActorState` has them). In Dagger the
  completing code lives in the NOT-copied `DaggerActorFactory` layer, so a
  verbatim copy yields an avatar that cannot stand in the dungeon or be
  equipped. The UW factory must attach pose + inventory + equipment (or unify
  the two facades), replicating that finishing work for UW slots.
- Consciously keep or remove the player-special-cases the split creates:
  `AttackExecution.cs:40,110` (defeat check, actor enumeration) and
  `TargetingService.cs:34,44,54` (current-target state) branch on the player
  because `All` (an `ActorBody` query the player never appears in) and
  `TryGet` (requires `ActorBody`, so the player is unresolvable) exclude it.
  Single-avatar UW keeps the enforcement ("already has a player") but must
  decide each branch deliberately.
- `Actors/ActorNavigationCoordinator.cs` (79) + `Actors/PassiveTrackRecovery.cs`
  (73): COPY — navigation coordination and recovery ticking fit UW schedules
  and rest recovery.

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

### Targeting — COPY for avatar-aim (observer is avatar-only)

- `Targeting/TargetingService.cs` (58) + `Targeting/InteractionTargetingService.cs`
  (209): COPY — Engine perception + explicit ruleset policy is precisely the
  UW targeting design (melee aim, Look/Get/Use detection, missile paths).
- Caveat: `Select` observes solely from `actors.Player` and filters
  candidates through `TryGet`, which requires `ActorBody` — so the avatar
  can never be returned as a target. Fine for UW avatar aim, but
  critter-vs-avatar AI selection must NOT reuse this path; route it through
  pursuit-style explicit targets instead.

### Inventory — COPY core, ADAPT slots

- `Inventory/MechanicsInventoryCoordinator.cs` (360): grant/consume/atomic
  grant+consume/split/merge/destroy + revision-tracked equipment reads over
  Engine Mechanics. COPY — UW stacks, how-many splits, container nesting,
  and barter transfers all ride these operations (UW-T11/T12/T28).
- `Inventory/MechanicsInventoryContainerCoordinator.cs` (395): COPY for the
  same reason (corpse/chest/bag containers share one lifecycle).
- `Inventory/InventoryGridLayout.cs` (51): COPY — "Presentation positions
  only" slot bookkeeping (reconcile/move-swap/place-displace + revision) with
  no Dagger content. UW1 interaction IS drag-based (manual: RIGHT-press-hold
  drag; drop/throw by release region; torch+corn combining), and a drag UI
  needs exactly this position/revision layer.
- Equipment slot IDs (`EquipmentSlotId`, paperdoll assignments) are stringly
  typed records — ADAPT the slot catalog to UW wear slots (weapon/off-hand/
  shoulders/head/torso/hands/legs/feet/fingers).

### Loot — COPY

- `Loot/CorpseLootCoordinator.cs` (123): corpse-owned inventories +
  `CorpseLootTransferResult` through the canonical container coordinator;
  rulesets keep identity/generation/eligibility. COPY — UW corpses persist
  and stay searchable, same contract (UW-T15).

### AI — ADAPT (no retreat, flat-world steering)

- `Ai/PursuitCoordinator.cs` (157): ADAPT, don't copy blindly.
  `PursuitState` includes `Retreat` (assigned by the ruleset's critter policy),
  so morale and flee need no Kit enum change; the chase steering still flattens
  the target to actor height, which remains the open vertical-policy decision
  (a fork, stated upfront) or an
  explicit retreat==Idle mapping. The Chase steering also flattens its
  target to actor height while the Attack/Chase decision uses 3D distance —
  mixed frames that misbehave across pits, ledges, and bridges. UW must set
  a vertical policy for its multi-level dungeon at copy time.

### Effects — COPY, verify one bound

- `Effects/ActiveEffectLifecycle.cs` (337): source identity, modifier
  removal, conditions, duration/stacking, `IActiveEffectContribution`
  add/remove. COPY for UW active effects (UW-T19), with two decided points:
  the lifecycle stores states in an unbounded `Dictionary` with no
  count/capacity check anywhere, so UW max-3 (+stable/unstable time) is a
  **ruleset admission gate before `Admit`** — no Kit fork needed. And do NOT
  copy `AdvanceInitialMagicRound` ("donor-style initial round",
  immediate-first-tick semantic): the manual has no such concept, so leave
  it behind unless a UW effect task proves otherwise.

### Progression — COPY

- `Progression/ProgressionState.cs` (62): per-actor progression bookkeeping
  component. COPY; UW XP/level/mantra effects compose over it (UW-T34).

### Controls — COPY systems, ADAPT bindings

- `Controls/SpatialMovementSystem.cs` (462): `Step` with
  `CharacterStepEnvironment`/`CharacterStepControls`, wall-probe/climb
  queries, ray casts, collider projection, continuation capture. COPY — UW
  walk/run/jump + ≤2 ft climb ride `Step`. Decided at survey time: there is
  NO swim/water/drown support in Kit Controls (zero hits) — UW swimming is
  new Kit work, and the named extension point is the caller-supplied
  `VerticalVelocity` drive in `Step`, which zeroes gravity and jump buffers.
  Related decisions at copy time: `SpatialTuning` configures chunk-streamed
  collision/navigation sizes (open-world shaping — retune for one continuous
  tile-mapped dungeon), and level transitions must go through
  `ReplaceContent`, which forces the session-per-level vs
  session-per-dungeon decision the plan's area 4 must settle first.
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
- `Presentation/SpriteAtlasAdapter.cs` (65): COPY — pure pixel→UV and
  frame-timing math with zero Dagger references. Import packs supply pixels;
  something must still map them into Engine sprite requests for UW's
  sprite-rendered critters/objects, and this is that something.

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
| `Daggerfall.Import/` (122 .cs files) | Arena2 formats; zero UW overlap | Pipeline shape only: format readers → normalization → publication + `.Tool` separation + provenance records (our `UltimaUnderworld.Import`) |
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
