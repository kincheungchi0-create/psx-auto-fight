/** Apply the visual state for the same hit result that changed health. */
export function applyCombatReaction(target, result, now) {
  if (!result || result.dodged) return null;
  if (target.hp <= 0) {
    target.pendingHit = null;
    target.alive = false;
    target.state = 'death';
    target.reactionStart = now;
    target.busyUntil = now + 0.585;
    target.pose.rotation.x = -0.15;
    target.play('idle');
    return '擊倒';
  }
  if (result.blocked) {
    target.blockUntil = now + 0.3;
    target.pose.rotation.x = -0.2;
    return '格擋';
  }
  target.pendingHit = null;
  target.state = 'hit';
  target.reactionStart = now;
  target.busyUntil = now + 0.585;
  target.pose.rotation.x = -0.2;
  target.play('idle');
  return '受擊';
}
