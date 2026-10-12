# Credits and Licenses

Every third-party asset used in Lantern Keeper is listed here. Add a row when an asset is imported, not at the end of the project.

Only free assets are used: CC0, the SIL Open Font License, or the Standard Unity Asset Store EULA. Nothing here needs attribution by its license (Kenney asks for a credit as a courtesy, and gets one), and nothing restricts use in a game.

Each pack's own license file is kept beside its files in the project.

## Code, characters and animation

| Asset | Author | License | Source URL | Used for |
|---|---|---|---|---|
| Starter Assets – Third Person Character Controller | Unity Technologies | [Standard Unity Asset Store EULA](https://unity.com/legal/as-terms) | Unity Asset Store ("Starter Assets Third Person") | Player character model, the idle / walk / run / jump / land clips, the third-person controller, and the Animator Controller that `Animations/Player.controller` is a copy of |
| Ghost clips `Animations/Ghost_*.anim` and `Ghost.controller` | Original, generated for this project (`LanternKeeperSetup.Animation.cs`) | – | – | Ghost animation: float idle, drift, notice, chase, repelled, eating |
| Player lantern-raise, carry and throw motion | Original, procedural (`LanternProp.cs`, humanoid hand IK) | – | – | Flare and candy-throw animation on the player |

## Art (models, textures, environment)

| Asset | Author | License | Source URL | Used for |
|---|---|---|---|---|
| **Graveyard Kit 5.0**, `Art/ThirdParty/KenneyGraveyardKit/`: `pumpkin`, `pumpkin-tall`, `pumpkin-carved`, `pumpkin-tall-carved`, `hay-bale`, `hay-bale-bundled`, `pine`, `pine-crooked`, `pine-fall`, `pine-fall-crooked`, `gravestone-bevel`, `gravestone-round`, `gravestone-wide`, `gravestone-decorative`, `gravestone-roof`, `crypt`, `crypt-large`, `crypt-small`, `pillar-obelisk`, `candle-multiple`, `coffin-old`, `shovel-dirt`, `bench-damaged`, `urn-round`, `rocks`, `rocks-tall` (FBX) and `Textures/colormap.png` | Kenney (www.kenney.nl) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | https://kenney.nl/assets/graveyard-kit | Level01 dressing: the pumpkin patch and hay-bale walls (S1), the fifteen gravestones, crypts and graveyard props (S3), yard clutter (S4, S5), and the pines outside the fence along the whole level |
| **Nature Kit 2.1**, `Art/ThirdParty/KenneyNatureKit/`: `crops_cornStageD`, `crops_cornStageC`, `grass`, `grass_large`, `lily_large`, `lily_small`, `mushroom_redGroup`, `rock_smallA`, `rock_smallFlatB`, `stump_old`, `log_stack` (FBX) | Kenney (www.kenney.nl) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | https://kenney.nl/assets/nature-kit | Level01 dressing: corn stalks on the cornfield walls (S5), grass tufts in every section, lily pads on the pond and rocks by the creek (S2) |
| `Art/Generated/Meshes/Kenney_crops_cornStageD.asset`, `Kenney_urn_round.asset` | Kenney (merged copies made by `Dress Level01`: the kit's model is several meshes; this is the same geometry as one mesh) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | https://kenney.nl/assets/nature-kit, https://kenney.nl/assets/graveyard-kit | Same as the models they are merged from |
| `Art/Generated/Meshes/LK_*.asset` (ghost, pumpkin and jack-o'-lantern, candle, flame, candy, candy bowl, iron gate, rune, lantern cage, pillar cap, note signpost, hanging lantern, crate, fence and iron-fence panels, hedge, dead tree, reeds, stepping stone, scarecrow, farmhouse, porch roof, flat-colour boxes) | Original, generated for this project (`LanternKeeperSetup.Meshes.cs`) | – | – | Every prefab's final look, and the level dressing that is not a Kenney model |
| `Art/Generated/Textures/T_LK_Palette.png`, `T_LK_PaletteEmission.png` | Original, generated for this project (`LanternKeeperSetup.Meshes.cs`) | – | – | The flat-colour palette all generated meshes share (`M_LK_Palette`) |
| `Art/Generated/Textures/T_SoftDot.png`, `T_Spark.png`, `T_Mist.png`, `T_Leaf.png` | Original, generated for this project (`Tools/generate_textures.py`) | – | – | Particles: lantern flame, fireflies, ghost repel burst and crumbs (soft dot), pickup sparkle (spark), graveyard mist and gate dust (mist), drifting leaves (leaf) |
| `Art/Generated/Textures/UI_Flame.png`, `UI_Candy.png`, `UI_Rounded.png`, `UI_Parchment.png` | Original, generated for this project (`Tools/generate_textures.py`) | – | – | HUD candle flame and candy icon, button and bar shapes, the note panel |
| `Art/Shaders/SpiritPlatform.shader`, `NightSky.shader` | Original, written for this project | – | – | Spirit platform rim glow; the night sky with stars and moon |

## Audio (sound effects, music, ambience)

| Asset | Author | License | Source URL | Used for |
|---|---|---|---|---|
| **Impact Sounds 1.0**, `Audio/ThirdParty/KenneyImpactSounds/`: `footstep_grass_000` … `004`, `impactSoft_heavy_000`, `impactPunch_medium_000`, `impactGeneric_light_000`, `_002`, `_003`, `impactMining_001` (OGG) | Kenney (www.kenney.nl) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | https://kenney.nl/assets/impact-sounds | Footsteps, landing, player hit, candy landing, candy drop, plate press and release |
| **Interface Sounds 1.0**, `Audio/ThirdParty/KenneyInterfaceSounds/`: `click_001`, `tick_001`, `confirmation_002`, `switch_002`, `error_004`, `error_006` (OGG) | Kenney (www.kenney.nl) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | https://kenney.nl/assets/interface-sounds | Menu click and hover, title start, pause and resume, "no candy", "not enough candle to flare" |
| **RPG Audio**, `Audio/ThirdParty/KenneyRPGAudio/`: `bookOpen`, `bookClose`, `handleCoins`, `handleCoins2`, `doorOpen_2` (OGG) | Kenney Vleugels (www.kenney.nl) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) | https://kenney.nl/assets/rpg-audio | Note open and close, candy bowl refill, candy pickup, the farmhouse door |
| `Audio/SFX/*.wav` (23 files): `candle_low`, `extinguish`, `respawn`, `flare_start`, `flare_loop`, `flare_stop`, `platform_appear`, `platform_vanish`, `ghost_notice`, `ghost_chase`, `ghost_repel`, `ghost_eat`, `ghost_hit`, `ghost_return`, `candy_throw`, `candy_eaten`, `candle_pickup`, `checkpoint_lit`, `gate_open`, `gate_close`, `win_jingle`, `prompt_chime`, `pickup_sparkle` | Original, generated for this project (`Tools/generate_audio.py`: synthesised from sine waves and noise, no samples) | – | – | Every game-specific sound. The Audio section of `docs/SETUP_WORLD.md` maps each event to its clip |
| `Audio/Ambience/night_wind_crickets.wav`, `creek_water.wav`, `graveyard_choir.wav` | Original, generated for this project (`Tools/generate_audio.py`) | – | – | Level-wide night ambience; water zones at the creek, pond and trench; the graveyard zone |
| `Audio/Music/music_title.wav`, `music_explore.wav`, `music_chase.wav` | Original, composed and generated for this project (`Tools/generate_audio.py`) | – | – | Title loop; exploration loop; the chase layer that fades in during evasion |

## Fonts

| Asset | Author | License | Source URL | Used for |
|---|---|---|---|---|
| Creepster (`Art/ThirdParty/Fonts/Creepster-Regular.ttf`, baked to `Art/Fonts/LK_Title SDF.asset`) | Font Diner, Inc | [SIL Open Font License 1.1](https://openfontlicense.org) (`Creepster-OFL.txt`) | https://fonts.google.com/specimen/Creepster | Headings: the game title, "Paused", the win heading, note titles |
| Lato (`Art/ThirdParty/Fonts/Lato-Regular.ttf`, baked to `Art/Fonts/LK_Body SDF.asset`) | Łukasz Dziedzic (tyPoland) | [SIL Open Font License 1.1](https://openfontlicense.org) (`Lato-OFL.txt`) | https://fonts.google.com/specimen/Lato | All other text: HUD, prompts, menus, note text |

The OFL allows a font to be embedded in a game. Its one condition that matters here is that the font files are not sold on their own; the license text travels with them in the repository.

## Unity packages

These are installed through the Unity Package Manager under the [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license) and are listed in `Packages/manifest.json`. They include Universal Render Pipeline, Input System, AI Navigation, Cinemachine and TextMeshPro (part of Unity UI).

## Not used

Nothing from Mixamo, Freesound, Pixabay or the Unity Asset Store (other than Starter Assets) is in the project: each needs an account, and Step 5 was done without one. `docs/ASSET_TODO.md` lists where such assets would drop in.
