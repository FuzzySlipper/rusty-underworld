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
  yawRadians: 0,
  wind: 0,
  outcome: '',
  defeated: false,
  swimming: false,
  flying: false,
  presentActors: 0,
  slots: 1,
  menu: baseMenu(),
  ...overrides,
});

function mount(t, ctx = context(), deps = {}) {
  const dom = new JSDOM('<body></body>');
  globalThis.document = dom.window.document;
  const ui = mountProductUi(dom.window.document.body, ctx, deps);
  t.after(() => ui.dispose());
  return { dom, ctx, ui, document: dom.window.document };
}

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
  for (const button of menu.querySelectorAll('button')) {
    button.disabled = false;
    button.dispatchEvent(new (document.defaultView.MouseEvent)('click', { bubbles: true }));
  }

  const declared = new Set([
    'abyss.lifecycle.start', 'abyss.lifecycle.pause', 'abyss.lifecycle.resume', 'abyss.lifecycle.stop',
    'abyss.action.quicksave', 'abyss.action.journey-onward', 'abyss.action.respawn',
  ]);
  assert.deepEqual(
    ctx.claimed.map((claim) => claim.intent).sort(),
    ['abyss.action.journey-onward', 'abyss.action.quicksave', 'abyss.action.respawn',
      'abyss.lifecycle.resume', 'abyss.lifecycle.start', 'abyss.lifecycle.stop'].sort(),
  );
  for (const claim of ctx.claimed) {
    assert.ok(declared.has(claim.intent), `${claim.intent} is not a declared intent`);
    assert.deepEqual(claim.value, { kind: 'digital', active: true });
  }
  // The projection into gameplay captures once, and resume, start and respawn
  // re-capture even though the mode already reads gameplay.
  assert.equal(ctx.ui.focused, 4);
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
  const toggles = [...document.querySelectorAll('.abyss-controls button')];
  toggles[1].dispatchEvent(new (document.defaultView.MouseEvent)('click', { bubbles: true }));
  assert.equal(metrics.hidden, true);
  toggles[0].dispatchEvent(new (document.defaultView.MouseEvent)('click', { bubbles: true }));
  assert.equal(document.querySelector('.abyss-debug').hidden, false);

  ui.dispose();
  assert.equal(debug.panelMounts[0].disposed, 1);
  // The hide toggle disposes the first widget and mounts nothing in its place.
  assert.equal(debug.metricsMounts.length, 1);
  assert.equal(debug.metricsMounts[0].disposed, 1);
});

test('interactive surfaces are marked for the Engine input lane', (t) => {
  const { document } = mount(t);
  for (const selector of ['.abyss-menu', '.abyss-debug', '.abyss-metrics', '.abyss-controls']) {
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
