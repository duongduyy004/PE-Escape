# Chess AI exam: final verification

Date: 2026-10-13. Environment: Unity 6000.3.23f1 **Editor on Windows**. Nothing here was run on Android or any device.

Plans: `docs/superpowers/plans/2026-10-13-chess-ai-core.md` (search, generator, grading), `...-chess-game-loop.md` (game, hints, HUD, dialogue), `...-chess-persistence-results.md` (this one: results, save, retry, copy). Spec: `docs/superpowers/specs/2026-10-12-chess-generated-mate-design.md`. Earlier reports: `2026-10-13-chess-generator-performance.md`, `2026-10-13-chess-game-loop.md`.

## Whole-project test runs (Editor, after the last code change)

| Suite | Command | Result |
| --- | --- | --- |
| EditMode | `tools/run-unity-tests.sh EditMode "" p3-final-edit` | `total=1289 passed=1280 failed=1 skipped=8` |
| PlayMode | `tools/run-unity-tests.sh PlayMode "" p3-final-play` | `total=420 passed=419 failed=1 skipped=0` |

The two failures are **not related to chess** and predate this work (they also failed in this workspace's runs from 13:45–14:40 today, before any chess change):

- `KMA.Tests.Gameplay.FrogJump.FrogJumpRulesTests.FasterDefaultSweepsEachEdgeInPointSevenSeconds` (EditMode).
- `KMA.Tests.Presentation.FrogJumpDialogueTests.FinishOnFinalLandingDoesNotQueueGoodJump` (PlayMode).

The 8 skipped EditMode tests are the explicit measurement tests (`ChessGeneratorMeasurement`), run on purpose below. The run script prints no summary on this machine (no `python3`), so the numbers above were read from the Unity result XML.

## What Plan 3 added, and how it was checked

- **A+ label:** chess shows `A+` where it stores `Rank.S`; other subjects still read `S` (`RankLabelsTests`, the Presentation suites unchanged).
- **Save compatibility:** a record saved before the generated puzzles loads with its score, time, mistakes and hint intact and `scoringVersion == 0`; a new record round-trips every field including the per-move list (`ChessFinalSaveTests`, `CelebrationSummaryTests`). Save version stays 9.
- **Result screen text:** moves used/allowed, optimal count, time used/allowed, end reason, hint state, and the quality of each move on a failure (`ChessResultTextTests`, PlayMode `ResultDetailListsMovesAndTimeAfterAWin`, `ChessRankLabelShowsAPlus`).
- **Retry:** Journey retry repeats the failed puzzle (same FEN, limits, time); a fresh start gets a new one; passing clears it; a failed retry start does not leave the intent armed (`ChessRetryStoreTests`, `ChessFinalRoutingTests`, scene-level tests in `ChessFinalSceneTests`). Not saved: the puzzle is not resumed after closing the app.
- **Celebration row:** `Đạt  Bài kiểm tra cuối · A+ · 2/3 · 00:42 · Không dùng gợi ý`; a legacy record keeps its old wording and never gets a grade.
- **Copy:** no shipped chess text says "90 giây", "sửa sai" or "chiếu hết trong 2 nước" (`ChessCopyAuditTests`).

## Measurements (Editor)

Full tables in `2026-10-13-chess-generator-performance.md`. Final run, 30 seeds per level, default budget (2 s / 200,000 nodes):

| Level | Generated | Fallback | Worst generation | First-move analysis Unknown | Worst first-move analysis |
| --- | ---: | ---: | ---: | ---: | ---: |
| Easy | 30/30 | 0/30 | 0.01 s | 0/30 | 0.001 s |
| Normal | 30/30 | 0/30 | 0.04 s | 0/30 | 0.004 s |
| Hard | 30/30 | 0/30 | 0.91 s | 0/30 | 0.147 s |

Spec target (at least 80% Generated per level, no reliance on fallback) is met in the Editor. Grading and answering **every** legal first move on Hard: 0/174 Unknown, but the slowest single request took **1.75 s** of the 2 s budget.

UI: generating a Hard puzzle on the worker while sampling 500 frames gave a worst frame of 9 ms (`GeneratingAHardPuzzleDoesNotStallTheFrames`). `Assets/_Project/Scripts/Gameplay/Chess/Core` uses no `UnityEngine`; the worker and the provider pass only strings, ints and structs to the pool thread.

A real defect found by the measurement and fixed here: a Hard hint searched each candidate to the full move limit instead of the mate distance, ran out of budget and returned no move. Fixed in `ChessHintBuilder`; test `HardHintsFinishWithinTheDefaultBudgetAtFullMovesLeft` failed before and passes now.

## Not verified

- **Interactive playthrough in the real scene and all result screenshots.** The background Editor used by the screenshot tool does not run game frames here (the clock stays at its start value and the `chess-*` QA states never apply), so no state capture was usable. Only one intro frame exists (`2026-10-13-chess-game-loop-intro.png`). Layout, text fit and clipping of the result panel (including the long move list on a failure), the analysis-error card, and the new celebration row are therefore checked by code and PlayMode assertions only, not by eye. The celebration row was shortened until `CelebrationSceneTests.SummaryTitleStaysInsideTheCard` (two widths) passed.
- **Each Journey difficulty on a real route.** Easy and Hard were reached only through scene tests with a synthetic context, not through a played Journey.
- **Android:** device behaviour, performance of the worker on device, IL2CPP threading, memory in a long session. The Hard worst case of 1.75 s in the Editor suggests a device may return the retryable analysis error for some non-optimal moves.
- The remaining items are listed in the final review of each plan; none is claimed fixed here.

To check on a device, build with the existing `tools/build-apk.*` scripts; this report does not claim that.
