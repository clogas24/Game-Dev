# Iterations

One row per playtest-driven change. Cite the logger data behind each finding: the CSV file name, the section, and the columns (for example `DeathsBlade`, `FailedSpends`, `Seconds`). CSVs are saved to `Saved/Playtests/` (see the README). The full write-up of each iteration is in `docs/GAME_SCRIPT.md`, under Iteration History.

The `p3-*` tags below are the planned names. They don't exist in git yet and have to be created on the listed commits.

| Iteration | Tag | Date | Playtest finding (with logger data) | Change made | Why | Commit hash |
|---|---|---|---|---|---|---|
| Baseline | p3-v0.1-playtest1 | 2026-11-05 | None: this is the build tested in Playtest 1. Pre-playtest smoke-test tuning only (no logged playtest data) | Smoke-test tuning, in `a6671f9`: tester ID removed from the CSV; boulder ramp walled (rails and backstop); collapse timer keeps the time left on fall and hazard deaths, reset only on timeout; tutorial and sign text shortened to numbered steps | Make the logger anonymous; keep the boulder on its ramp so the rewind is reliable; make a Collapse death cost progress, not the whole clock; keep instructions readable at a glance | `9619003` (merge of `a6671f9`) |
| Iteration 1 | p3-v0.2-playtest2 | _TBD_ | _TBD: from Playtest 1 CSVs_ | _TBD_ | _TBD_ | _TBD_ |
| Iteration 2 | p3-v0.3-playtest3 | _TBD_ | _TBD: from Playtest 2 CSVs_ | _TBD_ | _TBD_ | _TBD_ |
