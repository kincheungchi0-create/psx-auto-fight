# Sakuga Auto Fight — desktop prototype

Last updated on GitHub: 2026-10-27 15:14 HKT (UTC+8)

This separate Godot 4.7 .NET project uses [Sakuga Engine](https://github.com/NoisyChain/Sakuga-Engine), commit `d54581b131ef2a1f6b8511a79c74ea37168c31b1`, MIT license; see `LICENSE`. It does not replace the browser game in the parent folder. Sakuga's Godot 4 C# version does not currently export to the web.

Two CPU fighters use Sakuga's sample fighter states, hitboxes, AI and round rules. Their sprites are hidden and the existing 3D characters are driven by Sakuga's state and frame through `CharacterVisual.cs`. This is an early adaptation: both fighters share the sample moves and hitboxes, and existing animation clips are mapped to those states. Each attack clip is time-warped so its furthest reach lands on the move's first active hitbox frame. Both fighters are currently the Anime Girl (girl vs girl); pass `playerIndex == 0` to `CharacterVisual.Initialize` in `GameManager.cs` to bring back the Kung Fu fighter as player 2. The girl uses an upright boxing and MMA set from Mixamo in `Models/AnimeGirl/mixamo/`: idle, walk, step back, run, jab, jab-cross, uppercut, roundhouse, low kick, hurricane kick, block, light and big hit reactions, knockdown, get-up and victory. The FBX clips are converted to the model's rest pose on load. The Kung Fu fighter has no reaction clips, so his blocks hold a guard pose, hits lean his stance back, and knockdowns lay his most upright frame flat. The original sample assets remain part of the upstream project.

## Run on this Mac

Double-click `launch-sakuga.command`, or run `bash launch-sakuga.command`. It builds the C# project, imports assets and launches the game. The first import may take several minutes. The installed Godot .NET editor and .NET SDK are under `~/Library/Application Support/SakugaAutoFight` and are not committed. On another machine install Godot 4.7 .NET and .NET SDK 8, import this project in Godot, build the C# project, and run `Scenes/TestScene.tscn`. The script expects those tools under the same paths on macOS.

## Character credits

- [Anime Girl Character Free GameReady Animated](https://sketchfab.com/3d-models/anime-girl-character-free-gameready-animated-b8e1967c0703492e8121d9457f056d27) by RahulTambat, CC BY 4.0; modified and animated with [Adobe Mixamo](https://www.mixamo.com/).
- [Mr Sexy Man (Fighter with 13 Animations)](https://sketchfab.com/3d-models/mr-sexy-man-fighter-with-13-animations-127cc242790c422fb9599f774b1c9642) by Jungle Jim, CC BY 4.0; modified for material compatibility.
