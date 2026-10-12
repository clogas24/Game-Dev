# Lantern Keeper: working notes

CSCI 5999B, Project 2 (graduate). Unity 6 (6000.6.3f1), URP, C#. Run instructions and controls are in `README.md`.

## Status (2026-10-11, Step 4: greybox level + Playtest 1 build)

- The full level is playable start to finish as a greybox: `Assets/_Project/Scenes/Level01_PumpkinPatch.unity`, five sections, 10 ghosts, 5 spirit bridges, 2 plate gates, 8 checkpoints.
- The level is generated once from an editor script and tuned by hand afterwards; a read-only validator checks ghost routes, plates, checkpoints and the walking route, and prints a pacing estimate (6.9 minutes at walking speed).
- A playtest logger writes two CSV files per session (summary and timestamped events) for the three planned playtests.
- A one-click editor script makes the zipped Windows playtest build.
- Next: run Playtest 1, record findings in `docs/ITERATIONS.md`, then Step 5 (art, audio, animation, iteration).

Why it matters: this is the first build other people can play, and the first one that produces evidence (timings, deaths per section, causes of death) for the iteration history the rubric asks for.

## Status (2026-10-11, Step 5: polish build 0.2.0)

- Art, animation, audio and story are in, with no change to how the level plays: the read-only validator reports the same route, ghost and puzzle numbers as the greybox.
- The level's look is one root object rebuilt by a menu item, so the hand-tuned gameplay objects are never regenerated; greybox blocks keep their colliders and are only hidden behind models.
- Ghosts have an Animator Controller and six clips authored in Unity by an editor script, with states and transitions that mirror the ghost FSM one for one.
- Every event has a sound through an AudioMixer (Music, SFX, Ambience), and the music crossfades into a chase layer while a ghost is after the player.
- All assets are CC0 (Kenney), OFL fonts, or original (meshes built in code; sound and music synthesised by a script). `docs/CREDITS.md` lists every one.
- Checked headless: a Play-mode test of 65 checks (music, mixer, ghost animation against the FSM, notes, plates, win), frames rendered from the game camera in every section, and every menu item run three times.
- Next: Playtest 1 on the 0.2 build, then the playtest-driven iterations.

Why it matters: the rubric's audio and graduate animation lines are now met, and the playtests can judge the real game instead of grey boxes.

## Resume-bullet drafts

- Built a 3D light-puzzle platformer in Unity 6 (C#, URP): finite-state-machine ghost AI on NavMesh, light-revealed platforms, pressure-plate puzzles, and a checkpoint and respawn system.
- Wrote editor tooling that generates a full greybox level from code, validates AI patrol routes and puzzle wiring, and produces a zipped release build from the command line.
- Added in-game playtest telemetry that logs per-section time, deaths by cause and ability use to CSV, to drive design iteration from tester data.
- Authored character animation procedurally in Unity: an Animator Controller generated from the enemy FSM with blend-shape and transform clips written by an editor script, and humanoid hand IK for the player's prop.
- Built a repeatable art pipeline: one menu command dresses a hand-tuned level from CC0 kits and code-generated meshes without touching its colliders or NavMesh, verified by a headless Play-mode test suite.
