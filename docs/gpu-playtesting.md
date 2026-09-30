# Crew GPU playtesting

## Interaction controls

`W/A/S/D` walk, the mouse looks, and holding the primary button builds a charge
that a release swings. `E` uses the current in-reach target (take, container,
door, or linked Use trigger). `L` examines that same target and dispatches its
linked Look triggers. `Alt+S` quicksaves and `Alt+L` journeys onward (loads the
newest save); `Alt+L` is also a physical `L`, so the same press examines what is
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
Profiles load at service startup: inspect `playtest status`, then restart the
service only when no other sessions are active. Do not interrupt other tests.

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
