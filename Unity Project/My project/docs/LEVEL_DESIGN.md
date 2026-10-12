# Level 01: Pumpkin Patch (greybox)

Scene: `Assets/_Project/Scenes/Level01_PumpkinPatch.unity`. Built once by **Lantern Keeper > Create Level01 Greybox** and tuned by hand from then on. This page describes the level as generated for Playtest 1 (`v0.1-playtest1`); update it when the layout changes.

- The level runs north, along **+Z**. Ground tops are at y = 0.
- Sections 1–2 are a strip 14 m wide (x −7..7). Sections 3–5 are 28 m wide (x −14..14).
- Invisible walls close the strip on all sides. The only ways to fall are the deliberate ones.
- `GameManager > Fall Death Height` is **−4**. Creek, pond and trench floors are at −1.6 (safe); pit floors are at −6 (deadly).

## Colour code

| Colour | Meaning | Material |
|---|---|---|
| Dark grey | Safe ground | `M_LK_Ground` |
| Cyan, see-through | Spirit platform (only exists in light) | `M_SpiritPlatform` |
| Dark red | Deadly pit floor, and a strip along each pit edge | `M_LK_Hazard` |
| Dark blue | Shallow water: a fall costs a walk back up a ramp, not a life | `M_LK_Shallow` |
| Brown | Things to jump over or walk round (crates, fence, rails) | `M_LK_Obstacle` |
| Light grey | Gravestones: cover from ghosts | `M_LK_Stone` |
| Olive | Corn walls and hedges | `M_LK_Corn` |
| Blue-grey | Solid walls, crypts, the farmhouse | `M_Wall` |
| Bright yellow | Pressure plate (turns green while pressed) | plate `Idle Color` |
| Bright pink | Candy bowl | `M_LK_CandyBowl` |
| Bright green disc | Checkpoint (porch lantern) | `M_LK_Checkpoint` |
| Warm orange | The goal: farmhouse door, its porch and a beacon light | `M_LK_Goal` |

## Map

Top-down, north at the top, so read it from the **bottom** up. Not to scale across; the z values on the left are metres.

