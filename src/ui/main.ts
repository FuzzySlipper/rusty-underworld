/**
 * The Abyss product's DOM companion: vitals and charge from the product's one
 * projection, the product-owned pause menu, and the Engine's own live-debug
 * panel and renderer-metrics widget.
 *
 * It owns no state, evaluates no rules, and starts no loop or timer. Values are
 * rendered only from the last admitted projection, and every control sends one
 * of the product's declared intents — the same names the staged manifest
 * declares, so a control cannot send something nothing handles. The debug
 * console is the Engine panel over the Engine's live-debug route: this file
 * forwards no commands of its own and keeps no transcript.
 */

interface ProjectionEnvelope {
  readonly contract: string;
  readonly value: unknown;
}

interface ProductUiContext {
  readonly projection?: {
    subscribe(listener: (projection: ProjectionEnvelope | null) => void): () => void;
  };
  readonly intents?: {
    claim(
      intent: string,
      value: { kind: 'digital'; active: boolean },
    ): void;
  };
  readonly ui?: {
    readonly active: () => boolean;
    readonly allowsGameplayInput: (event: Event) => boolean;
    readonly focusGameplay: () => void;
    readonly interactionMode: () => string;
    readonly setInteractionMode: (mode: string) => void;
  };
}

/**
 * The Engine browser shell publishes the live-debug panel at this stable
 * specifier through its import map. The packaged typings live in the installed
 * runtime pack, which a checkout without that pack cannot type-check against,
 * so the two members this companion uses are declared here.
 */
type LiveDebugModule = typeof import('@rusty-engine/live-debug');
type LiveDebugMount = import('@rusty-engine/live-debug').LiveDebugMount;

export interface ProductUiDependencies {
  /** Overridden in DOM tests, which cannot run the Engine panel bundle. */
  readonly loadLiveDebug?: () => Promise<LiveDebugModule>;
}

const HUD_CONTRACT = 'abyss.ui.snapshot.v1';

/** Declared direct intents; AbyssProductEntry and the csproj declare the same names. */
const INTENTS = {
  start: 'abyss.lifecycle.start',
  // Declared and consumed, but not claimed from here: the menu only exists while
  // the product is paused, so its pause producer is the pointer-lock loss the
  // Engine reports to the product.
  pause: 'abyss.lifecycle.pause',
  resume: 'abyss.lifecycle.resume',
  stop: 'abyss.lifecycle.stop',
  quicksave: 'abyss.action.quicksave',
  journeyOnward: 'abyss.action.journey-onward',
  respawn: 'abyss.action.respawn',
} as const;

interface SlotView {
  readonly key: string;
  readonly label: string;
  readonly savedAtUtc: string;
}

interface MenuView {
  readonly visible: boolean;
  readonly mode: string;
  readonly canStart: boolean;
  readonly canResume: boolean;
  readonly canSave: boolean;
  readonly canLoad: boolean;
  readonly canRespawn: boolean;
  readonly journeyOnward: string;
  readonly slots: readonly SlotView[];
}

interface SnapshotView {
  readonly ready: boolean;
  readonly mode: string;
  readonly hp: number;
  readonly maxHp: number;
  readonly mana: number;
  readonly maxMana: number;
  readonly charge: number;
  readonly outcome: string;
  readonly level: number;
  readonly avatar: string;
  readonly defeated: boolean;
  readonly menu: MenuView;
}

