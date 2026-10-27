import test from 'node:test';
import assert from 'node:assert/strict';
import { applyCombatReaction } from '../combat-reaction.mjs';

function fighter(hp = 100) {
  return {hp, alive:true, state:'guard', pendingHit:{}, pose:{rotation:{x:0}}, play(kind) { this.played=kind; }};
}
test('blocked hit visibly recoils without interrupting the guard', () => {
  const f=fighter();
  assert.equal(applyCombatReaction(f,{blocked:true,damage:3},10),'格擋');
  assert.equal(f.state,'guard'); assert.equal(f.blockUntil,10.3);
  assert.equal(f.pose.rotation.x,-0.2);
});
test('unblocked hit visibly interrupts and starts hurt reaction', () => {
  const f=fighter();
  assert.equal(applyCombatReaction(f,{damage:20},10),'受擊');
  assert.equal(f.state,'hit'); assert.equal(f.pendingHit,null);
  assert.equal(f.pose.rotation.x,-0.2); assert.equal(f.reactionStart,10);
});
test('lethal block damage still causes a knockout', () => {
  const f=fighter(0);
  assert.equal(applyCombatReaction(f,{blocked:true,damage:3},10),'擊倒');
  assert.equal(f.state,'death'); assert.equal(f.alive,false);
});
test('dodging does not cause a reaction', () => {
  const f=fighter();
  assert.equal(applyCombatReaction(f,{dodged:true,damage:0},10),null);
  assert.equal(f.pose.rotation.x,0);
});