```
Legend   P start        C checkpoint      * candle stub     B candy bowl     L porch lamp (reveal light)
         = spirit platform                ~ shallow water   X deadly pit     # wall / corn / crypt
         o gravestone   [1] plate         GATE              a..j ghost, with <-> or | for its patrol
         .. T ..  tutorial prompt line    ----S#----  section trigger

 z        x = -14                    0                    +14
392      +------------------------------------------------+
         |          ########################              |   farmhouse (dark block)
384      |          #########[ DOOR ]#######              |   GOAL: door + orange porch + beacon
         |                    porch                       |
372      |XXXXXXXXXXXXXXXXXXXXX====XXXXXXXXXXXXXXXXXXXXXXX|   run C (5.4 m)
         |XXXXXXXXXXXXXXXXXXXXX    XXXXXXXXXXXXXXXXXXXXXXX|     jump 0.8 m
         |XXXXXXXXXXXXXXXXXXXXX====XXXXXXXXXXXXXXXXXXXXXXX|   run B (4.9 m)      S5 last pit
         |XXXXXXXXXXXXXXXXXXXXX    XXXXXXXXXXXXXXXXXXXXXXX|     jump 0.8 m       (ghosts stop at the edge)
356      |XXXXXXXXXXXXXXXXXXXXX====XXXXXXXXXXXXXXXXXXXXXXX|   run A (4.1 m)
         |                         L                      |   lamp shows run A
349      |          j <----------------------> j          |   ghost J
344      |          i <----------------------> i          |   ghost I   (the pair)
338      |####################      ######################|   corn 5: 4 m gap
         |                    B        C                  |   S5_Checkpoint_Mid, bowl
328      |############################        ############|   corn 4: gap x 3..7.5
         | *  (dead end)                                  |   row 3: stub in the dead end
322      |################      ##################        |   corn 3: gaps x -4..0 and 9.5..14
319.5    | h <--------------------------------------> h   |   row 2: ghost H
316      |        ################      ##################|   corn 2: gaps x -14..-9.5 and 0..4
         |                                                |   row 1
310      |######################################          |   corn 1: gap x 9.5..14
         |            *     B     C                       |   S5_Checkpoint_Entry, bowl, stub
300      |#####################GATE#######################|   S4_Gate_TwoPlate (2 plates, latches open)
         |----------------------S5------------------------|
         |      f                              g          |   ghosts F, G: short north-south patrols
288.5    |      f                              g          |     (z 288.5..293.5, x = -8.5 / +8.5)
280      |        [A]                      [B]            |   S4_Plate_TwoA, S4_Plate_TwoB
273      |                   B                            |   bowl
270      |###################      #######################|   hedge corridor, 5 m wide
265.5    |###################  C   #######################|   S4_Checkpoint_TwoPlate
262      |#####################GATE#######################|   S4_Gate_OnePlate (1 plate, closes again)
254      |      e                                         |
246      |      |  \ e                     [1] *          |   ghost E (triangle), S4_Plate_One, stub
240      |      e                                         |
         |.......................T6.......................|   "A plate opens its gate while something stands on it."
236.5    |                          B                     |   bowl
232      |######################    ######################|   wall: 3.2 m opening (the narrow path)
227.5    |                 d <------> d                   |   ghost D guards the opening
         |                                                |   lure yard: throw candy to either side
207      |ooooooooooooooooooo      ooooooooooooooooooooooo|   porch rails: 5 m opening
205      |                    B  C                        |   S4_Checkpoint_Porch, bowl
         |.......................T5.......................|   "Take candy from the bowl..."
197      |----------------------S4------------------------|
188      |~~~~~~~~~~~~~~~~~~~~~====~~~~~~~~~~~~~~~~~~~~~~~|
         |~~~~~~~~~~~~~~~~~~~~~====~~~~~~~~~~~~~~~~~~~~~~~|   S3 trench (shallow), 8 m bridge
180      |/ramp~~~~~~~~~~~~~~~~====~~~~~~~~~~~~~~~~~ramp\ |   ramps lead back to the near side
177.5    |                      c                         |
174      |       c <------------+------------> c          |   ghost C: triangle in front of the bridge
170      |          o                    o                |
165.5    |   *                                            |   stub, just past the corridor
163      |#####################    #######################|
160.5    |#####################  b #######################|   ghost B: up and down a corridor
153.5    |#####################  b #######################|     3.2 m wide (two crypts)
150      |#####################    #######################|
141      |     a <------------------------------> a       |   ghost A: 18 m east-west patrol
136      |        o     o      o    o                     |
131      |    o      o      o    o      o                 |   gravestones (cover)
         |.......................T4.......................|   "Hold F to flare..."
124      |       o    o      o      o                     |
116      |###################      #######################|   graveyard wall: 4 m gate opening
114      |                     C                          |   S3_Checkpoint_GraveyardGate
107.5    |----------------------S3------------------------|
106      +----------------+              +----------------+
                          |XXXXXXXX==XXXX|                    S2 deep pit (deadly), 16 m bridge
                          |XXXXXXXX==XXXX|                    you cannot see across until you walk
90                        |XXXXXXXX==XXXX|
88.3                      |          C   |                    S2_Checkpoint_DeepBridge (its lamp lights the first tiles)
80                        |~~~~~~~~~~==~~|
                          |~==*~~~~~~==~~|                    S2 pond (shallow): bridge 2 doglegs
71.4                      |~========== ~~|                      north, east, north; a 2-tile spur
                          |~==~~~~~~~~~~~|                      holds a candle stub
64                        |/~==~~~~~~~~~\|
58                        |~~~~~==~~~~~~~|                    S2 creek (shallow): bridge 1, 6 m
52                        |/~~~~==~~~~~~\|
50.4                      |        L     |                    lamp lights the first platform only
                          |......T3......|                    "Spirit platforms exist only inside light."
48.5                      |------S2------|
47                        |   C          |                    S2_Checkpoint_Creek
42                        |    ##########|                    pumpkin wall B   } an S-bend
36                        |##########    |                    pumpkin wall A   }
28..31                    |    ditch     |                    3 m wide, 1 m deep: sprint + jump
19..26                    |### * ###     |                    nook with a candle stub, left of the path
                          |......T2......|                    "Your candle burns down..."
16                        |oooooooooooooo|                    fence, 0.8 m: jump
10                        |oooooooooooooo|                    pumpkin crates, 0.5 m: jump
5                         |......T1......|                    "Space to jump. Hold Left Shift to sprint..."
1.5                       |------S1---C--|                    S1_Checkpoint_Start (lit)
0                         |      P       |
-8                        +--------------+
                       x = -7     0     +7
```

