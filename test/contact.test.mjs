import test from 'node:test';
import assert from 'node:assert/strict';
import { findContact } from '../contact.mjs';

test('contact requires a limb to approach within the target radius', () => {
  const target = [{ x: 0, y: 1, z: 0 }];
  const previous = new Map();
  const strike = x => [{ key: 'hand', p: { x, y: 1, z: 0 } }];
  assert.equal(findContact(strike(0.8), target, 0.34, previous), null);
  assert.equal(findContact(strike(0.4), target, 0.34, previous), null);
  assert.ok(findContact(strike(0.33), target, 0.34, previous));
  assert.equal(findContact(strike(0.33), target, 0.34, previous), null);
  assert.equal(findContact(strike(0.34), target, 0.34, previous), null);
});

test('a moving target does not turn a stationary limb into an attack', () => {
  const history = new Map();
  const hand = [{ key: 'hand', p: { x: 0, y: 1, z: 0 } }];
  assert.equal(findContact(hand, [{ x: 0.5, y: 1, z: 0 }], 0.34, history), null);
  assert.equal(findContact(hand, [{ x: 0.3, y: 1, z: 0 }], 0.34, history), null);
});

test('a limb moving away cannot hit an approaching target', () => {
  const history = new Map();
  assert.equal(findContact([{ key: 'hand', p: { x: 0, y: 1, z: 0 } }], [{ x: 0.6, y: 1, z: 0 }], 0.34, history), null);
  assert.equal(findContact([{ key: 'hand', p: { x: -0.01, y: 1, z: 0 } }], [{ x: 0.3, y: 1, z: 0 }], 0.34, history), null);
});
