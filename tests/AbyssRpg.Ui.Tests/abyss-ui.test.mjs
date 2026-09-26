import { test } from 'node:test';
import assert from 'node:assert/strict';
import { JSDOM } from 'jsdom';
import { mountProductUi } from '../../src/ui/generated/main.js';

/**
 * The companion's contract: one projection stream in, declared intents out, and
 * the Engine's own live-debug panel mounted through the shell's stable
 * specifier. The Engine panel bundle cannot run in jsdom, so these tests inject
 * a stand-in loader and assert the mounting contract rather than the panel.
 */

function context() {
  const listeners = [];
  const claimed = [];
  const ui = {
    focused: 0,
    modes: [],
    focusGameplay() { this.focused += 1; },
    interactionMode: () => ui.modes.at(-1) ?? 'interface',
    setInteractionMode(mode) { ui.modes.push(mode); },
    active: () => true,
    allowsGameplayInput: () => true,
  };
  return {
    listeners,
    claimed,
    ui,
    projection: {
      subscribe: (fn) => {
        listeners.push(fn);
        return () => { listeners.splice(listeners.indexOf(fn), 1); };
      },
    },
    intents: { claim: (intent, value) => claimed.push({ intent, value }) },
  };
}

function liveDebug() {
  const panelMounts = [];
  const metricsMounts = [];
  return {
    panelMounts,
    metricsMounts,
    module: {
      mountLiveDebugPanel: async (host, options) => {
        const mount = { host, options, disposed: 0, dispose() { this.disposed += 1; } };
        panelMounts.push(mount);
        return mount;
      },
      mountRendererMetricsWidget: (host, options) => {
        const mount = { host, options, disposed: 0, dispose() { this.disposed += 1; } };
        metricsMounts.push(mount);
        return mount;
      },
    },
  };
}

const baseMenu = () => ({
  visible: false,
  mode: 'playing',
  canStart: false,
  canResume: false,
  canSave: true,
  canLoad: true,
  canRespawn: false,
  journeyOnward: 'autosave/level-1',
  invertY: false,
  detail: 'High',
  slots: [{ key: 'autosave/level-1', label: 'Autosave level-1', savedAtUtc: '2026-09-25T00:00:00.0000000Z' }],
});

const snapshot = (overrides = {}) => ({
  ready: true,
  mode: 'playing',
  level: 1,
  avatar: 'Tester',
  hp: 20,
  maxHp: 40,
  mana: 6,
  maxMana: 12,
  charge: 0.5,
  lightRadius: 6,
  yawRadians: 0,
  wind: 0,
  outcome: '',
  defeated: false,
  swimming: false,
  flying: false,
  presentActors: 0,
  slots: 1,
  menu: baseMenu(),
  conversation: null,
  ...overrides,
});

function mount(t, ctx = context(), deps = {}) {
  const dom = new JSDOM('<body></body>');
  globalThis.document = dom.window.document;
  const ui = mountProductUi(dom.window.document.body, ctx, deps);
  t.after(() => ui.dispose());
  return { dom, ctx, ui, document: dom.window.document };
}

test('the HUD reads how far the light reaches', async (t) => {
  const { ctx, document } = mount(t);
  ctx.listeners.forEach((fn) => fn({ contract: 'abyss.ui.snapshot.v1', value: snapshot() }));
  assert.equal(document.querySelector('.abyss-hud .light').textContent, 'light 6 tiles');

  // Light decides how much of the world is drawn, so a projection that does not
  // say how far it reaches is not one this HUD will show: it keeps the last
  // projection it could read instead of half-rendering the new one.
  ctx.listeners.forEach((fn) =>
    fn({ contract: 'abyss.ui.snapshot.v1', value: snapshot({ lightRadius: undefined }) }));
  assert.equal(document.querySelector('.abyss-hud').dataset.ready, 'true');
  assert.equal(document.querySelector('.abyss-hud .light').textContent, 'light 6 tiles');
});

