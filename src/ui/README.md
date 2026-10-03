# Product DOM companion

`main.ts` renders HUD projections and provides menu/debug controls. Staging the
Host compiles it to browser ESM in ignored `generated/` through the SDK's UI build
(`scripts/build-ui.mjs`), typed against the Engine pair's own
`@rusty-engine/product-ui` and `@rusty-engine/live-debug` declarations; only that
output is staged as UI assets. `tsconfig.json` holds the compiler options, and the
build script adds the pair's declarations to it.

The entry publishes the HUD, the menu, the conversation panel and the Engine's
own diagnostics surfaces; the DOM tests check the companion's contract, and GPU
runs check what the product actually draws. See [GPU playtesting](../../docs/gpu-playtesting.md).

The companion owns no gameplay state, world rendering, transport, or game loop.
