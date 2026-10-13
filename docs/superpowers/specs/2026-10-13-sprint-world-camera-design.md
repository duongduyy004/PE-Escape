# Sprint World Space & Follow Camera Design

**Date:** 2026-10-13
**Status:** Draft — awaiting review

## Goal

Make the Sprint race feel like a real 150 m dash seen by a tracking camera. Today every runner is
squeezed onto a fixed 16-unit stretch of screen (`-9.6 → 6.5`), the camera never moves, and the finish
line slides in from the right edge. After this change:

1. The player starts at the left, runs right until they reach the middle of the screen, then stays
   centred while the track and backdrop scroll left.
2. Every rival has its own position, velocity and acceleration.
3. The camera follows the player only.
4. The finish line has a fixed world coordinate.
5. Motion reads as real: reaction off the gun, acceleration phase, top speed, late-race fade or kick,
   running through the line and braking, legs that match ground speed.

## Decisions

| Decision | Choice | Why |
|---|---|---|
| Architecture | Real world-space model: pure logic in metres, one mapping `WorldX(m)`, presentation reads it | Logic stays EditMode-testable; satisfies "fixed finish" and "camera follows player" literally. Rejected: treadmill (move world under a fixed player) and Rigidbody2D (non-deterministic, hard to balance). |
| World scale | 1 m = 1 world unit | Human scale (runner ≈ 2 units tall); strong sense of speed. |
| Start / finish | `StartX = -9.6`; finish at `WorldX(TargetDistance)` = 140.4 for 150 m | Keeps today's start spot; finish is a static object. |
| Off-screen rivals | Edge chip in the rival's lane row showing `+4.2 m` / `−3.1 m` relative to the player | Player always knows how far ahead/behind each rival is. |
| Finish behaviour | Everyone runs through the line and brakes to a stop within a per-runner rolled 3–5 m | Real but short run-out. |
| Learn | Endless run, no finish line, rivals tethered near the player | Removes the 66 % clamp hack; keeps a race feel without rivals vanishing hundreds of metres ahead. |
| Player tap model | Unchanged (impulse, drag, combo boost, fall on wrong tap, stand still when idle) | It is the gameplay; only the finish behaviour changes. |

## World & Camera

- `SprintTrackLayout` gains `StartX = -9.6`, `WorldX(float metres) = StartX + metres`,
  `FinishWorldX(float distance) = WorldX(distance)`. The fixed-mapping use of `TrackLength`/`FinishX`
  and the reveal helpers in `SprintUiLayout` (`FinishVisible`, `FinishReveal01`, `FinishRevealDistance`)
  are removed.
