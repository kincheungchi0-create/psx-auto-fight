/** Rules for a two-fighter, best-of-three match. Time values are in seconds. */
export class FightingEngine {
  constructor(fighters, { roundSeconds = 60, winsToWin = 2, startTime = 0, onRoundStart = () => {}, onRoundEnd = () => {} } = {}) {
    if (fighters.length !== 2) throw new Error('A match requires two fighters');
    this.fighters = fighters;
    this.roundSeconds = roundSeconds;
    this.winsToWin = winsToWin;
    this.onRoundStart = onRoundStart;
    this.onRoundEnd = onRoundEnd;
    this.scores = [0, 0];
    this.round = 0;
    this.phase = 'waiting';
    this.attacks = new Map();
    this.nextRound(startTime);
  }

  nextRound(now) {
    this.round++;
    this.phase = 'ready';
    this.phaseEnd = now + 2;
    this.remaining = this.roundSeconds;
    this.result = '';
    this.attacks.clear();
    this.onRoundStart(this.round);
  }

  step(now) {
    if (this.phase === 'ready' && now >= this.phaseEnd) {
      this.phase = 'fight';
      this.deadline = now + this.roundSeconds;
    }
    if (this.phase === 'fight') {
      this.remaining = Math.max(0, this.deadline - now);
      const dead = this.fighters.map(f => f.hp <= 0);
      if (dead.some(Boolean) || this.remaining <= 0) {
        let winner = -1;
        if (this.fighters[0].hp !== this.fighters[1].hp) winner = this.fighters[0].hp > this.fighters[1].hp ? 0 : 1;
        if (winner >= 0) this.scores[winner]++;
        const matchWon = winner >= 0 && this.scores[winner] >= this.winsToWin;
        this.phase = matchWon ? 'matchOver' : 'roundOver';
        this.result = winner < 0 ? '平手' : `${this.fighters[winner].name} ${matchWon ? '贏得對戰' : '贏得回合'}`;
        this.phaseEnd = now + (matchWon ? 5 : 3);
        this.attacks.clear();
        this.onRoundEnd(winner, matchWon);
      }
    } else if ((this.phase === 'roundOver' || this.phase === 'matchOver') && now >= this.phaseEnd) {
      if (this.phase === 'matchOver') { this.scores = [0, 0]; this.round = 0; }
      this.nextRound(now);
    }
  }

  startAttack(attacker, target, { activeFrom, activeUntil, damage }) {
    if (this.phase !== 'fight' || !attacker.alive || !target.alive || activeUntil < activeFrom) return false;
    this.attacks.set(attacker, { target, activeFrom, activeUntil, damage, consumed: false });
    return true;
  }

  cancelAttack(fighter) { this.attacks.delete(fighter); }

  tryHit(attacker, target, now, connected) {
    const attack = this.attacks.get(attacker);
    if (this.phase !== 'fight' || !attack || attack.target !== target || attack.consumed || !attacker.alive || !target.alive || now < attack.activeFrom || now > attack.activeUntil || !connected) return null;
    attack.consumed = true;
    if (target.state === 'dodge' && now < target.busyUntil) return { dodged: true, damage: 0 };
    target.hp = Math.max(0, target.hp - attack.damage);
    this.cancelAttack(target);
    return { dodged: false, damage: attack.damage };
  }
}
