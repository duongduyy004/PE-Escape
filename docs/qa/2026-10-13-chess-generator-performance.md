# Chess puzzle generator: performance measurement

Date: 2026-10-13. Plan: `docs/superpowers/plans/2026-10-13-chess-ai-core.md` (Task 6). Spec: `docs/superpowers/specs/2026-10-12-chess-generated-mate-design.md` §3.2, §10.

Environment: Unity 6000.3.23f1 **Editor** (EditMode test runner, Mono, Windows 11). **Not measured on Android.**
Budget: `ChessGeneratorLimits.Default` = 2 s and 200,000 nodes in total, 30,000 nodes per candidate.
Method: `ChessGeneratorMeasurement` (explicit test), seeds 1..30 per level, no avoid-FEN, no cancellation.

| Level | Generated | Fallback | Avg nodes | Avg time | Worst time |
| --- | ---: | ---: | ---: | ---: | ---: |
| Easy (mate in 1) | 30/30 (100%) | 0/30 | 102 | 0.00 s | 0.01 s |
| Normal (mate in 2) | 30/30 (100%) | 0/30 | 1042 | 0.01 s | 0.04 s |
| Hard (mate in 3) | 30/30 (100%) | 0/30 | 12568 | 0.16 s | 0.91 s |

Raw lines from the run:

```
[MEASURE] Easy: generated 30/30 (100%), fallback 0/30, avg nodes 102, avg 0.00s, worst 0.01s
[MEASURE] Normal: generated 30/30 (100%), fallback 0/30, avg nodes 1042, avg 0.01s, worst 0.04s
[MEASURE] Hard: generated 30/30 (100%), fallback 0/30, avg nodes 12568, avg 0.16s, worst 0.91s
```

## Verdict against the spec

- Target: at least 80% Generated per level within budget (spec §10). Result: 100% for all three levels, with no reliance on the fallback pool. Pass for the Editor.
- Hard worst case (0.91 s) uses about 45% of the 2 s budget on this machine. Slower devices will cut into that margin, so Android must be measured before this is called safe there.
- Android: **not measured** (Plan 3).

## Notes

- Normal and Hard piece sets originally contained single-queen sets (3 pieces total), which violated the 4–6 piece rule (spec §3.1). They were replaced with two-extra-piece sets before this measurement; there is no earlier measurement to compare against.
- Fallback pool (`Resources/Chess/fallback_pool.json`) was built offline with a 5,000,000-node / 600,000-per-candidate / 120 s budget: 12 puzzles per level, re-verified by `ChessFallbackLibraryTests` (engine re-analysis for all, independent brute-force check for Easy/Normal and two Hard).
- Search is deterministic for a given seed and node budget; only wall-clock limits can change an outcome.

## Runtime analysis (grader + opponent), Editor

Method: `ChessGeneratorMeasurement.MeasureRuntimeAnalysis`, 5 generated puzzles per level; for every legal first move: `ChessAttemptGrader.Evaluate` with `movesLeft = M`, then `ChessOpponent.Choose` with `M - 1`; each request gets a fresh engine and a fresh default budget (2 s / 200,000 nodes), i.e. no cache sharing.

| Level | Grade Unknown | Reply Unknown | Worst single request |
| --- | ---: | ---: | ---: |
| Normal (M=3) | 0/191 | 0/191 | 0.03 s |
| Hard (M=5) | 0/174 | 0/174 | 1.83 s |

Risk: the Hard worst case uses about 90% of the 2 s budget in the Editor. On Android the same request would probably return Unknown (retryable analysis error). Before Plan 3 calls this safe on device: reuse one engine (cache) across a game with `UseBudget`, add move ordering, and measure on Android. Not done here because the worker lifecycle belongs to Plan 2.

## Final numbers (Plan 3, Editor, after the hint-search fix)

The full 30-seed measurement was repeated with the final build.

| Level | Generated | Fallback | Mean / worst generation | First-move analysis Unknown | Mean / worst first-move analysis |
| --- | ---: | ---: | ---: | ---: | ---: |
| Easy | 30/30 | 0/30 | 0.00 s / 0.01 s | 0/30 | 0.000 s / 0.001 s |
| Normal | 30/30 | 0/30 | 0.01 s / 0.04 s | 0/30 | 0.001 s / 0.004 s |
| Hard | 30/30 | 0/30 | 0.16 s / 0.91 s | 0/30 | 0.027 s / 0.147 s |

"First-move analysis" is the request the game sends after the optimal first move of a fresh puzzle (grade it and pick the reply). The all-moves runtime figures above were re-run too: Hard grade Unknown 0/174, reply Unknown 0/174, worst single request **1.75 s** (a non-optimal first move; the 2 s budget is nearly used). That remaining risk is unchanged: Android may return the retryable analysis error for such moves.

A defect found by this measurement and fixed: `ChessHintBuilder` searched every candidate move down to the full allowed move count (5 on Hard) instead of the mate distance (3), so a Hard hint could run out of budget and return no move (`a1a1`). It now searches to the mate distance only. Test: `ChessHintBuilderTests.HardHintsFinishWithinTheDefaultBudgetAtFullMovesLeft` (12 Hard seeds; failed before the fix, passes after).

UI responsiveness: `ChessFinalSceneTests.GeneratingAHardPuzzleDoesNotStallTheFrames` ran a Hard generation on the worker while sampling 500 frames in the Editor: worst frame 9 ms. `grep -rn "UnityEngine" Assets/_Project/Scripts/Gameplay/Chess/Core` returns nothing, and the worker/provider hand the pool thread only strings, ints and structs.
