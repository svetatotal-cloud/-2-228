# D-World Full Unity Folder

This is the full drag-and-drop Unity version with code and music.

## How to use

1. Create a Unity project.
2. Open the Unity project.
3. Drag the whole `DWorld_Full_Unity` folder into the Unity `Assets` window.
4. Press Play.

The script auto-creates the runtime object. If your Unity version does not auto-start it, create an Empty GameObject and attach `DWorldUnity`.

## Included

- `DWorldUnity.cs` - the game code
- `Resources/DWorldAudio/LOST.mp3` - background music
- `Resources/DWorldAudio/myinstants.mp3` - death sound

## Android / Google Play

For Android testing, build an `.apk`.

For Google Play, build an `.aab`.

Unity path:

`File -> Build Settings -> Android -> Switch Platform -> Build`

An `.apk` can be sent to someone in Telegram, and they can install it on Android if their phone allows installing apps from unknown sources.

Google Play normally wants an `.aab`, which is uploaded through Google Play Console.
