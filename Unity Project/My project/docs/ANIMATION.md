# Lantern Keeper: Animation

Two characters are animated: the player and the ghosts. Smaller animated things (gate, plate, checkpoint, win screen) are listed at the end.

Everything on this page is built by the **Lantern Keeper** menu. No clip was made in an external tool and none comes from Mixamo.

| Asset | Path | Made by |
|---|---|---|
| Ghost Animator Controller | `Assets/_Project/Animations/Ghost.controller` | `LanternKeeperSetup.Animation.cs` (`EnsureGhostAnimator`) |
| Six ghost clips | `Assets/_Project/Animations/Ghost_*.anim` | Same file, one `GhostClip(...)` block each |
| Ghost mesh with hem blend shapes | `Assets/_Project/Art/Generated/Meshes/LK_Ghost.asset` | `LanternKeeperSetup.Meshes.cs` (`BuildGhostMesh`) |
| Player Animator Controller | `Assets/_Project/Animations/Player.controller` | Copy of the Starter Assets controller (`EnsurePlayerAnimator`) |
| Player prop and hand animation | `Assets/_Project/Scripts/Player/LanternProp.cs` | Procedural, at runtime |

---

## 1. Ghost

### What is animated

```
Ghost                 NavMeshAgent, Rigidbody, CapsuleCollider (trigger), GhostAI, GhostVisualFx
├─ Visual             Animator (Ghost.controller)
│  └─ Pivot           <- every clip moves, tilts and scales THIS
│     ├─ Body         SkinnedMeshRenderer (LK_Ghost): blend shapes WaveA, WaveB, Billow
│     ├─ Eye L, Eye R, Mouth
├─ Glow               Light (GhostAI sets its colour to the state tint)
├─ RepelBurst         ParticleSystem (white burst on entering Repelled)
└─ Crumbs             ParticleSystem (while eating)
```

The clips only touch `Pivot` and `Pivot/Body`. The root is never animated, so the NavMeshAgent, the trigger collider that the flare, plates and gates detect, and the ghost's position in the FSM are exactly what they were before Step 5.

The hem is three blend shapes on the sheet mesh. `WaveA` and `WaveB` are the same ripple a quarter of a turn apart; keyed as a sine and a cosine they send the ripple travelling round the hem. `Billow` flares the skirt outward.

### Parameters

`GhostAI` has set these since Step 3, whenever the Animator has them. Nothing was added to `GhostAI` for animation.

| Parameter | Type | Set in | Value |
|---|---|---|---|
| `State` | Int | `GhostAI.EnterState` | The `GhostState`: 0 Drift, 1 Notice, 2 Chase, 3 Repelled, 4 Distracted, 5 Return |
| `Speed` | Float | `GhostAI.Update` | The NavMeshAgent's speed in m/s |

### States: one Animator state per FSM state

| FSM state (`GhostState`) | Animator state | Motion | What it looks like |
|---|---|---|---|
| Drift (0) | `Drift` | Blend tree on `Speed`: `Ghost_FloatIdle` at 0, `Ghost_DriftMove` at 1.6 (drift speed) | Bobbing and swaying while it waits at a waypoint; leaning forward with the hem streaming while it moves |
| Notice (1) | `Notice` | `Ghost_Notice` (0.9 s, does not loop) | A startled stretch upward, a rebound, then a held, quivering lean back. The clip is as long as the notice wind-up |
| Chase (2) | `Chase` | `Ghost_Chase` | A hard forward lean and a lunge, the hem whipping three times a loop |
| Repelled (3) | `Repelled` | `Ghost_Repelled` | Thrown back and squashed, the skirt blown out, the body flickering off and on |
| Distracted (4) | `Distracted` | Blend tree on `Speed`: `Ghost_Eat` at 0, `Ghost_DriftMove` at 2.8 (distracted speed) | Drifting to the candy, then dipped over it, nibbling |
| Return (5) | `Return` | Blend tree on `Speed`: `Ghost_FloatIdle` at 0, `Ghost_DriftMove` at 2.2 (return speed) | Drifting home |

Drift, Distracted and Return are blend trees because the ghost both stands still and moves inside each of them (waiting at a waypoint, eating, arriving).

### Clips

