# Crew GPU playtesting

## Interaction controls

`W/A/S/D` walk, the mouse looks, and holding the primary button builds a charge
that a release swings. `E` uses the current in-reach target (take, container,
door, or linked Use trigger). `L` examines that same target and dispatches its
linked Look triggers. `Alt+S` quicksaves and `Alt+L` journeys onward (loads the
newest autosave, or the respawn anchor). To restore a quicksave, press Escape
and choose that save in the pause menu; `Alt+L` is also a physical `L`, so the same press examines what is
in reach before the load replaces the session. The pause menu's slot buttons
send the declared `abyss.action.load-slot-N` intents, one per slot the Host
owns. Pickup
triggers run after a successful take. Door-open triggers use the UW1 open event;
closing does not dispatch the donor's UW2-only close event.

## Start the product

Install the pinned Engine pair with `rusty install` if needed,
then run from the repository root:

```bash
npm ci
den-serve up rusty-underworld -repo "$PWD"
den-serve status rusty-underworld -repo "$PWD"
```

The ordinary packaged CoreCLR host serves port 4177. Staging compiles
`src/ui/main.ts` to browser ESM through the SDK's UI build (only when a UI input
changed), stages only `src/ui/generated`, and declares the HUD projection
stream; the SDK derives the watch roots from the UI source and content roots. The broker probes
`/product-ui/main.js` for `mountProductUi`; this establishes serving, not a
session, scene, or gameplay readiness.

Use `den-serve restart rusty-underworld -repo "$PWD"` after changes when a fresh
host is needed. `den-serve logs` and the state record identify the current log
directory. If initial startup never becomes healthy, inspect that directory's
`server.stderr.log`: the broker may still be waiting for its startup deadline.

## Run without Crew

For an unattended check that needs no GPU capture, the Engine CLI runs the
product with a headless page that keeps the world drawing and the UI mounted:

```bash
rusty dev --project src/AbyssRpg.Host/AbyssRpg.Host.csproj --live-debug --headless
```

## Register the profile

The Crew service is `crew-playtest.service`, with CLI
`/home/agent/.local/bin/playtest` and API `http://127.0.0.1:48200`.
The checked-in profile is `configs/playtest/rusty-underworld.json`, kept equal to
the registered one. Its URL must be reachable from the machine the Crew browser
runs on (on this installation the same machine, so `127.0.0.1`).

Inspect `systemctl --user cat crew-playtest.service` for its `--games` path (on
this installation `/home/agent/.config/crew-playtest/games.json`). Merge the
profile into that JSON array by `id`, preserving all other profiles.
Run `playtest reload` after editing profiles; it preserves active sessions.
For independent testing, register `rusty-underworld-hosted` with
`"host": { "repo": "/home/agent/dev/rusty-underworld" }` in place of the URL.
Each session then owns its product host, and stopping it releases that host.

```bash
playtest games
playtest game show rusty-underworld
playtest start rusty-underworld
# Keep the returned session and slot IDs.
playtest observe SESSION
playtest input SESSION --json '[{"kind":"hold","keys":[87],"ms":500}]'
playtest input SESSION --json '[{"kind":"hold","keys":[27],"ms":100}]'
playtest stop SESSION
```

