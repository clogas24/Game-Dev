# Playtest data

`PlaytestLogger` (in `Assets/_Project/Scripts/Core`) records every run of the level. It works in release builds; it does not need the F1 debug overlay.

## Where the files are

`Application.persistentDataPath/playtests/`, which on Windows is:

```
C:\Users\<user>\AppData\LocalLow\Carson Logas\Lantern Keeper\playtests\
```

(`~/Library/Application Support/Carson Logas/Lantern Keeper/playtests/` on macOS, `~/.config/unity3d/Carson Logas/Lantern Keeper/playtests/` on Linux.)

The game itself does not show this folder or offer a way to open it: open it from the file browser.

## Sessions and file names

A **session** is one run of the level: from leaving the title screen to a win, a quit, or a restart. Each session writes two files:

| File | Contents |
|---|---|
| `<session>_summary.csv` | A header row and one data row of totals |
| `<session>_events.csv` | A timestamped log, one row per event |

`<session>` is a tester id the game generates the first time play starts after launching: `T-<yyyyMMdd>-<4 characters>`, for example `T-20261009-K7QD`. Nothing is typed in. The pause and win screens show it next to the folder path, so it can be copied onto the observation sheet.

A restart (Pause > Restart, or Play again) in the same launch keeps the id and adds a number: `T-20261009-K7QD`, `T-20261009-K7QD_2`, `T-20261009-K7QD_3`, so nothing is ever overwritten. The `tester_id` column stays the same across those files; the `session` column is the file name prefix. Closing the game and launching it again gives a new id, so **relaunch the game for each new tester**.

## When the files are written

| Moment | What happens |
|---|---|
| Leaving the title screen | Both files are created; the summary already holds a row |
| Every 5 seconds, and at each death and section change | The events file is flushed to disk |
| Pause | Summary rewritten |
| Win | Summary rewritten with `completed = yes` |
| Pause > Quit | `quit` event, summary rewritten, events file closed |
| Closing the window, Alt+F4 (`OnApplicationQuit`) | Same |
| Restart | `restart` event, summary rewritten, events file closed; the new run is a new session |

An unfinished run therefore always leaves both files. If the game is killed from Task Manager, the events file is at most 5 seconds behind and the summary is as of the last pause.

## Time

All times are **play time** in seconds: `GameManager.PlayTime`, which runs while playing and while the lantern is out, and stops on the title, pause and win screens. Time spent reading the title or paused is not counted.

## Summary columns

| Column | Meaning | How I use it |
|---|---|---|
| `tester_id` | Generated id, one per launch of the game | Match the row to the observation sheet and survey |
| `session` | File name prefix (`tester_id`, or `tester_id_2`…) | A `_2` means this tester restarted: read the earlier session too |
| `date`, `time_started` | Local date and time the session began | Order sessions; match to notes |
| `build_version` | `Application.version` (0.1.0 for Playtest 1, 0.2.0 for the Step 5 polish build) | Keep playtests of different builds apart. Bump the version for each tagged build |
| `completed` | `yes` if the farmhouse door was reached | Completion rate. Fewer than about 2 in 3 finishing means the level is too hard or too unclear somewhere |
| `end_reason` | `completed`, `pause_menu_quit`, `application_quit`, `restart`, or `in_progress` (written at a pause; if it is still there, the game was killed) | Why a run stopped. A quit in the middle of a section is a strong signal |
| `last_section` | Id of the last section entered | Where an unfinished run stopped. A cluster of quits in one section is the first thing to fix |
| `total_time_s` | Play time, all sections | Compare with the 6–9 minute target (360–540 s). Under 5 minutes: too easy or too short. Over 10: find the section that ate the time |
| `S1_time_s` … `S5_time_s` | Play time between entering that section and entering the next | Compare with the estimates in `LEVEL_DESIGN.md` (50, 58, 83, 119, 103 s). A section at double its estimate is where players are stuck |
| `S1_deaths` … `S5_deaths` | Times the lantern went out in that section | **High S2 deaths = bridge readability problem.** High S3 = ghosts or flare not understood. High S4 = candle running out during puzzles. High S5 = finale too punishing, or the jump bridge |
| `S1_ghost_hits` … `S5_ghost_hits` | Ghost hits taken in that section (20 candle each) | Hits without deaths are healthy tension. Many hits in S4, where ghosts should be tools, mean the patrols are too close to the player's route |
| `deaths_total`, `ghost_hits_total` | Sums of the above | Overall difficulty. The target is 1–4 deaths for a first run |
| `flare_count` | Number of times the flare was started | 0 by the end of S3 means the flare lesson failed. Very high means the flare is being used as the answer to everything |
| `flare_seconds` | Total seconds the flare was held | Divide by `flare_count`: long flares (over 3 s each) mean players hold it and do not see the cost; the candle budget assumes about 11 s in total |
| `candy_thrown` | Candies thrown or dropped | 0 past S4 is impossible without flaring through everything. Over about 15 means throws are missing: aiming or candy rolling is a problem |
| `candle_pickups` | Candle stubs collected (6 in the level) | Under 4 means stubs are being missed: move them closer to the path or make them brighter |
| `final_candle_pct` | Candle left when the session ended | For finished runs the design target is roughly 20–40%. Mostly above 60: remove a stub. Mostly near 0, or many `drained` deaths in S5: add one |
| `chases` | Times any ghost entered Chase | Tension. Near 0: ghosts are too easy to avoid. Dozens: the player cannot get away, or is not trying to |
| `pauses` | Times the game was paused | Several pauses in a row usually means looking for controls or changing sensitivity: ask in the survey |
| `notes_read` | Times a story note was opened (5 notes in the level; opening one twice counts twice). Added in 0.2.0, as the last column | Whether the story is being found at all. 0 across most testers: the signposts are not noticed, or the prompt is not understood |

