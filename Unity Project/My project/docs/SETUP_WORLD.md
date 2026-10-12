# Ghost, world and game-flow setup (manual fallback)

The normal route is the menu: **Lantern Keeper > Build Sandbox Scene**. It runs every other item first (placeholder assets, Player, Ghost, World prefabs), rebuilds `Assets/_Project/Scenes/Sandbox.unity` and bakes its NavMesh. Every item can be run again; assets are overwritten in place.

Use this page if a menu item fails, or when changing the level by hand. Field names are the Inspector labels. Player setup is in `SETUP_PLAYER.md`.

The real level is different: `Assets/_Project/Scenes/Level01_PumpkinPatch.unity` is built **once** by **Lantern Keeper > Create Level01 Greybox** and tuned by hand from then on (section 7). Build Sandbox Scene and Run All Setup never open it. They do regenerate the prefabs the level uses, in place, so prefab changes still reach the level.

## 1. Layers, tags, input

| What | Value |
|---|---|
| Layer 8 | `Player` (the player's CharacterController) |
| Layer 9 | `Ghost` (the ghost root and all its children) |
| Tag | The player root is tagged `Player`; every trigger below checks that tag |
| Input | `LanternKeeper.inputactions` has a second map, `Game`: **Start** (any key, left click), **Pause** (Esc), **Restart** (R). Keyboard and mouse only |

Triggers, plates, gates and walls stay on **Default**.

## 2. Ghost prefab (`Prefabs/Ghost.prefab`)

Root `Ghost`, layer `Ghost`:

| Component | Settings |
|---|---|
| NavMeshAgent | Agent Type Humanoid, Radius 0.4, Height 1.9, Angular Speed 480, Acceleration 16, Stopping Distance 0.05, Obstacle Avoidance High Quality. Speed is set by GhostAI per state |
| Rigidbody | **Is Kinematic** on, Use Gravity off (needed because the collider moves) |
| CapsuleCollider | **Is Trigger** on, Radius 0.55, Height 1.9, Center (0, 0.95, 0). The flare, pressure plates and gates find the ghost through this collider |
| GhostAI | **Tint Renderers** = Body and Head renderers. **Glow Light** = the Glow light. **Patrol Points** = set per instance in the scene |

Children (all on layer `Ghost`, no colliders), since Step 5:

| Child | What it is |
|---|---|
| `Visual` | Animator with `Animations/Ghost.controller`. The clips move its child `Pivot`, never the root |
| `Visual/Pivot/Body` | SkinnedMeshRenderer, mesh `LK_Ghost` (a sheet with hem blend shapes), `M_Ghost` (transparent, emissive). The only renderer in **Tint Renderers** |
| `Visual/Pivot/Eye L`, `Eye R`, `Mouth` | Dark spheres, `M_GhostFace`. Not tinted |
| `Glow` | Point light (range 4) |
| `RepelBurst`, `Crumbs` | Particle systems played by GhostVisualFx |

Also on the root: **GhostVisualFx** (**Animator** = `Visual`, **Glow Renderers** = `Body`, **Glow Light** = `Glow`, the two particle systems). It only reads GhostAI. `GhostAI > Animator` is wired to `Visual`, and the six audio clips are filled in (section 8).

In a scene: place the ghost on the NavMesh, create empty waypoint objects, and drag them into **Patrol Points** in visiting order. With no patrol points the ghost idles where it was placed. Keep patrol routes off pressure plates, or a drifting ghost will press them.

Animator: GhostAI sets float `Speed` and int `State` (0 Drift, 1 Notice, 2 Chase, 3 Repelled, 4 Distracted, 5 Return) only if those parameters exist. They do: `docs/ANIMATION.md` describes the controller, its six states and its clips.

## 3. World prefabs

| Prefab | Objects and components | Wire |
|---|---|---|
| PressurePlate | Root: PressurePlate, PlateRune. `Base` cube 2.2 x 0.06 x 2.2 with collider. `Pad` cube 2 x 0.08 x 2, no collider, with child `Rune` (mesh `LK_Rune`, `M_PlateRune`) | **Pad** = Pad transform, **Pad Renderer** = Pad renderer. Detection box defaults to 2 x 1.2 x 2 above the plate (yellow gizmo). PlateRune **Rune** = the Rune renderer |
| Gate | Root: Gate, GateFx. `Frame` > `Pillar L`, `Pillar R` (cubes with NavMeshObstacle, Carve on), `Beam` (with stone caps and two jack-o'-lanterns as children). `Door` cube 3 x 3 x 0.25 with **no renderer**: BoxCollider, kinematic Rigidbody, NavMeshObstacle (Carve on, Carve Only Stationary off); its children `Bars` (mesh `LK_IronGate`) and `Veil` are what is drawn. `Dust` particles | **Door** = Door transform. GateFx **Bars** = `Bars`, **Dust** = `Dust`. Per instance: **Plates**, **Required Plates**, **Stay Open Once Opened** |
| Checkpoint | Root: SphereCollider (**Is Trigger**, radius 1.6), Checkpoint. `Post`. `Lamp` at y 2.3: Light (Point), RevealLight (radius 5, linked to the Light), `Lantern (unlit)` (a pumpkin with a dark carved face, looking back down the path), `Lantern (lit)` (the glowing face and a flame, with CheckpointGlow). `RespawnPoint` at local (0, 0, -1.5) | **Respawn Point**, **Lamp Light**, **Reveal Light**, **Lit Visual** = `Lantern (lit)`. CheckpointGlow **Lamp Light**, **Flame** |
| SectionTrigger | Root: BoxCollider (**Is Trigger**, 8 x 3 x 1), SectionTrigger | Per instance: **Section Id** (unique), optional **Display Name** |
| TutorialTrigger | Root: BoxCollider (**Is Trigger**, 8 x 3 x 1), TutorialTrigger | Per instance: **Tutorial Id**, **Text** (tokens `{flare}` `{throw}` `{drop}` `{jump}`), **Seconds** |
| GoalTrigger | Root: BoxCollider (**Is Trigger**) in front of the door, GoalTrigger. `Door` (with a lit window and a knob), `Frame`, `PorchLight` | Nothing |
| CandyBowl | Root: SphereCollider (**Is Trigger**, radius 1.2), CandyBowl. `Stand`, `Bowl` (mesh `LK_Bowl`; keep the name, the level tints it pink by it), `Candy Heap` | Nothing |
| GameMenus | Canvas (Screen Space Overlay, sort order 20), CanvasScaler (1920 x 1080), GraphicRaycaster, GameMenus. Child `EventSystem` (EventSystem + InputSystemUIInputModule). Panels `TitlePanel`, `PausePanel`, `WinPanel`, saved inactive. The pause panel has three sliders (camera sensitivity, music volume, sound volume); the pause and win panels each have a save-path text and an "Open playtest folder" button; the win panel has a CanvasGroup and UiFadeIn. Also on the root: UiSounds. Give every object in this prefab a unique name, or it gets new file IDs each time it is regenerated | The three panels, **Controls Label**, **Resume / Restart / Quit Button**, **Sensitivity / Music / Sfx Slider** and their **Value Label**s, **Win Stats Label**, **Win Restart Button**, **Pause / Win Save Path Label**, **Pause / Win Open Folder Button** (the playtest widgets are optional). UiSounds: three clips |

Gate notes:
- The door slides **down** into the ground (`Slide Offset` 0, -3.1, 0), so it cannot pin anyone. The ground under a gate must be thin enough, or the gate raised, for that to look right.
- **Never closes on someone**: while anything on `Blocker Layers` (Player, Ghost) is inside the doorway box (red gizmo, 3 x 3 x 1.6) the door stays open.
- For a two-plate gate where the player stands on one plate, tick **Stay Open Once Opened**; otherwise the gate shuts when they step off.
- Build walls on both sides of the gate so it cannot be walked around.

## 4. Scene objects for game flow

| Object | Components | Wire |
|---|---|---|
| GameManager | GameManager, DebugOverlay | **Input Actions** = `LanternKeeper.inputactions`. Optional **Start Point**. **Fall Death Height**: below the lowest safe floor and above the pit floors (-4 in Level01) |
| PlaytestLogger | PlaytestLogger | Nothing. One per scene; writes the playtest CSVs (`PLAYTEST_DATA.md`) |
| GameMenus | GameMenus prefab | Nothing (finds GameManager and the player itself) |
| HUD | HUD prefab (Step 2) | Nothing |

Only one GameManager and one EventSystem per scene. GameManager finds the player through `CandleManager.Instance`.

## 5. NavMesh

Ghosts need a baked NavMesh. The sandbox does it like this, and the real level should do the same:

1. Put all **static, walkable or blocking geometry** (ground, walls, rocks) under one parent object, e.g. `Environment`. Give each piece a collider.
2. Put everything else (spirit platforms, gates, plates, pickups, ghosts, triggers) under a different parent, e.g. `Gameplay`.
3. Add **NavMeshSurface** to `Environment`: Agent Type Humanoid, **Collect Objects = Current Object Hierarchy** (Children), **Use Geometry = Physics Colliders**, Include Layers = Default.
4. Press **Bake**. The data is saved next to the scene (the sandbox uses `Scenes/Sandbox-NavMesh.asset`).

Consequences, all intended:
- **Spirit platforms are not walkable for ghosts.** They are not under `Environment`, so they are not baked. A ghost chasing the player across a spirit bridge stops at the edge.
- Gate doors are not baked either. A closed door blocks ghosts through its carving NavMeshObstacle, and stops blocking once it has slid below the ground.
- A gap between two ground pieces separates the NavMesh into islands. A ghost only patrols, chases and reaches candy on its own island; candy thrown to another island is ignored after one attempt.

Humanoid agent settings (Project Settings, unchanged): radius 0.5, height 2, step height 0.75, max slope 45.

## 6. Placing sections and checkpoints (for Step 4)

- **SectionTrigger**: one at the start of each of the five level sections, scaled to span the whole path so it cannot be skipped. Ids must be unique; they are what the playtest logger records. Each fires once.
- **Checkpoint**: on a porch at the start of each section after the first, plus one at the spawn with **Lit At Start** ticked. Rotate the checkpoint so `RespawnPoint` (blue Z arrow) faces the way the player should continue, and keep `RespawnPoint` on solid, baked ground at least 7 m from any ghost patrol point (the notice range), so nobody respawns into a chase.
- **TutorialTrigger**: just before the place where the ability is needed.

## 7. Level01 (the real level)

| Menu item | What it does |
|---|---|
| **Create Level01 Greybox** | Builds the level from code and bakes its NavMesh. If the scene file already exists it logs a warning and does nothing |
| **Rebuild Level01 Greybox (DESTRUCTIVE)** | Throws the scene away and generates it again. Asks first in the Editor; in batch mode it only runs with the `-forceRebuild` command-line flag |
| **Validate Level01 (read-only)** | Checks ghosts, patrol points, plates, gates, checkpoints, sections and the walking route, and prints the pacing estimate. Changes nothing. Run it after moving things |
| **Dress Level01** | The art pass (section 9). Deletes and rebuilds the `Dressing` root, and nothing else in the hierarchy. Safe to run again at any time |
| **Undress Level01 (remove the dressing)** | Removes `Dressing` and switches the hidden greybox renderers back on, to see or edit the bare greybox |
| **Build Playtest (Windows)** | Release build of the level alone to `Builds/Playtest/LanternKeeper_Win_v0.2/`, zipped next to it |

Scene layout:

| Root object | Contents |
|---|---|
| `Environment` | Static, baked geometry in `S1_…` to `S5_…` groups plus `Boundaries (invisible)`. Has the NavMeshSurface |
| `Gameplay` | Spirit platforms, pickups, candy bowls, plates, gates, ghosts and their `…_Patrol_n` points, checkpoints, porch lamps, by section |
| `Triggers` | `Section_…`, `Tutorial_…` and the goal |
| `Lighting` | Moon and the farmhouse beacon |
| `Systems` | GameManager (with DebugOverlay), PlaytestLogger, GameMenus, HUD |
| `Player`, `MainCamera`, `PlayerFollowCamera` | As in the sandbox |
| `DesignNotes (EditorOnly)` | `GoldenPath`: empty objects marking the route the pacing estimate is measured along. Tagged EditorOnly, so not in builds |
| `Dressing` | Step 5. Everything decorative, built by **Dress Level01**: scenery by section, particles, the post-processing volume, ambience zones, `MusicDirector`, the pickup sparkle and the five story notes. No colliders. Do not edit it by hand: the next run replaces it |

After changing the level by hand:

- Moved or added anything under `Environment`? Select `Environment` and press **Bake** on the NavMeshSurface.
- Moved a ghost? Move its `…_Patrol_n` points too. The ghost starts at, and resets to, where it stands in the scene.
- Changed the route? Move the `GoldenPath` points, or the validator will report gaps and blocked segments that are no longer real.
- Then run **Validate Level01 (read-only)**.

Level-only settings on the ghosts (prefab overrides): **Line Of Sight Blockers = Default** on all of them, and **Candy Sense Range = 11** on the three plate ghosts. `LEVEL_DESIGN.md` explains why.

## 8. Audio (Step 5)

`Assets/_Project/Audio/LanternKeeper.mixer`: **Master** with three children, **Music**, **SFX** and **Ambience**. Their volumes are exposed as `MusicVolume`, `SfxVolume` and `AmbienceVolume`. The pause menu's two sliders set `GameAudio.MusicVolume` and `GameAudio.SfxVolume` (0 to 1, saved in PlayerPrefs as `LK_MusicVolume` and `LK_SfxVolume`); the sound slider drives both SFX and Ambience.

One `MusicDirector` per scene (under `Dressing > Audio and Fx` in the level). It plays the title loop on the title screen, then the exploration loop; while `GameManager.IsEvasion` is true (it listens to `OnEvasionChanged`) it crossfades to the chase layer over 1.5 s, and back over 1.5 s. Both loops start on the same audio-clock tick, so they stay in step. It also runs the night ambience and gives `Sfx` the SFX group.

Every one-shot goes through `Sfx`. `Sfx.PlayAt` uses pooled 3D sources with a linear rolloff: full volume within 2.5 m, silent beyond 26 m (footsteps 1.5 to 14 m), so a ghost across the map is not heard. `Sfx.Play2D` is for sounds that belong to the player or the screen.

**Lantern Keeper > Create Polish Assets** makes (or brings up to date) everything the prefabs and the level share: the generated meshes and materials, the mixer, the two font assets, the ghost clips and both Animator Controllers. Every other menu item calls it first, so it rarely needs running by itself.

The clips are assigned by the menu items, in the prefabs (and on the scene's GameManager by **Dress Level01** and **Build Sandbox Scene**):

| Event | Component > field | Clip | Source | 2D / 3D |
|---|---|---|---|---|
| Candle drops below 25% | CandleManager > Candle Low Clip | `SFX/candle_low` | Generated | 2D |
| Lantern goes out | CandleManager > Extinguish Clip | `SFX/extinguish` | Generated | 2D |
| Respawn | GameManager > Respawn Clip | `SFX/respawn` | Generated | 2D |
| Flare starts / stops | LanternController > Flare Start / End Clip | `SFX/flare_start`, `SFX/flare_stop` | Generated | 3D at the player |
| Flare held | PlayerSounds > Flare Loop Clip | `SFX/flare_loop` | Generated | 2D loop, fades in and out |
| Flare pressed with too little candle | LanternController > Flare Denied Clip | `error_006` | Kenney Interface Sounds | 3D at the player |
| Footsteps, landing | PlayerSounds > Footstep Clips, Land Clip | `footstep_grass_000`…`004`, `impactSoft_heavy_000` | Kenney Impact Sounds | 3D, 1.5 to 14 m |
| Player hit | PlayerKnockback > Hit Clip | `impactPunch_medium_000` | Kenney Impact Sounds | 3D |
| Candy thrown / dropped / none left | CandyThrower > Throw / Drop / Empty Clip | `SFX/candy_throw`, `impactGeneric_light_002`, `error_004` | Generated, Kenney, Kenney | 3D |
| Candy lands / is eaten | Candy > Land / Consume Clip | `impactGeneric_light_000`, `SFX/candy_eaten` | Kenney, generated | 3D |
| Spirit platform appears / vanishes | SpiritPlatform > Appear / Vanish Clip (volume 0.4) | `SFX/platform_appear`, `SFX/platform_vanish` | Generated | 3D |
| Ghost notices / chases / is repelled / eats / hits / returns | GhostAI > Notice / Chase / Repelled / Eat / Hit / Return Clip | `SFX/ghost_notice`, `ghost_chase`, `ghost_repel`, `ghost_eat`, `ghost_hit`, `ghost_return` | Generated | 3D |
| Candle stub collected | CandlePickup > Collect Clip | `SFX/candle_pickup` | Generated | 2D |
| Candy collected | CandyPickup > Collect Clip | `handleCoins2` | Kenney RPG Audio | 2D |
| Any pickup (with the sparkle burst) | PickupSparkles > Sparkle Clip | `SFX/pickup_sparkle` | Generated | 3D |
| Candy bowl refill | CandyBowl > Refill Clip | `handleCoins` | Kenney RPG Audio | 2D |
| Plate pressed / released | PressurePlate > Press / Release Clip | `impactMining_001`, `impactGeneric_light_003` | Kenney Impact Sounds | 3D |
| Gate opens / closes | Gate > Open / Close Clip | `SFX/gate_open`, `SFX/gate_close` | Generated | 3D |
| Checkpoint lit | Checkpoint > Lit Clip | `SFX/checkpoint_lit` | Generated | 3D |
| Tutorial prompt | TutorialTrigger > Shown Clip | `SFX/prompt_chime` | Generated | 2D |
| Note opened / closed | NoteReader > Open / Close Clip | `bookOpen`, `bookClose` | Kenney RPG Audio | 2D |
| Pause and resume | GameManager > Pause Clip | `switch_002` | Kenney Interface Sounds | 2D |
| Menu hover / click | UiSounds > Hover / Click Clip | `tick_001`, `click_001` | Kenney Interface Sounds | 2D |
| Title dismissed | UiSounds > Start Clip | `confirmation_002` | Kenney Interface Sounds | 2D |
| Win | GameManager > Win Clip; GoalTrigger > Reached Clip | `SFX/win_jingle`; `doorOpen_2` | Generated; Kenney RPG Audio | 2D |
| Title / exploration / chase music | MusicDirector | `Music/music_title`, `music_explore`, `music_chase` | Generated | 2D, Music group |
| Night ambience | MusicDirector > Ambience Loop | `Ambience/night_wind_crickets` | Generated | 2D, Ambience group |
| Water (creek, pond, trench), graveyard | AudioSources under `Dressing` | `Ambience/creek_water`, `Ambience/graveyard_choir` | Generated | 3D zones, Ambience group |

`ThirdPersonController > Footstep Audio Volume` is 0 on the Player prefab on purpose: Starter Assets plays its steps with `AudioSource.PlayClipAtPoint`, which bypasses the mixer, so `PlayerSounds` plays them instead from the same animation events.

SectionTrigger has no sound field and makes none.

## 9. Dressing the level (Step 5)

**Lantern Keeper > Dress Level01** is the art pass. Run it again after moving anything under `Environment` that it replaces (a gravestone, a crate row, the farmhouse): it reads each block's position and size from the scene.

What it changes, and nothing else:

| What | Where |
|---|---|
| The `Dressing` root, deleted and rebuilt | Scene hierarchy |
| `MeshRenderer.enabled` = off on the greybox blocks that a model replaces: the crate row, fence, nook stacks and pumpkin walls (S1), the fifteen gravestones (S3), the porch rails and hedges (S4), the farmhouse (S5). Their colliders, positions and sizes are untouched | Under `Environment` |
| Fog, ambient light, skybox | Lighting settings |
| The Moon light's colour, intensity, angle and shadow strength | `Lighting > Moon (Directional Light)` |
| Clear Flags = Skybox and Post Processing = on | `MainCamera` |
| Far clip plane = 190 m | `PlayerFollowCamera` lens |
| The three audio clips | `Systems > GameManager` |
| Colours of `M_LK_Ground`, `M_LK_Hazard`, `M_LK_Shallow`, `M_LK_Corn`, `M_LK_Porch` | Material assets |

It does not touch the NavMesh. `Dressing` is not under `Environment`, so a re-bake ignores it too.

The scene file changes every time it runs, even with nothing new to do: the objects it recreates get new file IDs. Everything outside `Dressing` stays byte for byte the same from one run to the next.

Story notes are `StoryNote` objects under `Dressing > Notes`. Their text, position and **Read Range** are in `Notes()` in `LanternKeeperSetup.Dress.cs`; edit them there, not in the scene.
