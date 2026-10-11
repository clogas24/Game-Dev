# Ghost, world and game-flow setup (manual fallback)

The normal route is the menu: **Lantern Keeper > Build Sandbox Scene**. It runs every other item first (placeholder assets, Player, Ghost, World prefabs), rebuilds `Assets/_Project/Scenes/Sandbox.unity` and bakes its NavMesh. Every item can be run again; assets are overwritten in place.

Use this page if a menu item fails, or when building the real level by hand in Step 4. Field names are the Inspector labels. Player setup is in `SETUP_PLAYER.md`.

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

Children (all on layer `Ghost`, no colliders): `Body` capsule (`M_Ghost`, transparent), `Head` sphere (`M_GhostHead`, transparent), `Glow` point light (range 4).

In a scene: place the ghost on the NavMesh, create empty waypoint objects, and drag them into **Patrol Points** in visiting order. With no patrol points the ghost idles where it was placed. Keep patrol routes off pressure plates, or a drifting ghost will press them.

Animator (Step 5): GhostAI sets float `Speed` and int `State` (0 Drift, 1 Notice, 2 Chase, 3 Repelled, 4 Distracted, 5 Return) only if those parameters exist.

## 3. World prefabs

| Prefab | Objects and components | Wire |
|---|---|---|
| PressurePlate | Root: PressurePlate. `Base` cube 2.2 x 0.06 x 2.2 with collider. `Pad` cube 2 x 0.08 x 2, no collider | **Pad** = Pad transform, **Pad Renderer** = Pad renderer. Detection box defaults to 2 x 1.2 x 2 above the plate (yellow gizmo) |
| Gate | Root: Gate. `Frame` > `Pillar L`, `Pillar R` (cubes with NavMeshObstacle, Carve on), `Beam`. `Door` cube 3 x 3 x 0.25: BoxCollider, kinematic Rigidbody, NavMeshObstacle (Carve on, Carve Only Stationary off) | **Door** = Door transform. Per instance: **Plates**, **Required Plates**, **Stay Open Once Opened** |
| Checkpoint | Root: SphereCollider (**Is Trigger**, radius 1.6), Checkpoint. `Post`. `Lamp` at y 2.3: Light (Point), RevealLight (radius 5, linked to the Light), `Lantern (unlit)` cube, `Lantern (lit)` glow sphere. `RespawnPoint` at local (0, 0, -1.5) | **Respawn Point**, **Lamp Light**, **Reveal Light**, **Lit Visual** = `Lantern (lit)` |
| SectionTrigger | Root: BoxCollider (**Is Trigger**, 8 x 3 x 1), SectionTrigger | Per instance: **Section Id** (unique), optional **Display Name** |
| TutorialTrigger | Root: BoxCollider (**Is Trigger**, 8 x 3 x 1), TutorialTrigger | Per instance: **Tutorial Id**, **Text** (tokens `{flare}` `{throw}` `{drop}` `{jump}`), **Seconds** |
| GoalTrigger | Root: BoxCollider (**Is Trigger**) in front of the door, GoalTrigger. `Door`, `Frame`, `PorchLight` | Nothing |
| CandyBowl | Root: SphereCollider (**Is Trigger**, radius 1.2), CandyBowl. `Stand`, `Bowl` | Nothing |
| GameMenus | Canvas (Screen Space Overlay, sort order 20), CanvasScaler (1920 x 1080), GraphicRaycaster, GameMenus. Child `EventSystem` (EventSystem + InputSystemUIInputModule). Panels `TitlePanel`, `PausePanel`, `WinPanel`, saved inactive | The three panels, **Controls Label**, **Resume / Restart / Quit Button**, **Sensitivity Slider**, **Sensitivity Value Label**, **Win Stats Label**, **Win Restart Button** |

Gate notes:
- The door slides **down** into the ground (`Slide Offset` 0, -3.1, 0), so it cannot pin anyone. The ground under a gate must be thin enough, or the gate raised, for that to look right.
- **Never closes on someone**: while anything on `Blocker Layers` (Player, Ghost) is inside the doorway box (red gizmo, 3 x 3 x 1.6) the door stays open.
- For a two-plate gate where the player stands on one plate, tick **Stay Open Once Opened**; otherwise the gate shuts when they step off.
- Build walls on both sides of the gate so it cannot be walked around.

## 4. Scene objects for game flow

| Object | Components | Wire |
|---|---|---|
| GameManager | GameManager, DebugOverlay | **Input Actions** = `LanternKeeper.inputactions`. Optional **Start Point** |
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
