/** Track each limb/body pair across frames. A strike connects only while closing. */
export function findContact(strikes, bodies, radius, previous = new Map()) {
  let contact = null;
  for (const strike of strikes) {
    bodies.forEach((body, index) => {
      const key = `${strike.key}:${index}`;
      const distance = Math.hypot(strike.p.x - body.x, strike.p.y - body.y, strike.p.z - body.z);
      const before = previous.get(key);
      previous.set(key, distance);
      if (before !== undefined && distance <= radius && distance < before - 0.0005 && (!contact || distance < contact.distance)) {
        contact = { distance, limb: strike.key, point: strike.p };
      }
    });
  }
  return contact;
}
