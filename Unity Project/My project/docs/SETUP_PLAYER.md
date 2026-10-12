# Player setup (manual fallback)

The normal route is the menu: **Lantern Keeper > Build Sandbox Scene**. It creates the layers and tag, the placeholder prefabs and materials, the Player prefab, and `Assets/_Project/Scenes/Sandbox.unity`. All menu items can be run again safely; they overwrite the same assets.

Use this page only if a menu item fails, or to check what it should have produced. Field names below are the labels shown in the Inspector. Ghosts, gates, checkpoints, menus and the NavMesh are in `SETUP_WORLD.md`.

## 1. Project settings

| What | Where | Value |
|---|---|---|
| Tag | Tags and Layers > Tags | `CinemachineTarget` (used by Starter Assets on `PlayerCameraRoot`). `Player` and `MainCamera` are built in. |
| Layer 8 | Tags and Layers > Layers | `Player` (the Starter Assets prefab is already on layer 8; this only names it) |
| Layer 9 | Tags and Layers > Layers | `Ghost` |
| TextMeshPro | Window > TextMeshPro > Import TMP Essential Resources | Needed before the HUD can be built |
| Build Settings | File > Build Profiles > Scene List | `Assets/_Project/Scenes/Level01_PumpkinPatch.unity` first (index 0), then `Assets/_Project/Scenes/Sandbox.unity` |

Spirit platforms and the ground stay on **Default**: `ThirdPersonController > Ground Layers` is Default, and the player must not be on a ground layer.

## 2. Input

`Assets/_Project/Settings/LanternKeeper.inputactions` is a copy of the Starter Assets actions with the gamepad bindings removed and three actions added. Action map `Player`, control scheme `KeyboardMouse`.

| Action | Type | Bindings |
|---|---|---|
| Move | Value / Vector2 | W A S D |
| Look | Value / Vector2 | Pointer delta (mouse or trackpad) |
| LookKeys | Value / Vector2 | Arrow keys (2D vector composite) |
| Jump | Button | Space |
| Sprint | Pass Through | Left Shift |
| Flare | Button | F, right mouse button |
| Throw | Button | E, left mouse button |
| Drop | Button | G |

The scripts find `Flare`, `Throw`, `Drop`, `Look` and `LookKeys` by name on the player's `PlayerInput`, so the names must match.

Step 3 added a second map, `Game` (Start, Pause, Restart), used by GameManager. See `SETUP_WORLD.md`.

## 3. Player prefab

`Assets/_Project/Prefabs/Player.prefab` is a **prefab variant** of `Assets/Starter Assets/Runtime/ThirdPersonController/Prefabs/PlayerArmature.prefab`. To make it by hand: drag PlayerArmature into a scene, rename it `Player`, drag it into `Assets/_Project/Prefabs`, and choose **Prefab Variant**.

### Player (root): tag `Player`, layer `Player` (8)

Already on it from Starter Assets: Animator, CharacterController, ThirdPersonController, BasicRigidBodyPush, StarterAssetsInputs, PlayerInput.

Step 5 overrides two things on those: **Animator > Controller** = `Animations/Player.controller` (a copy of the Starter Assets controller with IK Pass on, see `ANIMATION.md`), and **ThirdPersonController > Footstep Audio Volume** = 0 (PlayerSounds plays the steps through the mixer instead).

| Component | Set or wire |
|---|---|
| PlayerInput | **Actions** = `LanternKeeper.inputactions`, **Default Scheme** = `KeyboardMouse`, **Default Map** = `Player`, **Behavior** = Send Messages (unchanged) |
| CandleManager | Nothing to wire |
| LanternController | **Lantern Light** = `Lantern` child's Light. **Reveal Light** = `Lantern` child's RevealLight. **Ghost Layers** = `Ghost` |
| CandyThrower | **Candy Prefab** = `Prefabs/Candy`. **Throw Origin** = `ThrowOrigin` child |
| PlayerKnockback | Nothing to wire |
| PlayerStateTracker | Nothing to wire |
| KeyboardCameraLook | Nothing to wire |
| LanternProp (Step 5) | **Prop** = `Lantern/LanternVisual`, **Handle** = `LanternHandle`, **Lantern Light** = the Lantern's Light, **Glow Renderers** = `LanternFace`, **Flame** = `LanternFlame`. Lantern-raise, carry sway, hand IK and the throw motion |
| PlayerSounds (Step 5) | **Footstep Clips**, **Land Clip**, **Flare Loop Clip**, **Output Group** = the mixer's SFX group |

The audio clip fields on CandleManager, LanternController, CandyThrower and PlayerKnockback are all filled in (`SETUP_WORLD.md`, section 8).

### Player > Lantern (new child, local position 0.3, 1.0, 0.35)

| Component | Set or wire |
|---|---|
| Light | Type Point, colour warm orange (255, 158, 64), Intensity 4, Shadows None. Range is overwritten at runtime |
| RevealLight | **Radius** 6, **Linked Light** = the Light on this object, **Overexposing** off |

Child `LanternVisual` (local position 0, -0.13, 0): the jack-o'-lantern the player carries. It holds `LanternPumpkin` (mesh `LK_Pumpkin`, `M_LK_Palette`), `LanternFace` (mesh `LK_JackFace`, `M_Lantern`, emissive), `LanternHandle` (an empty the right hand is pinned to) and `LanternFlame` (particles). LanternProp moves `LanternVisual`; the `Lantern` object itself, with the Light and the RevealLight, never moves.

### Player > ThrowOrigin (new child, local position 0.3, 1.3, 0.5)

Empty transform. No components.

### Already there, leave alone

`PlayerCameraRoot` (tag `CinemachineTarget`), `Geometry`, `Skeleton`.

## 4. Other prefabs

