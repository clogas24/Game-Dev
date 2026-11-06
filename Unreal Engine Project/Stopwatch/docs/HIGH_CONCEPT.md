# Stopwatch - High Concept Document

Carson Logas, CSCI 5999B, Project 3 (theme: Time)

## High concept

You are a clockmaker's apprentice climbing a clock tower that is falling apart. You carry your master's stopwatch, which can stop a piece of clockwork in place or wind it back to how it was, as long as there is sand in its glass. Freeze and rewind the clockwork to climb to the great clock face at the top before the tower collapses.

## Concept of the game

Stopwatch is a 3D first-person puzzle-platformer made in Unreal Engine 5.8. You aim a dot at brass clockwork and use one of two powers. **Freeze** stops it in place for 5 seconds, so a crusher can be walked under or a falling crate becomes a step. **Rewind** sends it back through its past, so a bridge that fell into a pit rebuilds itself and a platform that dropped rises again. Both powers cost Time Sand, and hourglasses refill it. The tower has five floors, and the last one is a 120-second race to the top.

The idea came from a "dream of doing" question: what if you could pause or undo one thing in the world? Everything is built around the fact that time is the tool and also the cost. There is no fighting. You never attack anything.

## Player's role

You are the apprentice with the stopwatch. What you actually do: walk, jump, aim at clockwork, freeze it, rewind it, watch your sand, pick up hourglasses, reach checkpoints, and race the clock on the last floor.

## Genre

Mostly a puzzle game (the main challenge is working out which power to use, on what, and when), mixed with platforming (jumping between ledges and onto frozen objects) and a timed action finale.

## Primary gameplay mode

- **Camera model:** first person. It is dynamic, meaning the player turns it with the mouse or the right stick. A dot in the middle of the screen is used to aim.
- **Interaction model:** avatar based. You control one character inside the world.
- **Challenges:** puzzles (which power, on which object), timing (freezing a moving object at the right moment), physical (jumps and aiming) and managing one resource (Time Sand).

The other modes are respawning after a death, the Collapse countdown, and the shell screens (title, pause, settings, win). They are all described in the Game Script.

## Expected player

Players about age 12 and up who already know how to move and look in a first-person game. They like puzzles where you work out a trick, like *Portal* or *Braid*, more than fast reflex games.

Using the five domains of play from class, this player likes:
- **Novelty:** a lot. Stopping and reversing single objects is a new way to solve a puzzle.
- **Challenge:** a medium amount. You can fail, but checkpoints are close and dying refills your sand.
- **Stimulation:** low. It is a calm game you play alone, until the timed finale.
- **Threat:** mild. Hazards kill you, but there is no horror and no enemy chasing you.
- **Harmony:** does not really apply, because it is single player.

I designed it player centric. I am not the typical player, so every power is taught with a short numbered prompt using your real keys, everything works on a keyboard and mouse or a gamepad, and mouse sensitivity can be changed.

## What the game is teaching

- **Managing one resource.** Both powers come out of the same 100 sand, and it never refills on its own.
- **Timing and prediction.** Clockwork moves in fixed patterns, so you learn to watch, then act at the right moment.
- **Thinking backwards.** Rewind makes you ask what happened to an object, and what undoing it would give you.
- **Choosing the right tool.** Freeze and Rewind can both solve some problems, at different costs.

The Game Script says where each ability is taught and where it gets reused in a new way.

## Machine and competition mode

Windows computers, with keyboard and mouse or a gamepad. It is single player. The player competes against the game, and against the clock on the last floor. The win screen shows your time, deaths and sand used, so you can also try to beat your own run.

## How the game goes from beginning to end

Title screen, then the first floor, the Workshop, which teaches jumping and Freeze. The Gear Hall uses Freeze for timing. The Broken Bridge teaches Rewind and uses it twice. The Belfry mixes both powers with less sand. The last floor, the Collapse, starts a 120-second countdown and uses everything to reach the clock face. The game ends at a win screen with your stats, and R plays again. A first play through should take about 8 minutes. A run by someone who already knew every solution took a little over 3 minutes.

## Game world and background

The world is the inside of a giant clock tower: stone walkways, brass pendulums, gears, crushing hammers, and a huge clock face at the top. The tower was built by your master, a clockmaker, over a whole lifetime. Now it is coming apart. Bridges have fallen into the shafts, platforms drop out from under you, and the machinery is running wild.

You are the apprentice. You climb floor by floor with your master's stopwatch. When you reach the top, the clock strikes and you have made it.

Things you need to know that the story does not say: brass objects can be frozen and rewound, red objects kill you, green pedestals are checkpoints, glowing amber cylinders are hourglasses, and red discs on the floor are pressure plates.
