# Crew GPU playtesting

## Start the product

Install the pinned Engine pair with `scripts/install-engine-pair.sh` if needed,
then run from the repository root:

```bash
npm ci
den-serve up rusty-underworld -repo "$PWD"
den-serve status rusty-underworld -repo "$PWD"
```

The ordinary packaged CoreCLR host serves port 4177. The Host build compiles
`src/ui/main.ts` to browser ESM, stages only `src/ui/generated`, and declares
source/content watch roots and the HUD projection stream. The broker probes
`/product-ui/main.js` for `mountProductUi`; this establishes serving, not a
session, scene, or gameplay readiness.

Use `den-serve restart rusty-underworld -repo "$PWD"` after changes when a fresh
host is needed. `den-serve logs` and the state record identify the current log
directory. If initial startup never becomes healthy, inspect that directory's
`server.stderr.log`: the broker may still be waiting for its startup deadline.

## Register the profile

The Crew service is `crew-playtest.service`, with CLI
`/home/agent/.local/bin/playtest` and API `http://127.0.0.1:48200`.
The checked-in profile is `configs/playtest/rusty-underworld.json`. Its URL must
be reachable from the GPU execution machine, and its title matches the Engine
product title. On this installation it uses the agent machine's LAN address.

Inspect `systemctl --user cat crew-playtest.service` for its `--games` path.
Merge the profile into that JSON array by `id`, preserving all other profiles.
The current installation uses `/home/system/crew-services/playtest/games.json`.
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
Wolf provides the remote GPU browser and native input; a headless browser run
is separate evidence. Keep original captures, action receipts, and cleanup
receipts. The maintained reference is
`/home/dev/crew-services/docs/playtest.md`.

## Import the level the product launches

Original game data is operator-supplied and never committed, so the level pack
is generated into the ignored content tree before the product can compose a
world:

```bash
scripts/import-level.sh            # level 1 from local/extracted/uw/UW/DATA
scripts/import-level.sh 2 path/to/UW/DATA
```

The script emits the Engine collision artifact, the visible geometry, the level
manifest with its declared content identity, and the content-pack descriptor
that the default bundle selects. Launching without it fails with the command to
run. Importing a second level is a second descriptor; the bundle lists the packs
it composes.

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
`abyss.save`, `abyss.respawn`, `abyss.damage`, `abyss.rune <index>` and
`abyss.cast <spellId>` answer from the live session; the diagnostics route reports product-callback
failures the browser would otherwise swallow.

## Current product limitations

The slice imports a level's collision, visible geometry, spawn, object
placements and the object tables. The tile probe refuses a tile the level does not admit as open, because a
capsule inside solid geometry makes the Engine refuse the next step and taints
the runtime. `abyss.travel` moves without a clock cost: the gameplay caller that
owns a transition decides what it costs, and nothing in play walks one yet.

Judge-side pointer input did not reliably reach the product's DOM below roughly
the middle of the 1280×720 stream, so the menu also answers `c` (Engine console)
and `m` (renderer metrics). The renderer reports `GPU timer: unavailable` on
this host, and the metrics readout is the only frame-pacing evidence available
from the product side.

A connected Wolf session is still not by itself gameplay acceptance: the captures
and the state reads recorded in the Den document `playtest-evidence-log` are what
establish the visible result, and the earlier failed run remains recorded in that
document's task 8576 section.
