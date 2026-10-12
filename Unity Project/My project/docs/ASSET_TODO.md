# Asset upgrades to do by hand (optional)

Step 5 used only what could be downloaded without an account: Kenney's CC0 kits and two OFL fonts. Everything else was generated for the project (`docs/CREDITS.md` has the full list).

Nothing below is needed for the game to be complete: every event has a sound and every object has its final look. These are the places where an asset from a site that needs a login would be an upgrade, and how to drop it in.

**The rule that makes a swap one drag:** Unity finds an asset by the GUID in its `.meta` file, not by its content. Replace a file **under the same name, in the same folder, keeping its `.meta`**, and every prefab and scene that used the old one uses the new one. No re-wiring.

If the new file has a different extension (an `.ogg` for a `.wav`), delete the old file but keep its `.meta`, renamed to match (`music_explore.wav.meta` → `music_explore.ogg.meta`), then let Unity import. If that feels fragile, put the new file in as a new asset and run the menu item named in the table instead.

## Audio

Do not run `python Tools/generate_audio.py` after replacing a file listed here: it writes every `.wav` again.

| Placeholder (original, synthesised) | Upgrade | Where to get it | Drop it at | Then |
|---|---|---|---|---|
| `Audio/Music/music_explore.wav` | A calm, slightly eerie loop, 60–90 s | [Pixabay Music](https://pixabay.com/music/) (login to download; Pixabay Content License) or [Freesound](https://freesound.org) filtered to CC0 | Same path and name | Nothing |
| `Audio/Music/music_chase.wav` | A tense loop | Same | Same path and name | Read the note below this table |
| `Audio/Music/music_title.wav` | A title theme | Same | Same path and name | Nothing |
| `Audio/Ambience/night_wind_crickets.wav` | A real night field recording, 30 s or more, loopable | Freesound, CC0 ("night crickets wind loop") | Same path and name | Nothing |
| `Audio/Ambience/creek_water.wav` | A real stream recording | Freesound, CC0 ("stream loop") | Same path and name | Nothing |
| `Audio/Ambience/graveyard_choir.wav` | A distant choir or wind-through-trees pad | Pixabay Music / Freesound CC0 | Same path and name | Nothing |
| `Audio/SFX/ghost_*.wav` (6 files) | Recorded "ooh" and "boo" voices | Freesound, CC0, or record them | Same paths and names | Nothing |
| `Audio/SFX/gate_open.wav`, `gate_close.wav` | A real iron gate | Freesound, CC0 ("metal gate creak") | Same paths and names | Nothing |
| Any other `Audio/SFX/*.wav` | – | – | Same path and name | Nothing |

**Chase music.** The exploration and chase loops are started on the same tick and crossfaded, so they sound best when they are the same length and tempo (the generated pair is 26.67 s at 72 BPM in D minor). Two unrelated tracks still work, they just crossfade like two songs. If the chase track should restart from its beginning each time a chase begins, that is a small change in `MusicDirector`, not an asset swap.

Record every file you add in `docs/CREDITS.md` (asset, author, license, URL, where used). Pixabay's license allows use in a game without attribution; a Freesound file must be **CC0**, not CC-BY-NC.

## Character animation

Needs an Adobe account (free). `docs/ANIMATION.md` has the steps for both:

| Placeholder | Upgrade | Where | Drop it at |
|---|---|---|---|
| Player lantern-raise and throw (procedural hand IK) | Mixamo "Standing Torch Light" / "Holding Up", "Throw Object" | [mixamo.com](https://www.mixamo.com), FBX for Unity, Without Skin | `Assets/_Project/Art/ThirdParty/Mixamo/`, then "Swapping in Mixamo clips later" under Player in `ANIMATION.md` |
| Ghost clips (authored in code) | A rigged ghost with Mixamo clips | mixamo.com | Same folder, then "Swapping in Mixamo clips later" under Ghost in `ANIMATION.md` |

## Models

| Placeholder (generated mesh) | Upgrade | Where | How |
|---|---|---|---|
| Player character (Starter Assets robot) | A child in a costume | Unity Asset Store free characters, or [Quaternius](https://quaternius.com) "Ultimate Modular Characters" (CC0, direct download) | Humanoid rig; swap the mesh under `Player > Geometry` and the Animator's Avatar. The scripts do not care what the model is |
| `LK_Farmhouse`, `LK_FarmhousePorch` | A house model | Kenney "Fantasy Town Kit" (CC0, direct zip) or Quaternius buildings | In `LanternKeeperSetup.Dress.cs`, `Section5`: replace `Piece("LK_Farmhouse", ...)` with `Fit("G/<model>", house, ...)` after copying the FBX into `Art/ThirdParty/<Pack>/`; then **Lantern Keeper > Dress Level01** |
| `LK_DeadTree`, `LK_Reeds`, `LK_Hedge` | Tree, reed and hedge models | Quaternius "Stylized Nature MegaKit" (CC0) | Same pattern: copy the FBX into a pack folder, change the `Piece(...)` call to `Prop(...)`, re-run **Dress Level01** |
| `LK_Scarecrow` | A scarecrow model | Kenney "Halloween Kit" if a direct zip appears on kenney.nl (it had none on 2026-10-11) | Same pattern |

To add a new Kenney pack: unzip it somewhere outside the project, copy **only the FBX files you use** and the pack's `License.txt` (plus `Textures/colormap.png` for kits that have one) into `Assets/_Project/Art/ThirdParty/<PackName>/`, and add a row to `CREDITS.md`. `LoadModel` in `LanternKeeperSetup.Dress.cs` maps the `G/` and `N/` prefixes to the two existing pack folders; add a prefix there for a third.

## Audio mixer (only if it ever has to be rebuilt)

`Assets/_Project/Audio/LanternKeeper.mixer` is created by code through an internal Unity API. If a future Unity version breaks that, the Console says so, and the mixer can be made by hand in two minutes:

1. **Assets > Create > Audio > Audio Mixer**, name it `LanternKeeper`, in `Assets/_Project/Audio/`.
2. In the Audio Mixer window add three groups under **Master**: `Music`, `SFX`, `Ambience`.
3. For each of the three: select it, right-click **Volume** in the Inspector, **Expose 'Volume' to script**. In the window's **Exposed Parameters** list rename them `MusicVolume`, `SfxVolume`, `AmbienceVolume`.
4. Run **Lantern Keeper > Run All Setup**, then **Lantern Keeper > Dress Level01**, so the prefabs and the level pick the mixer up.