| Clip | Length | Loops | Pivot | Hem |
|---|---|---|---|---|
| `Ghost_FloatIdle` | 2.4 s | yes | Bob 7 cm, sway 3.5°, slow turn 5°, a breath of squash and stretch | One ripple per loop, Billow 12 |
| `Ghost_DriftMove` | 1.2 s | yes | Lean forward 10°, bob 4 cm twice a loop | Two ripples per loop, Billow 28 |
| `Ghost_Notice` | 0.9 s | no | At 0.12 s stretched to 132% tall and 82% wide and lifted 20 cm; rebound at 0.3 s; then leaning back 5° and quivering | Billow jumps to 70 and settles at 25 |
| `Ghost_Chase` | 0.6 s | yes | Lean forward 24°, pushed 5 cm ahead, stretched 10% along the direction of travel | Three ripples per loop, Billow 45 |
| `Ghost_Repelled` | 0.5 s | yes | Lean back 16°, pulled 12 cm back, squashed to 82% tall and 114% wide, pulsing | Billow 100; the body renderer switches off twice a loop (flicker) |
| `Ghost_Eat` | 0.8 s | yes | Dipped 24 cm, leaning forward 30°, nodding six times a loop, rocking 5° | One ripple per loop, Billow 35 |

### Transitions

Every transition has one condition, `State equals <n>`, no exit time, and a short fixed blend. They are the transitions of `docs/FSM.md` section 2, and no others.

| From | To | Condition | Blend | FSM transition it follows |
|---|---|---|---|---|
| Drift | Notice | State = 1 | 0.05 s | Sees the lit player in range |
| Notice | Chase | State = 2 | 0.12 s | Notice wind-up (0.9 s) elapsed |
| Notice | Return | State = 5 | 0.2 s | Player left range, lantern out, or sight blocked |
| Chase | Return | State = 5 | 0.2 s | Hit the player, lost the player (16 m), or lantern out |
| Return | Drift | State = 0 | 0.25 s | Reached its patrol point (also `ResetToPatrol`) |
| Drift, Notice, Chase, Return | Distracted | State = 4 | 0.2 s | Claimed a landed candy in range |
| Distracted | Return | State = 5 | 0.2 s | Candy eaten, lost, or unreachable |
| Drift, Notice, Chase, Return, Distracted | Repelled | State = 3 | 0.06 s | `Repel` called (flare in range), from any state |
| Repelled | Return | State = 5 | 0.2 s | Repel timer (2 s) ran out |
| Notice, Chase, Distracted, Repelled | Drift | State = 0 | 0.15 s | `ResetToPatrol` (player respawn) |

That is 20 transitions in the controller. FSM transitions that do not change the state (`Repelled → Repelled` restarting its timer, `Distracted → Distracted` picking a new candy) need none.

**Checked against `docs/FSM.md`:** the six states and the State values match `GhostState`; each row of the ghost transition table there has a transition here with the same source and destination; the controller has no transition that the FSM lacks. The headless test in Step 5 put one ghost through Drift → Notice → Chase → Repelled → Return → Distracted (eating) → Return → Drift and read the Animator's state at each step; all matched.

**Safety net.** If two FSM changes land in one frame, the Animator can miss the first and be left in a state with no transition to the current one. `GhostVisualFx` watches for a mismatch lasting longer than 0.4 s and cross-fades to the right state. It is not part of the design; it only keeps a rare timing accident from leaving a ghost in the wrong pose.

### Changing the ghost animation

- **A clip's motion:** edit its `GhostClip(...)` block in `LanternKeeperSetup.Animation.cs` and run any ghost menu item (**Lantern Keeper > Create Ghost Prefab**). Clips are rewritten in place. A clip edited by hand in the Animation window is overwritten the next time that runs, so change the code, or stop calling `EnsureGhostAnimator`.
- **States or transitions:** edit `EnsureGhostAnimator` and bump `GhostControllerVersion`. The controller is rebuilt once when the version differs.

### Swapping in Mixamo clips later

The ghost is a Generic rig with no skeleton, so Mixamo's humanoid clips cannot drive this mesh as it is. To use them:

1. Get a rigged humanoid ghost (a Mixamo character, or a ghost model auto-rigged on mixamo.com). Download it and each animation as **FBX for Unity**, "Without Skin" for the animations.
2. Put them under `Assets/_Project/Art/ThirdParty/Mixamo/`. In each FBX's import settings: **Rig > Animation Type = Humanoid**, and for the animations **Avatar Definition = Copy From Other Avatar** (the character's). Tick **Loop Time** on the looping clips.
3. In `Prefabs/Ghost`, replace the children of `Visual/Pivot` with the model, and set the `Visual` Animator's **Avatar** to the character's avatar. Leave the Animator on `Visual` and leave the root alone.
4. Open `Animations/Ghost.controller` and drag the new clips into the six states' motions (and into the three blend trees). States, parameters and transitions stay as they are, because they follow the FSM, not the clips.
5. Point `GhostAI > Tint Renderers` and `GhostVisualFx > Glow Renderers` at the new model's renderer, and give it a material with a transparent base colour and emission.
6. Stop the menu from putting the generated ghost back: in `CreateGhostPrefab` remove the `BuildGhostVisual` call (or never run **Create Ghost Prefab** again). Record the model and clips in `CREDITS.md`; Mixamo assets are royalty-free for use in games under Adobe's terms.