- **`SprintCameraFollow`** (new MonoBehaviour on the main camera):
  - `targetX = max(cameraStartX, playerWorldX)`, with `cameraStartX = 0` (the camera's authored X).
    The camera is still while the player runs from -9.6 to the centre, then tracks them.
  - Moves toward `targetX` with a critically damped spring (smooth time ≈ 0.12 s). No look-ahead, so the
    player stays centred. Only reads the player's world X; never reads rivals.
  - The maths lives in a static function `SprintCameraFollow.Step(currentX, velocity, targetX, dt)` for tests.
  - Camera Y and orthographic size are untouched.
- **`SprintParallax`** takes camera X as input instead of player distance. Each layer has a
  world-follow factor: track = 1 (locked to the world, so runners and the finish line sit on the painted
  track), stands ≈ 0.35, sky ≈ 0.15. Layer offset = `cameraX × (1 − factor)` relative to the camera,
  tiles recycle around the camera as today. The parallax maths is a static function for tests.
- **Finish line**: `SprintFinishLinePresenter` places the checkerboard once at `FinishWorldX(TargetDistance)`
  and keeps it active (camera culling hides it off-screen). A start line is drawn at `StartX`.
  Learn hides the finish line.
- **Runners**: `RunnerVisualPresenter` and `RivalRunnerAI` set `x = WorldX(distance)` (rivals still divide
  out their parent's scale). The Learn 66 % clamp is removed.
- **`SprintOffscreenRivalChips`** (new, HUD): for each rival whose world X is outside the camera view,
  show a chip at the left or right screen edge at that rival's lane row, with the rival's colour/name and
  signed gap to the player in metres (one decimal). Hidden when the rival is on screen and in Tutorial,
  Countdown and Resolve.

## Motion Model

### Rivals — `RivalMotion` (new, pure C#)

State `{x, v, a}` integrated every tick. Rolled per rival at race start:

| Parameter | Range | Effect |
|---|---|---|
| Reaction time | 0.12–0.25 s | `v = 0` until the reaction has elapsed |
| Top speed `vMax` | Shuffled bands across 8.5–10 m/s (existing `RollField` banding) | Always a clearly fast, middle and slow rival |
| Acceleration constant `τ` | 0.9–1.4 s | `a = clamp((vTarget − v) / τ, −aBrake, 7 m/s²)`; ≈ 95 % of `vMax` after 3–4 s |
| Pace variation | Existing surge/fade segment schedule | Multiplies `vTarget`; `v` chases it through acceleration, so the 0.4 s blend is no longer needed |
| Finish style | −6 % … +8 % | Applied to `vTarget` from 80 % of the distance: faders slow, closers kick, places can swap late |
| Run-out | Stop distance `d` rolled in 3–5 m | After crossing, `vTarget = 0` and braking `a = −v²/(2d)` (computed at the crossing) so the rival stops `d` metres past the line |

`vTarget(t) = vMax × paceMultiplier(t) × finishStyle(x)`. The field stays calibrated so the fastest
rival finishes 150 m in ≈ 15–17 s and the slowest in ≈ 18–20 s.

`RivalPaceProfile` keeps the pace schedule and becomes the source of `vTarget`; `RivalPaceRandomizer`
rolls the new parameters alongside the existing band and schedule. `IsSurging` / `IsSlowing` are
derived from actual `v` against `vMax`.

**Learn tether:** in Learn each rival has a slot offset rolled in ±2–5 m around the player.
`vTarget = playerSpeed + k × (playerX + slot − x)` plus the rival's own pace variation, so rivals edge
ahead and drop back but stay within about 6 m of the player. No finish, no run-out.

### Player

- Tap model unchanged.
- On crossing the line: the result, elapsed time and rank lock; taps are ignored; speed is not zeroed —
  the player brakes with `a = −v²/(2d)`, `d` rolled in 3–5 m, and the camera keeps following.

### Finish timing & rank

- Crossing time is interpolated within the tick (`t0 + (goal − x0) / (x1 − x0) × dt`) for the player and
  every rival, and stored as `FinishTime`.
- Rank: finished runners ordered by `FinishTime`, ahead of unfinished runners ordered by distance.
  Two runners crossing in the same tick are ordered by their interpolated times.
- The result card still waits for all rivals to cross (or `MaxRivalWaitSeconds`), as today.

### Animation

- `animator.speed` scales with actual velocity (clamped) so the stride matches the ground scrolling past.
- State selection from velocity and phase: Idle (stopped / pre-race / after the run-out),
  Run, Burst (finishing stretch or surging), Stumble (player fall), Celebrate / Fail in Resolve.

## Files

| File | Change |
|---|---|
| `Gameplay/Sprint/SprintRules.cs` | `RivalMotion[]` replaces `rivalDistances`; player run-out; `FinishTime`s; rank by finish time |
| `Gameplay/Sprint/RivalMotion.cs` | New |
| `Gameplay/Sprint/RivalPaceProfile.cs`, `RivalPaceRandomizer.cs` | `vTarget` source; roll reaction, `τ`, finish style, stop distance |
| `Gameplay/Sprint/SprintTrackLayout.cs`, `SprintUiLayout.cs` | `StartX`, `WorldX`, `FinishWorldX`; remove reveal helpers |
| `Gameplay/Sprint/SprintCameraFollow.cs` | New |
| `Gameplay/Sprint/SprintParallax.cs` | Driven by camera X with per-layer world-follow factor |
| `Gameplay/Sprint/SprintFinishLinePresenter.cs` | Static finish + start line; hidden in Learn |
| `Gameplay/Sprint/RunnerVisualPresenter.cs`, `RivalRunnerAI.cs` | World X; animator speed; remove Learn clamp |
| `Gameplay/Sprint/SprintOffscreenRivalChips.cs` | New |
| `Gameplay/Sprint/SprintController.cs` | Expose player world X / finish state as needed; terminal flow unchanged |
| `Gameplay/Sprint/SprintFestivalPresentation.cs`, `Editor/SprintSceneConfigurator.cs`, `MG_Sprint.unity` | Wire camera follow, chips, start line; track layer factor = 1 |

## Testing

- **EditMode — motion:** rival stands still during reaction; reaches ≈ 95 % `vMax` within 3–4 s; field
  finish times within 15–20 s over many seeds; every runner (player and rivals) stops 3–5 m past the
  line; rank by interpolated finish time including two crossings in one tick; Learn rivals stay within
  6 m of a steadily running player; finish style changes speed only after 80 %.
- **EditMode — presentation maths:** `WorldX` / `FinishWorldX`; camera step holds at `cameraStartX`
  until the player passes it, then converges on the player without overshoot; parallax offsets per factor.
- **PlayMode:** update `SprintControllerTests`, `SprintRuntimeInputTests`, `SprintChallengeRuntimeTests`,
  `SprintPresentationGateTests` and any test assuming the `-9.6 → 6.5` mapping or the finish reveal.
- **Visual:** screenshots via `testing-unity-ui-with-screenshots` at the start, mid-race with the camera
  scrolling, at the finish line, and with an off-screen rival chip.

## Out of Scope

- Changing the tap model, combo, fall rules, time limits or scoring.
- Distance markers along the track, camera zoom or shake.
