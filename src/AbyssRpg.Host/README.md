# AbyssRpg.Host

The ordinary Engine product entry, built-in ruleset selection, lifecycle,
save helpers, menu projections, and spatial-session adapter live here.

The project builds the TypeScript companion as browser ESM and stages it through
the packaged Engine SDK. `den-serve` launches that CoreCLR product; see
[GPU playtesting](../../docs/gpu-playtesting.md).

The entry does not yet load the default bundle, create/attach a dungeon session,
or publish world rendering. Existing attachment and menu APIs are exercised by
component tests; they are not an implemented player launch flow.

Ownership remains defined in [code organization](../../docs/code-organization.md).
