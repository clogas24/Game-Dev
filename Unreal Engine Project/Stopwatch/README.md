# Stopwatch

You're a clockmaker's apprentice. Freeze and rewind clockwork to climb the collapsing tower.

Stopwatch is a first-person puzzle-platformer made in Unreal Engine 5.8 (C++). Aim at brass clockwork and **Freeze** it, stopping it in place for 5 seconds, or **Rewind** it, sending it back through its past: a fallen bridge rebuilds itself and a dropped platform rises again. Both cost Time Sand, which hourglasses refill. Climb five sections of a clock tower, and finally race a 120-second collapse to the clock face at the top.

## Controls

| Action | Keyboard and mouse | Gamepad |
|---|---|---|
| Move | WASD | Left stick |
| Look | Mouse | Right stick |
| Jump | Space | A |
| Freeze | Left mouse button | Right trigger |
| Rewind | Right mouse button | Left trigger |
| Pause | Esc or P | Start |
| Settings (mouse sensitivity) | Tab | |
| Play again (win screen) | R | A |

On the title screen, any key or Start begins the game.

Development builds also have these debug keys:
- **F1**: overlay with FPS, sand, game mode, section, checkpoint and the aimed target.
- **F5**: teleport to the next checkpoint.
- **F6**: refill sand.
- **V**: toggle the third-person view.

## Running the game

_Placeholder: instructions for the final Windows build go here._

## Playtest logs

Every run writes a CSV, flushed one row at a time, to:

```
<project or build folder>/Saved/Playtests/Playtest_<timestamp>.csv
```

For a packaged build, the folder is under the build's own `Saved` folder. The pause and win screens show the exact path. Each CSV records:
- the build version and date;
- one row per section as it ends, per death, and when the run ends (quit, restart or win);
- a final summary row.

Each row counts time, deaths by cause, freezes, rewinds, failed spends, sand spent, sand picked up, and whether the run was completed.

## Platform

The instructor approved Windows-only builds for this project.

## For developers

- Levels are built by scripts: `Scripts/build_level.py` builds `Lvl_ClockTower` (the game's start map) and `Scripts/build_test_arena.py` builds the `Lvl_TimeTest` arena. Run them headless with the editor closed:
  ```
  UnrealEditor-Cmd.exe "<path>\Stopwatch.uproject" -run=pythonscript -script="<path>\Scripts\build_level.py" -unattended -nullrhi -nosplash -nosound
  ```
- Tests: `Automation RunTests Stopwatch` in the editor, or in `-game` on `Lvl_TimeTest` (`Stopwatch.Arena`) or `Lvl_ClockTower` (`Stopwatch.ClockTower`).
- Design notes: `docs/LEVEL_DESIGN.md`, `docs/FSM.md`, `docs/ITERATIONS.md`.

## Credits

_Placeholder._