## Sections

Times are estimates (see [Pacing](#pacing)). "Remix" names the earlier ability and the new way it is used.

| Section | Goal | Teaches | Applies | Remixes | Est. time | Pickups and why | Checkpoint |
|---|---|---|---|---|---|---|---|
| **S1 Pumpkin Patch** (z −8..48) | Reach the creek | Move, jump, sprint, camera; the candle drains; candle stubs refill it | – | – | 50 s | **1 candle stub** in a nook 5 m left of the path. It is the lesson itself: the candle prompt appears as the glow comes into view, and the detour is safe and short | `S1_Checkpoint_Start` beside the spawn, lit at start |
| **S2 Spirit Bridge** (z 48..106) | Cross the creek, the pond and the pit | Light reveals spirit platforms (a porch lamp shows the first one before the player's own light reaches it) | Bridge 2: follow the platforms round two corners with only ~6 m lit. Bridge 3: 16 m over a deadly pit, far side unseen | Move/jump: walking slowly and precisely is now the skill | 58 s | **1 candle stub** on a two-tile dead-end spur of bridge 2. It rewards sweeping the light around at a corner and shows that bridges can branch; it floats over water, so it also hints that something invisible is there | `S2_Checkpoint_Creek` on the way to bridge 1. `S2_Checkpoint_DeepBridge` at the foot of bridge 3: lighting it reveals the bridge's first tiles |
| **S3 Graveyard** (z 106..196) | Get past three ghosts and over the trench | Ghosts: Drift, Notice, Chase. The flare repels them and costs candle 8× faster | Ghost B in a 3.2 m corridor: the obvious way through needs a flare | **Light reveal + flare:** ghost C circles in front of a short bridge. Flaring at it erases the bridge, so the player must stop flaring or pick the moment. **Candle:** the flare spends it | 83 s | **1 candle stub** just beyond ghost B's corridor. It pays back the forced flare, so the lesson costs little, and it is the first thing seen on leaving the corridor | `S3_Checkpoint_GraveyardGate` in front of the gate opening, 27 m from the nearest patrol point |
| **S4 Candy Porch → Cemetery Gate** (z 196..300) | Open two gates | Throw candy to lure a ghost (ghost D guards a narrow opening; candy thrown aside pulls it away) | One-plate gate: a ghost must eat candy **on** the plate. The player can press the plate, but it is 16 m from the doorway and the gate shuts first | **Candy:** two-plate gate (`requiredPlates = 2`, `stayOpenOnceOpened`). Two ghosts on two plates, or one ghost plus the player. **Ghosts** are now tools, not threats | 119 s | **1 candle stub** beside plate one. The plate puzzles are the longest waits in the level, so this is where a careful player would otherwise run low; placed next to the plate it also draws the eye to the plate. Candy comes from three bowls (porch, each gate yard), which refill, so a puzzle can always be retried | `S4_Checkpoint_Porch` between the rails. `S4_Checkpoint_TwoPlate` in the corridor behind gate one, filling it, so a death in the two-plate yard never sends the player back through gate one |
| **S5 Cornfield → Farmhouse** (z 300..392) | Reach the farmhouse door | – | Corn maze with ghost H: cover (corn blocks sight), timing, candy or flare, the player's choice | **Everything:** two ghosts guard a gap, and one candy takes only one of them (split the pair). **Light reveal:** the last bridge is the escape route, since ghosts cannot follow, and a flare there erases it. **Move/jump:** two 0.8 m jumps between platform runs, with a shrinking light if the candle is low. **Candle:** a 20 m detour through the maze for a stub, or press on | 103 s | **2 candle stubs.** One in plain sight at the entry: the last guaranteed fuel, so nobody starts the finale empty. One in the maze's dead end: risk for reward, a 20 m detour in ghost H's rows. Candy bowls at the entry and at the midway glade | `S5_Checkpoint_Entry` behind gate two. `S5_Checkpoint_Mid` in the gap out of the maze, before the pair |

## Ability coverage

| Ability | S1 | S2 | S3 | S4 | S5 |
|---|---|---|---|---|---|
| Move / jump / sprint / camera | **Teach** | Apply | Apply | – | **Remix** |
| Candle management | **Teach** | Apply | **Remix** | Apply | **Remix** |
| Light reveals platforms | – | **Teach** + Apply | **Remix** | – | **Remix** |
| Flare | – | – | **Teach** + Apply | Apply (optional) | **Remix** |
| Candy | – | – | – | **Teach** + Apply + **Remix** | **Remix** |

What each cell means:

- **Move/jump.** S1 teaches (crates, fence, sprint-jump ditch, S-bend). S2 and S3 apply (careful walking on bridges, weaving between gravestones). S5 remixes: jumps between spirit platforms over a deadly pit, usually with ghosts behind and the light shrinking.
- **Candle management.** S1 teaches (it drains; stubs refill it). S2 and S4 apply (go off the path for a stub; waiting costs candle). S3 remixes: the candle becomes fuel for the flare. S5 remixes: arrive low and choose between a detour for a stub and pressing on.
- **Light reveal.** S2 teaches with a lamp and applies it twice. S3 remixes: the flare, the same light made stronger, erases the bridge. S5 remixes: the bridge is a refuge that ghosts cannot enter.
- **Flare.** S3 teaches (ghost A, with room to retreat) and applies (ghost B). S4 allows it as an expensive way past ghost D. S5 remixes: flaring doubles how far away ghosts notice the player and erases the escape bridge, so it only works at the right moment.
- **Candy.** S4 teaches (lure), applies (ghost on a plate) and remixes (two plates). S5 remixes: one candy per ghost, so two ghosts need two throws or candy plus a flare.

Every ability has at least one Teach and one Remix.

## Ghosts

All ten ghosts use the Ghost prefab. Two fields are overridden in this level:

- **Line Of Sight Blockers = Default** on every ghost. Walls, corn, crypts, gravestones (1.7 m, above the 1.2 m eye line) and closed gates hide the player. The sandbox leaves this off.
- **Candy Sense Range = 11 m** (instead of 9) on the three plate ghosts E, F, G. Their routes stay 8.5 m from their plate so a player standing on it is outside the 7 m notice range; 11 m lets them find candy anywhere on the plate.

| Ghost | Section | Patrol (x, z) | Job |
|---|---|---|---|
| `S3_Ghost_A` | S3 | (−9, 141) ↔ (9, 141) | First ghost. Seen from the gate, 24 m away. Time the gap, use the gravestones, or flare |
| `S3_Ghost_B` | S3 | (0, 153.5) ↔ (0, 160.5) | Fills a 3.2 m corridor: forces a flare |
| `S3_Ghost_C` | S3 | (−8, 174) → (8, 174) → (0, 177.5) | Circles 2.5 m from the trench bridge: flaring at it erases the bridge |
| `S4_Ghost_D` | S4 | (−3.5, 227.5) ↔ (3.5, 227.5) | Guards the narrow opening: teaches the lure |
| `S4_Ghost_E` | S4 | (−0.5, 246) → (−8, 240) → (−8, 254) | The weight for plate one |
| `S4_Ghost_F` | S4 | (−8.5, 288.5) ↔ (−8.5, 293.5) | The weight for plate A |
| `S4_Ghost_G` | S4 | (8.5, 288.5) ↔ (8.5, 293.5) | The weight for plate B |
| `S5_Ghost_H` | S5 | (−11.5, 319.5) ↔ (11.5, 319.5) | Sweeps the middle row of the maze |
| `S5_Ghost_I` | S5 | (−7, 344) ↔ (7, 344) | One of the pair behind the 4 m gap |
| `S5_Ghost_J` | S5 | (7, 349) ↔ (−7, 349) | The other, starting at the opposite end |

Checked by **Lantern Keeper > Validate Level01 (read-only)**: all 22 patrol points are on the NavMesh with complete paths between them; the closest a patrol route comes to a plate is 8.5 m; the closest patrol point to a respawn point is 11.3 m (the rule is 7 m). The 35 spirit platforms are not on the NavMesh, so a ghost chasing the player stops at the edge of a bridge. The validator reports each plate as reachable by two ghosts. Closed gate doors carve the NavMesh, so I read that as D and E for plate one and F and G for plates A and B; the validator only prints the count.

## Pacing

The estimate is path length ÷ walking speed (2 m/s) plus waiting time. Path lengths are measured by the validator along the route stored in the scene under `DesignNotes (EditorOnly) > GoldenPath`, which visits every pickup. Waiting times are my estimates for a first-time player, in `Level01WaitSeconds` in `LanternKeeperSetup.Level01.cs`.

| Section | Path | Walking | Waiting (what for) | Total |
|---|---|---|---|---|
| S1 | 65.9 m | 33.0 s | 17 s (get bearings, two prompts, fumbled jumps) | 50.0 s |
| S2 | 76.2 m | 38.1 s | 20 s (one prompt, hesitating on three bridges) | 58.1 s |
| S3 | 106.1 m | 53.0 s | 30 s (watch ghost A, one prompt, three ghost encounters) | 83.0 s |
| S4 | 127.5 m | 63.8 s | 55 s (two prompts, three lures at about 3 s travel + 4 s eating each, working out two puzzles) | 118.8 s |
| S5 | 146.5 m | 73.2 s | 30 s (timing ghost H, splitting the pair, two jumps) | 103.2 s |
| **Total** | **522.2 m** | **261 s** | **152 s** | **413 s = 6.9 min** |

Sprinting the whole route with the same waits would take 4.2 minutes; that is the floor for a returning player, not the estimate for a first one. Replace the waiting column with real numbers from `S1_time_s`…`S5_time_s` after Playtest 1.

### Candle budget (model, not measured)

Passive drain is 0.5 per second, the flare adds 3.5 per second on top, a stub gives +35, and the candle caps at 100. For a player who walks the route above, takes every stub, flares for about 11 seconds in total (3 s at ghost A, 4 s at B, 2 s at C, 2 s in S5) and is never hit:

| Point | Candle |
|---|---|
| End of S1 | about 84% |
| End of S2 | about 86% |
| S3, before the stub (after two flares) | about 34% |
| End of S3 | about 48% |
| S4, before the stub by plate one | about 21% (low-candle warning) |
| End of S4 | about 24% |
| S5, after the entry stub | about 57% |
| Farmhouse door | **about 35%** |

Each extra second of flare costs 3.5, each ghost hit 20. A player who skips the maze stub arrives with about 5%, or runs out and respawns at `S5_Checkpoint_Mid` with 60%.

## Known risks to watch in Playtest 1

1. **Two-plate gate has no prompt.** It is the remix, so it is deliberately unexplained. If testers stall for more than about 90 seconds, add a short TutorialTrigger at z ≈ 272 ("This gate needs both plates.").
2. **Candle runs out while solving a plate puzzle.** The model has players at 20–25% through the second half of S4. Look for `extinguished,drained` events in S4. Fix: move the stub, or add one in the two-plate yard.
3. **The maze stub is nearly required.** Players who skip the dead end may run dry at the last bridge. Look at `final_candle_pct` and S5 deaths with cause `drained`.
4. **Candy rolling off a plate.** Thrown candy bounces. G (drop at feet) is the reliable way to put candy on a plate, but the candy prompt only mentions it briefly.
5. **Jumps on the last bridge.** The 0.8 m gaps are generous for a walking jump (1.6 m) and each run is long enough for a sprinting jump (4.3 m), but this is the first time platforms need a jump. Look for S5 deaths with cause `fell`.
6. **Flare-erases-bridge is discovered, not taught.** The trench is shallow so the discovery is cheap, but a tester may not connect the two. Listen for it in the think-aloud.
7. **Ghost B can be sprinted past** (a sprint is faster than a chase). That is fine, but it skips the flare lesson; note who does it.
8. **Line of sight is new.** Ghosts that do not notice a player behind a gravestone may read as broken or as clever. Ask.
9. **Brightness and readability.** Lighting was set without looking at it (ambient and moon are a little brighter than the sandbox). Check that the colour code reads, and that spirit platforms are still clearly invisible in the dark.
10. **Greybox scale.** Corridors, the 28 m yards and the 3 m walls have not been seen through the game camera. Watch for camera clipping in the 3.2 m corridor and the maze.
11. **Checkpoint triggers are large** (up to 4 m radius) so they cannot be walked past. A checkpoint may light a moment before the player reaches the lantern.
