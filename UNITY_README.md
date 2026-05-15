# D-World Unity version

This repo now has a one-file Unity port:

`DWorldUnity.cs`

How to run:

1. Create a new Unity 2D or 3D project.
2. Copy `DWorldUnity.cs` into the project.
3. Press Play.

The script can auto-create its runtime object. If auto-start is disabled in your Unity version, add an empty GameObject and attach `DWorldUnity` manually.

No external images, textures, audio files, prefabs, or folders are required. The game draws its player, enemies, bullets, walls, menu, and UI procedurally from code.

PC controls:

- `WASD`: move
- `Left Ctrl`: dash / temporary invisibility
- `Left Mouse`: shoot
- `Right Mouse`: shotgun burst
- `Space`: radial ultimate
- `F`: hold and release for targeted radial shot
- `R`: flash / slow enemies
- `Esc`: pause or go back

Mobile controls:

- Left virtual stick: move
- Right virtual stick: aim, release to shoot
- `DASH`: dash / temporary invisibility
- `FLASH`: flash / slow enemies
- `ULT`: radial ultimate

Phone setup:

- The script forces landscape orientation.
- The game scales itself to the current screen, so there is no resolution menu.
- For a Google Play build, set Unity to Android in Build Settings, then build normally.

Important monetization note:

Real Google Play monetization cannot be fully included in a single standalone C# file, because ads and in-app purchases need an official Unity SDK/package and Android project setup. The script includes `ShowRewardedAdOrIapHook()` as the place where a friend can connect Google Mobile Ads, Unity LevelPlay, or Unity IAP later.
