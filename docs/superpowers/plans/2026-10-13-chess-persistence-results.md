# Chess Persistence, Results and QA Implementation Plan (Plan 3 of 3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show the F–A+ grade and the attempt breakdown on every chess result screen, save the new chess record without breaking old saves, let Journey "retry" reuse the same puzzle, remove the old "90 s / 2 mistakes" copy, and record honest QA evidence.

**Architecture:** The game loop (Plan 2) produces a plain `ChessResultSummary` carried inside `ChallengeMetrics`. Progression stores it in the existing journey result and best-record JSON without bumping the save version (new fields default to "legacy"). Presentation reads only that data. A small `RankLabels` helper prints `A+` for chess while the stored enum stays `Rank.S`. A one-shot `ChallengeRetryIntent` plus an in-memory `ChessRetryStore` give "same puzzle on retry, new puzzle otherwise".

**Tech Stack:** C# (Unity 6000.3.23f1), `JsonUtility` saves, uGUI/TextMeshPro, NUnit EditMode + PlayMode.

**Spec:** `docs/superpowers/specs/2026-10-12-chess-generated-mate-design.md` (sections 7 results, 9, 10, 11).

**Depends on:** Plan 1 (`2026-10-13-chess-ai-core.md`) and Plan 2 (`2026-10-13-chess-game-loop.md`) merged: `ChessAttemptMachine` (`Result`, `Moves`, `EndReason`, `Snapshot`, `Clock`, `ExplicitHintUsed`, `HintUsed`), `ChessFinalController.BuildResult/BuildMetrics`, `ChessGrade`.

## Global Constraints

- Do not change the `Rank` enum, its serialized values, `ScoreUtil`, or how other subjects display `S` (spec §7). Chess shows `A+` where it stores `Rank.S`.
- `SaveData.CurrentVersion` stays 9: new JSON fields default to zero/empty and old data is marked by `scoringVersion == 0`; never infer missing values (spec §9).
- Journey updates the best chess record only on success and only when the score is higher; a failed Final attempt costs no life (existing behaviour, spec §9).
- A new play creates a new puzzle snapshot; only Journey retry (result-panel "CHƠI LẠI" or continue-after-fail) reuses the failed snapshot (spec §9). No resume of an unfinished game after closing the app.
- A result is emitted and committed once per attempt (spec §8).
- Result text must not claim the score was computed with the new formula for legacy records (spec §9).
- The word "S" must still appear for other subjects; tests for them must keep passing unchanged.
- Project convention: commit straight to `master`; no `Co-Authored-By` or AI trailer. Run tests with `tools/run-unity-tests.sh <EditMode|PlayMode> "<filter>" <name>`, Unity Editor closed. Do not claim Android verification from Editor runs.

## Review Focus

- An old save (version 9, chess record without the new fields) loads with its score/time/hint intact, shows the legacy line, and is never shown as an A+/new-formula result (Task 2).
- A save written by the new build and read by the same build round-trips every new field, including the per-move list (Task 2).
- Failing the Final then pressing CHƠI LẠI gives the identical FEN/N/M/T; passing it, or starting from the map after a failure, gives a fresh puzzle; a failed retry start never leaves the intent armed (Task 4).
- The A+ label shows in the Journey result panel, the direct-scene result, the course summary and the celebration chess row, but a sprint/volleyball/football `Rank.S` still reads `S` (Task 1).
- A failed attempt still lists the quality of the moves played and the end reason without claiming a score (Task 3).
- Result text with long move lists must fit the result panel at the target frame (Task 3 screenshot step).
- No remaining screen says "90 giây", "2 lần sửa sai" or "chiếu hết trong 2 nước" for the chess exam (Task 6).

---

### Task 1: Chess rank labels (A+) without changing the enum

**Files:**
- Create: `Assets/_Project/Scripts/Gameplay/Common/RankLabels.cs`
- Create: `Assets/Tests/EditMode/Gameplay/Common/RankLabelsTests.cs`
- Modify: `Assets/_Project/Scripts/UI/ResultPanel.cs` (lines ~191 and ~281)
- Modify: `Assets/_Project/Scripts/UI/JourneyCourseSummary.cs` (lines ~62–64)
- Modify: `Assets/_Project/Scripts/Gameplay/Chess/ChessFinalController.cs` (tell the panel to use chess labels)

**Interfaces:**
- Produces: `static class RankLabels { static string For(Rank rank, bool chess); static string For(Rank rank, SubjectId subject) }` and `ResultPanel.UseChessRanks(bool)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using KMA.Gameplay;
using NUnit.Framework;

namespace KMA.Tests.Gameplay
{
    public sealed class RankLabelsTests
    {
        [TestCase(Rank.S, true, "A+")]
        [TestCase(Rank.A, true, "A")]
        [TestCase(Rank.B, true, "B")]
        [TestCase(Rank.C, true, "C")]
        [TestCase(Rank.D, true, "D")]
        [TestCase(Rank.F, true, "F")]
        [TestCase(Rank.S, false, "S")]
        [TestCase(Rank.A, false, "A")]
        public void ChessShowsAPlusAndEverythingElseIsUnchanged(Rank rank, bool chess, string expected) =>
            Assert.That(RankLabels.For(rank, chess), Is.EqualTo(expected));

        [Test]
        public void SubjectOverloadOnlyChangesChess()
        {
            Assert.That(RankLabels.For(Rank.S, SubjectId.Chess), Is.EqualTo("A+"));
            Assert.That(RankLabels.For(Rank.S, SubjectId.Sprint), Is.EqualTo("S"));
            Assert.That(RankLabels.For(Rank.S, SubjectId.Volleyball), Is.EqualTo("S"));
            Assert.That(RankLabels.For(Rank.S, SubjectId.Football), Is.EqualTo("S"));
        }
    }
}
```

Put it in the Common EditMode test assembly (`KMA.Gameplay.EditMode.Tests`). Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.RankLabelsTests" rank-labels`. Expected: compile error, `RankLabels` not defined.

- [ ] **Step 2: Implement**

```csharp
namespace KMA.Gameplay
{
    /// The stored Rank.S means "top grade". For chess the top grade is shown as A+ (spec section 7).
    public static class RankLabels
    {
        public static string For(Rank rank, bool chess) => chess && rank == Rank.S ? "A+" : rank.ToString();

        public static string For(Rank rank, SubjectId subject) => For(rank, subject == SubjectId.Chess);
    }
}
```

If `SubjectId` lives in an assembly `Common` does not reference, move only the `SubjectId` overload next to `JourneyCourseSummary` (UI asm) instead and keep the `bool` overload here; the tests must then be split the same way.

- [ ] **Step 3: Apply the labels**