Suggested Mixamo clips: Floating (idle), Flying / Hover Forward (drift), Surprised (notice), Zombie Run or Fast Hover (chase), Hit Reaction / Cower (repelled), Eating (distracted).

---

## 2. Player

### Locomotion: Starter Assets, unchanged

`Player.controller` is a copy of `StarterAssetsThirdPerson.controller`, so the Starter Assets clips play as before, driven by `ThirdPersonController`:

| State | Clip(s) | Plays when |
|---|---|---|
| `Idle Walk Run Blend` | `Stand--Idle`, `Locomotion--Walk_N`, `Locomotion--Run_N`, blended on `Speed` | Grounded |
| `JumpStart` | `Jump--Jump` | `Jump` is set |
| `InAir` | `Jump--InAir` | `FreeFall` is set |
| `JumpLand` | `Locomotion--Walk_N_Land`, `Locomotion--Run_N_Land`, blended on `Speed` | `Grounded` again |

The copy differs from the original in two ways only: **IK Pass** is ticked on the base layer, and it has four extra parameters that the player scripts have looked for since Step 2 (`Flaring` and `Extinguished` Bool and `State` Int, set by `PlayerStateTracker`; `Throw` Trigger, set by `CandyThrower`). No state or transition uses them yet. They are there so a clip can be hooked to them later without touching a script.

The walk, run and land clips raise the `OnFootstep` and `OnLand` animation events. `PlayerSounds` receives them and plays the step and landing sounds through the mixer.

### Lantern-raise and throw: procedural

Starter Assets has no clip for raising an arm or throwing, and no other humanoid clip is in the project, so these two are done in code by `LanternProp` (on the Player root, beside the Animator):

| Motion | Driven by | How |
|---|---|---|
| **Carry** | Always | The right hand is pinned to the jack-o'-lantern's handle with humanoid hand IK (`OnAnimatorIK`), on top of whatever locomotion clip is playing. The lantern sways and bobs with the player's speed. |
| **Lantern-raise** | `LanternController.IsFlaring` | The lantern prop lifts 62 cm and tips forward, eased in and out. The arm follows it, because the hand is pinned to it. The light gets brighter and whiter through `LanternController`, as before. |
| **Throw** | `CandyThrower.IsThrowing` going true (a throw; a drop with G does not set it) | The left hand is driven by IK through wind-up, release and follow-through over 0.42 s. |
| **Flame** | `CandleManager.IsLit`, the lantern light's colour and intensity | The carved face glows in the light's colour; flame particles stop while the lantern is out. |

Only the visual prop moves. The `Lantern` object that holds the Light and the RevealLight stays where it was, because its position is the centre of the reveal and repel radius.

An upper-body Animator layer was not added: a layer needs a humanoid clip to play, and there is none to give it. Hand IK gives the same result (the arm does something different from the legs) without one.

### Swapping in Mixamo clips later

1. Download "Standing Torch Light" / "Holding Up" (raise) and "Throw Object" (throw) from Mixamo as **FBX for Unity, Without Skin**, into `Assets/_Project/Art/ThirdParty/Mixamo/`. Import as **Humanoid**, **Avatar Definition = Copy From Other Avatar** using the Starter Assets avatar.
2. In `Animations/Player.controller` add a layer `UpperBody` with weight 1 and an Avatar Mask covering the arms and chest. Give it an empty default state, a `Raise` state entered while `Flaring` is true, and a `Throw` state entered by the `Throw` trigger with an exit time back to empty. The parameters are already there and already set.
3. On the Player prefab, set `LanternProp > Raise Offset` to zero and both hand weights to 0 (or remove the component), so the clip and the IK do not both move the arm. Parent `LanternVisual` to the right-hand bone if the clip should carry it.

---

## 3. Smaller animations

| Thing | Animation | Where |
|---|---|---|
| Gate | The door slides down into the ground with a smoothstep ease (since Step 3). New: the iron bars rattle and dust puffs out as it starts to open or close | `Gate.cs` (unchanged); `GateFx.cs` |
| Pressure plate | The pad sinks and changes colour (since Step 3). New: the rune on it fades from carved to glowing | `PressurePlate.cs` (unchanged); `PlateRune.cs` |
| Checkpoint | When lit, the flame grows from nothing with a small overshoot and the lamp's light fades in over 0.9 s, then both flicker | `CheckpointGlow.cs` |
| Spirit platform | Fade in and out (since Step 2). New: a pulsing rim glow in the shader | `SpiritPlatform.cs` (unchanged); `SpiritPlatform.shader` |
| Pickups | Spin and bob (since Step 2). New: a sparkle burst when collected | `Pickup.cs` (unchanged); `PickupSparkles.cs` |
| Win screen | Fades in over 0.9 s | `UiFadeIn.cs` |
| HUD candle | The candle shortens and the flame rides down with it; hidden when the lantern is out | `HUD.cs` |