test('HUD waits for a snapshot instead of showing full bars', (t) => {
  const { document } = mount(t);
  assert.equal(document.querySelector('.abyss-hud').dataset.ready, 'false');
  assert.notEqual(document.querySelector('.abyss-hud .hp .fill').style.width, '100%');
  assert.match(document.querySelector('.abyss-hud .status').textContent, /Waiting for the first session snapshot/);
});

test('HUD renders projected vitals and charge, and ignores other contracts', (t) => {
  const { ctx, document } = mount(t);
  ctx.listeners.forEach((fn) => fn({ contract: 'abyss.ui.snapshot.v1', value: snapshot() }));
  assert.equal(document.querySelector('.abyss-hud').dataset.ready, 'true');
  assert.equal(document.querySelector('.abyss-hud .hp .fill').style.width, '50%');
  assert.equal(document.querySelector('.abyss-hud .mana .fill').style.width, '50%');
  assert.equal(document.querySelector('.abyss-hud .charge .fill').style.width, '50%');
  assert.match(document.querySelector('.abyss-hud .status').textContent, /20\/40 hp · 6\/12 mana · level 1/);

  // A malformed value and a foreign contract leave the last good frame alone.
  ctx.listeners.forEach((fn) => fn({ contract: 'abyss.ui.snapshot.v1', value: { hp: 'a lot' } }));
  ctx.listeners.forEach((fn) => fn({ contract: 'abyss.other.v1', value: {} }));
  assert.equal(document.querySelector('.abyss-hud .hp .fill').style.width, '50%');
});

test('menu visibility and availability follow the projection, not local state', (t) => {
  const { ctx, document } = mount(t);
  const menu = document.querySelector('.abyss-menu');
  assert.equal(menu.hidden, true);

  ctx.listeners.forEach((fn) => fn({
    contract: 'abyss.ui.snapshot.v1',
    value: snapshot({
      mode: 'paused',
      menu: {
        ...baseMenu(),
        visible: true,
        mode: 'paused',
        canSave: true,
        canResume: true,
        canRespawn: false,
        slots: [{ key: 'quicksave/0', label: 'Quicksave 0', savedAtUtc: '2026-09-25T01:00:00.0000000Z' }],
      },
    }),
  }));
  assert.equal(menu.hidden, false);
  assert.match(menu.querySelector('.title').textContent, /Session paused/);

  const buttons = [...menu.querySelectorAll('button')];
  const byLabel = (text) => buttons.find((b) => b.textContent.startsWith(text));
  assert.equal(byLabel('Resume').disabled, false);
  assert.equal(byLabel('Save').disabled, false);
  assert.equal(byLabel('Respawn at the anchor').disabled, true);
  assert.equal(byLabel('Journey Onward').disabled, false);
  assert.match(byLabel('Journey Onward').textContent, /autosave\/level-1/);
  assert.equal(menu.querySelectorAll('.slots li').length, 1);

  // A projection that hides the menu hides it again: nothing is DOM-local.
  ctx.listeners.forEach((fn) => fn({ contract: 'abyss.ui.snapshot.v1', value: snapshot() }));
  assert.equal(menu.hidden, true);

  // The Engine shell owns cursor capture, so the mode follows the projection
  // and repeats are not re-issued.
  assert.deepEqual(ctx.ui.modes, ['interface', 'gameplay']);
  ctx.listeners.forEach((fn) => fn({ contract: 'abyss.ui.snapshot.v1', value: snapshot() }));
  assert.deepEqual(ctx.ui.modes, ['interface', 'gameplay']);

  // Resume and respawn ask for gameplay focus again.
  ctx.listeners.forEach((fn) => fn({
    contract: 'abyss.ui.snapshot.v1',
    value: snapshot({ mode: 'paused', menu: { ...baseMenu(), visible: true, canResume: true } }),
  }));
  ctx.ui.focused = 0;
  const resume = [...menu.querySelectorAll('button')].find((b) => b.textContent.startsWith('Resume'));
  resume.dispatchEvent(new (document.defaultView.MouseEvent)('click', { bubbles: true }));
  assert.deepEqual(ctx.ui.modes, ['interface', 'gameplay', 'interface', 'gameplay']);
  assert.equal(ctx.ui.focused, 1);
});

