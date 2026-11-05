# Lvl_ClockTower: level design

Built by `Scripts/build_level.py`. Rebuild it after changing the script (see the README). Every number below is from that script. Positions are given as **u**, the distance along the section's path from its start, in Unreal units (cm). The character jumps 127 high, about 510 far at a run, and walks 500 units a second.

## Side view

Five stories, 1500 apart, stacked in one tower. The odd sections run east (+X), the even ones west. A stairwell of two 30° ramps at each section's end climbs to the next. The Collapse climbs a further 1400 on its own to the clock.

```
 z                                                                        [CLOCK FACE]
 7400 |                                              ____bridge(2s)____[GOAL]   |
      |                                    ramp____/                            |
 6900 |                     ___chute___plat/ (5) THE COLLAPSE  ->  120 s countdown
      |      lift  _crushers_/ramp
 6000 |  [CP]__/                                                          stair |
      |  stair|                                                                 |
 4740 |       |__plat(choice)__bridge(3s)__ledge                                |
 4500 |       (4) THE BELFRY  <-                   \_block_step__chute__[CP]____|
      |                                                                   stair |
 3000 |  [CP]___bridge(rewind)___falling plat___boulder ramp > door___________  |
      |  (3) THE BROKEN BRIDGE  ->                                              |
      |  stair|                                                                 |
 1500 |       |__blades__sliding plat(freeze)__crushers__________________[CP]___|
      |       (2) THE GEAR HALL  <-                                       stair |
    0 |  [PS][CP]_jumps_trench_beam+pendulum_[CP]_blade gate__________________  |
      |  (1) THE WORKSHOP  ->
 -450 |       pit floors sit 450 below each walkway; a fall dies 250 below it
      +---------------------------------------------------------------------- x
         x = 0                                                    x = 6000
```

All five sections share the same rules: red clockwork kills, brass clockwork can be frozen and rewound, green pedestals are checkpoints, and the glowing amber cylinders are hourglasses (+35 sand). Every hourglass respawns 8 s after it's taken. Rails line every walkway, so the only way to fall is into a pit.

| # | Section | Teaches / applies / remixes | First-time estimate | Checkpoints | Hourglasses |
|---|---|---|---|---|---|
| 1 | The Workshop | teaches move, jump, Freeze | 1:30 | 2 | 4 |
| 2 | The Gear Hall | applies Freeze to timing | 1:20 | 3 | 4 |
| 3 | The Broken Bridge | teaches Rewind, then applies it twice | 2:00 | 3 | 5 |
| 4 | The Belfry | remixes Freeze and Rewind, less sand | 1:50 | 4 | 5 |
| 5 | The Collapse | everything, against 120 s | 1:30 | 3 | 5 |
| | **Total** | | **about 8 minutes** | 15 | 23 |

The estimates are judgment, not measurement: walking time from the path lengths, plus a guess at reading, aiming and one or two retries per challenge. The playtest logger records the real time per section.

---

## 1. The Workshop (z 0, runs +X, 6000 long)

**Goal:** learn to move, jump and Freeze where failing is safe, then meet a hazard that can only be passed with Freeze.

| u | What | Why |
|---|---|---|
| 0–1100 | start landing, PlayerStart at 150 | |
| 1100–1700 | two blocks up 80 each, then down | jumping, taught by the layout |
| 1900–2150 | trench 250 wide, 100 deep | a missed jump costs a jump back out, not a death |
| 2380 | **tutorial**: freeze the pendulum as it swings out to the side, then walk the beam | Freeze, taught on something harmless |
| 2500–3300 | 200-wide beam, pendulum swinging across it (period 2 s); safe ledge 200 below, steps back up | knocked off = walk back, not die |
| 3900 | **tutorial**: the red blade is too fast to dodge, freeze it | |
| 4180–4400 | blade gate: a lethal pendulum (period 1 s) whose kill zone covers the whole doorway on every swing | Freeze applied as taught, and required |

The beam pendulum is the one place where *when* you freeze matters: frozen in the middle of its swing, its bob blocks the beam. The tutorial says so.

**Checkpoints** (min sand 100, so sand is plentiful here)
- u 400: section start.
- u 3500: after the beam, before the gate. A gate death costs about 10 s.

**Hourglasses**
- u 2350: before the beam, for retrying the freeze.
- u 3450: after the beam.
- u 3950: before the gate.
- u 4700: after the gate, to arrive at the Gear Hall full.

## 2. The Gear Hall (z 1500, runs −X, 6000 long)

**Goal:** apply Freeze to timing: wait for an opening, or make one.

| u | What | Why |
|---|---|---|
| 800–2600 | crusher corridor, 2 crushers (cycle 3.3 s, up for 1.5 s), walls on both sides | run under in the gap, or freeze one up |
| 2600–4200 | pit with narrow piers (300 wide) at both ends; a 600×400 platform slides across it (2.5 s per pass) | freeze it lined up with the piers. Each jump is 300; the alignment window is about 1.1 s |
| 4550–5650 | narrowed hall, 2 spinning blade arms (radius 300) at height 60 | jump the arms or freeze them |

