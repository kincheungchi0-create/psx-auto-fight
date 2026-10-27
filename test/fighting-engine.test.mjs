import test from 'node:test';
import assert from 'node:assert/strict';
import { FightingEngine } from '../fighting-engine.mjs';

function setup(opts = {}) {
  const a = { name: 'A', hp: 100, alive: true, state: 'idle', busyUntil: 0 };
  const b = { name: 'B', hp: 100, alive: true, state: 'idle', busyUntil: 0 };
  const engine = new FightingEngine([a, b], { ...opts, onRoundStart: () => { a.hp = b.hp = 100; a.alive = b.alive = true; } });
  engine.step(2);
  return { engine, a, b };
}

test('hits require contact and the active window and land only once', () => {
  const { engine, a, b } = setup();
  engine.startAttack(a, b, { activeFrom: 3, activeUntil: 4, damage: 20 });
  assert.equal(engine.tryHit(a, b, 2.9, true), null);
  assert.equal(engine.tryHit(a, b, 3.1, false), null);
  assert.equal(engine.tryHit(a, b, 3.1, true).damage, 20);
  assert.equal(engine.tryHit(a, b, 3.2, true), null);
  assert.equal(b.hp, 80);
});

test('a dodge avoids a hit and interruptions cancel attacks', () => {
  const { engine, a, b } = setup();
  engine.startAttack(a, b, { activeFrom: 2, activeUntil: 4, damage: 20 });
  b.state = 'dodge'; b.busyUntil = 4;
  assert.equal(engine.tryHit(a, b, 3, true).dodged, true);
  assert.equal(b.hp, 100);
  engine.startAttack(b, a, { activeFrom: 2, activeUntil: 4, damage: 30 });
  engine.cancelAttack(b);
  assert.equal(engine.tryHit(b, a, 3, true), null);
});

test('KO and timeout award rounds and best-of-three resets after a match', () => {
  const { engine, a, b } = setup({ roundSeconds: 5 });
  b.hp = 0; engine.step(3);
  assert.deepEqual(engine.scores, [1, 0]);
  assert.equal(engine.phase, 'roundOver');
  engine.step(6); engine.step(8);
  b.hp = 40; engine.step(13);
  assert.equal(engine.phase, 'matchOver');
  assert.deepEqual(engine.scores, [2, 0]);
  engine.step(18);
  assert.deepEqual(engine.scores, [0, 0]);
  assert.equal(engine.round, 1);
  assert.equal(a.hp, 100); assert.equal(b.hp, 100);
});

test('timeout with equal health is a draw', () => {
  const { engine } = setup({ roundSeconds: 5 });
  engine.step(7);
  assert.equal(engine.result, '平手');
  assert.deepEqual(engine.scores, [0, 0]);
});
