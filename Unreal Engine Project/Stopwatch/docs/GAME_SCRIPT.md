# Stopwatch - Game Script

Carson Logas, CSCI 5999B, Project 3 (theme: Time)

This is the rules and core mechanics of the game written out so that someone could understand how it plays without the computer. The short pitch is in the High Concept Document.

## Concept

Stopwatch is a 3D first-person puzzle-platformer made in Unreal Engine 5.8 for the Time theme. You are a clockmaker's apprentice climbing a collapsing clock tower. You aim at brass clockwork and either Freeze it (stop it for 5 seconds) or Rewind it (send it back through its past). Both cost Time Sand from one shared meter, and hourglasses refill it. Five floors teach each power, use it, then mix the two. The last floor is a 120-second race to the top. There is no combat.

## Expected player

Players about age 12 and up who know basic first-person controls and like puzzle games where you work out a trick. Not players looking for fast action. They play alone, with a keyboard and mouse or a gamepad.

## Background information

Story: the clock tower was built by your master, a clockmaker, over a whole lifetime, and now it is falling apart. You are the apprentice, carrying your master's stopwatch, which can stop or rewind clockwork as long as it has sand. The title screen gives the premise, each floor shows its name when you enter it, and the win screen says "The clock strikes. You made it to the top."

Things you need to know: brass objects can be frozen and rewound, red objects kill, green pedestals are checkpoints, glowing amber cylinders are hourglasses, and red discs on the floor are pressure plates.

To play: download the zip from Box, unzip it, and run Stopwatch.exe (Windows 10 or 11, 64-bit). If Windows warns about it, click More info, then Run anyway. Any key starts the game.

### Controls

- Move: W A S D (gamepad: left stick)
- Look and aim: mouse (right stick)
- Jump: Space (A)
- Freeze: left mouse button (right trigger)
- Rewind: right mouse button (left trigger)
- Pause: Esc or P (Start)
- Mouse sensitivity: Tab
- Play again from the win screen: R (A)

## What the game is teaching

The player is learning four things.

- **Managing one resource.** Both powers come out of the same 100 sand, and it never refills on its own. Later floors need more than one full meter (the Belfry needs about 175), so you must pick up hourglasses before the expensive puzzles.
- **Timing and prediction.** Clockwork moves in fixed patterns. You learn to watch, then freeze at the right moment, like catching a crate as it falls level with the floor.
- **Thinking backwards.** Rewind makes you ask "what happened here, and what if I undo it?" A pressure plate rolls a boulder into a doorway, and you rewind the boulder back up its ramp.
- **Choosing the right tool.** At the end of the Belfry you can freeze a shaking platform for 25 sand, or let it fall and rewind it for 35.

The game teaches by making you do the thing. Each new power gets a short numbered prompt with your real keys, and then you are put somewhere that power is the answer. After that the prompts stop and each ability comes back in new ways:

- **Jumping.** Taught: two blocks and a trench, where a miss only costs a jump back out. New: jumping onto clockwork you froze in mid-air.
- **Freeze.** Taught: a harmless pendulum over a beam, then a red blade too fast to dodge. New: freezing a sliding platform when it lines up, a falling crate as a step, and a bridge so it lasts long enough to cross.
- **Rewind.** Taught: rebuilding a collapsed bridge. New: making a platform fall on purpose and rewinding it, rewinding a boulder that jammed a door, and rewinding a block then freezing it halfway as a stair.
- **Sand.** Taught: the first floor has plenty. New: later puzzles cost more than one full meter, so where you refill matters.
- **Checkpoints.** Taught: one at the start of each floor. New: in the Collapse, dying sends you back but the clock keeps running.

## Rules

I split these the way we did in class: operational rules (what the player needs to know), constitutive rules (the logic and numbers underneath) and implicit rules (the unwritten ones).

### Operational rules

- Reach the clock face at the top of the tower.
- Aim at brass clockwork. Freeze stops it for a few seconds. Rewind sends it back in time. Both cost sand, and hourglasses give more.
- Falling into a pit, getting hit by a crusher or a red blade, or running out of time sends you back to the last checkpoint. You cannot lose for good.
- Walking past a green pedestal makes it your checkpoint.
- On the last floor, reach the top before the 120-second timer runs out.

### Constitutive rules