**Checkpoints** (min sand 60)
- u 400: section start.
- u 2650: after the crushers, on the near pier.
- u 4350: after the platform, before the blades.

**Hourglasses** (the first sand pressure, so there's one after each hard part)
- u 600: start.
- u 2550: after the crushers.
- u 4150: on the far pier, after the platform.
- u 5800: after the blades.

## 3. The Broken Bridge (z 3000, runs +X, 6000 long)

**Goal:** learn Rewind on something that stays fixed, then use it on things that fall.

| u | What | Why |
|---|---|---|
| 800 | **tutorial**: aim at the broken planks in the pit, press Rewind | Rewind, taught |
| 1000–2300 | pit; bridge planks lie in view on the pit floor; HoldDuration 0, so once rebuilt it stays | |
| 2650 | **tutorial**: step on, jump back, rewind it, cross | |
| 3000–4100 | falling platform (shakes 1 s, then drops into the pit) | Rewind applied to a fallen object |
| 4100–4300 | open landing | |
| 4550–4850 | 240-wide pass with a red plate filling it | the plate can't be avoided |
| 4400–5200 | ramp beside the path; the plate rolls a 300-wide boulder down it | |
| 5500–5800 | a 40-deep socket, then a 260×230 doorway | the boulder jams in the door |
| 5450 | **tutorial**: rewind the boulder back up the ramp | Rewind applied to undo a trap |

**Checkpoints** (min sand 60)
- u 400: section start.
- u 2800: before the falling platform.
- u 4200: before the boulder.

**Hourglasses**
- u 600: start.
- u 2450: after the bridge.
- u 2850: before the falling platform.
- u 4250: before the boulder.
- u 5900: after the door.

## 4. The Belfry (z 4500, runs −X, 6600 long)

**Goal:** remix both abilities, with tighter sand that is always enough.

| u | What | Why |
|---|---|---|
| 900–1700 | pit, too wide to jump; a chute drops a crate every 3 s; crates drift down (damping 4) | freeze one as a step mid-air. The usable window is 0.71 s (measured) |
| 1950 | red plate; the block falls off beside a 480 tower | |
| 2100–2700 | step at 100, the block's landing spot, then a 240 ledge face | rewind the block (slowed to 0.1×), freeze it as it rises past the step, then climb. The window is 0.71 s (measured) |
| 3200–5000 | 1800 pit; the bridge holds 3 s but takes 3.6 s to cross | rewind it, then freeze it on the way |
| 5300–6000 | the choice: a falling platform (shakes 1.5 s) | freeze it while it shakes (25 sand, 5 s), or let it fall and rewind it (35 sand, stays up). Both work, and a sign says so |

**Checkpoints** (min sand 60)
- u 400: section start.
- u 1800: after the chute.
- u 2950: on the ledge, before the bridge.
- u 5150: before the choice.

**Hourglasses** (one before each costly challenge)
- u 700: before the chute.
- u 1800: before the block, which costs 60.
- u 3100: before the bridge, which costs 60.
- u 5150: before the choice.
- u 6300: at the end, to enter the finale with sand.

## 5. The Collapse (z 6000, runs +X, 10400 long, climbs to +1400)

**Goal:** everything, timed. Walking into the collapse trigger at u 700 starts 120 s. A fall or a hazard keeps the time left; only running out of time restarts it.

| u | What |
|---|---|
| 800–2000 | a lift rides diagonally up 400, across a pit |
| 2000–3400 | 2 crushers on the ledge |
| 3400–4266 | ramp up to 900 |
| 4600–5400 | chute gap (damped crates) |
| 5800–6900 | falling platform to rewind |
| 7200–8066 | ramp up to 1400 |
| 8400–9800 | bridge that holds 2 s; crossing takes 2.8 s, so it must be frozen |
| 10100 | **goal**, under a 1400-wide clock face |

**Checkpoints** (min sand 60)
- u 400: the finale start.
- u 4350: before the chute.
- u 7050: after the falling platform.

The mid-finale checkpoints keep a death under about 45 s of lost progress. The clock keeps running across deaths, so dying still costs time.

**Hourglasses**
- u 150: before the trigger.
- u 3300: after the crushers.
- u 4450: before the chute.
- u 5600: before the falling platform.
- u 8200: before the bridge.

## Lessons from the arena, and where they are applied

- **Every trigger is visible.** Plates are red discs and checkpoints are green pedestals. Section and tutorial triggers are invisible but only show text.
- **Every timeable target is in view and in reach from where the player stands.** `Stopwatch.ClockTower.Layout` traces from each tutorial trigger to its target.
- **Tutorials are numbered steps with the player's real keys**, through the `{freeze}` and `{rewind}` tokens.
- **Hourglasses before and after every costly challenge**, all respawning (8 s), so F6 is never needed.
- **Timing windows of at least 0.6 s**, measured by the Layout test.
- **A checkpoint at every section's start, and mid-section ones**, keep a death under about 45 s.
