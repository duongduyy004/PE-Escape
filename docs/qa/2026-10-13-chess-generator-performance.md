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
