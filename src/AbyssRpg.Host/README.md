# AbyssRpg.Host

The ordinary Engine product entry, built-in ruleset selection, lifecycle,
save helpers, menu projections, and spatial-session adapter live here.

The project builds the TypeScript companion as browser ESM and stages it through
the packaged Engine SDK. `den-serve` launches that CoreCLR product; see
[GPU playtesting](../../docs/gpu-playtesting.md).

The entry loads the selected bundle, creates the session, admits the imported
level with its placements, publishes the Engine scene and camera, and streams
session state to the companion UI. The tests in `tests/AbyssRpg.Host.Tests`
subject that entry, not the components under it.

Ownership remains defined in [code organization](../../docs/code-organization.md).
