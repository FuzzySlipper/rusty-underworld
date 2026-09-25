/**
 * The Abyss product's DOM companion: HUD, menus, debug console, and
 * renderer-metrics toggle over Engine-delivered projections.
 *
 * It owns no state, evaluates no rules, and starts no loop or timer. Every
 * value it shows arrived in the last projection from the product, and every
 * action it sends is an intent the product defines. Patterns follow the
 * sibling `src/ui` companions (crawler/rifles); nothing is copied from game
 * donors.
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
      value: { kind: 'product-payload'; contract: string; data: Record<string, unknown> },
    ): void;
  };
}

const HUD_CONTRACT = 'abyss.ui.snapshot.v1';
const MAP_CONTRACT = 'abyss.ui.map.v1';
const UI_ACTION_INTENT = 'abyss.ui';
const UI_ACTION_CONTRACT = 'abyss.ui.action.v1';
const DEBUG_INTENT = 'abyss.debug';

interface HudView {
  readonly hp: number;
  readonly maxHp: number;
  readonly mana: number;
  readonly maxMana: number;
  readonly charge: number;
  readonly yaw: number;
  readonly outcome: string;
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
}
.abyss-hud .bar { width: 12rem; height: 0.55rem; background: #2c2822; border-radius: 0.2rem; }
.abyss-hud .bar > div { height: 100%; border-radius: 0.2rem; }
.abyss-hud .hp > div { background: #b04434; }
.abyss-hud .mana > div { background: #3f6fb5; }
.abyss-hud .charge > div { background: #c9a13b; }
.abyss-menu, .abyss-debug {
  position: fixed;
  border: 1px solid rgba(210, 196, 158, 0.35);
  border-radius: 0.35rem;
  background: rgba(18, 16, 14, 0.92);
  color: #e8e0cc;
  font: 13px/1.45 system-ui, sans-serif;
}
.abyss-menu { top: 0.75rem; right: 0.75rem; padding: 0.6rem 0.75rem; min-width: 12rem; }
.abyss-menu button { display: block; width: 100%; margin: 0.2rem 0; }
.abyss-debug { left: 0.75rem; top: 0.75rem; padding: 0.5rem 0.75rem; min-width: 18rem; }
.abyss-debug output { display: block; white-space: pre-wrap; max-height: 12rem; overflow: auto; }
`;

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function readHud(value: unknown): HudView | null {
  if (!isRecord(value)) return null;
  const { hp, maxHp, mana, maxMana, charge, yawRadians, outcome } = value;
  if (
    typeof hp !== 'number' || typeof maxHp !== 'number' ||
    typeof mana !== 'number' || typeof maxMana !== 'number' ||
    typeof charge !== 'number' || typeof yawRadians !== 'number' ||
    typeof outcome !== 'string'
  ) {
    return null;
  }
  return { hp, maxHp, mana, maxMana, charge, yaw: yawRadians, outcome };
}

function bar(className: string): { root: HTMLElement; fill: HTMLElement } {
  const root = document.createElement('div');
  root.className = `bar ${className}`;
  const fill = document.createElement('div');
  root.append(fill);
  return { root, fill };
}

/**
 * Mounts the companion into `root` and returns a disposer. The Esc menu
 * exits pointer lock (unlocking the mouse) and offers pause/resume;
 * metrics and console ride in the debug panel.
 */
