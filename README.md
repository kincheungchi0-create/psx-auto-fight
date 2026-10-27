# PSX Auto Fight

Simple Three.js **auto mode** demo: four low-poly characters fight with 直拳／重拳／旋風腳.

## Moves

- **直拳 / 重拳** — PSX People `attack` clip + hand bone contact
- **旋風腳** — Mixamo [Spin Flip Kick](https://www.mixamo.com/) retargeted onto PSX People bones (`models/spin_flip_kick.glb`)

Contact hits require Hand/Foot vs opponent Hips/Spine/Head.

## Credits

- [PSX People Free](https://heyheythere.itch.io/psx-people-free) by heyheythere — CC BY 4.0
- Kick animation: Adobe Mixamo — Spin Flip Kick (motion only)

## Local

```bash
npm start
```

Open the printed URL (models need HTTP, not `file://`).

## Assets for upload

- `models/spin_flip_kick.glb` — retargeted clip (PSX bone names)
- `models/spin_flip_kick.fbx` — original Mixamo FBX
- `assets/mixamo/` — copies of both
