# Product DOM companion

`main.ts` renders HUD projections and provides menu/debug controls. The Host
build compiles browser ESM into ignored `generated/`; only that output is staged
as UI assets. Source maps and declaration files are not runtime assets.

The entry publishes the HUD, the menu, the conversation panel and the Engine's
own diagnostics surfaces; the DOM tests check the companion's contract, and GPU
runs check what the product actually draws. See [GPU playtesting](../../docs/gpu-playtesting.md).

The companion owns no gameplay state, world rendering, transport, or game loop.