test('every control claims a declared intent', (t) => {
  const { ctx, document } = mount(t);
  ctx.listeners.forEach((fn) => fn({ contract: 'abyss.ui.snapshot.v1', value: snapshot() }));
  const menu = document.querySelector('.abyss-menu');
  const actionButtons = [...menu.querySelectorAll('button')].filter(
    (b) => !['Engine console', 'Renderer metrics'].some((label) => b.textContent.startsWith(label)),
  );
  for (const button of actionButtons) {
    button.disabled = false;
    button.dispatchEvent(new (document.defaultView.MouseEvent)('click', { bubbles: true }));
  }

  const declared = new Set([
    'abyss.lifecycle.start', 'abyss.lifecycle.pause', 'abyss.lifecycle.resume', 'abyss.lifecycle.stop',
    'abyss.action.quicksave', 'abyss.action.journey-onward',
    ...Array.from({ length: 13 }, (_, index) => `abyss.action.load-slot-${index + 1}`),
    'abyss.action.respawn',
  ]);
  assert.deepEqual(
    ctx.claimed.map((claim) => claim.intent).sort(),
    ['abyss.action.journey-onward', 'abyss.action.load-slot-1', 'abyss.action.quicksave',
      'abyss.action.respawn', 'abyss.lifecycle.resume', 'abyss.lifecycle.start', 'abyss.lifecycle.stop'].sort(),
  );
  for (const claim of ctx.claimed) {
    assert.ok(declared.has(claim.intent), `${claim.intent} is not a declared intent`);
    assert.deepEqual(claim.value, { kind: 'digital', active: true });
  }
  // Every control that returns to play keeps the pointer-lock gesture alive by
  // asking for gameplay focus, even where the mode already reads gameplay.
  for (const label of ['Resume', 'Start a new session', 'Respawn at the anchor']) {
    const button = [...document.querySelectorAll('.abyss-menu button')]
      .find((candidate) => candidate.textContent.startsWith(label));
    ctx.ui.focused = 0;
    button.disabled = false;
    button.dispatchEvent(new (document.defaultView.MouseEvent)('click', { bubbles: true }));
    assert.equal(ctx.ui.focused, 1, `${label} did not ask for gameplay focus`);
  }
  assert.equal(ctx.ui.modes.at(-1), 'gameplay');
});

test('the Engine live-debug panel and metrics widget are mounted and disposed', async (t) => {
  const debug = liveDebug();
  const { document, ui } = mount(t, context(), { loadLiveDebug: async () => debug.module });
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(debug.panelMounts.length, 1);
  assert.equal(debug.metricsMounts.length, 1);
  assert.deepEqual(debug.panelMounts[0].options, { enabled: true, presentation: 'inline' });
  assert.equal(debug.panelMounts[0].host, document.querySelector('.abyss-debug .panel'));
  assert.equal(debug.metricsMounts[0].host, document.querySelector('.abyss-metrics > div'));
  // The widget reads the Engine renderer's own widget state, so the first mount
  // asks the Engine to show it.
  assert.deepEqual(debug.metricsMounts[0].options, { initiallyVisible: true });

  const metrics = document.querySelector('.abyss-metrics');
  assert.equal(metrics.hidden, false);
  const toggle = (label) => [...document.querySelectorAll('.abyss-menu button')]
    .find((b) => b.textContent.startsWith(label));
  toggle('Renderer metrics').dispatchEvent(new (document.defaultView.MouseEvent)('click', { bubbles: true }));
  await new Promise((resolve) => setImmediate(resolve));
  assert.equal(metrics.hidden, true);
  // Hiding asks the Engine to hide its widget rather than dropping the request.
  assert.deepEqual(debug.metricsMounts.at(-1).options, { initiallyVisible: false });
  assert.equal(debug.metricsMounts.at(-1).disposed, 0);
  toggle('Engine console').dispatchEvent(new (document.defaultView.MouseEvent)('click', { bubbles: true }));
  assert.equal(document.querySelector('.abyss-debug').hidden, false);

  ui.dispose();
  assert.equal(debug.panelMounts[0].disposed, 1);
  // The show mount was replaced by the hide mount, and disposal ends the last one.
  assert.equal(debug.metricsMounts.length, 2);
  assert.equal(debug.metricsMounts[0].disposed, 1);
  assert.equal(debug.metricsMounts[1].disposed, 1);
});

