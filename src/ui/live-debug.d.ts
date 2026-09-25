/**
 * Declarations for the Engine-owned live-debug panel.
 *
 * The Engine browser shell serves this module at the stable specifier
 * `@rusty-engine/live-debug` through its import map; the packaged typings live
 * in the installed runtime pack, which a checkout cannot type-check against.
 * Only the members this companion mounts are declared here, and they mirror
 * `share/live-debug-panel/index.d.ts` in the runtime pack.
 */
declare module '@rusty-engine/live-debug' {
  export interface LiveDebugMount {
    dispose(): void;
  }

  export interface LiveDebugPanelOptions {
    readonly enabled: boolean;
    readonly presentation?: 'inline' | 'dock' | 'overlay';
  }

  export interface RendererMetricsOptions {
    readonly initiallyVisible?: boolean;
  }

  export function mountLiveDebugPanel(
    host: HTMLElement,
    options: LiveDebugPanelOptions,
  ): Promise<LiveDebugMount>;

  export function mountRendererMetricsWidget(
    host: HTMLElement,
    options?: RendererMetricsOptions,
  ): LiveDebugMount;
}
