# D-World Web Versions

This repo includes two static websites:

- `DWorld_Web_Mobile` - phone-first version with fullscreen, touch sticks, ability buttons, music, and boss training.
- `DWorld_Web_Desktop` - desktop version with keyboard, mouse aiming, fullscreen, music, and boss training.

## Run locally

Open either `index.html` in a browser, or run a tiny local server from the repo root:

```bash
python3 -m http.server 8080
```

Then open:

- Mobile: `http://localhost:8080/DWorld_Web_Mobile/`
- Desktop: `http://localhost:8080/DWorld_Web_Desktop/`

## Permanent free hosting

The easiest free option is GitHub Pages.

1. Merge the pull request.
2. Open the repo on GitHub.
3. Go to `Settings -> Pages`.
4. Choose branch `main`.
5. Choose folder `/root`.
6. Save.

After GitHub publishes it, the links will look like:

- `https://kovsh1kk.github.io/-2-228/DWorld_Web_Mobile/`
- `https://kovsh1kk.github.io/-2-228/DWorld_Web_Desktop/`

No paid server is needed because these are static sites.

## Telegram / phone sharing

For the web version, send the GitHub Pages link in Telegram.

If you want a downloadable Android app file, build the Unity version as an `.apk` and send that file. A website link and an Android APK are different formats.