- Sand goes from 0 to 100 and starts full. It never goes up on its own. Freeze costs 25 and Rewind costs 35. Sand is only spent if the power actually works. If you can't afford it, nothing happens.
- Both powers reach 30 m.
- A frozen object stops for 5 seconds but stays solid, so you can stand on it or walk under it. Freezing it again costs 25 more and restarts the 5 seconds.
- Moving objects (platforms, crates, the boulder) rewind by replaying their last 8 seconds of movement in reverse. A fallen platform stays up after it is rewound.
- Broken bridges rebuild in 2 seconds. One stays up forever, the Belfry bridge lasts 3 seconds and the Collapse bridge 2, then they crumble. Freezing a rebuilt bridge pauses its countdown.
- An hourglass gives 35, up to 100, and comes back 8 seconds after you take it. If you are full, it waits for you.
- Pits kill 2.5 m below the walkway. Crushers and red blades kill on touch. The brass pendulum on the first floor only knocks you off.
- On a death the screen fades for about 1.5 seconds. You return to the last checkpoint, that floor's puzzles reset, and your sand is topped up to at least 60 (100 on the first floor).
- The Collapse timer starts at 120 seconds and stops while paused. A death from a fall or hazard keeps the time left. If it reaches 0, you restart at your checkpoint with a full 120.
- You walk at 5 m/s and jump about 1.3 m high and 5 m far.
- Nothing is random. The only small variation is physics: the boulder and crates can bounce slightly differently.

### Implicit rules

- You stay inside the tower. Rails line every walkway, so the only way to fall is into a pit.
- A frozen object holds your weight, and normal physics apply everywhere else.
- You are expected to keep climbing. Only the last floor forces you to hurry.
- Pausing really stops everything.

## Game modes

A gameplay mode is the set of things you can do at one time plus the screen that goes with it. These are all the modes in the game.

- **Playing (main mode).** Almost all the time is spent here. You can move, jump, look, aim, Freeze and Rewind. The screen shows the dot, the sand meter and a controls hint. Some moments are about timing, others are puzzles, but the actions don't change.
- **Collapse.** The same actions as Playing, plus the 120-second countdown and a camera shake that speeds up below 20 seconds. It starts on the last floor.
- **Respawning.** You died. No control for about 1.5 seconds while the screen fades out and in, showing the cause. Then you are back at your checkpoint.
- **Title screen (shell).** The title, the premise and the controls. Any key starts.
- **Pause menu (shell).** Esc, P or Start. Resume, Restart or Quit. Time is stopped.
- **Settings (shell).** Tab during play. A mouse sensitivity slider. Time is stopped.
- **Win screen (shell).** Reaching the clock face. Shows your time, deaths, sand spent, freezes and rewinds. R or A plays again.

## Levels

One clock tower with five floors joined by ramps. There are 15 checkpoints and 23 hourglasses, so a death never costs more than about 45 seconds.

**The Workshop (teach).** Hop up two blocks and over a trench. Freeze a pendulum at the side of its swing to cross a beam, then freeze a red blade to get through a doorway.

**The Gear Hall (apply).** No prompts. Run under two crushers or freeze one, freeze a sliding platform when it lines up with the ledges, then get past two spinning blades.

**The Broken Bridge (teach and apply Rewind).** Rewind a collapsed bridge. Make a platform fall, then rewind it. A pressure plate rolls a boulder into the door, so rewind it back up the ramp.

**The Belfry (remix).** Less sand, harder combinations. Freeze a falling crate as a step. Rewind a block and freeze it as a stair. Rewind a bridge that only lasts 3 seconds and freeze it halfway. Then choose: freeze the last platform or rewind it.

**The Collapse (everything, timed).** A 120-second clock. A lift, two crushers, a falling crate, a fallen platform, and a 2-second bridge you must rewind and freeze, up to the clock face.

## Game mechanics (The Art of Game Design)

These are the mechanics from slide deck 4 and how the game fits each one.

**Space.** A continuous 3D tower, but each floor is really one 13 m wide path with rails. The simple space keeps the attention on the clockwork. Gaps are set against the jump distance, so some can only be crossed on something you froze.

**Time.** Real time, and time is the main mechanic: the player stops it and reverses it for one object at a time. Hazards repeat on fixed cycles, and the Collapse adds a race against the clock.

**Objects, attributes and states.**
- The player has sand (0 to 100) and a checkpoint.
- Each clockwork object has attributes (can it be frozen or rewound, how long it holds) and one state: Normal, Frozen, Rewinding, Held or Crumbling.
- Hourglasses are available or taken, and checkpoints are lit or unlit.

