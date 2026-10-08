# Lantern Keeper

A 3D third-person light-puzzle platformer made in Unity 6 (URP) for CSCI 5999B, Project 2. Theme: Halloween.

On Halloween night, a kid is lost in a haunted pumpkin patch with only a jack-o'-lantern for light. Its candle is burning down. Cross the pumpkin patch, the graveyard and the cornfield to reach the old farmhouse before the light goes out.

The lantern light does three jobs:

- **It reveals the path.** Spirit platforms only exist inside the light.
- **It pushes back the ghosts.** Flare the lantern to repel them, at the cost of burning the candle faster.
- **It is the clock.** When the candle hits zero, you respawn at the last lit porch lantern.

Throw candy to lure ghosts away, or onto pressure plates to open gates.

A first playthrough takes about 6–9 minutes.

---

## Controls

Lantern Keeper is played with keyboard and mouse, and is fully playable on a laptop trackpad. Every action has a keyboard key; mouse buttons are optional alternatives.

| Action | Keyboard | Mouse (optional) |
|---|---|---|
| Move | WASD | — |
| Look / turn camera | Arrow keys | Move mouse or trackpad |
| Jump | Space | — |
| Sprint (hold) | Left Shift | — |
| Flare lantern (hold) | F | Right mouse button |
| Throw candy | E | Left click |
| Pause | Esc | — |
| Restart (on win screen) | R | — |

Camera sensitivity can be adjusted from the pause menu.

### Playing on a laptop trackpad

- **Use the arrow keys to turn the camera** if moving with WASD and steering with the trackpad at the same time is uncomfortable. Everything in the game can be done from the keyboard.
- **Windows:** by default, Windows ignores the touchpad for a moment after each key press, so the camera won't move while you hold W. To fix this, open **Settings → Bluetooth & devices → Touchpad → Taps** and set **Touchpad sensitivity** to **Most sensitive**. Change it back after playing if you like.

---

## Running the game (builds)

Download the zip for your platform and **unzip the whole folder** before running anything. The game needs the files next to the executable.

### Windows (x86_64)

1. Open the unzipped folder and run `LanternKeeper.exe`.
2. If Windows SmartScreen shows "Windows protected your PC", click **More info → Run anyway**. The game isn't code-signed, so Windows shows this for any download.

### macOS (Intel and Apple Silicon)

1. Unzip, then move `LanternKeeper.app` somewhere such as your Desktop or Applications folder.
2. **First launch:** right-click (or Control-click) `LanternKeeper.app` and choose **Open**, then **Open** again in the dialog.
   - **macOS 15 Sequoia and later:** if there's no Open button, click **Done**, go to **System Settings → Privacy & Security**, scroll down and click **Open Anyway** next to the LanternKeeper message, then confirm.
3. **If macOS says the app "is damaged" or "can't be opened"**, open Terminal in the folder containing the app and run:
   ```sh
   xattr -cr LanternKeeper.app
   chmod +x LanternKeeper.app/Contents/MacOS/*
   ```
   Then try step 2 again. The app was built on Windows, so it isn't signed by Apple, and zipping it can drop the "executable" permission.

### Linux (x86_64)

1. Unzip, then open a terminal in the unzipped folder.
2. Make the game executable and run it:
   ```sh
   chmod +x LanternKeeper.x86_64
   ./LanternKeeper.x86_64
   ```

---

## Opening the project in Unity

- **Unity version:** 6000.6.3f1 (Unity 6). Unity Hub will offer to install it if it's missing.
- **Render pipeline:** URP.

1. **Install [Git LFS](https://git-lfs.com) before cloning** and run `git lfs install` once. Textures, models and audio are stored with Git LFS. Without it, those files download as small placeholder text files and the project shows missing or pink assets.
2. Clone the repository. The Unity project is the `Unity Project/My project` folder; the repository also contains an unrelated Godot project.
3. In **Unity Hub**, click **Add → Add project from disk** and select the `Unity Project/My project` folder.
4. Open it. The first open takes a few minutes while Unity rebuilds its `Library` cache.
5. The game's scenes are in `Assets/_Project/Scenes`. Open the level scene and press **Play**.

### Project layout

| Path | Contents |
|---|---|
| `Assets/_Project/` | All game-specific code, prefabs, scenes, materials, audio, art and animations |
| `Assets/Starter Assets/` | Unity's Starter Assets third-person character controller (third-party) |
| `docs/` | Design and production documents: credits, iteration history |
| `Packages/`, `ProjectSettings/` | Unity package list and project settings |

Unity regenerates `Library/`, `Temp/`, `Logs/`, `UserSettings/` and the IDE project files, so they are not committed. Platform builds go in `Builds/`, which is also not committed.

---

## Credits

Lantern Keeper uses free and CC0 third-party assets. Every asset, its author, license and source is listed in [docs/CREDITS.md](docs/CREDITS.md).
