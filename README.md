# PSX Auto Fight

A Three.js auto-fight demo with Anime Girl and Gundam. Fighters move and use hand and foot contact to attack; Anime Girl and Gundam use a variety of Mixamo capoeira animations.

## Match rules

The game is an automatic best-of-three match. Each round lasts up to 60 seconds. A hit must connect during an attack’s active window; a timed dodge avoids it. The fighter with more health wins on timeout, and equal health is a draw. A completed match restarts automatically.

## Local development

```bash
npm start
```

Run `npm test` to verify the combat and round rules. Open the printed URL. Models require HTTP; opening `index.html` directly will not load them.

## Credits

- [Anime Girl Character Free GameReady Animated](https://sketchfab.com/3d-models/anime-girl-character-free-gameready-animated-b8e1967c0703492e8121d9457f056d27) by RahulTambat, CC BY 4.0. Modified for the game: normalized skeleton, removed backdrop and added animations.
- [Mr Sexy Man (Fighter with 13 Animations)](https://sketchfab.com/3d-models/mr-sexy-man-fighter-with-13-animations-127cc242790c422fb9599f774b1c9642) by Jungle Jim, CC BY 4.0. Original model and animations adapted for the game.
- Fight animations: [Adobe Mixamo](https://www.mixamo.com/).
