# Playtest kit

Everything needed to run a Lantern Keeper playtest and turn it into rows in `docs/ITERATIONS.md`. Print one observation sheet and one survey per tester.

- Build: `Builds/Playtest/LanternKeeper_Win_v0.1.zip` (made by **Lantern Keeper > Build Playtest (Windows)**).
- Data: see `docs/PLAYTEST_DATA.md`. Design intent and estimates: `docs/LEVEL_DESIGN.md`.
- Aim for 3–5 testers per playtest who have not seen the game. One session takes about 20 minutes: 2 to set up, up to 12 to play, 5 for the survey.

## Before each tester

1. Unzip the build into a clean folder and run `LanternKeeper.exe`.
2. **Launch the game fresh for each tester.** Each launch gets its own id (`T-<date>-<4 characters>`), which names their CSV files.
3. Have a timer, the observation sheet and a pen ready. Sit where you can see the screen and the tester's hands.
4. If they are on a laptop trackpad on Windows, set **Settings > Bluetooth & devices > Touchpad > Touchpad sensitivity** to **Most sensitive** (see the README), or tell them the arrow keys turn the camera.

## Facilitator script

Read this out, more or less word for word:

> Thanks for helping. This is an early version of a short game: the art is just coloured blocks and there is no sound yet. I am testing the game, not you. Nothing you do is wrong, and anything confusing is my fault and exactly what I need to find.
>
> Please **think out loud** as you play: what you are trying to do, what you expect to happen, what surprises you.
>
> I am going to stay quiet and take notes. **I will not answer questions about how to play**, because I will not be there when other people play it. If you get stuck, just tell me what you are thinking.
>
> The controls are on the first screen. Play until you finish or until you want to stop; either is fine. Press any key when you are ready.

Rules for me while they play:

- **Do not help.** No hints, no pointing, no "try the other key". If they ask, say "What do you think?" or "What would you try?".
- **Only step in** if the game is broken (stuck in geometry, a crash), or after **3 minutes with no progress** in one place. Then give the smallest possible hint, and write down the hint and the time.
- **Prompt thinking aloud** when they go quiet for 20 seconds: "What are you thinking?" Never "Why did you do that?".
- **Write down exact quotes**, in quotation marks, with the section. One real sentence is worth more than my summary of it.
- **Note where they stop moving**, look around, re-read a prompt, open the pause menu, or sigh. Note the section and what was on screen.
- **Note what they try that the game does not support** (jumping onto a wall, throwing candy at a gate, flaring at a plate).
- Keep a neutral face. Do not react to deaths or to solutions.
- If they want to stop, let them. Ask them to use **Esc > Quit** so the data is saved (closing the window also saves it).

Afterwards: press Esc (or look at the win screen) and copy the id shown after "Playtest data" onto the observation sheet and the survey. Then hand over the survey before discussing anything. Then ask, "Is there anything you wanted to tell me that the survey did not ask?" Finally, open the playtest folder (Pause or win screen > **Open playtest folder**) and check that both CSV files for their id are there.

## Observation sheet

Tester id (from the pause or win screen): ____________  Date: ____________  Build tag: ____________  Input: mouse / trackpad / arrow keys

Finished? yes / no    Stopped at: ____________    Hints given (what, when): ________________________________

| Section | What to watch for | Seen? | Notes and quotes |
|---|---|---|---|
| **Title** | Reads the controls before starting? | | |
| **S1 Pumpkin Patch** | Jumps the crates and fence without trouble | | |
| | Uses sprint for the ditch (or falls in and climbs out) | | |
| | Notices the candle bar draining | | |
| | Takes the candle stub in the nook; understands what it did | | |
| | Turns the camera comfortably through the S-bend (mouse, trackpad or arrows?) | | |
| **S2 Spirit Bridge** | Sees the lamp-lit platform; understands platforms need light | | |
| | Falls in the creek or pond? Finds the ramp out? | | |
| | Follows the dogleg bridge; finds the spur and its stub | | |
| | Finds the start of the deep bridge; crosses without falling | | |
| **S3 Graveyard** | Watches ghost A before approaching? Uses gravestones as cover? | | |
| | Reads the flare prompt; first flare is deliberate or panicked | | |
| | Notices the flare burning candle | | |
| | Ghost B in the corridor: flares, sprints past, or gets hit | | |
| | Trench bridge: flares and loses the bridge? Says why it vanished? | | |
| **S4 Candy Porch** | Takes candy; throws (E) or drops (G); aims with the camera | | |
| | Understands the ghost went for the candy; walks past ghost D | | |
| | One-plate gate: steps on the plate first? How long until a ghost is used? | | |
| | Gets through gate one before it shuts (first try?) | | |
| | Two-plate gate: two ghosts, or one ghost plus self? Time to solve: ______ | | |
| **S5 Cornfield** | Route through the maze; takes the dead-end stub? | | |
| | Deals with ghost H by: timing / cover / candy / flare | | |
| | The pair: one candy, two candies, or flare | | |
| | Chased onto the last bridge? Makes both jumps? | | |
| | Candle when they reach the door: ______ % | | |
| **Overall** | Moments of visible fun (leaning in, laughing, "oh!") | | |
| | Moments of visible frustration | | |
| | Things they tried that the game does not allow | | |