A section's time, deaths and hits go to the **last section entered**. If a player dies early in S3 and respawns at the S2 checkpoint, the time spent crossing the S2 bridge again counts as S3. That is deliberate: it is time S3 cost them.

## Events file

Columns: `time_s`, `section`, `event`, `detail`.

- `time_s`: play time when it happened.
- `section`: the last section entered (empty before S1).

| Event | Detail | How I use it |
|---|---|---|
| `session_start` | `tester=…;version=…;scene=…` | Start marker |
| `section_entered` | Section id | Section boundaries for everything below |
| `checkpoint_reached` | Checkpoint name | Which porch lanterns were lit. A missing one was walked past: move it or widen its trigger |
| `extinguished` | Cause: `drained` (candle ran down), `flared_out` (ran out while flaring), `ghost_hit` (a hit took the last of it), `fell` (below the fall height) | The most useful row in the file. `fell` in S2 or S5: bridges. `flared_out`: the flare cost is not understood. `drained` in S4: puzzles take longer than the candle allows. `ghost_hit` in S5: finale too hard at low candle |
| `respawned` | Checkpoint name, or `start` | How far back a death sent the player |
| `ghost_hit` | Ghost name | Which ghost is doing the damage. One ghost with most of the hits has a patrol that is too tight |
| `flare_start`, `flare_stop` | `seconds=…` on stop | Where flares happen. Flares in S2, before the tutorial, mean the title screen controls were read. Flares on a bridge followed by `fell` confirm the S3 remix landed (or hurt) |
| `candy_thrown` | – | Where candy is used; count between `plate` events to see how many throws a puzzle took |
| `candle_pickup`, `candy_pickup` | Pickup name | Which stubs are found and which are missed |
| `plate_pressed`, `plate_released` | Plate name | Puzzle attempts. Many press/release pairs with no `gate_opened`: the player is stepping on and off, not yet thinking of ghosts |
| `gate_opened`, `gate_closed` | Gate name | Time from `section_entered` (or the checkpoint) to `gate_opened` is the puzzle's solve time. `gate_opened` then `gate_closed` then `gate_opened` on gate one means the player did not get through the first time |
| `ghost_notice`, `ghost_chase` | Ghost name | Where the player is seen. Notice without chase means they backed off in time: good readability |
| `tutorial_shown` | Tutorial id (`T1_MoveJumpSprint` … `T6_Plate`) | Check the gap between a prompt and the first use of that ability |
| `note_read` | Note id (`Note1_Lanterns`, `Note2_SpiritBridge`, `Note3_Farmhands`, `Note4_Candy`, `Note5_Home`) | Which notes are found. The candle is paused while a note is open, so a long gap after this row is reading time, not a stall |
| `paused`, `resumed` | – | Where players stop |
| `level_completed` | `time_s=…;candle_pct=…` | End marker for a finished run |
| `quit` | `pause_menu_quit` or `application_quit` | End marker for a quit |
| `restart` | `restart` | The tester restarted; the next session continues the story |

Not logged, because nothing in the game raises an event for it: candy eaten, ghost Repelled/Distracted/Return states, and each jump.

## Reading the data

Both files are plain CSV with a header row and open in Excel or Google Sheets. To compare testers, paste every `_summary.csv` data row under one header row. Fields that contain a comma are quoted.

After each playtest, copy the folder into the repository's evidence (for example `docs/playtests/playtest1/`) before the next build, and put the findings into `docs/ITERATIONS.md` using the table in `docs/PLAYTEST_KIT.md`.
