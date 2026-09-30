# AbyssRpg.Host

The ordinary Engine product entry, built-in ruleset selection, lifecycle,
save slots and store, menu and HUD projections live here.

Staging builds the TypeScript companion as browser ESM through the SDK's UI
build (`RustyEngineProductUiBuildCommand` runs `scripts/build-ui.sh` against the
pair's UI declarations, and only when a UI input changed) and stages it. `den-serve` launches that CoreCLR product; see
[GPU playtesting](../../docs/gpu-playtesting.md).

The entry loads the selected bundle, creates the session, admits the imported
level with its placements, publishes the Engine scene and camera, and streams
session state to the companion UI. The tests in `tests/AbyssRpg.Host.Tests`
subject that entry, not the components under it.

Ownership remains defined in [code organization](../../docs/code-organization.md).