## Post-play survey

Tester id: ____________

Circle one number for each.

1. **How much fun was it?**  1 (none)  ·  2  ·  3  ·  4  ·  5 (a lot)
2. **How clear was it what you could do and what you had to do?**  1 (very unclear)  ·  2  ·  3  ·  4  ·  5 (very clear)
3. **How hard was it?**  1 (far too easy)  ·  2  ·  3 (about right)  ·  4  ·  5 (far too hard)

Write a sentence or two for each.

4. **What was the most confusing moment?**

5. **Where did you get stuck, if anywhere? What got you unstuck?**

6. **Was there anything you wanted to do but could not?**

7. **What was your favourite moment?**

8. **Would you play again, or play more levels?**  yes / maybe / no. Why?

9. *(optional)* **In your own words, what does the lantern do?**

10. *(optional)* **How often do you play games with a third-person camera?**  never / sometimes / often

## After the session: results table

Fill one row per **finding**, not per tester. A finding needs evidence from at least one of: an observation, a quote, a survey answer, a CSV column. Then decide the change. The seven columns on the right are the columns of `docs/ITERATIONS.md`, so a finished row can be pasted straight into its **Changes** table. Every change gets its own commit, and the commit hash goes in the last column after committing.

| Finding (what happened, how many testers) | Evidence (quote, sheet row, CSV column and value) | Iteration | Tag | Date | Tester feedback | Change made | Why | Commit hash |
|---|---|---|---|---|---|---|---|---|
| *Example: 3 of 4 testers fell off the deep bridge at least twice* | *`S2_deaths` 2, 3, 2, 0; `extinguished,fell`; P1-02: "I can't tell where it goes"* | *2* | *`v0.2-playtest2`* | *2026-10-11* | *"I can't tell where it goes" (P1-02); 3 of 4 fell at least twice* | *Widened the deep bridge from 2 m to 3 m and added a second porch lamp halfway* | *Deaths came from not seeing the next platform, not from the idea being hard* | *(hash)* |
| | | | | | | | | |
| | | | | | | | | |
| | | | | | | | | |

How the columns map:

| `ITERATIONS.md` column | What goes in it |
|---|---|
| Iteration | The number of the iteration this change belongs to: changes made after Playtest 1 are Iteration 2, after Playtest 2 Iteration 3 |
| Tag | The tag of the build that will **contain** the change (`v0.2-playtest2` for changes made after Playtest 1) |
| Date | The date the change was made |
| Tester feedback | The evidence, shortened: a quote and a count |
| Change made | What changed in the game, specifically enough to find in the diff |
| Why | The reasoning from evidence to change |
| Commit hash | `git log --oneline`, after committing that change alone |

Also fill the **Playtest sessions** table at the top of `ITERATIONS.md`: the tag tested, the date, how many testers, and what data was collected.

## Summary numbers to compute per playtest

From the summary CSVs (one row per session), for the write-up:

| Number | From |
|---|---|
| Completion rate | `completed` |
| Median total time, against the 6–9 minute target | `total_time_s` |
| Median time per section, against the estimates | `S1_time_s` … `S5_time_s` |
| Deaths per section, summed over testers | `S1_deaths` … `S5_deaths` |
| Median fun, clarity and difficulty | Survey questions 1–3 |
| Most common cause of death | `extinguished` rows in the events files |
