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

## Current product limitations

The packaged entry constructs `AbyssProduct`, but does not attach a `UuSession`
or `AbyssSpatialSession`. `Attach` is empty, and `Update` exits while the session
is absent. `AbyssSceneBootstrap` prepares a collision artifact; it does not create
rendering. No ordinary launch caller loads the default level bundle and publishes
a world scene. The imported level artifact also needs runtime bundle admission.

Combat, conversation, casting, and menu helper tests do not supply those missing
callers. The staged input manifest has no intents/mappings. The DOM debug control
forwards an undeclared intent, and its metrics text is a placeholder; those need
real Engine debug/metrics integration. A connected Wolf session therefore must
not be reported as a playable dungeon. The task evidence record tracks the
observations and receiving work separately.