const STYLES = `
.abyss-hud {
  position: fixed;
  left: 0.75rem;
  bottom: 0.75rem;
  padding: 0.5rem 0.75rem;
  border: 1px solid rgba(210, 196, 158, 0.35);
  border-radius: 0.35rem;
  background: rgba(18, 16, 14, 0.82);
  color: #e8e0cc;
  font: 13px/1.45 system-ui, sans-serif;
  min-width: 15rem;
}
.abyss-hud .bar { display: block; width: 13rem; margin: 0.15rem 0; }
.abyss-hud .bar .track { display: block; height: 0.5rem; background: #2c2822; border-radius: 0.2rem; }
.abyss-hud .bar .fill { height: 100%; width: 0; border-radius: 0.2rem; }
.abyss-hud .hp .fill { background: #b04434; }
.abyss-hud .mana .fill { background: #3f6fb5; }
.abyss-hud .charge .fill { background: #c9a13b; }
.abyss-hud .label { display: block; opacity: 0.75; font-size: 10px; line-height: 1.2; text-transform: uppercase; letter-spacing: 0.06em; }
.abyss-hud .status { margin: 0.4rem 0 0; max-width: 16rem; }
.abyss-hud[data-ready="false"] .bar { opacity: 0.35; }
.abyss-menu, .abyss-debug, .abyss-metrics {
  position: fixed;
  border: 1px solid rgba(210, 196, 158, 0.35);
  border-radius: 0.35rem;
  background: rgba(18, 16, 14, 0.92);
  color: #e8e0cc;
  font: 13px/1.45 system-ui, sans-serif;
}
.abyss-menu { top: 0.75rem; right: 0.75rem; padding: 0.6rem 0.75rem; min-width: 13rem; }
.abyss-menu[hidden] { display: none; }
.abyss-menu button { display: block; width: 100%; margin: 0.2rem 0; }
.abyss-menu .tools { display: block; margin-top: 0.4rem; }
.abyss-menu .slots { margin: 0.4rem 0 0; padding: 0; list-style: none; opacity: 0.8; font-size: 12px; }
.abyss-debug { left: 0.75rem; top: 14rem; padding: 0.5rem 0.75rem; max-width: 24rem; }
.abyss-debug[hidden] { display: none; }
.abyss-metrics { left: 0.75rem; top: 3.5rem; padding: 0.4rem 0.6rem; }
.abyss-metrics .panel:empty { display: none; }
.abyss-metrics[hidden] { display: none; }
`;

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function readSlots(value: unknown): SlotView[] {
  if (!Array.isArray(value)) return [];
  const slots: SlotView[] = [];
  for (const entry of value) {
    if (!isRecord(entry)) continue;
    const { key, label, savedAtUtc } = entry;
    if (typeof key !== 'string' || typeof label !== 'string' || typeof savedAtUtc !== 'string') continue;
    slots.push({ key, label, savedAtUtc });
  }
  return slots;
}

function readMenu(value: unknown): MenuView | null {
  if (!isRecord(value)) return null;
  const {
    visible, mode, canStart, canResume, canSave, canLoad, canRespawn, journeyOnward, slots,
  } = value;
  if (
    typeof visible !== 'boolean' || typeof mode !== 'string' ||
    typeof canStart !== 'boolean' || typeof canResume !== 'boolean' ||
    typeof canSave !== 'boolean' || typeof canLoad !== 'boolean' ||
    typeof canRespawn !== 'boolean' || typeof journeyOnward !== 'string'
  ) {
    return null;
  }
  return {
    visible, mode, canStart, canResume, canSave, canLoad, canRespawn, journeyOnward,
    slots: readSlots(slots),
  };
}

/** Rejects a snapshot that is not the declared contract in full. */
function readSnapshot(value: unknown): SnapshotView | null {
  if (!isRecord(value)) return null;
  const {
    ready, mode, hp, maxHp, mana, maxMana, charge, outcome, level, avatar, defeated, menu,
  } = value;
  if (
    typeof ready !== 'boolean' || typeof mode !== 'string' ||
    typeof hp !== 'number' || typeof maxHp !== 'number' ||
    typeof mana !== 'number' || typeof maxMana !== 'number' ||
    typeof charge !== 'number' || typeof outcome !== 'string' ||
    typeof level !== 'number' || typeof avatar !== 'string' ||
    typeof defeated !== 'boolean'
  ) {
    return null;
  }
  const view = readMenu(menu);
  if (view === null) return null;
  return { ready, mode, hp, maxHp, mana, maxMana, charge, outcome, level, avatar, defeated, menu: view };
}

function bar(className: string, label: string): { root: HTMLElement; fill: HTMLElement } {
  const root = document.createElement('div');
  root.className = `bar ${className}`;
  const caption = document.createElement('span');
  caption.className = 'label';
  caption.textContent = label;
  const track = document.createElement('div');
  track.className = 'track';
  const fill = document.createElement('div');
  fill.className = 'fill';
  track.append(fill);
  root.append(caption, track);
  return { root, fill };
}

async function loadEngineLiveDebug(): Promise<LiveDebugModule> {
  return import('@rusty-engine/live-debug');
}

/**
 * Mounts the companion into `root` and returns a disposer. The Engine panel is
 * mounted asynchronously; the disposer waits for it so a fast unmount cannot
 * leak a panel.
 */