1. `ResultPanel.cs`: add `bool chessRanks;` and `public void UseChessRanks(bool value) => chessRanks = value;`. Line ~191: `rankLabel.text = VietText.Fix($"XẾP HẠNG {RankLabels.For(result.Rank, chessRanks)}");`. Line ~281: `VietText.Fix($"XẾP HẠNG {RankLabels.For(result.ExamResult.Rank, chessRanks || result.Metrics?.Chess != null)}")` (the `Metrics.Chess` member is added in Task 2; if you implement Task 1 first, use `chessRanks` only and revisit in Task 2 Step 4).
2. `JourneyCourseSummary.cs` ~64: `scores.Add($"{Labels[index]}: {score…} · {RankLabels.For(rank, CourseOrder[index])}");` and keep `JourneyScoreRow.Rank` as the enum (the row stores the enum; add `public string RankLabel` computed with the same call so UI tests can assert it).
3. `ChessFinalController.Start`: after `FindFirstObjectByType<ResultPanel>` add `panel.UseChessRanks(true);`.

- [ ] **Step 4: Run the affected suites**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.RankLabelsTests" rank-labels`, then the existing Presentation/Progression suites that touch the result panel and summary: `tools/run-unity-tests.sh EditMode "KMA.Tests.Presentation" presentation-edit` and `... PlayMode "KMA.Tests.Presentation" presentation-play` (use the real namespaces; check with `grep -rn "ResultPanel\|JourneyCourseSummary" Assets/Tests -l`). Expected: pass, with no change in expected text for non-chess subjects.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Gameplay/Common/RankLabels.cs* Assets/Tests/EditMode/Gameplay/Common/RankLabelsTests.cs* Assets/_Project/Scripts/UI/ResultPanel.cs Assets/_Project/Scripts/UI/JourneyCourseSummary.cs Assets/_Project/Scripts/Gameplay/Chess/ChessFinalController.cs
git commit -m "feat(chess): show the top chess grade as A+ while storing Rank.S"
```

---

### Task 2: Result summary data and save compatibility

**Files:**
- Create: `Assets/_Project/Scripts/Progression/Journey/ChessResultSummary.cs`
- Modify: `Assets/_Project/Scripts/Progression/Journey/ChallengeAttempt.cs` (`ChallengeMetrics`)
- Modify: `Assets/_Project/Scripts/Progression/Journey/JourneyStateData.cs` (`JourneyChessRecordData`, `JourneyResultData`)
- Modify: `Assets/_Project/Scripts/Progression/Journey/JourneyProgress.cs` (`OfferChessResult`, load block around line 270)
- Modify: `Assets/_Project/Scripts/Progression/Journey/CelebrationSummary.cs`
- Modify: `Assets/Tests/EditMode/Progression/ChessFinalSaveTests.cs`, `CelebrationSummaryTests.cs`

**Interfaces:**
- Produces:
  - `[Serializable] sealed class ChessMoveEntry { string uci; float q; int best; int chosen; }`
  - `[Serializable] sealed class ChessResultSummary { const int CurrentScoringVersion = 1; int scoringVersion; int movesUsed, maxMoves, optimalMoves; int allowedSeconds; float usedSeconds; bool hintUsed, explicitHintUsed, hintCapped; string endReason; string gradeLabel; float score; string fen; string source; int seed; int estimatorVersion; ChessMoveEntry[] moves; ChessResultSummary Copy(); }`
  - `ChallengeMetrics.Chess` (`ChessResultSummary`, optional last constructor argument `chess`).
  - `JourneyChessRecordData` new fields: `int scoringVersion; int movesUsed; int optimalMoves; int maxMoves; int allowedSeconds; string gradeLabel;`
  - `JourneyResultData.chess` (`ChessResultSummary`).
  - `CelebrationSummary` new properties: `ChessScoringVersion, ChessMovesUsed, ChessMaxMoves, ChessOptimalMoves, ChessAllowedSeconds, ChessGradeLabel`.

- [ ] **Step 1: Write the failing tests**

Extend `ChessFinalSaveTests.cs` (it already builds `SaveData` via `SaveSystem`) with:

```csharp
[Test]
public void OldChessRecordWithoutNewFieldsLoadsAsLegacyAndKeepsItsValues()
{
    // A version-9 save written before this change: only the five original chess fields exist in the JSON.
    string json = "{\"version\":9,\"lives\":3,\"subjects\":[],\"tutorialSeen\":[],\"settings\":{}," +
        "\"journey\":{\"completedChallengeIds\":[\"sprint_learn\"],\"chessBest\":{\"recorded\":true,\"score\":7.5," +
        "\"thinkSeconds\":42.0,\"mistakes\":1,\"hintUsed\":true}}}";
    Directory.CreateDirectory(directory);
    File.WriteAllText(Path.Combine(directory, "save.json"), json);

    JourneyChessRecordData best = saveSystem.Load().journey.chessBest;

    Assert.That(best.recorded, Is.True);
    Assert.That(best.score, Is.EqualTo(7.5f));
    Assert.That(best.thinkSeconds, Is.EqualTo(42f));
    Assert.That(best.mistakes, Is.EqualTo(1));
    Assert.That(best.hintUsed, Is.True);
    Assert.That(best.scoringVersion, Is.EqualTo(0), "legacy data is marked, not guessed");
    Assert.That(best.movesUsed, Is.EqualTo(0));
    Assert.That(best.gradeLabel, Is.Null.Or.Empty);
}

[Test]
public void NewChessRecordRoundTripsEveryField()
{
    SaveData data = SaveData.CreateDefault();
    data.journey.chessBest = new JourneyChessRecordData
    {
        recorded = true, score = 9.2f, thinkSeconds = 31f, mistakes = 0, hintUsed = false,
        scoringVersion = ChessResultSummary.CurrentScoringVersion, movesUsed = 2, optimalMoves = 2, maxMoves = 3,
        allowedSeconds = 90, gradeLabel = "A+"
    };
    saveSystem.Save(data);
    JourneyChessRecordData back = saveSystem.Load().journey.chessBest;
    Assert.That(back.scoringVersion, Is.EqualTo(ChessResultSummary.CurrentScoringVersion));
    Assert.That(back.movesUsed, Is.EqualTo(2));
    Assert.That(back.optimalMoves, Is.EqualTo(2));
    Assert.That(back.maxMoves, Is.EqualTo(3));
    Assert.That(back.allowedSeconds, Is.EqualTo(90));
    Assert.That(back.gradeLabel, Is.EqualTo("A+"));
    Assert.That(back.score, Is.EqualTo(9.2f));
}

