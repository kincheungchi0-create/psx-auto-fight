/** Detect a strike moving toward the target, using positions from consecutive frames. */
export function findContact(strikes, bodies, radius, previous = new Map()) {
  let contact = null;
  for (const strike of strikes) {
    const last = previous.get(`limb:${strike.key}`);
    bodies.forEach((body, index) => {
      const key = `${strike.key}:${index}`;
      const dx = body.x - strike.p.x, dy = body.y - strike.p.y, dz = body.z - strike.p.z;
      const distance = Math.hypot(dx, dy, dz);
      const before = previous.get(key);
      previous.set(key, distance);
      const forward = last && distance > 0 ? ((strike.p.x - last.x) * dx + (strike.p.y - last.y) * dy + (strike.p.z - last.z) * dz) / distance : 0;
      if (before !== undefined && distance <= radius && distance < before - 0.0005 && forward > 0.0005 && (!contact || distance < contact.distance)) {
        contact = { distance, limb: strike.key, point: strike.p };
      }
    });
    previous.set(`limb:${strike.key}`, { x: strike.p.x, y: strike.p.y, z: strike.p.z });
  }
  return contact;
}
