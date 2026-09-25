# Product DOM companion

`main.ts` renders HUD projections and provides menu/debug controls. The Host
build compiles browser ESM into ignored `generated/`; only that output is staged
as UI assets. Source maps and declaration files are not runtime assets.

The current ordinary entry does not publish a gameplay HUD. Menu and debug
controls also need their real runtime consumers; their DOM tests alone do not
prove those actions work in the product. See [GPU playtesting](../../docs/gpu-playtesting.md).

The companion owns no gameplay state, world rendering, transport, or game loop.