test('the menu keys open the Engine console and hide the metrics widget', async (t) => {
  const debug = liveDebug();
  const { ctx, document } = mount(t, context(), { loadLiveDebug: async () => debug.module });
  await new Promise((resolve) => setImmediate(resolve));
  const key = (k) => document.defaultView.document.dispatchEvent(
    new (document.defaultView.KeyboardEvent)('keydown', { key: k, bubbles: true }),
  );

  // Closed menu: the keys are gameplay keys, not tool keys.
  key('c');
  assert.equal(document.querySelector('.abyss-debug').hidden, true);

  ctx.listeners.forEach((fn) => fn({
    contract: 'abyss.ui.snapshot.v1',
    value: snapshot({ mode: 'paused', menu: { ...baseMenu(), visible: true, canResume: true } }),
  }));
  key('c');
  assert.equal(document.querySelector('.abyss-debug').hidden, false);
  key('c');
  assert.equal(document.querySelector('.abyss-debug').hidden, true);

  assert.equal(document.querySelector('.abyss-metrics').hidden, false);
  key('m');
  assert.equal(document.querySelector('.abyss-metrics').hidden, true);

  // An open console can still be closed after play resumed and the menu went,
  // but a closed one cannot be opened from gameplay.
  key('c');
  assert.equal(document.querySelector('.abyss-debug').hidden, false);
  ctx.listeners.forEach((fn) => fn({ contract: 'abyss.ui.snapshot.v1', value: snapshot() }));
  assert.equal(document.querySelector('.abyss-menu').hidden, true);
  key('c');
  assert.equal(document.querySelector('.abyss-debug').hidden, true);
  key('c');
  assert.equal(document.querySelector('.abyss-debug').hidden, true);
  key('m');
  assert.equal(document.querySelector('.abyss-metrics').hidden, true);
});

test('interactive surfaces are marked for the Engine input lane', (t) => {
  const { document } = mount(t);
  for (const selector of ['.abyss-menu', '.abyss-debug', '.abyss-metrics']) {
    assert.equal(document.querySelector(selector).hasAttribute('data-rusty-ui-interactive'), true, selector);
  }
});

test('a missing Engine debug panel degrades to a stated message', async (t) => {
  const { document } = mount(t, context(), {
    loadLiveDebug: async () => { throw new Error('no live-debug lane'); },
  });
  await new Promise((resolve) => setImmediate(resolve));
  assert.match(document.querySelector('.abyss-debug .title').textContent, /unavailable/);
});

/**
 * The injected loader above asserts the mounting contract; this test closes the
 * other half by loading the packaged Engine module the runtime pack actually
 * serves. The pack is installed by `scripts/verify.sh`, so in the ordinary loop
 * this runs; without it the check reports a skip rather than passing silently.
 */