Use this Crew CLI, not the retired `mcp__den_playtest__playtest_start` tools.
The latter expects a different broker manifest and does not use this profile.
The service runs the browser on the same machine as the product, so a capture is
the browser's view of the runtime's frame stream (JPEG frames the runtime
renders on this machine's GPU); the maintained reference is
`/home/agent/dev/crew-services/docs/playtest.md`.

Registering or editing a product profile takes effect with `playtest reload`,
which re-reads the profile list and the pool from disk atomically and leaves
running sessions alone. A profile that is in the file but not loaded answers
`unknown game`, which is a stale registry rather than a bad profile. Keep
original captures, action receipts, and cleanup receipts.

## Import the level the product launches

Original game data is operator-supplied and never committed, so the level pack
is generated into the ignored content tree before the product can compose a
world:

```bash
scripts/import-level.sh            # level 1 from local/extracted/uw/UW/DATA
scripts/import-level.sh 2 path/to/UW/DATA
```

The script emits the Engine collision artifact, the visible geometry, the level
manifest with its declared content identity, and the content-pack descriptor,
plus the install-global object tables, item catalog, strings and conversations.
Everything lands under `content/abyss/imports/`, which the default bundle admits
by root, so the tracked bundle is never edited. Launching without a level fails
with the command to run; importing a second level makes it reachable by travel.

## Reading the running product

The packaged host admits the Engine live-debug route when the product declares
it, and the companion mounts the Engine's own panel over it. The same route is
the fastest way to read live state from a script:

```bash
curl -s -X POST -H 'content-type: text/plain; charset=utf-8' \
  --data-binary 'abyss.status' \
  http://127.0.0.1:4177/__rusty/product/runtime/debug/execute
curl -s -X POST -H 'content-type: application/json' -d '{}' \
  http://127.0.0.1:4177/__rusty/product/runtime/diagnostics/read
```

`abyss.product`, `abyss.status`, `abyss.where`, `abyss.actors`, `abyss.goto`,
`abyss.travel <level>`, `abyss.slots`, `abyss.play`, `abyss.pause`, `abyss.load`,
`abyss.save`, `abyss.respawn`, `abyss.damage`, `abyss.pack`, `abyss.projectiles`,
`abyss.rune <index>` and `abyss.cast <spellId>` answer from the live session
(the registered set is the Engine catalog `abyss.` prefix lists); the diagnostics route reports product-callback
failures the browser would otherwise swallow.

## Persistence checks

Take an item and loot a container, quicksave with `Alt+S`, then press
Escape and select the quicksave in the pause menu: `abyss.pack` should name
the same durable items once, and using the
container again should report it empty. Defeated actors remain lootable corpses;
travel and save/load retain their position, health and contents. For a fresh
product load use a hosted profile with the same product state store.
Label debug `abyss.goto` or `abyss.travel` positioning separately from ordinary
movement; the use/save/load controls must still be exercised through the browser.
The combined composition suite also checks wounds, an open door, a one-shot
trigger and automap in one save restored twice. An open door does not establish
lock/key persistence; equipment, conversation and progression interoperability
remain tracked with their gameplay owners in Den.

## Current product limitations

The slice imports a level's collision, visible geometry, spawn, object
placements and the object tables. The tile probe refuses a tile the level does not admit as open, because a
capsule inside solid geometry makes the Engine refuse the next step, which
faults and pauses the product. `abyss.travel` moves without a clock cost: the gameplay caller that
owns a transition decides what it costs. In play, pit traps drop the avatar a
level (costing clock time) and teleport traps can change level; stairs do not
walk a transition yet.

Judge-side pointer input did not reliably reach the product's DOM below roughly
the middle of the 1280×720 stream, so the menu also answers `c` (Engine console)
and `m` (renderer metrics). The renderer reports `GPU timer: unavailable` on
this host, and the metrics readout is the only frame-pacing evidence available
from the product side.

A connected session is still not by itself gameplay acceptance: the captures
and the state reads recorded in the Den document `playtest-evidence-log` are what
establish the visible result, including the earlier failed runs recorded there.

## Door traversal checks

Approach a visible closed door using browser movement. Record the avatar
stopping at its leaf, then press `E` from outside it. Record the leaf opening
and ordinary movement crossing the same plane. Close it and check that it
blocks again. The static level collision artifact is installed once; the
current Engine character proposal receives the leaf's call-local bounds and
enabled state. Save/load derives both drawing and collision from the saved
door state. Debug positioning may set up this scenario but does not prove
ordinary traversal. Also capture an admitted prop and creature, and a charged
swing visibly lowering the creature into its corpse shape.
Back away and look down when observing a small creature: standing immediately
beside it can place its body below the field of view.
Also try closing while overlapping the open leaf. The outcome must report an
obstruction and leave it open; step clear, close, and test blocking from both
sides. Use the rendered leaf's actual bounds rather than the whole door tile;
its depth is much thinner than a tile. A close message followed by one-way escape
through an overlapping leaf is a failure.

## Geometry light checks

Compare a corridor with the avatar's base light, the same view with a maintained
light spell, and the view after that spell expires. Geometry beyond the current
source ranges must stay dark, and the brighter range must move with the avatar.
A burning floor light stays at its admitted object pose until taken; loading a
save with that item carried must not recreate a floor light. The product uses
Engine point lights without the default global rig. Attenuation is our tuning,
and geometry shadows remain limited by the Engine; object visibility and map
reveal still apply the tile line-of-sight policy.
