# Lantern Keeper: working notes

CSCI 5999B, Project 2 (graduate). Unity 6 (6000.6.3f1), URP, C#. Run instructions and controls are in `README.md`.

## Status (2026-10-11, Step 4: greybox level + Playtest 1 build)

- The full level is playable start to finish as a greybox: `Assets/_Project/Scenes/Level01_PumpkinPatch.unity`, five sections, 10 ghosts, 5 spirit bridges, 2 plate gates, 8 checkpoints.
- The level is generated once from an editor script and tuned by hand afterwards; a read-only validator checks ghost routes, plates, checkpoints and the walking route, and prints a pacing estimate (6.9 minutes at walking speed).
- A playtest logger writes two CSV files per session (summary and timestamped events) for the three planned playtests.
- A one-click editor script makes the zipped Windows playtest build.
- Next: run Playtest 1, record findings in `docs/ITERATIONS.md`, then Step 5 (art, audio, animation, iteration).

Why it matters: this is the first build other people can play, and the first one that produces evidence (timings, deaths per section, causes of death) for the iteration history the rubric asks for.

## Resume-bullet drafts

- Built a 3D light-puzzle platformer in Unity 6 (C#, URP): finite-state-machine ghost AI on NavMesh, light-revealed platforms, pressure-plate puzzles, and a checkpoint and respawn system.
- Wrote editor tooling that generates a full greybox level from code, validates AI patrol routes and puzzle wiring, and produces a zipped release build from the command line.
- Added in-game playtest telemetry that logs per-section time, deaths by cause and ability use to CSV, to drive design iteration from tester data.
