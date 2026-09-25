import { test } from 'node:test';
import assert from 'node:assert/strict';
import { JSDOM } from 'jsdom';
import { mountProductUi } from '../../src/ui/generated/main.js';

function context() {
  const listeners = [];
  const claimed = [];
  return {
    listeners,
    claimed,
    projection: { subscribe: (fn) => { listeners.push(fn); return () => {}; } },
    intents: { claim: (intent, value) => claimed.push({ intent, value }) },
  };
}

const hud = {
  contract: 'abyss.ui.snapshot.v1',
  value: { hp: 20, maxHp: 34, mana: 10, maxMana: 12, charge: 0.5, yawRadians: 0, wind: 0, outcome: '' },
};

test('HUD renders vitals and charge width', () => {
  const dom = new JSDOM('<body></body>');
  globalThis.document = dom.window.document;
  const ctx = context();
  const ui = mountProductUi(dom.window.document.body, ctx);
  ctx.listeners.forEach((fn) => fn(hud));
  const hpFill = dom.window.document.querySelector('.abyss-hud .hp > div');
  assert.ok(hpFill.style.width.startsWith('58.'));
  const chargeFill = dom.window.document.querySelector('.abyss-hud .charge > div');
  assert.equal(chargeFill.style.width, '50%');
  ui.dispose();
});

test('Esc toggles menu; console forwards unknown commands as intents', () => {
  const dom = new JSDOM('<body></body>');
  globalThis.document = dom.window.document;
  const ctx = context();
  const ui = mountProductUi(dom.window.document.body, ctx);
  const menu = dom.window.document.querySelector('.abyss-menu');
  assert.equal(menu.hidden, true);
  dom.window.document.dispatchEvent(new dom.window.KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
  assert.equal(menu.hidden, false);

  const input = dom.window.document.querySelector('.abyss-debug input');
  input.value = 'help';
  input.dispatchEvent(new dom.window.KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
  assert.equal(ctx.claimed.length, 0);
  input.value = 'spawn torch';
  input.dispatchEvent(new dom.window.KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
  assert.equal(ctx.claimed.length, 1);
  assert.equal(ctx.claimed[0].intent, 'abyss.debug');
  ui.dispose();
});
