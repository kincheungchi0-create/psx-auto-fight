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