export function mountProductUi(
  root: HTMLElement,
  context: ProductUiContext,
  dependencies: ProductUiDependencies = {},
): { dispose(): void } {
  const style = document.createElement('style');
  style.textContent = STYLES;

  const hud = document.createElement('section');
  hud.className = 'abyss-hud';
  hud.dataset['ready'] = 'false';
  const hpBar = bar('hp', 'health');
  const manaBar = bar('mana', 'mana');
  const chargeBar = bar('charge', 'charge');
  const status = document.createElement('p');
  status.className = 'status';
  // Before the first projection there is nothing to show. Say so instead of
  // rendering full bars, which is what an unset width looks like.
  status.textContent = 'Waiting for the first session snapshot…';
  hud.append(hpBar.root, manaBar.root, chargeBar.root, status);

  const menu = document.createElement('nav');
  menu.className = 'abyss-menu';
  menu.hidden = true;
  menu.setAttribute('data-rusty-ui-interactive', '');
  const menuTitle = document.createElement('p');
  menuTitle.className = 'title';
  const resumeButton = button('Resume');
  const startButton = button('Start a new session');
  const saveButton = button('Save (quicksave)');
  const loadButton = button('Journey Onward');
  const respawnButton = button('Respawn at the anchor');
  const stopButton = button('Quit to menu');
  const slotList = document.createElement('ul');
  slotList.className = 'slots';
  menu.append(menuTitle, resumeButton, startButton, saveButton, loadButton, respawnButton, stopButton, slotList);

  const debug = document.createElement('aside');
  debug.className = 'abyss-debug';
  debug.hidden = true;
  debug.setAttribute('data-rusty-ui-interactive', '');
  const debugTitle = document.createElement('p');
  debugTitle.className = 'title';
  debugTitle.textContent = 'Engine live debug';
  const debugHost = document.createElement('div');
  debugHost.className = 'panel';
  debug.append(debugTitle, debugHost);

  let metricsVisible = true;
  let menuVisible = false;

  const metrics = document.createElement('section');
  metrics.className = 'abyss-metrics';
  // Shown only once a widget is mounted: before that, and after a load failure,
  // an empty bordered box would be a placeholder for nothing.
  metrics.hidden = true;
  metrics.setAttribute('data-rusty-ui-interactive', '');
  const metricsHost = document.createElement('div');
  metricsHost.className = 'panel';
  metrics.append(metricsHost);

  // The Engine console and the renderer metrics readout are menu-time tools:
  // while the product is playing the Engine keeps the pointer captured, so a
  // control outside the menu could not be clicked.
  const toggleDebug = (): void => {
    debug.hidden = !debug.hidden;
    debugToggle.setAttribute('aria-pressed', String(!debug.hidden));
  };

  // While the product plays, the Engine keeps the pointer captured and a click
  // belongs to the canvas, so the menu also offers single-key access.
  const debugToggle = button('Engine console (c)');
  const metricsToggle = button('Renderer metrics (m)');
  debugToggle.setAttribute('aria-pressed', 'false');
  metricsToggle.setAttribute('aria-pressed', String(metricsVisible));
  menu.append(debugToggle, metricsToggle);

  root.append(style, hud, menu, debug, metrics);

  const claim = (intent: string): void => {
    context.intents?.claim(intent, { kind: 'digital', active: true });
  };

  // The Engine's shell owns cursor capture: while it stays in gameplay mode a
  // pointer-locked click belongs to the canvas, so an open menu could never be
  // clicked. The projection decides the mode, and a redundant switch is skipped.
  let appliedMode: string | null = null;
  const applyInteractionMode = (mode: 'gameplay' | 'interface'): void => {
    if (appliedMode === mode) return;
    appliedMode = mode;
    context.ui?.setInteractionMode(mode);
    if (mode === 'gameplay') context.ui?.focusGameplay();
  };

  // Entering play always asks for gameplay mode and pointer capture: the menu
  // was interactive, and the shell must hand the pointer back even if the last
  // projection already said gameplay.
  const enterPlay = (intent: string): void => {
    claim(intent);
    appliedMode = 'gameplay';
    context.ui?.setInteractionMode('gameplay');
    context.ui?.focusGameplay();
  };

  resumeButton.addEventListener('click', () => enterPlay(INTENTS.resume));
  startButton.addEventListener('click', () => enterPlay(INTENTS.start));
  saveButton.addEventListener('click', () => claim(INTENTS.quicksave));
  loadButton.addEventListener('click', () => claim(INTENTS.journeyOnward));
  respawnButton.addEventListener('click', () => enterPlay(INTENTS.respawn));
  stopButton.addEventListener('click', () => claim(INTENTS.stop));

  debugToggle.addEventListener('click', toggleDebug);
  metricsToggle.addEventListener('click', () => {
    metrics.hidden = metricsVisible;
    setMetricsVisible(!metricsVisible);
  });

  const onMenuKey = (event: KeyboardEvent): void => {
    if (event.ctrlKey || event.altKey || event.metaKey) return;
    // A tool can always be opened from the menu and always closed again, even
    // once play resumed and the menu went away.
    const consoleKey = menuVisible || !debug.hidden;
    const metricsKey = menuVisible || metricsVisible;
    if (!consoleKey && !metricsKey) return;
    const target = event.target as { closest?: (selector: string) => Element | null } | null;
    if (typeof target?.closest === 'function'
      && target.closest('input, textarea, [contenteditable="true"]') !== null) {
      return;
    }
    if (consoleKey && (event.key === 'c' || event.key === 'C')) {
      toggleDebug();
      event.preventDefault();
    } else if (metricsKey && (event.key === 'm' || event.key === 'M')) {
      metrics.hidden = metricsVisible;
      setMetricsVisible(!metricsVisible);
      event.preventDefault();
    }
  };

  // A lost pointer lock is a real interaction change: the product decides what
  // it means (it pauses and publishes the menu) and the shell re-locks only
  // when gameplay input is requested again.
  const onPointerLockChange = (): void => {
    const locked = typeof document !== 'undefined' && document.pointerLockElement !== null;
    root.dataset['pointerLocked'] = String(locked);
  };
  if (typeof document !== 'undefined') {
    document.addEventListener('pointerlockchange', onPointerLockChange);
    document.addEventListener('keydown', onMenuKey);
    onPointerLockChange();
  }

  let panelMount: LiveDebugMount | null = null;
  let metricsMount: LiveDebugMount | null = null;
  let disposed = false;
  const loadLiveDebug = dependencies.loadLiveDebug ?? loadEngineLiveDebug;

  // The renderer-metrics widget follows the Engine renderer's own widget state,
  // so the toggle asks the Engine to show or hide it by re-mounting with the
  // wanted state instead of hiding a DOM node the Engine would keep updating.
  // The mount that carries the hide command stays alive so the request lands.
  const setMetricsVisible = (visible: boolean): void => {
    metricsVisible = visible;
    metricsToggle.setAttribute('aria-pressed', String(visible));
    metrics.hidden = !visible;
    metricsMount?.dispose();
    metricsMount = null;
    void ready.then((module) => {
      if (disposed || module === null) return;
      metricsMount = module.mountRendererMetricsWidget(metricsHost, { initiallyVisible: visible });
      metrics.hidden = !metricsVisible;
    });
  };

  const ready = (async (): Promise<LiveDebugModule | null> => {
    try {
      const module = await loadLiveDebug();
      if (disposed) return null;
      const panel = await module.mountLiveDebugPanel(debugHost, { enabled: true, presentation: 'inline' });
      if (disposed) {
        panel.dispose();
        return null;
      }
      panelMount = panel;
      return module;
    } catch {
      debugTitle.textContent = 'Engine live debug unavailable';
      return null;
    }
  })();

  void ready.then((module) => {
    if (disposed || module === null) return;
    setMetricsVisible(metricsVisible);
  });

  const renderMenu = (view: MenuView, defeated: boolean): void => {
    menu.hidden = !view.visible;
    menuVisible = view.visible;
    menuTitle.textContent = defeated ? 'You have fallen' : `Session ${view.mode}`;
    resumeButton.disabled = !view.canResume;
    startButton.disabled = !view.canStart;
    saveButton.disabled = !view.canSave;
    loadButton.disabled = !view.canLoad;
    loadButton.textContent = view.journeyOnward === '' ? 'Journey Onward' : `Journey Onward (${view.journeyOnward})`;
    respawnButton.disabled = !view.canRespawn;
    slotList.replaceChildren(...view.slots.map((slot) => {
      const item = document.createElement('li');
      item.textContent = `${slot.label} — ${slot.savedAtUtc}`;
      return item;
    }));
  };

  const renderSnapshot = (view: SnapshotView): void => {
    hud.dataset['ready'] = String(view.ready);
    const percent = (value: number, maximum: number): string =>
      `${maximum > 0 ? (100 * Math.max(0, Math.min(value, maximum))) / maximum : 0}%`;
    hpBar.fill.style.width = percent(view.hp, view.maxHp);
    manaBar.fill.style.width = percent(view.mana, view.maxMana);
    chargeBar.fill.style.width = `${100 * Math.max(0, Math.min(1, view.charge))}%`;
    applyInteractionMode(view.menu.visible ? 'interface' : 'gameplay');
    const vitals = `${Math.round(view.hp)}/${Math.round(view.maxHp)} hp · ${Math.round(view.mana)}/${Math.round(view.maxMana)} mana`;
    status.textContent = view.outcome === ''
      ? `${view.ready ? vitals : 'No live session'} · level ${view.level}`
      : view.outcome;
    renderMenu(view.menu, view.defeated);
  };

  const unsubscribe = context.projection?.subscribe((projection) => {
    if (projection?.contract !== HUD_CONTRACT) return;
    const view = readSnapshot(projection.value);
    if (view === null) return;
    renderSnapshot(view);
  });

  return {
    dispose(): void {
      disposed = true;
      unsubscribe?.();
      if (typeof document !== 'undefined') {
        document.removeEventListener('pointerlockchange', onPointerLockChange);
        document.removeEventListener('keydown', onMenuKey);
      }
      panelMount?.dispose();
      metricsMount?.dispose();
      style.remove();
      hud.remove();
      menu.remove();
      debug.remove();
      metrics.remove();
    },
  };
}

function button(label: string): HTMLButtonElement {
  const element = document.createElement('button');
  element.type = 'button';
  element.textContent = label;
  return element;
}
