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
| Build Settings | File > Build Profiles > Scene List | Add `Assets/_Project/Scenes/Sandbox.unity` |

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

| Component | Set or wire |
|---|---|
| PlayerInput | **Actions** = `LanternKeeper.inputactions`, **Default Scheme** = `KeyboardMouse`, **Default Map** = `Player`, **Behavior** = Send Messages (unchanged) |
| CandleManager | Nothing to wire |
| LanternController | **Lantern Light** = `Lantern` child's Light. **Reveal Light** = `Lantern` child's RevealLight. **Ghost Layers** = `Ghost` |
| CandyThrower | **Candy Prefab** = `Prefabs/Candy`. **Throw Origin** = `ThrowOrigin` child |
| PlayerKnockback | Nothing to wire |
| PlayerStateTracker | Nothing to wire |
| KeyboardCameraLook | Nothing to wire |

### Player > Lantern (new child, local position 0.3, 1.0, 0.35)

| Component | Set or wire |
|---|---|
| Light | Type Point, colour warm orange (255, 158, 64), Intensity 4, Shadows None. Range is overwritten at runtime |
| RevealLight | **Radius** 6, **Linked Light** = the Light on this object, **Overexposing** off |

Optional child `LanternVisual`: a sphere scaled 0.22 with its collider removed and material `M_Lantern`.

### Player > ThrowOrigin (new child, local position 0.3, 1.3, 0.5)

Empty transform. No components.

### Already there, leave alone

`PlayerCameraRoot` (tag `CinemachineTarget`), `Geometry`, `Skeleton`.

## 4. Other prefabs

| Prefab | Objects and components | Wire |
|---|---|---|
| Candy | Sphere scaled 0.2: MeshRenderer (`M_Candy`), SphereCollider, Rigidbody (mass 0.1, Collision Detection Continuous Dynamic), Candy | Nothing |
| CandlePickup | Root: SphereCollider (**Is Trigger**, radius 0.7), CandlePickup. Child `Visual`: cylinder, no collider, `M_CandlePickup` | **Visual** = the child |
| CandyPickup | Root: SphereCollider (**Is Trigger**, radius 0.7), CandyPickup. Child `Visual`: cube, no collider, `M_CandyPickup` | **Visual** = the child |
| SpiritPlatform | Cube scaled 2 x 0.3 x 2: MeshRenderer (`M_SpiritPlatform`, cast shadows off), BoxCollider, SpiritPlatform. Layer Default | Nothing (renderers and colliders are found automatically) |
| PorchLamp | Root empty. Child `Post`: cylinder. Child `Lamp` at y 2.2: Light (Point), RevealLight (**Radius** 4). Grandchild `Glow`: sphere, no collider, `M_LampGlow` | RevealLight **Linked Light** = the Light on `Lamp` |
| HUD | Root: Canvas (Screen Space Overlay), CanvasScaler (Scale With Screen Size, 1920 x 1080), HUD. Children: `CandleBar` (Image, dark) > `Fill` (Image, orange, stretched) and `CandleLabel` (TMP); `CandyLabel` (TMP); `Prompt` (TMP + CanvasGroup, alpha 0) | **Candle Fill** = `Fill` RectTransform, **Candle Fill Image** = `Fill` Image, **Candle Label**, **Candy Label**, **Prompt Label**, **Prompt Group** |

`M_SpiritPlatform` must be URP/Lit with **Surface Type = Transparent**, Blending Mode Alpha, cyan base colour. The script writes the alpha through a MaterialPropertyBlock, so an opaque material will not fade.

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

The player uses the Starter Assets controller as it is. These parameters are **optional today**: each script checks whether the parameter exists and skips it if not. Add them in Step 5 when the flare pose and throw animation go in.

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