test('the packaged Engine live-debug module exports and mounts', async (t) => {
  const pack = new URL('../../.runtime/runtime-pack/share/live-debug-panel/index.js', import.meta.url);
  let module;
  try {
    module = await import(pack.href);
  } catch (error) {
    t.diagnostic(`SKIP: the runtime pack is not installed (${error.message}).`);
    return;
  }

  assert.equal(typeof module.mountLiveDebugPanel, 'function');
  assert.equal(typeof module.mountRendererMetricsWidget, 'function');

  const dom = new JSDOM('<!doctype html><html><body><div id="host"></div></body></html>');
  const previous = {
    window: globalThis.window,
    document: globalThis.document,
    fetch: globalThis.fetch,
    HTMLElement: globalThis.HTMLElement,
    Element: globalThis.Element,
    Node: globalThis.Node,
  };
  globalThis.window = dom.window;
  globalThis.document = dom.window.document;
  globalThis.HTMLElement = dom.window.HTMLElement;
  globalThis.Element = dom.window.Element;
  globalThis.Node = dom.window.Node;
  // The module polls the Engine's live-debug route through its own transport;
  // a stub keeps the assertion about mounting rather than about a served host.
  const transport = {
    async execute() {
      return { message: JSON.stringify({ available: false, diagnostic: 'test stub' }) };
    },
  };
  try {
    const host = dom.window.document.getElementById('host');
    const panel = await module.mountLiveDebugPanel(host, { enabled: false, presentation: 'inline', transport });
    assert.ok(host.textContent.length > 0);
    panel.dispose();

    const metricsHost = dom.window.document.createElement('div');
    dom.window.document.body.append(metricsHost);
    const metrics = module.mountRendererMetricsWidget(metricsHost, { initiallyVisible: false, transport });
    assert.ok(metricsHost.childElementCount >= 1);
    metrics.dispose();
    assert.equal(metricsHost.childElementCount, 0);
  } finally {
    globalThis.window = previous.window;
    globalThis.document = previous.document;
    globalThis.fetch = previous.fetch;
    globalThis.HTMLElement = previous.HTMLElement;
    globalThis.Element = previous.Element;
    globalThis.Node = previous.Node;
    dom.window.close();
  }
});

test('the HUD shows the conversation the session projects, and hides it again', (t) => {
  const ctx = context();
  mount(t, ctx);
  const panel = () => globalThis.document.querySelector('.abyss-talk');

  // No conversation: the panel stays out of the way.
  assert.equal(panel().hidden, true);

  ctx.listeners.forEach((fn) => fn({
    contract: 'abyss.ui.snapshot.v1',
    value: snapshot({
      conversation: {
        npc: 'a hooded figure',
        lines: ['A stranger walks the Abyss.', 'What do you want of me?'],
        prompts: ['name', 'job'],
        attitude: 2,
        lastTrade: '',
      },
    }),
  }));

  assert.equal(panel().hidden, false);
  assert.match(panel().querySelector('.title').textContent, /a hooded figure/);
  assert.match(panel().querySelector('.say').textContent, /A stranger walks the Abyss\./);
  assert.match(panel().querySelector('.say').textContent, /What do you want of me\?/);
  assert.match(panel().querySelector('.prompts').textContent, /name · job/);

  // A trade shows in the panel's title, and ending the talk hides it again.
  ctx.listeners.forEach((fn) => fn({
    contract: 'abyss.ui.snapshot.v1',
    value: snapshot({
      conversation: {
        npc: 'a merchant', lines: ['Done.'], prompts: [], attitude: 3, lastTrade: 'Accepted',
      },
    }),
  }));
  assert.match(panel().querySelector('.title').textContent, /Accepted/);

  ctx.listeners.forEach((fn) => fn({
    contract: 'abyss.ui.snapshot.v1',
    value: snapshot({ conversation: null }),
  }));
  assert.equal(panel().hidden, true);
});

test('a conversation of the wrong shape is not rendered as a contract', (t) => {
  const ctx = context();
  mount(t, ctx);
  ctx.listeners.forEach((fn) => fn({
    contract: 'abyss.ui.snapshot.v1',
    value: snapshot({ conversation: { npc: 5, lines: 'not a list' } }),
  }));
  assert.equal(globalThis.document.querySelector('.abyss-talk').hidden, true);
});

test('a listed save can be resumed by name', (t) => {
  const ctx = context();
  mount(t, ctx);
  ctx.listeners.forEach((fn) => fn({
    contract: 'abyss.ui.snapshot.v1',
    value: snapshot({
      menu: {
        ...baseMenu(),
        visible: true,
        slots: [{ key: 'quicksave/0', label: 'Quicksave', savedAtUtc: '2026-01-01T00:00:00.0000000Z' }],
      },
    }),
  }));

  const rows = globalThis.document.querySelectorAll('.abyss-menu .slots li button');
  assert.equal(rows.length, 1);
  assert.match(rows[0].textContent, /Quicksave/);
  rows[0].dispatchEvent(new (globalThis.document.defaultView.MouseEvent)('click', { bubbles: true }));
  assert.deepEqual(ctx.claimed.map((claim) => claim.intent), ['abyss.action.load-slot-1']);
});