[Test]
public void LastCommittedResultKeepsTheChessSummary()
{
    var summary = new ChessResultSummary
    {
        scoringVersion = 1, movesUsed = 3, maxMoves = 3, optimalMoves = 2, allowedSeconds = 95, usedSeconds = 80.5f,
        endReason = "OutOfMoves", gradeLabel = "F", fen = "7k/8/8/8/8/8/R7/1R4K1 w - - 0 1", source = "Generated",
        seed = 12, estimatorVersion = 1,
        moves = new[] { new ChessMoveEntry { uci = "g1f1", q = 0.5f, best = 2, chosen = 4 } }
    };
    var result = new JourneyResultData
    {
        context = new JourneyAttemptData { attemptId = "a1", challengeId = "chess_final" },
        pass = false, chess = summary
    };
    JourneyResultData copy = result.Copy();
    Assert.That(copy.chess, Is.Not.SameAs(summary));
    Assert.That(copy.chess.moves.Length, Is.EqualTo(1));
    Assert.That(copy.chess.moves[0].uci, Is.EqualTo("g1f1"));
    Assert.That(copy.chess.endReason, Is.EqualTo("OutOfMoves"));
    Assert.That(copy.chess.fen, Is.EqualTo(summary.fen));
}

[Test]
public void OfferingAHigherScoreStoresTheNewMetadataAndALowerOneDoesNot()
{
    // Uses JourneyProgress exactly as production does; see the fixture helpers already in this file.
    JourneyProgress progress = NewJourneyAtChessFinal();
    CommitFinal(progress, score: 7.0f, grade: "B", movesUsed: 3);
    Assert.That(progress.ChessBest.gradeLabel, Is.EqualTo("B"));
    CommitFinal(progress, score: 6.0f, grade: "C", movesUsed: 3);
    Assert.That(progress.ChessBest.score, Is.EqualTo(7.0f), "a lower score never replaces the best");
    Assert.That(progress.ChessBest.gradeLabel, Is.EqualTo("B"));
    CommitFinal(progress, score: 9.5f, grade: "A+", movesUsed: 2);
    Assert.That(progress.ChessBest.gradeLabel, Is.EqualTo("A+"));
    Assert.That(progress.ChessBest.movesUsed, Is.EqualTo(2));
}
```

`NewJourneyAtChessFinal()` / `CommitFinal(...)` do not exist yet: model them on how `JourneyProgress` is driven elsewhere (search `JourneyProgress` usages in `Assets/Tests/EditMode/Progression/*.cs` for the "complete all other challenges, then commit the Final" pattern, e.g. `JourneyProgressTests`), and put them in this file as private helpers. `CommitFinal` builds `new ChallengeMetrics(elapsed: 30f, completedTargets: movesUsed, chess: new ChessResultSummary{ gradeLabel = grade, movesUsed = movesUsed, scoringVersion = 1, ... })` and an exam `MinigameResult(true, score, Rank.S)`.

Also add to `CelebrationSummaryTests.cs`: a summary built from a legacy record has `ChessScoringVersion == 0` and keeps `ChessThinkSeconds`/`ChessMistakes`/`ChessHintUsed`; one built from a new record exposes the new properties.

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Progression.ChessFinalSaveTests" chess-save` and `... "CelebrationSummaryTests" celebration-summary`. Expected: compile errors (`ChessResultSummary` etc. undefined).

- [ ] **Step 2: Add the data classes**

`ChessResultSummary.cs`:

```csharp
using System;

namespace KMA.Gameplay
{
    [Serializable]
    public sealed class ChessMoveEntry
    {
        public string uci;
        public float q;
        public int best;
        public int chosen;
    }

    /// Everything the result screens and the save need about one chess attempt (spec sections 7 and 9).
    [Serializable]
    public sealed class ChessResultSummary
    {
        public const int CurrentScoringVersion = 1;

        public int scoringVersion;
        public int movesUsed;
        public int maxMoves;
        public int optimalMoves;
        public int allowedSeconds;
        public float usedSeconds;
        public bool hintUsed;
        public bool explicitHintUsed;
        public bool hintCapped;
        /// Mate, TimeUp, OutOfMoves, Mated or Draw.
        public string endReason;
        /// F, D, C, B, A or A+.
        public string gradeLabel;
        public float score;
        public string fen;
        public string source;
        public int seed;
        public int estimatorVersion;
        public ChessMoveEntry[] moves = Array.Empty<ChessMoveEntry>();

        public ChessResultSummary Copy()
        {
            var copy = (ChessResultSummary)MemberwiseClone();
            copy.moves = new ChessMoveEntry[moves?.Length ?? 0];
            for (int i = 0; i < copy.moves.Length; i++)
                copy.moves[i] = new ChessMoveEntry
                    { uci = moves[i].uci, q = moves[i].q, best = moves[i].best, chosen = moves[i].chosen };
            return copy;
        }
    }
}
```

- [ ] **Step 3: Wire the metrics, record and result data**

1. `ChallengeMetrics`: add constructor parameter `ChessResultSummary chess = null` as the last parameter, store it, expose `public ChessResultSummary Chess { get; }`.
2. `JourneyChessRecordData` add fields `scoringVersion, movesUsed, optimalMoves, maxMoves, allowedSeconds, gradeLabel` and include them in `Copy()` (object initializer lists all fields).
3. `JourneyResultData`: add `public ChessResultSummary chess;`, set it in `FromResult` from `metrics.Chess?.Copy()`, copy it in `Copy()` and in `ToResult()` pass `new ChallengeMetrics(..., chess: chess?.Copy())` (read the existing `ToResult` body and keep every other argument exactly as it is).
4. `JourneyProgress.OfferChessResult(ChallengeMetrics metrics, float score)`: when storing, also set `scoringVersion = metrics.Chess?.scoringVersion ?? 0`, `movesUsed`, `optimalMoves`, `maxMoves`, `allowedSeconds`, `gradeLabel` from `metrics.Chess` (all `0`/`null` when `metrics.Chess` is null), keeping the existing `recorded/score/thinkSeconds/mistakes/hintUsed` lines. The "higher score only" guard stays as is.
5. `JourneyProgress` load block (~line 270): the `new JourneyChessRecordData { ... }` literal there must copy the six new fields with `Math.Max(0, …)` for ints and the string as is.
6. `JourneySaveMigration` already uses `source.chessBest?.Copy()` (line ~80) so it needs no change; confirm with the round-trip test.
7. `CelebrationSummary.From`: copy the six new fields from `session.Journey.ChessBest` into the new properties; `Sample()` sets a new-style sample (`ChessScoringVersion = 1`, `ChessMovesUsed = 2`, `ChessMaxMoves = 3`, `ChessOptimalMoves = 2`, `ChessAllowedSeconds = 90`, `ChessGradeLabel = "A+"`) so the editor preview shows the new line.

- [ ] **Step 4: Finish Task 1's deferred use**

In `ResultPanel.cs` line ~281 use `result.Metrics?.Chess != null` in the `RankLabels.For` call as noted in Task 1 Step 3.

- [ ] **Step 5: Run and commit**

Run the two filters from Step 1 plus the whole Progression EditMode suite: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Progression" progression-edit`. Expected: `result=Passed`.

```bash
git add Assets/_Project/Scripts/Progression Assets/Tests/EditMode/Progression Assets/_Project/Scripts/UI/ResultPanel.cs
git commit -m "feat(chess): carry the attempt summary through metrics, results and the saved best record"
```

---

### Task 3: Result text and panel for the new attempt

**Files:**
- Create: `Assets/_Project/Scripts/Progression/Journey/ChessResultText.cs`
- Create: `Assets/Tests/EditMode/Progression/ChessResultTextTests.cs`
- Modify: `Assets/_Project/Scripts/Gameplay/Chess/ChessFinalController.cs` (`BuildMetrics`, `Detail`)
- Modify: `Assets/_Project/Scripts/UI/ResultPanel.cs` (Final detail path near line 288; detail label wrapping)
- Modify: `Assets/Tests/PlayMode/Gameplay/Chess/ChessFinalSceneTests.cs`

**Interfaces:**
- Consumes: `ChessResultSummary`, `ChessMoveEntry` (Task 2), `ChessAttemptMachine` (Plan 2).
- Produces:
  - `static class ChessResultText { static string Detail(ChessResultSummary s); static string EndReasonText(string endReason); static string MoveLines(ChessResultSummary s, int max); static string FormatClock(float seconds) }`
  - `ChessFinalController.BuildSummary()` → `ChessResultSummary`.

- [ ] **Step 1: Write the failing tests**

```csharp
using KMA.Gameplay;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Progression
{
    public sealed class ChessResultTextTests
    {
        static ChessResultSummary Win() => new ChessResultSummary
        {
            scoringVersion = 1, movesUsed = 2, maxMoves = 3, optimalMoves = 2, allowedSeconds = 95, usedSeconds = 41.2f,
            endReason = "Mate", gradeLabel = "A+", score = 10f,
            moves = new[]
            {
                new ChessMoveEntry { uci = "b1b7", q = 1f, best = 2, chosen = 2 },
                new ChessMoveEntry { uci = "a2a8", q = 1f, best = 1, chosen = 1 }
            }
        };

        [Test]
        public void SuccessLineShowsMovesOptimalTimeAndHintState()
        {
            string text = ChessResultText.Detail(Win());
            Assert.That(text, Does.Contain("Nước: 2/3"));
            Assert.That(text, Does.Contain("Tối ưu: 2"));
            Assert.That(text, Does.Contain("00:41/01:35"));
            Assert.That(text, Does.Contain("Không dùng gợi ý"));
            Assert.That(text, Does.Not.Contain("sửa"));
        }

        [Test]
        public void HintStatesAreDistinguished()
        {
            ChessResultSummary general = Win();
            general.hintUsed = true;
            Assert.That(ChessResultText.Detail(general), Does.Contain("Có dùng gợi ý"));
            ChessResultSummary capped = Win();
            capped.hintUsed = true; capped.explicitHintUsed = true; capped.hintCapped = true;
            Assert.That(ChessResultText.Detail(capped), Does.Contain("tối đa A"));
        }

        [TestCase("TimeUp", "Hết giờ")]
        [TestCase("OutOfMoves", "Hết số nước")]
        [TestCase("Mated", "Bị chiếu hết")]
        [TestCase("Draw", "Hòa cờ")]
        [TestCase("Mate", "Chiếu hết")]
        public void EndReasonsAreReadable(string reason, string expected) =>
            Assert.That(ChessResultText.EndReasonText(reason), Is.EqualTo(expected));

        [Test]
        public void FailureListsTheMovesPlayedWithTheirQualityAndNoScoreClaim()
        {
            ChessResultSummary s = Win();
            s.endReason = "OutOfMoves"; s.gradeLabel = "F"; s.score = 0f;
            s.moves = new[]
            {
                new ChessMoveEntry { uci = "g1f1", q = 0.5f, best = 2, chosen = 4 },
                new ChessMoveEntry { uci = "f1g1", q = 0f, best = 2, chosen = 0 }
            };
            string text = ChessResultText.Detail(s);
            Assert.That(text, Does.Contain("Hết số nước"));
            Assert.That(text, Does.Contain("g1f1"));
            Assert.That(text, Does.Contain("50%"));
            Assert.That(text, Does.Contain("0%"));
        }

        [Test]
        public void MoveLinesAreCappedAtTheRequestedCount()
        {
            var s = new ChessResultSummary { moves = new ChessMoveEntry[5] };
            for (int i = 0; i < 5; i++) s.moves[i] = new ChessMoveEntry { uci = "a1a" + (i + 2), q = 1f };
            Assert.That(ChessResultText.MoveLines(s, 3).Split('\n').Length, Is.EqualTo(4), "3 lines plus the ellipsis line");
        }

        [Test]
        public void NullSummaryYieldsEmptyText() => Assert.That(ChessResultText.Detail(null), Is.Empty);

        [TestCase(0f, "00:00")]
        [TestCase(59.4f, "00:59")]
        [TestCase(95f, "01:35")]
        public void ClockIsMinutesAndSeconds(float seconds, string expected) =>
            Assert.That(ChessResultText.FormatClock(seconds), Is.EqualTo(expected));
    }
}
```

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Progression.ChessResultTextTests" chess-result-text`. Expected: compile error.

- [ ] **Step 2: Implement `ChessResultText`**

```csharp
using System.Globalization;
using System.Text;

namespace KMA.Gameplay
{
    public static class ChessResultText
    {
        const int MaxMoveLines = 5;

        public static string Detail(ChessResultSummary s)
        {
            if (s == null) return string.Empty;
            var text = new StringBuilder();
            text.Append("Nước: ").Append(s.movesUsed).Append('/').Append(s.maxMoves)
                .Append(" · Tối ưu: ").Append(s.optimalMoves)
                .Append(" · ").Append(FormatClock(s.usedSeconds)).Append('/').Append(FormatClock(s.allowedSeconds));
            text.Append('\n').Append(EndReasonText(s.endReason)).Append(" · ").Append(HintText(s));
            string lines = MoveLines(s, MaxMoveLines);
            if (lines.Length > 0 && s.endReason != "Mate") text.Append('\n').Append(lines);
            return text.ToString();
        }

        public static string EndReasonText(string reason) => reason switch
        {
            "Mate" => "Chiếu hết",
            "TimeUp" => "Hết giờ",
            "OutOfMoves" => "Hết số nước",
            "Mated" => "Bị chiếu hết",
            "Draw" => "Hòa cờ",
            _ => string.Empty
        };

        public static string MoveLines(ChessResultSummary s, int max)
        {
            if (s?.moves == null || s.moves.Length == 0) return string.Empty;
            var text = new StringBuilder();
            for (int i = 0; i < s.moves.Length && i < max; i++)
            {
                if (i > 0) text.Append('\n');
                text.Append(i + 1).Append(". ").Append(s.moves[i].uci).Append(" · ")
                    .Append(Percent(s.moves[i].q));
            }
            if (s.moves.Length > max) text.Append("\n…");
            return text.ToString();
        }

        public static string FormatClock(float seconds)
        {
            int total = seconds <= 0f ? 0 : (int)seconds;
            return $"{total / 60:00}:{total % 60:00}";
        }

        static string HintText(ChessResultSummary s) =>
            s.hintCapped ? "Gợi ý chỉ rõ nước: hạng tối đa A" :
            s.hintUsed ? "Có dùng gợi ý" : "Không dùng gợi ý";

        static string Percent(float q) => ((int)System.Math.Round(q * 100f)).ToString(CultureInfo.InvariantCulture) + "%";
    }
}
```

Note the moves line shows each move's quality as a percentage (`q`), which is the spec's `d_best/d_chosen`; do not label it "độ chính xác" in code or UI to avoid implying engine-strength claims (spec §1 last paragraph).

- [ ] **Step 3: Build the summary in the controller**

In `ChessFinalController` replace the temporary `BuildMetrics`/`Detail` from Plan 2 with:

```csharp
ChessResultSummary BuildSummary() => new ChessResultSummary
{
    scoringVersion = ChessResultSummary.CurrentScoringVersion,
    movesUsed = machine.MovesUsed,
    maxMoves = machine.MaxMoves,
    optimalMoves = machine.OptimalMoves,
    allowedSeconds = Mathf.RoundToInt(machine.Clock.Limit),
    usedSeconds = machine.Clock.Elapsed,
    hintUsed = machine.HintUsed,
    explicitHintUsed = machine.ExplicitHintUsed,
    hintCapped = machine.Result.HintCapped,
    endReason = machine.EndReason.ToString(),
    gradeLabel = ChessAttemptGrader.Label(machine.Result.Grade),
    score = machine.Result.Score,
    fen = machine.Snapshot.fen,
    source = machine.Snapshot.source,
    seed = machine.Snapshot.seed,
    estimatorVersion = machine.Snapshot.estimatorVersion,
    moves = machine.Moves.Select(m => new ChessMoveEntry
        { uci = m.Uci, q = (float)m.Q, best = m.BestDistance, chosen = m.ChosenDistance }).ToArray()
};

public ChallengeMetrics BuildMetrics(ChallengeDefinition definition, MinigameResult result)
{
    ChessResultSummary summary = BuildSummary();
    return new ChallengeMetrics(elapsed: machine.Clock.Elapsed, completedTargets: machine.MovesUsed,
        mistakes: machine.Moves.Count(m => m.Q < 0.999), hintUsed: machine.HintUsed,
        detail: ChessResultText.Detail(summary), chess: summary);
}
```

- [ ] **Step 4: Show it in the result panel**

In `ResultPanel.cs` near line 288, the Final branch currently shows `result.Metrics.Detail`; change the condition to prefer the structured text:

```csharp
: saveError == null && final && result.Metrics.Chess != null
    ? ChessResultText.Detail(result.Metrics.Chess)
    : saveError == null && final && !string.IsNullOrEmpty(result.Metrics.Detail)
        ? result.Metrics.Detail
```

(keep the rest of the chain unchanged). Read `SetDetail` (line ~129) and the detail label in the scene prefab: if the label is single-line or auto-sized to one line, enable wrapping (`textWrappingMode = Normal`) and `enableAutoSizing` with a minimum size in `ResultPanel.ApplyTheme`, then verify with the screenshot step below.

- [ ] **Step 5: PlayMode check and screenshot**

Add to `ChessFinalSceneTests.cs`: `ResultDetailListsMovesAndTimeAfterAWin` (play the optimal line as in Plan 2, then assert the `ResultPanel` detail label text contains `Nước:`, `Tối ưu:` and the score label shows `10.0` or the formatted score) and `ChessRankLabelShowsAPlus` (rank label contains `A+` after a perfect win, using the panel's rank label text). Then follow the `testing-unity-ui-with-screenshots` skill to capture the result panel for a win (A+) and a failure (F with move lines) at the target frame; confirm no text clipping; fix label sizing if needed and recapture. Save captures under `docs/qa/` as that skill directs.

- [ ] **Step 6: Run and commit**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Progression" progression-edit` and `tools/run-unity-tests.sh PlayMode "KMA.Tests.Gameplay.Chess" chess-play`. Expected: pass.

```bash
git add Assets docs/qa
git commit -m "feat(chess): show moves, optimal count, time, hints and move quality on the result screen"
```

---

### Task 4: Journey retry reuses the same puzzle

**Files:**
- Modify: `Assets/_Project/Scripts/Progression/Journey/ChallengeAttempt.cs` (add `ChallengeRetryIntent`)
- Create: `Assets/_Project/Scripts/Gameplay/Chess/ChessRetryStore.cs`
- Modify: `Assets/_Project/Scripts/Core/SceneRouter.Journey.cs` (retry branch at ~line 263)
- Modify: `Assets/_Project/Scripts/Gameplay/Chess/ChessFinalController.cs` (`Boot`, `OnFinished`)
- Create: `Assets/Tests/EditMode/Gameplay/Chess/ChessRetryStoreTests.cs`
- Modify: `Assets/Tests/PlayMode/Progression/ChessFinalRoutingTests.cs`

**Interfaces:**
- Produces:
  - `static class ChallengeRetryIntent { static void Arm(string challengeId); static bool Consume(string challengeId); static void Clear(); }` (main thread only)
  - `static class ChessRetryStore { static void Remember(ChessPuzzleSnapshot failed); static void Forget(); static ChessPuzzleSnapshot TakeIfRetry(bool retryRequested); static bool HasSnapshot { get; } }`

- [ ] **Step 1: Write the failing EditMode tests**

```csharp
using KMA.Gameplay;
using KMA.Gameplay.Chess;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessRetryStoreTests
    {
        static ChessPuzzleSnapshot Snap(string fen) => new ChessPuzzleSnapshot { fen = fen, level = "Normal", maxMoves = 3, optimalMoves = 2, timeSeconds = 90 };

        [SetUp]
        public void SetUp()
        {
            ChessRetryStore.Forget();
            ChallengeRetryIntent.Clear();
        }

        [Test]
        public void RetryReturnsTheRememberedSnapshotOnceThenForgetsIt()
        {
            ChessRetryStore.Remember(Snap("fen-1"));
            ChessPuzzleSnapshot first = ChessRetryStore.TakeIfRetry(true);
            Assert.That(first.fen, Is.EqualTo("fen-1"));
            Assert.That(ChessRetryStore.TakeIfRetry(true), Is.Null);
        }

        [Test]
        public void NotARetryIgnoresAndClearsTheStore()
        {
            ChessRetryStore.Remember(Snap("fen-1"));
            Assert.That(ChessRetryStore.TakeIfRetry(false), Is.Null);
            Assert.That(ChessRetryStore.HasSnapshot, Is.False);
        }

        [Test]
        public void ReturnedSnapshotIsACopyOfTheStoredOne()
        {
            var original = Snap("fen-1");
            ChessRetryStore.Remember(original);
            original.fen = "mutated";
            Assert.That(ChessRetryStore.TakeIfRetry(true).fen, Is.EqualTo("fen-1"));
        }

        [Test]
        public void IntentMatchesOnlyItsOwnChallengeAndIsConsumedOnce()
        {
            ChallengeRetryIntent.Arm("chess_final");
            Assert.That(ChallengeRetryIntent.Consume("sprint_exam"), Is.False);
            Assert.That(ChallengeRetryIntent.Consume("chess_final"), Is.True, "a mismatched consume must not clear it");
            Assert.That(ChallengeRetryIntent.Consume("chess_final"), Is.False);
        }

        [Test]
        public void ClearDisarms()
        {
            ChallengeRetryIntent.Arm("chess_final");
            ChallengeRetryIntent.Clear();
            Assert.That(ChallengeRetryIntent.Consume("chess_final"), Is.False);
        }

        [Test]
        public void RememberNullClears()
        {
            ChessRetryStore.Remember(Snap("fen-1"));
            ChessRetryStore.Remember(null);
            Assert.That(ChessRetryStore.HasSnapshot, Is.False);
        }
    }
}
```

The test assembly `KMA.Gameplay.Chess.EditMode.Tests` already references `KMA.Gameplay.Progression` (where `ChallengeRetryIntent` lives) and `KMA.Gameplay.Chess`. Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Chess.ChessRetryStoreTests" chess-retry`; expect a compile error.

- [ ] **Step 2: Implement**

Append to `ChallengeAttempt.cs`:

```csharp
/// A one-shot hint from the router to the next scene: "this start is a retry of that challenge".
public static class ChallengeRetryIntent
{
    static string armedChallengeId;

    public static void Arm(string challengeId) => armedChallengeId = challengeId;

    public static void Clear() => armedChallengeId = null;

    public static bool Consume(string challengeId)
    {
        if (string.IsNullOrEmpty(challengeId) || armedChallengeId != challengeId) return false;
        armedChallengeId = null;
        return true;
    }
}
```

`ChessRetryStore.cs`:

```csharp
using UnityEngine;

namespace KMA.Gameplay.Chess
{
    /// Keeps the puzzle of a failed Final attempt in memory so "retry" repeats it. It is not saved: an unfinished or
    /// failed puzzle is not resumed after the app is closed (spec section 9).
    public static class ChessRetryStore
    {
        static ChessPuzzleSnapshot remembered;

        public static bool HasSnapshot => remembered != null;

        public static void Remember(ChessPuzzleSnapshot failed) =>
            remembered = failed == null ? null : JsonUtility.FromJson<ChessPuzzleSnapshot>(JsonUtility.ToJson(failed));

        public static void Forget() => remembered = null;

        /// The remembered puzzle when this start is a retry, otherwise null. Either way the store is emptied.
        public static ChessPuzzleSnapshot TakeIfRetry(bool retryRequested)
        {
            ChessPuzzleSnapshot taken = retryRequested ? remembered : null;
            remembered = null;
            return taken;
        }
    }
}
```

- [ ] **Step 3: Arm the intent in the router**

In `SceneRouter.Journey.cs` branch (~line 263) replace

```csharp
if (!TryStartChallenge(displayedChallenge.ChallengeId, displayedChallenge.Mode,
    displayedChallenge.Difficulty)) RestoreChallengeActions();
```

with

```csharp
ChallengeRetryIntent.Arm(displayedChallenge.ChallengeId);
if (!TryStartChallenge(displayedChallenge.ChallengeId, displayedChallenge.Mode,
    displayedChallenge.Difficulty))
{
    ChallengeRetryIntent.Clear();
    RestoreChallengeActions();
}
```

and in `TryStartChallenge(string id, ChallengeAttemptMode mode, ChallengeDifficulty difficulty, SaveData restartSnapshot)` leave the body alone; a start that did not come through the retry branch never arms the intent, and the Chess controller consumes the intent only for `chess_final`, so a stale arm for another challenge is harmless.

- [ ] **Step 4: Use it in the controller**

In `ChessFinalController.Boot()` (Plan 2) after resolving `level` and the context:

```csharp
string challengeId = adapter?.Context?.ChallengeId;
bool retry = ChallengeRetryIntent.Consume(challengeId);
ChessPuzzleSnapshot reuse = ChessRetryStore.TakeIfRetry(retry);
if (reuse != null) { lastFen = reuse.fen; StartAttempt(reuse); yield break; }
```

before creating the provider. In `OnFinished(bool solved)`: `if (solved) ChessRetryStore.Forget(); else ChessRetryStore.Remember(machine.Snapshot);`. The in-scene "Chơi lại đề" button (Plan 2) already restarts the same snapshot without the store.

- [ ] **Step 5: PlayMode routing tests**

In `ChessFinalRoutingTests.cs` add (using the fixtures already in that file for starting `chess_final` through `SceneRouter`):

- `FailingTheFinalThenRetryingKeepsTheSameFenLimitsAndTime`: start the Final, record `Machine.Snapshot` (fen, maxMoves, optimalMoves, timeSeconds), force a failure (`Machine.Tick(1000f)` after `Begin`), take the retry action through the result panel (`JourneyResultAction.Retry`), wait for the new `ChessFinalController`, assert the same four values and `MovesUsed == 0`.
- `StartingFreshAfterAFailureGeneratesANewPuzzle`: fail, go back to the map via `Continue`'s non-retry route if one exists, start the Final again from the map, assert the controller consumed no retry (`ChessRetryStore.HasSnapshot == false` before boot) — if the fresh start can produce the same FEN by chance, run the generation with a different seed by asserting `Machine.Snapshot.seed != previous.seed` instead.
- `FailedRetryStartDoesNotLeaveTheIntentArmed`: make `TryStartChallenge` fail (e.g. with a pending frog jump) and assert `ChallengeRetryIntent.Consume("chess_final")` is false afterwards.
- `PassingClearsTheStore`.

Read the existing tests in the file first and reuse their setup helpers; do not invent routing hooks. Run: `tools/run-unity-tests.sh PlayMode "KMA.Tests.Progression.ChessFinalRoutingTests" chess-routing` (use the real namespace). Expected: pass.

- [ ] **Step 6: Commit**

```bash
git add Assets
git commit -m "feat(chess): Journey retry repeats the failed puzzle, fresh starts get a new one"
```

---

### Task 5: Course summary and celebration use the new record

**Files:**
- Modify: `Assets/_Project/Scripts/Gameplay/Celebration/CelebrationSceneController.cs` (row 3 text near line 126)
- Modify: `Assets/_Project/Scripts/Progression/Journey/CelebrationSummary.cs` (helper `ChessLine`)
- Modify: `Assets/Tests/EditMode/Progression/CelebrationSummaryTests.cs`
- Modify: `Assets/Tests/PlayMode/Gameplay/Celebration/*` tests that assert the chess row text (search `Sai:` and `Không dùng gợi ý`)

**Interfaces:**
- Consumes: `CelebrationSummary.ChessScoringVersion/ChessMovesUsed/ChessMaxMoves/ChessOptimalMoves/ChessAllowedSeconds/ChessGradeLabel` (Task 2), `ChessResultText.FormatClock`.
- Produces: `CelebrationSummary.ChessLine` (`string`), used by the controller.

- [ ] **Step 1: Write the failing tests** in `CelebrationSummaryTests.cs`:

```csharp
[Test]
public void NewChessRecordLineShowsGradeMovesAndTime()
{
    CelebrationSummary s = CelebrationSummary.Sample(); // new-style sample from Task 2
    Assert.That(s.ChessLine, Does.Contain("Bài kiểm tra cuối"));
    Assert.That(s.ChessLine, Does.Contain("A+"));
    Assert.That(s.ChessLine, Does.Contain("2/3"));
    Assert.That(s.ChessLine, Does.Not.Contain("Sai:"));
}

[Test]
public void LegacyChessRecordKeepsTheOldLineAndDoesNotInventAGrade()
{
    // Build a summary from a legacy record exactly as production does (see the helper used by the existing tests).
    CelebrationSummary s = SummaryFromLegacyRecord(score: 7.5f, think: 42f, mistakes: 1, hint: true);
    Assert.That(s.ChessScoringVersion, Is.EqualTo(0));
    Assert.That(s.ChessLine, Does.Contain("00:42"));
    Assert.That(s.ChessLine, Does.Contain("Sai: 1"));
    Assert.That(s.ChessLine, Does.Contain("Có dùng gợi ý"));
    Assert.That(s.ChessLine, Does.Not.Contain("A+"));
}

[Test]
public void UnrecordedChessShowsOnlyTheTitle()
{
    CelebrationSummary s = SummaryFromUnrecordedChess();
    Assert.That(s.ChessLine, Is.EqualTo("Đạt  " + CelebrationSummary.ChessTitle));
}
```

`SummaryFromLegacyRecord`/`SummaryFromUnrecordedChess` follow how the existing tests in that file create a `GameSession` with a chess record; copy that setup.

- [ ] **Step 2: Implement `ChessLine`** in `CelebrationSummary`:

```csharp
public string ChessLine
{
    get
    {
        if (!ChessRecorded) return $"Đạt  {ChessTitle}";
        if (ChessScoringVersion >= 1 && !string.IsNullOrEmpty(ChessGradeLabel))
            return $"Đạt  {ChessTitle}  ·  Hạng {ChessGradeLabel}  ·  Nước {ChessMovesUsed}/{ChessMaxMoves}" +
                   $"  ·  {FormatClock(ChessThinkSeconds)}/{FormatClock(ChessAllowedSeconds)}" +
                   $"  ·  {(ChessHintUsed ? "Có dùng gợi ý" : "Không dùng gợi ý")}";
        return $"Đạt  {ChessTitle}  ·  {FormatClock(ChessThinkSeconds)}  ·  Sai: {ChessMistakes}" +
               $"  ·  {(ChessHintUsed ? "Có dùng gợi ý" : "Không dùng gợi ý")}";
    }
}
```

In `CelebrationSceneController.FillSummary` replace the `rows[3].text = ...` expression with `VietText.Fix(Summary.ChessLine)`. Do not change the three sports rows (they keep `Hạng {row.Rank}`).

- [ ] **Step 3: Check fit**: the chess row is longer than before. Follow `testing-unity-ui-with-screenshots` to capture the celebration screen with `Sample()` and with a legacy-style summary; if the row overflows, reduce wording (drop "Hạng" label first), not font size below the existing Shell token. Re-capture.

- [ ] **Step 4: Run and commit**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Progression.CelebrationSummaryTests" celebration-edit` and `tools/run-unity-tests.sh PlayMode "KMA.Tests.Gameplay.Celebration" celebration-play`. Expected: pass.

```bash
git add Assets docs/qa
git commit -m "feat(chess): celebration and summary show the new chess record with a legacy fallback"
```

---

### Task 6: Remove stale "90 giây / sửa sai / 2 nước" copy

**Files (find with the search below; known candidates):**
- Modify: `Assets/Editor/StudentJourneyContentBuilder.cs` (`chess_intro`, `chess_final` lines) and regenerate `Assets/_Project/Resources/Journey/JourneyDialogues.asset`
- Modify: `Assets/_Project/Scripts/UI/JourneyLessonPresentation.cs`, `JourneyLessonList.cs`, `MapStopBuilder.cs`, `MapNodeView.cs` if they carry chess copy
- Modify: `Chay-tron-the-chat-loi-thoai-bien-tap.md`, `PLAN.md`, `README.md`
- Modify: tests that assert the old strings (`JourneyDialogueDataTests`, `DialogueJourneyAcceptanceTests`, `JourneyRoutingTests`, `StudentJourneyFlowTests`, `FullGameplayFlowTests`, `ChessFinalRoutingTests`)
- Create: `Assets/Tests/EditMode/Progression/ChessCopyAuditTests.cs`

- [ ] **Step 1: Find every occurrence**

Run:
`grep -rn "90 giây\|hai lần sửa\|2 lần sửa\|sửa sai\|chiếu hết trong 2 nước\|Chiếu hết trong 2\|hai nước" Assets/_Project Assets/Editor Assets/Tests PLAN.md README.md Chay-tron-the-chat-loi-thoai-bien-tap.md docs/superpowers/specs docs/qa --include=* | grep -v "\.meta\|Binary"` and list each hit with its file. Specs and past QA reports under `docs/` are history and stay unchanged; everything else that describes the chess exam must be updated.

- [ ] **Step 2: Write the failing audit test**

```csharp
using System.IO;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Progression
{
    public sealed class ChessCopyAuditTests
    {
        static readonly string[] Stale = { "90 giây", "hai lần sửa", "2 lần sửa", "sửa sai", "Sai quá" };

        [Test]
        public void ShippedChessCopyHasNoFixedTimeOrMistakeAllowance()
        {
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            string[] files =
            {
                "Assets/_Project/Scripts/Gameplay/Chess/ChessGuide.cs",
                "Assets/_Project/Scripts/Gameplay/Chess/ChessFinalController.cs",
                "Assets/_Project/Scripts/Gameplay/Chess/ChessFinalHud.cs",
                "Assets/Editor/GameplayDialogueContentBuilder.cs",
                "Assets/Editor/StudentJourneyContentBuilder.cs"
            };
            foreach (string file in files)
            {
                string text = File.ReadAllText(Path.Combine(root, file));
                foreach (string phrase in Stale)
                    Assert.That(text, Does.Not.Contain(phrase), $"{file} still says \"{phrase}\"");
            }
        }
    }
}
```

If `StudentJourneyContentBuilder.cs` legitimately uses one of those phrases for a different (non-chess) exam, narrow the check to the chess lines by reading the `chess_intro`/`chess_final` blocks only instead of whole-file `Contains`. Run it: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Progression.ChessCopyAuditTests" chess-copy`. Expected: fails on the Journey intro lines.

- [ ] **Step 3: Rewrite the copy**

- `chess_intro.03` (`Chay-tron-the-chat-loi-thoai-bien-tap.md` line ~114): `Mỗi thế cờ có giới hạn số nước và thời gian riêng. Đọc đề cho kỹ rồi hãy đi.`; the badge `90 GIÂY` becomes `THẾ CỜ MỚI` (and the same strings in `StudentJourneyContentBuilder.cs`).
- `chess_intro.01`: `Bài cuối là một thế cờ. Chiếu hết trong số nước cho phép. Lần này dùng đầu, không dùng chân.`
- `chess_final` mission lines (md lines ~153 and ~170): `Chiếu hết trong số nước cho phép, trong thời gian của thế cờ.` / `... Trượt bài này không bị trừ lượt thi và không phải Bật cóc.`
- `PLAN.md` section 3 table row: `trạm thứ 4: chiếu hết thế cờ do AI sinh theo độ khó (Dễ 1/2, Vừa 2/3, Khó 3/5 nước tối ưu/tối đa), thời gian theo thế cờ; chấm F–A+; không trừ lượt, không Bật cóc`.
- `README.md`: update any line describing the chess exam.
- Regenerate `JourneyDialogues.asset` by running the content builder's menu/execute method (find it with `grep -n "MenuItem" Assets/Editor/StudentJourneyContentBuilder.cs`) and verify `git diff` only touches the chess entries.
- Update each test found in Step 1 that asserted the old strings so it asserts the new strings.

- [ ] **Step 4: Run and commit**

Run the audit test, then `tools/run-unity-tests.sh EditMode "KMA.Tests" editmode-all` and `tools/run-unity-tests.sh PlayMode "KMA.Tests" playmode-all`. Expected: `result=Passed`.

```bash
git add -A Assets PLAN.md README.md Chay-tron-the-chat-loi-thoai-bien-tap.md
git commit -m "docs(chess): update exam copy for generated puzzles, move limits and per-puzzle time"
```

---

### Task 7: Verification, performance and honest QA report

**Files:**
- Create: `docs/qa/2026-10-13-chess-ai-final.md`
- Modify: `docs/qa/2026-10-13-chess-generator-performance.md` (add the final numbers)

- [ ] **Step 1: Full regression** — run, with the Editor closed, each of: `tools/run-unity-tests.sh EditMode "" editmode-all`, `tools/run-unity-tests.sh PlayMode "" playmode-all`. Record the four summary lines (`result/total/passed/failed`) for each. A failure anywhere is reported verbatim and fixed or escalated; do not mark the work done on a red suite.

- [ ] **Step 2: Generator measurement on the final build** — run the explicit measurement test from Plan 1 again (`ChessGeneratorMeasurement`, 30 seeds per level) and the runtime analysis timing: in a PlayMode test or a scratch script, time `ChessTurnAnalyzer.Analyze` for the first move of 30 generated puzzles per level under the default budget (2 s / 200k nodes) and count how many return `Unknown`. Record per level: Generated %, Fallback %, mean/worst generation time, mean/worst analysis time, Unknown count. Spec target: >= 80% Generated per level and no Unknown at default budget for the optimal moves; if not met, say so and open a tuning note (do not describe a fallback-heavy result as a pass).

- [ ] **Step 3: UI responsiveness** — in a PlayMode test, run a Hard generation while sampling `Time.unscaledDeltaTime` over 120 frames in the scene; record the worst frame time. Confirm by code search that no `UnityEngine` API is used under `Assets/_Project/Scripts/Gameplay/Chess/Core/` (`grep -rn "UnityEngine" Assets/_Project/Scripts/Gameplay/Chess/Core` must return nothing) and that `ChessAnalysisWorker`/`ChessPuzzleProvider` pass only strings/structs to the pool thread.

- [ ] **Step 4: Interactive playthrough in the real scene** (skills `run` and `testing-unity-ui-with-screenshots`): open `Bootstrap`, start a New Game and reach the chess Final via the Journey (or the project's debug route used by `FullGameplayFlowTests`), then on one run for each difficulty available through the Journey context (Easy/Normal/Hard if the catalog can supply them; otherwise state which were not reachable) do: read intro (moves limit and time shown), play a non-optimal move (confirm it stays on the board and the counter advances), request all three hints, finish with a mate, check the result screen (grade, A+, breakdown), then fail once on purpose (run out of moves) and press CHƠI LẠI to confirm the same position returns. Capture screenshots for intro, mid-game, hint, analysis-error (force by temporarily lowering `SearchBudget.DefaultNodes` in a local, uncommitted edit), win result, fail result, celebration row.

- [ ] **Step 5: Write `docs/qa/2026-10-13-chess-ai-final.md`** with: what changed (links to the three plans), every command run with its output summary, the numbers from Steps 2–3, screenshots (paths), a list of what was verified in the Editor only, and an explicit **Not verified** section: Android device behaviour and performance, IL2CPP thread behaviour on device, long-session memory. If a device build is wanted next, the existing `tools/build-apk.*` scripts are the route; this plan does not claim it.

- [ ] **Step 6: Commit and report**

```bash
git add docs/qa
git commit -m "docs(qa): record chess AI verification, generator numbers and remaining gaps"
```

Do not push unless the user asks. Report separately: tests, build (none run unless Step 1 included one), screenshots, device checks (not done), commits.

---

## Self-Review (Plan 3)

- **Spec coverage:** §7 result content and A+ formatter in direct scene, Journey result, course summary and celebration (Tasks 1, 3, 5); §9 snapshot/record/migration/retry/no-resume/commit-once (Tasks 2, 4); §10 PlayMode retry/context/F–A+ display and performance report (Tasks 4, 7); §11 honesty about Android and Unknown (Task 7). Copy fixes from the user-facing consequence of the spec (Task 6) go beyond the spec text but follow from "không mô tả sai".
- **Placeholder scan:** Tasks 2, 4 and 5 reference existing test helpers ("copy the setup used by neighbouring tests") instead of inventing fixture code because those fixtures are not visible from the current exploration; the implementer must read the named neighbour tests first. Everything else has concrete code.
- **Type consistency:** `ChessResultSummary` fields, `ChessMoveEntry`, `JourneyChessRecordData` new fields, `ChallengeMetrics.Chess`, `ChessResultText.Detail/FormatClock`, `RankLabels.For`, `ChallengeRetryIntent`, `ChessRetryStore` use identical names across tasks. `gradeLabel` strings come from `ChessAttemptGrader.Label` (Plan 1).
- **Risks:** no save-version bump relies on `JsonUtility` ignoring unknown/missing fields (covered by the legacy-load test); the router change touches shared journey code and is guarded by an intent keyed to the challenge id; result-panel text fit needs screenshots (Tasks 3, 5).
