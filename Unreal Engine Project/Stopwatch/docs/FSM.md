# State machines

Both diagrams are read from the code: the game mode in `Source/Stopwatch/StopwatchGameMode.cpp` (`EStopwatchMode`), and the time states in `Source/Stopwatch/TimeableComponent.cpp`.

## Game mode

Every transition goes through `AStopwatchGameMode::SetMode`, which logs `Game mode: Old -> New` and broadcasts `OnModeChanged`. The player controller shows the right screen, cursor and input for each mode.

```mermaid
stateDiagram-v2
    [*] --> Title: level loads
    [*] --> Playing: level reloaded by Restart (?Restart option)

    Title --> Playing: any key or Start pressed\nStartPlaying() begins the playtest log

    Playing --> Paused: Esc / P / gamepad Start\nTogglePause(), world paused
    Paused --> Playing: Esc / P / Start / Resume\n(paused from Playing)
    Collapse --> Paused: Esc / P / gamepad Start
    Paused --> Collapse: Esc / P / Start / Resume\n(paused from Collapse)

    Playing --> Respawning: player died (KillPlayer)
    Collapse --> Respawning: player died, or the countdown reached 0 (cause Collapse)
    Respawning --> Playing: fade out, teleport, reset, fade in done\n(died in Playing)
    Respawning --> Collapse: fade in done (died in Collapse)\ntime left kept, restarted only if it ran out

    Playing --> Collapse: ACollapseTrigger entered (only from Playing)

    Playing --> Won: AGoalTrigger entered
    Collapse --> Won: AGoalTrigger entered\ncountdown stops, log ends completed

    Paused --> [*]: Restart (reload) or Quit
    Won --> [*]: R / gamepad A (reload, straight to Playing) or Quit
```

What each mode allows:

| Mode | Pawn input | Freeze/Rewind | Deaths count | Cursor | Screen |
|---|---|---|---|---|---|
| Title | off | off | no | shown | title, premise, controls |
| Playing | on | on | yes | hidden | HUD |
| Paused | (world paused) | off | no | shown | Resume / Restart / Quit, log folder |
| Respawning | off | off | no | hidden | HUD with the death cause, fading |
| Collapse | on | on | yes | hidden | HUD with the countdown |
| Won | off | off | no | shown | stats, R / A to play again, Quit, log folder |

The respawn: fade out for `RespawnFadeSeconds` (0.75) showing the cause, then teleport to the last checkpoint (or the PlayerStart before one), stop all movement, face the checkpoint's yaw, reset every `IResettable` whose `SectionId` is the checkpoint's, top the sand up to the checkpoint's `MinSandOnRespawn`, then fade in for another 0.75 s.

## Time states of a timeable object

`UTimeableComponent` keeps `bIsFrozen` beside a phase (`Idle`, `Rewinding`, `Holding`, `Crumbling`). Frozen is shown as its own state here. Unfreezing goes back to the phase the object froze in, because freezing only stops the owner's time (`CustomTimeDilation = 0`). Normal is the `Idle` phase, and Held is `Holding`.

```mermaid
stateDiagram-v2
    [*] --> Normal

    Normal --> Frozen: Freeze(5 s)\n25 sand
    Frozen --> Normal: freeze timer ran out (world time)\nfroze while Normal
    Frozen --> Held: timer ran out, froze while Held\n(the hold countdown was paused)
    Frozen --> Crumbling: timer ran out, froze while Crumbling\nor froze an AuthoredPast object mid-rewind

    Normal --> Rewinding: Rewind(), 35 sand\nRecorded: Idle and 2+ samples of movement\nAuthoredPast: has an intact pose
    Crumbling --> Rewinding: Rewind() (AuthoredPast only)

    Rewinding --> Frozen: Freeze()\nRecorded: stops where it is, resumes with that moment's velocity\nAuthoredPast: will crumble once unfrozen
    Rewinding --> Normal: Recorded, reached the oldest sample\nwithout bHoldAfterRewind (physics restored)
    Rewinding --> Held: Recorded with bHoldAfterRewind,\nor AuthoredPast fully intact

    Held --> Frozen: Freeze()
    Held --> Normal: Recorded: HoldDuration ran out (dilated time)
    Held --> Crumbling: AuthoredPast: HoldDuration ran out
    Held --> Held: HoldDuration 0 means forever

    Crumbling --> Normal: back to the broken pose (CrumbleDuration)

    Normal --> Normal: ResetToStart / ResetTimeState\n(a respawn at a checkpoint of its section)
    Frozen --> Normal: ResetTimeState
    Rewinding --> Normal: ResetTimeState
    Held --> Normal: ResetTimeState
    Crumbling --> Normal: ResetTimeState
```

Where each piece sits: pendulums, crushers, gears, moving platforms, props and chutes use `Recorded`. The falling platform uses `Recorded` with `bHoldAfterRewind` and `HoldDuration` 0, so it stays up. Broken bridges use `AuthoredPast`, with `HoldDuration` 0 (stays) or a few seconds (crumbles again).