**Actions.**
- *Operative* (the basic actions): move, jump, aim, Freeze, Rewind.
- *Resultant* (strategies built from them): freezing a crusher to walk under it, freezing a crate as a step, making a platform fall and rewinding it, rewinding a block and freezing it as a stair, and timing a hazard for free instead of spending sand.

**Rules.** The operational, constitutive and implicit rules above. The goal is the clock face.

**Skill.** Mostly mental: planning sand, predicting movement, and thinking backwards. Some physical: aiming and timing a freeze.

**Chance.** Almost none, on purpose. Hazards move the same way every time, so players can learn them, and a failure feels like their own mistake, not bad luck. The only variation is physics bounce on the boulder and crates. Walls on the boulder ramp keep that from ever breaking the puzzle.

## Balance lenses

**Lens of Challenge.** It asks: what are the challenges? Are they too easy, too hard, or just right? Can they handle a range of skill levels? How does the challenge grow? Is there enough variety? What is the hardest part?
- The challenges are timing clockwork, working out Rewind, and managing sand.
- To keep it just right, every timed moment gives at least 0.6 seconds and checkpoints keep retries short.
- Weaker players get sand refills on death and can freeze hazards instead of timing them. Stronger players can skip those freezes.
- The challenge grows one step per floor: Freeze, timing, Rewind, combos with less sand, then a timer.
- Six kinds of clockwork, each power used at least three ways.
- The hardest part is the last bridge: rewind and freeze it while the timer runs.

**Lens of Meaningful Choices.** It asks: what choices does the player make? Are they meaningful? Is it the right number? Would more feel more powerful, or fewer be clearer? Is there a dominant strategy?
- The choices are Freeze or Rewind, spend sand or time a hazard for free, when to freeze, and whether to grab an hourglass or keep moving.
- They matter because both powers share one meter: sand spent now is gone for the next puzzle.
- Two powers is right for an 8-minute game, and most puzzles still have two ways through. A third power would blur the clear difference between stopping and reversing.
- Freeze is cheaper and could take over. Rewind stays necessary because some puzzles can only be solved with it. The play logs count freezes and rewinds, so playtests will show if Freeze takes over.

## Animation and audio

There is no character animation, since the game is first person. All movement is done in code or physics:
- pendulums swing, gears spin, crushers slam and platforms shake and fall;
- bridge planks fly back into place;
- the boulder and crates are physics objects.

Frozen and rewinding objects change color, hourglasses spin, checkpoints light up, and the screen fades on death and shakes in the Collapse. There is no audio yet, so nothing important depends on sound.

Nothing is copyrighted. The project uses Unreal Engine 5.8, Epic's Third Person template, and the engine's basic shapes, all under the Unreal Engine EULA. The code, level, materials and level-building scripts were made for this project.

## Iterations and what changed

Each version is a pull request on GitHub. The builds record play data (time per floor, deaths by cause, freezes, rewinds and sand), and that is what I look at between versions.

**Version 1: setup (PRs #18 to #20).** The first setup got messy, so I deleted it and remade Stopwatch as a clean C++ project.

**Version 2: Freeze and Time Sand (PR #21).** Freeze, the sand meter, the HUD and hourglasses, in a test room.

**Version 3: first person and Rewind (PR #22).** The camera moved to first person with a dot crosshair, so you aim where you look. Rewind was added, plus a test arena with one station per idea.

**Version 4: arena fixes (PR #23).** Playing the arena showed triggers and plates you could not see, targets out of view of their prompts, and long walks back between stations. Every trigger became visible, every target was placed in view, return pads were added, clockwork got its own brass material, and a controls hint was added.

**Version 5: the clock tower (PR #24).** The arena lessons became the real level: visible triggers, numbered prompts, hourglasses before every expensive puzzle, and timing windows of at least 0.6 seconds. Smoke testing then led to four changes:
- the tester ID was removed from the logs;
- the boulder ramp got walls so the boulder can't fall off;
- the Collapse timer now keeps your time when you die from a hazard;
- the tutorial text was shortened.

This is the Playtest 1 build (tag p3-v0.1-playtest1, commit 9619003).

**Version 6: release.** The Windows build on Box.

**Playtest versions (to be added).** Changes from Playtest 1 and Playtest 2, with the play data behind each (tags p3-v0.2-playtest2 and p3-v0.3-playtest3).