| Prefab | Objects and components | Wire |
|---|---|---|
| Candy | Root scaled 0.2: SphereCollider, Rigidbody (mass 0.1, Collision Detection Continuous Dynamic), Candy, **no renderer**. Child `Wrap`: mesh `LK_Candy`, `M_Candy` | Nothing |
| CandlePickup | Root: SphereCollider (**Is Trigger**, radius 0.7), CandlePickup. Child `Visual` (empty, spins and bobs) > `Wax` (mesh `LK_Candle`, `M_CandlePickup`) and `Flame` (mesh `LK_Flame`, `M_LampGlow`) | **Visual** = the child |
| CandyPickup | Root: SphereCollider (**Is Trigger**, radius 0.7), CandyPickup. Child `Visual` > `Wrap` (mesh `LK_Candy`, `M_CandyPickup`) | **Visual** = the child |
| SpiritPlatform | Cube scaled 2 x 0.3 x 2: MeshRenderer (`M_SpiritPlatform`, cast shadows off), BoxCollider, SpiritPlatform. Layer Default. No child renderers: the script takes its light sample points from the renderers' bounds | Nothing (renderers and colliders are found automatically) |
| PorchLamp | Root empty. Children `Post` (cylinder) and `Base`. Child `Lamp` at y 2.2: Light (Point), RevealLight (**Radius** 4), with `Glow` (sphere, `M_LampGlow`) inside `Cage` (mesh `LK_LanternCage`) | RevealLight **Linked Light** = the Light on `Lamp` |
| HUD | Root: Canvas (Screen Space Overlay), CanvasScaler (Scale With Screen Size, 1920 x 1080), HUD, NoteReader. Children: `CandleDish`; `CandleBar` (turned 90 degrees so it stands up) > `Fill` > `CandleFlame`; `CandleLabel`; `CandyIcon`; `CandyLabel`; `Prompt` (CanvasGroup) > `PromptBack`, `PromptLabel`; `NotePrompt` (CanvasGroup); `NotePanel` (CanvasGroup) > `NoteTitle`, `NoteBody`, `NoteHint` | HUD: **Candle Fill** = `Fill` RectTransform, **Candle Fill Image** = `Fill` Image, **Candle Flame**, **Candle Label**, **Candy Label**, **Prompt Label**, **Prompt Group**. NoteReader: the two CanvasGroups and four texts |

`M_SpiritPlatform` uses the shader `LanternKeeper/SpiritPlatform` (`Art/Shaders`): transparent, unlit, cyan, with a bright rim. The script writes the alpha of `_BaseColor` through a MaterialPropertyBlock, so any replacement shader must be transparent and have a `_BaseColor` property, or the platform will not fade.

Fonts: headings use `Art/Fonts/LK_Title SDF` (Creepster), everything else `Art/Fonts/LK_Body SDF` (Lato). Both are baked once from the TTFs in `Art/ThirdParty/Fonts`; delete a font asset and run any menu item to bake it again.

## 5. Sandbox scene

| Object | Notes |
|---|---|
| Moon (Directional Light) | Intensity 0.12, blue. Lighting > Environment: Source Color, very dark blue, skybox None, fog on |
| Environment | `Ground A` (z -10..10), `Ground B` (z 14..34), `Ground C` (z 44..56), tops at y 0. `Pit Floor` top at y -1 |
| Spirit Bridge 1 | Three SpiritPlatforms (scale 2 x 0.3 x 1) at z 10.75, 12, 13.25, y -0.15 |
| PorchLamp | At (3.6, 0, 9): its radius reaches platform 1 only |
| Spirit Bridge 2 | Six SpiritPlatforms (scale 2 x 0.3 x 1.5) across z 34..44 |
| Pickups | Two CandlePickups, two CandyPickups at y 0.7 |
| TestDummy | Capsule on layer `Ghost` with TestDummy (script now in `Scripts/Debug`). Nothing depends on it |
| Player | Player prefab at (0, 0, -6) |
| MainCamera | Starter Assets `MainCamera` prefab (has the CinemachineBrain) |
| PlayerFollowCamera | Starter Assets `PlayerFollowCamera` prefab. **Tracking Target** = `Player/PlayerCameraRoot` |
| HUD | HUD prefab. **Start Prompt** is left empty since Step 3; the title screen lists the controls and TutorialTriggers show the prompts |

## 6. Animator parameters

The player uses `Animations/Player.controller`, a copy of the Starter Assets controller. Since Step 5 it has the four parameters below (each script still checks that a parameter exists before setting it). No state uses them yet: the lantern-raise and the throw are procedural (`LanternProp`), and the parameters are ready for clips to be hooked to later. See `ANIMATION.md`.

| Parameter | Type | Set by | When |
|---|---|---|---|
| `Flaring` | Bool | PlayerStateTracker | True while the FSM state is Flaring |
| `Extinguished` | Bool | PlayerStateTracker | True while the FSM state is Extinguished |
| `State` | Int | PlayerStateTracker | 0 Idle, 1 Walking, 2 Running, 3 Airborne, 4 Flaring, 5 Throwing, 6 KnockedBack, 7 Extinguished |
| `Throw` | Trigger | CandyThrower | On each successful throw |

Already present and driven by Starter Assets: `Speed`, `MotionSpeed`, `Grounded`, `Jump`, `FreeFall`.

## 7. Batch mode (Editor closed)

```
Unity.exe -batchmode -nographics -projectPath . -executeMethod LanternKeeper.EditorTools.LanternKeeperSetup.ImportTmpEssentials -logFile -
Unity.exe -batchmode -quit -nographics -projectPath . -executeMethod LanternKeeper.EditorTools.LanternKeeperSetup.RunAll -logFile -
```

The first command has no `-quit` on purpose: it exits by itself once the TMP import has finished.
