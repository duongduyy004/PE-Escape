# Chess game loop: checks and captures

Date: 2026-10-13. Plan: `docs/superpowers/plans/2026-10-13-chess-game-loop.md`. Spec: `docs/superpowers/specs/2026-10-12-chess-generated-mate-design.md`.

Environment: Unity 6000.3.23f1 **Editor on Windows**. Nothing here was run on Android or on a device.

## Tests run

| Suite | Result |
| --- | --- |
| EditMode `KMA.Tests.Gameplay.Chess` + `ChessCelebrationBackdropTests` (after the last change) | `result=Passed total=194 passed=194 failed=0` |
| EditMode, whole project (before the intro-card test fix) | `total=1239 passed=1232 failed=2 skipped=5` |
| PlayMode `KMA.Tests.Gameplay.Chess` | `result=Passed total=25 passed=25 failed=0` |
| PlayMode, whole project (`KMA.Tests`) | `total=411 passed=410 failed=1` |

Failures in the whole-project runs:

- `ChessIntroCardIsOpaqueAndDoesNotRepeatTheObjective` (EditMode): asserted the old fixed "90 giây" intro text. Fixed in the same task; the rerun above passes.
- `FrogJumpRulesTests.FasterDefaultSweepsEachEdgeInPointSevenSeconds` (EditMode) and `FrogJumpDialogueTests.FinishOnFinalLandingDoesNotQueueGoodJump` (PlayMode): **not related to chess**. Both already failed in this workspace's earlier runs today (`editmode-all`, `editmode-final`, `playmode-final`, `play-wide` result files from 13:45–14:40), before any change of this work.

What the PlayMode chess suite covers on the real scene: board locked and clock still until Start; direct scene opens Normal (2 moves optimal, 3 allowed, clock 60–240 s); the optimal line solves and publishes a result with score ≥ 8.9; a suboptimal legal move is played and counts a move; an illegal move changes nothing and the clock keeps running; pause freezes the clock; an analysis result that arrives while paused is applied once after resume; a hint stops the clock until shown; Restart keeps the same puzzle after a forced analysis error; disabling the controller during analysis logs nothing unexpected; pausing during the boss slide still plays the boss move once after resume; poses follow the turn; dialogue ids for solved, time-out and out-of-moves; a hint is not replayed after the board changes.

## Screenshots

Captured: `docs/qa/2026-10-13-chess-game-loop-intro.png` (MG_ChessFinal opened directly, 1920x1080). It shows the new HUD (objective chip `Chiếu hết, tối ưu 2 nước`, clock `02:00`, `Nước: 0/3`, hint and pause buttons) behind the first-run guide. The guide opens before the puzzle exists, so its time line reads "Thời gian suy nghĩ được tính theo độ khó của thế cờ." instead of a number; the puzzle's own time is on the clock chip and in the intro card.

**Not captured:** the generating state, the intro card with the limits, mid-game `Nước: 1/3`, a hint shown, the analysis-error card, and the success and failure end states. The screenshot tool needs the game to run frames in the background Editor, and here it did not: the clock stayed at its start value and the `chess-*` QA states never applied, so every state capture came out as the intro frame. Those captures were discarded rather than kept as evidence. QA states `chess-select`, `chess-wrong`, `chess-boss`, `chess-hint`, `chess-win`, `chess-timeout` were rewritten for the new machine and a `chess-analysis-error` state was added in `Assets/Editor/PlayModeScreenshotQaStates.cs`; they compile but are **not exercised** yet.

Layout, text fit and overlap at the target frame are therefore not verified for those states. Run the states again with the Editor in the foreground before relying on them.

## Not verified

- Android device behaviour and worker-thread performance on device. The Hard worst case measured in the Editor is 1.83 s of the 2 s budget (`docs/qa/2026-10-13-chess-generator-performance.md`), so Android may hit the retryable analysis error.
- Result panel, A+ display, Journey retry with the same puzzle and saved data: Plan 3.