export function mountProductUi(root: HTMLElement, context: ProductUiContext): { dispose(): void } {
  const style = document.createElement('style');
  style.textContent = STYLES;

  const hud = document.createElement('section');
  hud.className = 'abyss-hud';
  const hpBar = bar('hp');
  const manaBar = bar('mana');
  const chargeBar = bar('charge');
  const status = document.createElement('p');
  hud.append(hpBar.root, manaBar.root, chargeBar.root, status);

  const menu = document.createElement('nav');
  menu.className = 'abyss-menu';
  menu.hidden = true;
  const resumeButton = document.createElement('button');
  resumeButton.type = 'button';
  resumeButton.textContent = 'Resume (lock mouse)';
  const pauseButton = document.createElement('button');
  pauseButton.type = 'button';
  pauseButton.textContent = 'Pause';
  menu.append(resumeButton, pauseButton);

  const debug = document.createElement('aside');
  debug.className = 'abyss-debug';
  const metricsButton = document.createElement('button');
  metricsButton.type = 'button';
  metricsButton.textContent = 'Renderer metrics';
  metricsButton.setAttribute('aria-pressed', 'false');
  const metricsOut = document.createElement('output');
  metricsOut.hidden = true;
  metricsOut.textContent = 'Renderer metrics arrive in Engine projections; none published yet.';
  const consoleInput = document.createElement('input');
  consoleInput.type = 'text';
  consoleInput.placeholder = 'debug console: help';
  consoleInput.setAttribute('aria-label', 'Debug console');
  const consoleOut = document.createElement('output');
  const isolate = (event: Event): void => event.stopPropagation();
  for (const name of ['pointerdown', 'keydown', 'keyup', 'click', 'wheel']) {
    debug.addEventListener(name, isolate);
  }
  debug.append(metricsButton, metricsOut, consoleInput, consoleOut);

  root.append(style, hud, menu, debug);

  const claim = (name: string): void => {
    context.intents?.claim(UI_ACTION_INTENT, {
      kind: 'product-payload',
      contract: UI_ACTION_CONTRACT,
      data: { action: name },
    });
  };

  resumeButton.addEventListener('click', () => {
    root.requestPointerLock?.();
    claim('session.resume');
    menu.hidden = true;
  });
  pauseButton.addEventListener('click', () => {
    claim('session.pause');
  });
  metricsButton.addEventListener('click', () => {
    const pressed = metricsButton.getAttribute('aria-pressed') === 'true';
    metricsButton.setAttribute('aria-pressed', String(!pressed));
    metricsOut.hidden = pressed;
  });
  consoleInput.addEventListener('keydown', (event) => {
    if (event.key !== 'Enter') return;
    const line = consoleInput.value.trim();
    consoleInput.value = '';
    if (line === '') return;
    if (line === 'help') {
      consoleOut.textContent = 'help | clear | metrics | <command forwarded as abyss.debug intent>';
      return;
    }
    if (line === 'clear') {
      consoleOut.textContent = '';
      return;
    }
    if (line === 'metrics') {
      metricsOut.hidden = false;
      metricsButton.setAttribute('aria-pressed', 'true');
      return;
    }
    context.intents?.claim(DEBUG_INTENT, {
      kind: 'product-payload',
      contract: UI_ACTION_CONTRACT,
      data: { command: line },
    });
    consoleOut.textContent += `> ${line}\n`;
  });
  const onKey = (event: KeyboardEvent): void => {
    if (event.key === 'Escape') menu.hidden = !menu.hidden;
  };
  document.addEventListener('keydown', onKey);

  const renderHud = (view: HudView): void => {
    hpBar.fill.style.width = `${(100 * view.hp) / Math.max(1, view.maxHp)}%`;
    manaBar.fill.style.width = `${(100 * view.mana) / Math.max(1, view.maxMana)}%`;
    chargeBar.fill.style.width = `${100 * Math.min(1, Math.max(0, view.charge))}%`;
    status.textContent =
      view.outcome === '' ? `${view.hp}/${view.maxHp} hp · ${view.mana}/${view.maxMana} mana` : view.outcome;
  };

  const unsubscribe = context.projection?.subscribe((projection) => {
    if (projection?.contract === HUD_CONTRACT) {
      const view = readHud(projection.value);
      if (view !== null) renderHud(view);
    }
    // Map-contract menu surfaces render as status text until dedicated
    // panels land; the shell never refuses the HUD over them.
    if (projection?.contract === MAP_CONTRACT && isRecord(projection.value)) {
      const journey = projection.value['journey'];
      if (typeof journey === 'string' && journey !== '') status.textContent = `Journey: ${journey}`;
    }
  });

  return {
    dispose(): void {
      unsubscribe?.();
      document.removeEventListener('keydown', onKey);
      style.remove();
      hud.remove();
      menu.remove();
      debug.remove();
    },
  };
}
