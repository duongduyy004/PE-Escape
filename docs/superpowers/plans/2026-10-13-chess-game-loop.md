# Chess Game Loop Implementation Plan (Plan 2 of 3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the fixed-answer chess exam with a real game against the AI: generated puzzle, move budget, think-time clock, per-move proof-based grading, hints computed from the live board, and an opponent that defends.

**Architecture:** A pure C# `ChessAttemptMachine` (Core) owns the board, turn, clock, history and result. It never searches itself: it emits `AnalysisRequest`/`HintRequest`, a background `ChessAnalysisWorker` runs `ChessTurnAnalyzer`/`ChessHintBuilder` on immutable data, and the controller feeds results back on the main thread. Stale results are dropped by request id; pause buffers a result instead of applying it. The legacy `PuzzleDefinition`/`PuzzleGrader`/`PuzzleVerifier`/`ChessFinalStateMachine` chain is kept until the controller no longer uses it, then deleted in Task 8.

**Tech Stack:** C# (Unity 6000.3.23f1), `System.Threading.Tasks`, uGUI/TextMeshPro, NUnit EditMode + PlayMode tests.

**Spec:** `docs/superpowers/specs/2026-10-12-chess-generated-mate-design.md` (sections 1, 4 runtime, 5 clock, 6.3, 7, 8, 10 PlayMode).

**Depends on:** Plan 1 (`2026-10-13-chess-ai-core.md`) merged: `ChessSearchEngine`, `SearchBudget`, `MateSearch`, `ChessOpponent`/`DefenceChoice`, `ChessAttemptGrader`/`MoveQuality`/`ChessScore`/`ChessGrade`, `ChessPuzzleSnapshot`, `ChessPuzzleGenerator`, `ChessGeneratorLimits`, `ChessFallbackLibrary`, `ChessTimeEstimator`.

**Out of scope here (Plan 3):** saving the snapshot/record, Journey retry reusing the same puzzle, result panel and A+ display, journey/guide/intro copy in other screens, QA screenshots of results, Android numbers.

## Global Constraints

- Every legal move is played for real and uses one of the player's M moves; no automatic undo. An illegal move changes nothing and costs nothing; the think clock keeps running (spec §1).
- The clock runs only in `PlayerTurn` (including piece selection, reading a shown hint, promotion choice). It is stopped in Intro/Generating/Analyzing/BossTurn/Paused/Completed/Failed/AnalysisError (spec §5).
- At the start of every `Submit`, if the clock is expired the attempt fails with TimeUp and the move is not accepted (spec §5).
- End-of-attempt order after a valid player move: record the move, checkmate, then draw (stalemate, threefold repetition, 50-move, insufficient material), then out-of-moves when `U == M`, otherwise the AI replies; after the AI reply: player checkmated, draw, then back to the player (spec §6.3). Checkmate beats draw and out-of-moves.
- Unknown analysis never fails the attempt and never subtracts a life: keep the played move, stop the clock and input, show a retryable error, allow retry or restart of the same puzzle (spec §4).
- Worker threads receive only immutable data (FEN strings, structs); they never touch `UnityEngine` objects. Results are applied on the main thread (spec §4).
- The explicit-move hint (level 3) caps the final score at 8.9; hint levels reset on each board revision but the "explicit hint used" flag persists for the whole attempt (spec §7).
- Difficulty comes from the Journey attempt context when present; a scene opened directly uses Normal. The puzzle must not be generated before the context is known (spec §3.2).
- Dialogue copy must not mention a number of allowed mistakes or a fixed time; conditions must not depend on puzzle id `m2_001` (spec §7).
- Project convention: commit straight to `master`; commit messages carry **no** `Co-Authored-By` or other AI trailer. Run tests with `tools/run-unity-tests.sh <EditMode|PlayMode> "<filter>" <name>` with the Unity Editor closed.

## Review Focus

- Pause lands on the exact frame an analysis result or boss slide finishes: the move must be committed once, after resume (Tasks 4, 6).
- A result for an old request (after Restart, retry or scene disable) must be ignored (Tasks 4, 5, 6).
- The player's mating move must finish the attempt even if the clock hits 0 during the animation that follows (Task 4: the check happens at the start of `Submit` only).
- A mating move must never be blocked by an analysis error (Task 2: mate short-circuit).
- Hint request while paused/analyzing, and a hint result arriving after the board changed (Tasks 3, 4).
- Opening the scene directly (no Journey context) and with a context for each difficulty yields the matching N/M; a missing fallback level must not borrow another level (Task 6).
- Promotion: choosing the piece does not consume a move until confirmed, and the move uses the chosen piece (Task 6, unchanged behaviour).
- Dialogue shown after Restart or board change must not replay a stale hint (Task 7).

---

### Task 1: Draw rules and position history

**Files:**
- Create: `Assets/_Project/Scripts/Gameplay/Chess/Core/ChessDrawRules.cs`
- Create: `Assets/Tests/EditMode/Gameplay/Chess/ChessDrawRulesTests.cs`

**Interfaces:**
- Produces:
  - `enum DrawReason { None, Stalemate, Repetition, FiftyMove, InsufficientMaterial }`
  - `sealed class PositionHistory { int Record(ChessPosition); void Clear() }` (`Record` returns how many times that position, ignoring move counters, has now occurred)
  - `static class ChessDrawRules { static DrawReason Evaluate(ChessPosition position, int repetitions); static bool IsInsufficientMaterial(ChessPosition) }`

- [ ] **Step 1: Write the failing tests**

```csharp
using KMA.Gameplay.Chess;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessDrawRulesTests
    {
        static ChessPosition P(string fen) => ChessPosition.FromFen(fen);

        [Test]
        public void StalemateIsADrawButCheckmateIsNot()
        {
            Assert.That(ChessDrawRules.Evaluate(P("7k/5K2/6Q1/8/8/8/8/8 b - - 0 1"), 1), Is.EqualTo(DrawReason.Stalemate));
            Assert.That(ChessDrawRules.Evaluate(P("7k/5KQ1/8/8/8/8/8/8 b - - 0 1"), 1), Is.EqualTo(DrawReason.None));
        }

        [TestCase("k7/8/8/8/8/8/8/K7 w - - 0 1", true)]            // K v K
        [TestCase("k7/8/8/8/8/8/8/KN6 w - - 0 1", true)]           // K+N v K
        [TestCase("k7/8/8/8/8/8/8/KB6 w - - 0 1", true)]           // K+B v K
        [TestCase("k7/8/8/8/8/8/8/KB1b4 w - - 0 1", true)]         // bishops on the same colour
        [TestCase("k7/8/8/8/8/8/8/KBb5 w - - 0 1", false)]         // bishops on opposite colours
        [TestCase("k7/8/8/8/8/8/8/KNN5 w - - 0 1", false)]         // two knights can still mate in theory
        [TestCase("k7/8/8/8/8/8/8/KR6 w - - 0 1", false)]
        [TestCase("k7/8/8/8/8/8/P7/K7 w - - 0 1", false)]
        public void InsufficientMaterial(string fen, bool expected)
        {
            Assert.That(ChessDrawRules.IsInsufficientMaterial(P(fen)), Is.EqualTo(expected), fen);
            Assert.That(ChessDrawRules.Evaluate(P(fen), 1) == DrawReason.InsufficientMaterial, Is.EqualTo(expected), fen);
        }

        [Test]
        public void FiftyMoveRuleStartsAtHundredHalfmoves()
        {
            Assert.That(ChessDrawRules.Evaluate(P("7k/8/8/8/8/8/8/R3K3 w - - 99 80"), 1), Is.EqualTo(DrawReason.None));
            Assert.That(ChessDrawRules.Evaluate(P("7k/8/8/8/8/8/8/R3K3 w - - 100 80"), 1), Is.EqualTo(DrawReason.FiftyMove));
        }

        [Test]
        public void ThirdOccurrenceIsRepetitionRegardlessOfMoveCounters()
        {
            var history = new PositionHistory();
            Assert.That(history.Record(P("7k/8/8/8/8/8/8/R3K3 w - - 0 1")), Is.EqualTo(1));
            Assert.That(history.Record(P("7k/8/8/8/8/8/8/R3K3 w - - 4 3")), Is.EqualTo(2));
            int third = history.Record(P("7k/8/8/8/8/8/8/R3K3 w - - 8 5"));
            Assert.That(third, Is.EqualTo(3));
            Assert.That(ChessDrawRules.Evaluate(P("7k/8/8/8/8/8/8/R3K3 w - - 8 5"), third), Is.EqualTo(DrawReason.Repetition));
        }

        [Test]
        public void SideToMoveDistinguishesPositions()
        {
            var history = new PositionHistory();
            history.Record(P("7k/8/8/8/8/8/8/R3K3 w - - 0 1"));
            Assert.That(history.Record(P("7k/8/8/8/8/8/8/R3K3 b - - 0 1")), Is.EqualTo(1));
        }

        [Test]
        public void ClearForgetsEverything()
        {
            var history = new PositionHistory();
            history.Record(P("7k/8/8/8/8/8/8/R3K3 w - - 0 1"));
            history.Clear();
            Assert.That(history.Record(P("7k/8/8/8/8/8/8/R3K3 w - - 0 1")), Is.EqualTo(1));
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Chess.ChessDrawRulesTests" chess-draw`
Expected: compile error, `ChessDrawRules` not defined.

- [ ] **Step 3: Implement**

```csharp
using System.Collections.Generic;

namespace KMA.Gameplay.Chess
{
    public enum DrawReason { None, Stalemate, Repetition, FiftyMove, InsufficientMaterial }

    public sealed class PositionHistory
    {
        readonly Dictionary<string, int> counts = new Dictionary<string, int>();

        /// Returns how many times this position (counters ignored) has occurred, this call included.
        public int Record(ChessPosition position)
        {
            string key = ChessSearchEngine.PositionKey(position);
            counts.TryGetValue(key, out int seen);
            counts[key] = ++seen;
            return seen;
        }

        public void Clear() => counts.Clear();
    }

    public static class ChessDrawRules
    {
        /// Checkmate is not a draw. `repetitions` is the count returned by PositionHistory.Record for this position.
        public static DrawReason Evaluate(ChessPosition position, int repetitions)
        {
            if (MoveGenerator.IsStalemate(position)) return DrawReason.Stalemate;
            if (repetitions >= 3) return DrawReason.Repetition;
            if (position.HalfmoveClock >= 100) return DrawReason.FiftyMove;
            if (IsInsufficientMaterial(position)) return DrawReason.InsufficientMaterial;
            return DrawReason.None;
        }

        public static bool IsInsufficientMaterial(ChessPosition position)
        {
            int minors = 0;
            int lightBishops = 0, darkBishops = 0, knights = 0;
            for (int square = 0; square < 64; square++)
            {
                sbyte piece = position[square];
                if (piece == 0) continue;
                int type = Piece.TypeOf(piece);
                if (type == Piece.Pawn || type == Piece.Rook || type == Piece.Queen) return false;
                if (type == Piece.Knight) { minors++; knights++; }
                else if (type == Piece.Bishop)
                {
                    minors++;
                    if ((Square.File(square) + Square.Rank(square)) % 2 == 0) darkBishops++; else lightBishops++;
                }
            }
            if (minors <= 1) return true;
            return knights == 0 && (lightBishops == 0 || darkBishops == 0); // only same-coloured bishops
        }
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run the same filter. Expected: `result=Passed`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Gameplay/Chess/Core/ChessDrawRules.cs* Assets/Tests/EditMode/Gameplay/Chess/ChessDrawRulesTests.cs*
git commit -m "feat(chess): add draw rules and position history"
```

---

### Task 2: Per-turn analyzer (grade the move and pick the reply)

**Files:**
- Create: `Assets/_Project/Scripts/Gameplay/Chess/Core/ChessTurnAnalyzer.cs`
- Create: `Assets/Tests/EditMode/Gameplay/Chess/ChessTurnAnalyzerTests.cs`

**Interfaces:**
- Consumes: `ChessAttemptGrader.Evaluate`, `MoveQuality`, `ChessOpponent.Choose`, `DefenceChoice`, `ChessSearchEngine`, `SearchStatus` (Plan 1).
- Produces:
  - `sealed class AnalysisRequest { int Id; string BeforeFen; ChessMove Move; int MovesLeftBefore; bool NeedDefence; int Seed; }` (public settable fields so the machine can fill them)
  - `sealed class AnalysisResult { int Id; SearchStatus Status; MoveQuality Quality; DefenceChoice Defence; }`
  - `static class ChessTurnAnalyzer { static AnalysisResult Analyze(ChessSearchEngine engine, AnalysisRequest request) }`

- [ ] **Step 1: Write the failing tests**

```csharp
using KMA.Gameplay.Chess;
using NUnit.Framework;
using static KMA.Tests.Gameplay.Chess.TestPuzzles;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessTurnAnalyzerTests
    {
        static ChessSearchEngine Engine(int nodes = 2_000_000) => new ChessSearchEngine(new SearchBudget(nodes, 60));

        static AnalysisRequest Request(string fen, string uci, int movesLeft, bool needDefence) => new AnalysisRequest
        {
            Id = 7, BeforeFen = fen, Move = Uci(uci), MovesLeftBefore = movesLeft, NeedDefence = needDefence, Seed = 3
        };

        [Test]
        public void OptimalMoveGetsQualityOneAndAReply()
        {
            AnalysisResult r = ChessTurnAnalyzer.Analyze(Engine(),
                Request(ChessSearchEngineTests.LadderMateIn2, "b1b7", 3, true));
            Assert.That(r.Id, Is.EqualTo(7));
            Assert.That(r.Status, Is.EqualTo(SearchStatus.Complete));
            Assert.That(r.Quality.Q, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(r.Defence.HasMove, Is.True);
            Assert.That(r.Defence.Move.ToUci(), Is.EqualTo("h8g8"));
        }

        [Test]
        public void NoDefenceIsComputedWhenNotNeeded()
        {
            AnalysisResult r = ChessTurnAnalyzer.Analyze(Engine(),
                Request(ChessSearchEngineTests.LadderMateIn2, "b1b7", 3, false));
            Assert.That(r.Defence.HasMove, Is.False);
            Assert.That(r.Status, Is.EqualTo(SearchStatus.Complete));
        }

        [Test]
        public void MatingMoveIsQualityOneWithoutAnySearch()
        {
            var engine = new ChessSearchEngine(new SearchBudget(1, 60)); // far too small to prove anything
            AnalysisResult r = ChessTurnAnalyzer.Analyze(engine,
                Request(ChessSearchEngineTests.BackRankMateIn1, "a1a8", 2, false));
            Assert.That(r.Status, Is.EqualTo(SearchStatus.Complete));
            Assert.That(r.Quality.Q, Is.EqualTo(1.0));
        }

        [Test]
        public void UnknownQualityIsUnknownResult()
        {
            AnalysisResult r = ChessTurnAnalyzer.Analyze(new ChessSearchEngine(new SearchBudget(2, 60)),
                Request(ChessSearchEngineTests.LadderMateIn2, "b1b7", 3, true));
            Assert.That(r.Status, Is.EqualTo(SearchStatus.Unknown));
        }

        [Test]
        public void UnknownDefenceIsUnknownResultEvenWhenQualityIsKnown()
        {
            // Quality is cached by the first call, then a tiny budget cannot finish the defence search.
            ChessSearchEngine engine = Engine();
            ChessTurnAnalyzer.Analyze(engine, Request(ChessSearchEngineTests.LadderMateIn2, "b1b7", 3, false));
            engine.UseBudget(new SearchBudget(1, 60));
            AnalysisResult r = ChessTurnAnalyzer.Analyze(engine,
                Request(ChessSearchEngineTests.LadderMateIn2, "b1b7", 3, true));
            Assert.That(r.Status, Is.EqualTo(SearchStatus.Unknown));
        }

        [Test]
        public void SameRequestGivesTheSameReply()
        {
            AnalysisRequest request = Request("k7/8/8/8/8/8/8/K6R w - - 0 1", "h1h2", 3, true);
            Assert.That(ChessTurnAnalyzer.Analyze(Engine(), request).Defence.Move,
                Is.EqualTo(ChessTurnAnalyzer.Analyze(Engine(), request).Defence.Move));
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Chess.ChessTurnAnalyzerTests" chess-turn`
Expected: compile error, `AnalysisRequest` not defined.

- [ ] **Step 3: Implement**

```csharp
namespace KMA.Gameplay.Chess
{
    /// Immutable-by-convention data handed to a worker: only strings, ints and structs.
    public sealed class AnalysisRequest
    {
        public int Id;
        /// Position before the player's move.
        public string BeforeFen;
        public ChessMove Move;
        /// Player moves left before this move, this one included.
        public int MovesLeftBefore;
        public bool NeedDefence;
        public int Seed;
    }

    public sealed class AnalysisResult
    {
        public int Id;
        public SearchStatus Status;
        public MoveQuality Quality;
        public DefenceChoice Defence;
    }

    public static class ChessTurnAnalyzer
    {
        public static AnalysisResult Analyze(ChessSearchEngine engine, AnalysisRequest request)
        {
            ChessPosition before = ChessPosition.FromFen(request.BeforeFen);
            ChessPosition after = before.Apply(request.Move);

            // A mating move is the shortest possible mate, so it needs no proof and can never be blocked by a budget.
            MoveQuality quality = MoveGenerator.IsCheckmate(after)
                ? new MoveQuality(SearchStatus.Complete, 1.0, 1, 1)
                : ChessAttemptGrader.Evaluate(engine, before, request.Move, request.MovesLeftBefore);
            if (quality.Unknown) return Unknown(request.Id);

            DefenceChoice defence = default;
            if (request.NeedDefence)
            {
                defence = ChessOpponent.Choose(engine, after, request.MovesLeftBefore - 1, request.Seed);
                if (defence.Status == SearchStatus.Unknown) return Unknown(request.Id);
            }
            return new AnalysisResult { Id = request.Id, Status = SearchStatus.Complete, Quality = quality, Defence = defence };
        }

        static AnalysisResult Unknown(int id) => new AnalysisResult
        {
            Id = id,
            Status = SearchStatus.Unknown,
            Quality = new MoveQuality(SearchStatus.Unknown, 0, 0, 0)
        };
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run the same filter. Expected: `result=Passed`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Gameplay/Chess/Core/ChessTurnAnalyzer.cs* Assets/Tests/EditMode/Gameplay/Chess/ChessTurnAnalyzerTests.cs*
git commit -m "feat(chess): analyze each player move and choose the opponent reply off-thread-safe"
```

---

### Task 3: Hint builder

**Files:**
- Create: `Assets/_Project/Scripts/Gameplay/Chess/Core/ChessHintBuilder.cs`
- Create: `Assets/Tests/EditMode/Gameplay/Chess/ChessHintBuilderTests.cs`

**Interfaces:**
- Consumes: `ChessSearchEngine`, `MateSearch`, `SearchStatus` (Plan 1).
- Produces:
  - `sealed class HintRequest { int Id; string Fen; int MovesLeft; int Level; }`
  - `readonly struct ChessHint { int Id; SearchStatus Status; int Level; bool MateExists; bool NamesMove; ChessMove Move; string Text }`
  - `static class ChessHintBuilder { const int MaxLevel = 3; static ChessHint Build(ChessSearchEngine engine, HintRequest request); static string PieceName(int type) }`

Behaviour: level 1 = tactical orientation, level 2 = piece to look at, level 3 = explicit move (`NamesMove = true`). When no forced mate remains within `MovesLeft` the hint says so truthfully and levels 2/3 name a safe waiting move (no stalemate, no automatic draw), never an invented mate.

- [ ] **Step 1: Write the failing tests**

```csharp
using KMA.Gameplay.Chess;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessHintBuilderTests
    {
        static ChessSearchEngine Engine(int nodes = 2_000_000) => new ChessSearchEngine(new SearchBudget(nodes, 60));
        static ChessHint Hint(string fen, int movesLeft, int level, ChessSearchEngine engine = null) =>
            ChessHintBuilder.Build(engine ?? Engine(), new HintRequest { Id = 4, Fen = fen, MovesLeft = movesLeft, Level = level });

        [Test]
        public void LevelOneDescribesTheIdeaWithoutNamingAMove()
        {
            ChessHint h = Hint(ChessSearchEngineTests.LadderMateIn2, 3, 1);
            Assert.That(h.Status, Is.EqualTo(SearchStatus.Complete));
            Assert.That(h.MateExists, Is.True);
            Assert.That(h.NamesMove, Is.False);
            Assert.That(h.Text, Is.Not.Empty);
            Assert.That(h.Text, Does.Not.Contain("b1"));
            Assert.That(h.Id, Is.EqualTo(4));
        }

        [Test]
        public void LevelTwoNamesThePieceAndSquareOfAnOptimalMove()
        {
            ChessHint h = Hint(ChessSearchEngineTests.LadderMateIn2, 3, 2);
            Assert.That(h.NamesMove, Is.False);
            Assert.That(h.Text, Does.Contain("Xe"));
            Assert.That(new[] { "b1", "a2" }, Does.Contain(Square.Name(h.Move.From)));
            Assert.That(h.Text, Does.Contain(Square.Name(h.Move.From)));
        }

        [Test]
        public void LevelThreeNamesAnOptimalMoveAndFlagsIt()
        {
            ChessHint h = Hint(ChessSearchEngineTests.LadderMateIn2, 3, 3);
            Assert.That(h.NamesMove, Is.True);
            var engine = Engine();
            ChessPosition position = ChessPosition.FromFen(ChessSearchEngineTests.LadderMateIn2);
            Assert.That(engine.MoveDistance(position.Apply(h.Move), 3).Distance, Is.EqualTo(2), "must be an optimal move");
            Assert.That(h.Text, Does.Contain(Square.Name(h.Move.From)).And.Contain(Square.Name(h.Move.To)));
        }

        [Test]
        public void HintIsDeterministic()
        {
            Assert.That(Hint(ChessSearchEngineTests.LadderMateIn2, 3, 3).Move,
                Is.EqualTo(Hint(ChessSearchEngineTests.LadderMateIn2, 3, 3).Move));
        }

        [Test]
        public void NoForcedMateIsStatedHonestlyAndLevelThreeStillNamesALegalNonDrawingMove()
        {
            const string fen = ChessSearchEngineTests.NoMateInTwo;
            ChessHint one = Hint(fen, 2, 1);
            Assert.That(one.MateExists, Is.False);
            Assert.That(one.Text, Does.Contain("không còn"));

            ChessHint three = Hint(fen, 2, 3);
            Assert.That(three.NamesMove, Is.True);
            ChessPosition position = ChessPosition.FromFen(fen);
            Assert.That(MoveGenerator.LegalMoves(position).Contains(three.Move), Is.True);
            Assert.That(ChessDrawRules.Evaluate(position.Apply(three.Move), 1), Is.EqualTo(DrawReason.None));
        }

        [Test]
        public void StalematingMoveIsNeverSuggestedWhenThereIsNoMate()
        {
            // Qg6 stalemates here; it must not be offered as the "safe" waiting move.
            ChessHint h = Hint("7k/5K2/8/6Q1/8/8/8/8 w - - 0 1", 1, 3);
            Assert.That(h.MateExists, Is.True, "this position does have mate in one");
            Assert.That(h.Move.ToUci(), Is.Not.EqualTo("g5g6"));
        }

        [Test]
        public void UnknownWhenTheBudgetRunsOut()
        {
            ChessHint h = Hint(ChessSearchEngineTests.LadderMateIn2, 3, 3, Engine(2));
            Assert.That(h.Status, Is.EqualTo(SearchStatus.Unknown));
            Assert.That(h.Text, Is.Null.Or.Empty);
        }

        [TestCase(Piece.Pawn, "Tốt")]
        [TestCase(Piece.Knight, "Mã")]
        [TestCase(Piece.Bishop, "Tượng")]
        [TestCase(Piece.Rook, "Xe")]
        [TestCase(Piece.Queen, "Hậu")]
        [TestCase(Piece.King, "Vua")]
        public void PieceNamesAreVietnamese(int type, string name) =>
            Assert.That(ChessHintBuilder.PieceName(type), Is.EqualTo(name));
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Chess.ChessHintBuilderTests" chess-hint`
Expected: compile error, `HintRequest` not defined.

- [ ] **Step 3: Implement**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace KMA.Gameplay.Chess
{
    public sealed class HintRequest
    {
        public int Id;
        public string Fen;
        /// Player moves left, the coming one included.
        public int MovesLeft;
        public int Level;
    }

    public readonly struct ChessHint
    {
        public ChessHint(int id, SearchStatus status, int level, bool mateExists, bool namesMove, ChessMove move,
            string text)
        {
            Id = id;
            Status = status;
            Level = level;
            MateExists = mateExists;
            NamesMove = namesMove;
            Move = move;
            Text = text;
        }

        public int Id { get; }
        public SearchStatus Status { get; }
        public int Level { get; }
        public bool MateExists { get; }
        /// True for level 3: the hint names an explicit move, which caps the final score.
        public bool NamesMove { get; }
        public ChessMove Move { get; }
        public string Text { get; }
    }

    public static class ChessHintBuilder
    {
        public const int MaxLevel = 3;

        public static ChessHint Build(ChessSearchEngine engine, HintRequest request)
        {
            int level = Math.Min(MaxLevel, Math.Max(1, request.Level));
            ChessPosition position = ChessPosition.FromFen(request.Fen);
            MateSearch best = engine.MateDistance(position, request.MovesLeft);
            if (best.Unknown) return Unknown(request.Id, level);

            ChessMove move;
            string idea;
            if (best.Forced)
            {
                if (!TryFindOptimal(engine, position, best.Distance, request.MovesLeft, out move, out bool unknown))
                    return unknown ? Unknown(request.Id, level) : Unknown(request.Id, level);
                bool check = MoveGenerator.IsInCheck(position.Apply(move));
                idea = check
                    ? $"Có đường chiếu hết trong {best.Distance} nước. Nhìn các nước chiếu trước."
                    : $"Có đường chiếu hết trong {best.Distance} nước. Nước đầu không chiếu: hãy khóa đường thoát của vua đen.";
            }
            else
            {
                move = SafeWaitingMove(position);
                idea = "Không còn đường chiếu hết bắt buộc trong số nước còn lại. Hãy giữ quân an toàn và tránh thế hòa.";
            }

            string piece = PieceName(Piece.TypeOf(position[move.From]));
            string text = level == 1
                ? idea
                : level == 2
                    ? $"Xem lại quân {piece} ở {Square.Name(move.From)}."
                    : $"Đi {piece} từ {Square.Name(move.From)} đến {Square.Name(move.To)}" +
                      (move.Promotion != 0 ? $" và phong thành {PieceName(move.Promotion)}" : string.Empty) +
                      ". Tôi đã chỉ rõ rồi.";
            return new ChessHint(request.Id, SearchStatus.Complete, level, best.Forced, level == 3, move, text);
        }

        public static string PieceName(int type) => type switch
        {
            Piece.Pawn => "Tốt",
            Piece.Knight => "Mã",
            Piece.Bishop => "Tượng",
            Piece.Rook => "Xe",
            Piece.Queen => "Hậu",
            _ => "Vua"
        };

        /// First optimal move in UCI order, so the same board always gives the same hint.
        static bool TryFindOptimal(ChessSearchEngine engine, ChessPosition position, int distance, int movesLeft,
            out ChessMove move, out bool unknown)
        {
            unknown = false;
            move = default;
            foreach (ChessMove candidate in MoveGenerator.LegalMoves(position)
                         .OrderBy(m => m.ToUci(), StringComparer.Ordinal))
            {
                ChessPosition after = position.Apply(candidate);
                if (MoveGenerator.IsCheckmate(after)) { if (distance == 1) { move = candidate; return true; } continue; }
                MateSearch result = engine.MoveDistance(after, movesLeft);
                if (result.Unknown) { unknown = true; return false; }
                if (result.Forced && result.Distance == distance) { move = candidate; return true; }
            }
            return false;
        }

        /// A legal move that neither stalemates nor triggers an automatic draw; checks are preferred, then UCI order.
        static ChessMove SafeWaitingMove(ChessPosition position)
        {
            List<ChessMove> ordered = MoveGenerator.LegalMoves(position).OrderBy(m => m.ToUci(), StringComparer.Ordinal).ToList();
            List<ChessMove> safe = ordered.Where(m => ChessDrawRules.Evaluate(position.Apply(m), 1) == DrawReason.None).ToList();
            if (safe.Count == 0) safe = ordered;
            foreach (ChessMove candidate in safe)
                if (MoveGenerator.IsInCheck(position.Apply(candidate))) return candidate;
            return safe[0];
        }

        static ChessHint Unknown(int id, int level) =>
            new ChessHint(id, SearchStatus.Unknown, level, false, false, default, null);
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run the same filter. Expected: `result=Passed`. `StalematingMoveIsNeverSuggestedWhenThereIsNoMate` documents that a mate-in-one position uses the real mate move.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Gameplay/Chess/Core/ChessHintBuilder.cs* Assets/Tests/EditMode/Gameplay/Chess/ChessHintBuilderTests.cs*
git commit -m "feat(chess): compute three-level hints from the live board"
```

---

### Task 4: Attempt state machine

**Files:**
- Create: `Assets/_Project/Scripts/Gameplay/Chess/Core/ChessAttemptMachine.cs`
- Create: `Assets/Tests/EditMode/Gameplay/Chess/ChessAttemptMachineTests.cs`

**Interfaces:**
- Consumes: `ChessPuzzleSnapshot` (`fen`, `optimalMoves`, `maxMoves`, `timeSeconds`, `seed`, `StartPosition`), `ThinkClock`, `MoveGenerator`, `ChessDrawRules`/`PositionHistory`/`DrawReason` (Task 1), `AnalysisRequest`/`AnalysisResult` (Task 2), `HintRequest`/`ChessHint`/`ChessHintBuilder` (Task 3), `ChessAttemptGrader.Score`/`ChessScore` (Plan 1).
- Produces:
  - `enum ChessAttemptPhase { Intro, PlayerTurn, Analyzing, BossTurn, Paused, Completed, Failed, AnalysisError }`
  - `enum ChessEndReason { None, Mate, TimeUp, OutOfMoves, Mated, Draw }`
  - `enum SubmitResult { NotConsidered, Illegal, TimeUp, Accepted }`
  - `sealed class AttemptMove { string Uci; double Q; int BestDistance; int ChosenDistance; int MovesLeftBefore; }`
  - `sealed class ChessAttemptMachine` with:
    `ctor(ChessPuzzleSnapshot)`; events `PhaseChanged(ChessAttemptPhase)`, `AnalysisRequested(AnalysisRequest)`, `HintRequested(HintRequest)`, `MoveCommitted(ChessMove, bool byPlayer)`, `IllegalMoveRejected(ChessMove)`, `Finished(bool solved)`, `HintFailed()`;
    props `Snapshot, Clock (ThinkClock), Phase, Position, MovesUsed, MaxMoves, OptimalMoves, EndReason, DrawReason, LastMove (ChessMove?), PendingBossReply (ChessMove?), PendingAnalysis (AnalysisRequest), HintLevel, HintText, HintUsed, ExplicitHintUsed, Moves (IReadOnlyList<AttemptMove>), Result (ChessScore), CanMove, Revision`;
    methods `Begin(), Submit(ChessMove) → SubmitResult, CompleteAnalysis(AnalysisResult) → bool, RetryAnalysis(), CompleteBossMove() → bool, RequestHint() → bool, CompleteHint(ChessHint) → bool, Pause(), Resume(), Tick(float), Restart()`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Linq;
using KMA.Gameplay.Chess;
using NUnit.Framework;
using static KMA.Tests.Gameplay.Chess.TestPuzzles;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessAttemptMachineTests
    {
        ChessAttemptMachine machine;
        ChessSearchEngine engine;
        int finishedCount;
        bool lastSolved;

        static ChessPuzzleSnapshot Snapshot(string fen, int n, int m, int seconds = 90) => new ChessPuzzleSnapshot
        {
            fen = fen, level = "Normal", seed = 5, optimalMoves = n, maxMoves = m, timeSeconds = seconds,
            branching = 3f, quietMoves = 1, estimatorVersion = 1, source = ChessPuzzleSnapshot.GeneratedSource
        };

        void Create(string fen, int n, int m, int seconds = 90)
        {
            machine = new ChessAttemptMachine(Snapshot(fen, n, m, seconds));
            engine = new ChessSearchEngine(new SearchBudget(2_000_000, 60));
            finishedCount = 0;
            machine.Finished += solved => { finishedCount++; lastSolved = solved; };
        }

        /// Runs the worker's job inline and hands the result back, as the controller does.
        bool Pump()
        {
            AnalysisRequest request = machine.PendingAnalysis;
            return machine.CompleteAnalysis(ChessTurnAnalyzer.Analyze(engine, request));
        }

        void Play(string uci)
        {
            Assert.That(machine.Submit(Uci(uci)), Is.EqualTo(SubmitResult.Accepted), uci);
            Assert.That(Pump(), Is.True);
        }

        [Test]
        public void IntroIgnoresMovesAndTheClock()
        {
            Create(ChessSearchEngineTests.LadderMateIn2, 2, 3);
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.Intro));
            Assert.That(machine.Submit(Uci("b1b7")), Is.EqualTo(SubmitResult.NotConsidered));
            machine.Tick(100f);
            Assert.That(machine.Clock.Elapsed, Is.EqualTo(0f));
            machine.Begin();
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.PlayerTurn));
            Assert.That(machine.Clock.Running, Is.True);
        }

        [Test]
        public void ExposesLimitsFromTheSnapshot()
        {
            Create(ChessSearchEngineTests.LadderMateIn2, 2, 3, 75);
            Assert.That(machine.OptimalMoves, Is.EqualTo(2));
            Assert.That(machine.MaxMoves, Is.EqualTo(3));
            Assert.That(machine.Clock.Limit, Is.EqualTo(75f));
        }

        [Test]
        public void IllegalMoveChangesNothingAndTheClockKeepsRunning()
        {
            Create(ChessSearchEngineTests.LadderMateIn2, 2, 3);
            machine.Begin();
            string fen = machine.Position.ToFen();
            ChessMove rejected = default;
            machine.IllegalMoveRejected += m => rejected = m;
            // g1g3 is illegal for sure (a king moves one square).
            Assert.That(machine.Submit(Uci("g1g3")), Is.EqualTo(SubmitResult.Illegal));
            Assert.That(rejected.ToUci(), Is.EqualTo("g1g3"));
            Assert.That(machine.MovesUsed, Is.EqualTo(0));
            Assert.That(machine.Position.ToFen(), Is.EqualTo(fen));
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.PlayerTurn));
            Assert.That(machine.Clock.Running, Is.True);
            machine.Tick(2f);
            Assert.That(machine.Clock.Elapsed, Is.EqualTo(2f));
        }

        [Test]
        public void ValidMoveIsPlayedStopsTheClockAndRequestsAnalysis()
        {
            Create(ChessSearchEngineTests.LadderMateIn2, 2, 3);
            machine.Begin();
            machine.Tick(5f);
            AnalysisRequest seen = null;
            machine.AnalysisRequested += r => seen = r;
            Assert.That(machine.Submit(Uci("b1b7")), Is.EqualTo(SubmitResult.Accepted));
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.Analyzing));
            Assert.That(machine.MovesUsed, Is.EqualTo(1));
            Assert.That(machine.Position.SideToMove, Is.EqualTo(PieceColor.Black), "board shows the move immediately");
            Assert.That(machine.Clock.Running, Is.False);
            machine.Tick(30f);
            Assert.That(machine.Clock.Elapsed, Is.EqualTo(5f), "no think time while analyzing");
            Assert.That(seen, Is.Not.Null);
            Assert.That(seen.MovesLeftBefore, Is.EqualTo(3));
            Assert.That(seen.NeedDefence, Is.True);
            Assert.That(machine.Submit(Uci("a2a8")), Is.EqualTo(SubmitResult.NotConsidered));
        }

        [Test]
        public void PerfectLineSolvesWithAPlusAndOneFinishedEvent()
        {
            Create(ChessSearchEngineTests.LadderMateIn2, 2, 3);
            machine.Begin();
            Play("b1b7");
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.BossTurn));
            Assert.That(machine.PendingBossReply.Value.ToUci(), Is.EqualTo("h8g8"));
            Assert.That(machine.CompleteBossMove(), Is.True);
            Assert.That(machine.CompleteBossMove(), Is.False);
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.PlayerTurn));
            Assert.That(machine.Clock.Running, Is.True);
            Play("a2a8");
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.Completed));
            Assert.That(machine.EndReason, Is.EqualTo(ChessEndReason.Mate));
            Assert.That(finishedCount, Is.EqualTo(1));
            Assert.That(lastSolved, Is.True);
            Assert.That(machine.Result.Pass, Is.True);
            Assert.That(machine.Result.Score, Is.EqualTo(10f));
            Assert.That(machine.Result.Grade, Is.EqualTo(ChessGrade.APlus));
            Assert.That(machine.Moves.Select(m => m.Q), Is.All.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void ASuboptimalMoveIsStillPlayedAndCountsAMove()
        {
            Create(ChessSearchEngineTests.LadderMateIn2, 2, 3);
            machine.Begin();
            string before = machine.Position.ToFen();
            Play("g1f1");
            Assert.That(machine.MovesUsed, Is.EqualTo(1));
            Assert.That(machine.Position.ToFen(), Is.Not.EqualTo(before));
            Assert.That(machine.Moves.Count, Is.EqualTo(1));
            Assert.That(machine.Moves[0].Q, Is.LessThan(1.0));
        }

        [Test]
        public void RunningOutOfMovesFailsWithZeroAndF()
        {
            Create(ChessSearchEngineTests.LadderMateIn2, 2, 3);
            machine.Begin();
            for (int i = 0; i < 3; i++)
            {
                Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.PlayerTurn), "move " + i);
                ChessMove move = MoveGenerator.LegalMoves(machine.Position)
                    .First(m => !MoveGenerator.IsCheckmate(machine.Position.Apply(m)) &&
                                MoveGenerator.LegalMoves(machine.Position.Apply(m)).Count > 0);
                Assert.That(machine.Submit(move), Is.EqualTo(SubmitResult.Accepted));
                Assert.That(Pump(), Is.True);
                if (machine.Phase == ChessAttemptPhase.BossTurn) machine.CompleteBossMove();
            }
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.Failed));
            Assert.That(machine.EndReason, Is.EqualTo(ChessEndReason.OutOfMoves));
            Assert.That(machine.Result.Pass, Is.False);
            Assert.That(machine.Result.Score, Is.EqualTo(0f));
            Assert.That(machine.Result.Grade, Is.EqualTo(ChessGrade.F));
            Assert.That(finishedCount, Is.EqualTo(1));
            Assert.That(lastSolved, Is.False);
        }

        [Test]
        public void MateOnTheLastAllowedMoveStillWins()
        {
            Create(ChessSearchEngineTests.BackRankMateIn1, 1, 1);
            machine.Begin();
            Play("a1a8");
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.Completed));
            Assert.That(machine.EndReason, Is.EqualTo(ChessEndReason.Mate));
        }

        [Test]
        public void StalematingMoveIsADrawNotAWin()
        {
            Create(ChessSearchEngineTests.StalemateTrap, 1, 2);
            machine.Begin();
            Play("g5g6");
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.Failed));
            Assert.That(machine.EndReason, Is.EqualTo(ChessEndReason.Draw));
            Assert.That(machine.DrawReason, Is.EqualTo(DrawReason.Stalemate));
            Assert.That(machine.Result.Grade, Is.EqualTo(ChessGrade.F));
        }

        [Test]
        public void ClockRunningOutFailsAndMovesAreRefused()
        {
            Create(ChessSearchEngineTests.LadderMateIn2, 2, 3, 60);
            machine.Begin();
            machine.Tick(59f);
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.PlayerTurn));
            machine.Tick(1f);
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.Failed));
            Assert.That(machine.EndReason, Is.EqualTo(ChessEndReason.TimeUp));
            Assert.That(machine.Submit(Uci("b1b7")), Is.EqualTo(SubmitResult.NotConsidered));
            Assert.That(finishedCount, Is.EqualTo(1));
        }

        [Test]
        public void SubmitChecksTheClockFirstEvenIfTickHasNotFiredYet()
        {
            Create(ChessSearchEngineTests.LadderMateIn2, 2, 3, 60);
            machine.Begin();
            machine.Clock.Tick(60f); // the clock ran out between two machine ticks
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.PlayerTurn));
            Assert.That(machine.Submit(Uci("b1b7")), Is.EqualTo(SubmitResult.TimeUp));
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.Failed));
            Assert.That(machine.MovesUsed, Is.EqualTo(0), "the late move is not accepted");
        }

        [Test]
        public void ATimeoutWhileAnalyzingDoesNotCancelAnAcceptedMate()
        {
            Create(ChessSearchEngineTests.BackRankMateIn1, 1, 2, 60);
            machine.Begin();
            machine.Tick(59.9f);
            Assert.That(machine.Submit(Uci("a1a8")), Is.EqualTo(SubmitResult.Accepted));
            machine.Tick(30f); // clock is stopped while analyzing
            Pump();
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.Completed));
        }

        [Test]
        public void UnknownAnalysisKeepsTheMoveStopsTheClockAndCanBeRetried()
        {
            Create(ChessSearchEngineTests.LadderMateIn2, 2, 3);
            machine.Begin();
            Assert.That(machine.Submit(Uci("b1b7")), Is.EqualTo(SubmitResult.Accepted));
            var starved = new ChessSearchEngine(new SearchBudget(2, 60));
            Assert.That(machine.CompleteAnalysis(ChessTurnAnalyzer.Analyze(starved, machine.PendingAnalysis)), Is.True);

            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.AnalysisError));
            Assert.That(finishedCount, Is.EqualTo(0), "no result is published on an analysis error");
            Assert.That(machine.MovesUsed, Is.EqualTo(1));
            Assert.That(machine.Clock.Running, Is.False);
            machine.Tick(10f);
            Assert.That(machine.Clock.Elapsed, Is.EqualTo(0f));
            Assert.That(machine.Submit(Uci("a2a7")), Is.EqualTo(SubmitResult.NotConsidered));

            machine.RetryAnalysis();
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.Analyzing));
            Assert.That(Pump(), Is.True);
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.BossTurn));
        }

        [Test]
        public void StaleAnalysisResultsAreIgnored()
        {
            Create(ChessSearchEngineTests.LadderMateIn2, 2, 3);
            machine.Begin();
            machine.Submit(Uci("b1b7"));
            AnalysisResult first = ChessTurnAnalyzer.Analyze(engine, machine.PendingAnalysis);
            machine.Restart();
            Assert.That(machine.CompleteAnalysis(first), Is.False);
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.Intro));
        }

        [Test]
        public void ResultArrivingWhilePausedIsAppliedOnceAfterResume()
        {
            Create(ChessSearchEngineTests.LadderMateIn2, 2, 3);
            machine.Begin();
            machine.Submit(Uci("b1b7"));
            AnalysisResult result = ChessTurnAnalyzer.Analyze(engine, machine.PendingAnalysis);
            machine.Pause();
            Assert.That(machine.CompleteAnalysis(result), Is.True);
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.Paused));
            Assert.That(machine.PendingBossReply, Is.Null, "not applied while paused");
            machine.Resume();
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.BossTurn));
            Assert.That(machine.PendingBossReply.Value.ToUci(), Is.EqualTo("h8g8"));
            Assert.That(machine.Moves.Count, Is.EqualTo(1), "the move was recorded once");
        }

        [Test]
        public void ResumeToPlayerTurnRestartsTheClockButOtherPhasesDoNot()
        {
            Create(ChessSearchEngineTests.LadderMateIn2, 2, 3);
            machine.Begin();
            machine.Pause();
            Assert.That(machine.Clock.Running, Is.False);
            machine.Resume();
            Assert.That(machine.Clock.Running, Is.True);
            machine.Submit(Uci("b1b7"));
            machine.Pause();
            machine.Resume();
            Assert.That(machine.Clock.Running, Is.False);
        }

        [Test]
        public void HintStopsTheClockWhileComputingThenResumes()
        {
            Create(ChessSearchEngineTests.LadderMateIn2, 2, 3);
            machine.Begin();
            HintRequest request = null;
            machine.HintRequested += r => request = r;
            Assert.That(machine.RequestHint(), Is.True);
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.Analyzing));
            Assert.That(machine.Clock.Running, Is.False);
            Assert.That(request.Level, Is.EqualTo(1));
            Assert.That(machine.RequestHint(), Is.False, "one request at a time");

            ChessHint hint = ChessHintBuilder.Build(engine, request);
            Assert.That(machine.CompleteHint(hint), Is.True);
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.PlayerTurn));
            Assert.That(machine.Clock.Running, Is.True);
            Assert.That(machine.HintLevel, Is.EqualTo(1));
            Assert.That(machine.HintText, Is.EqualTo(hint.Text));
            Assert.That(machine.HintUsed, Is.True);
            Assert.That(machine.ExplicitHintUsed, Is.False);
        }

        [Test]
        public void ExplicitHintCapsTheScoreAndTheFlagSurvivesLevelReset()
        {
            Create(ChessSearchEngineTests.LadderMateIn2, 2, 3);
            machine.Begin();
            HintRequest request = null;
            machine.HintRequested += r => request = r;
            for (int i = 0; i < 3; i++)
            {
                Assert.That(machine.RequestHint(), Is.True);
                Assert.That(machine.CompleteHint(ChessHintBuilder.Build(engine, request)), Is.True);
            }
            Assert.That(machine.HintLevel, Is.EqualTo(3));
            Assert.That(machine.ExplicitHintUsed, Is.True);
            Assert.That(machine.RequestHint(), Is.True, "level stays at the maximum but is still allowed");
            Assert.That(request.Level, Is.EqualTo(3));
            machine.CompleteHint(ChessHintBuilder.Build(engine, request));

            Play("b1b7");
            machine.CompleteBossMove();
            Assert.That(machine.HintLevel, Is.EqualTo(0), "levels reset with the board");
            Assert.That(machine.ExplicitHintUsed, Is.True, "the cap flag does not");
            Play("a2a8");
            Assert.That(machine.Result.Score, Is.EqualTo(8.9f));
            Assert.That(machine.Result.Grade, Is.EqualTo(ChessGrade.A));
            Assert.That(machine.Result.HintCapped, Is.True);
        }

        [Test]
        public void FailedHintReturnsToPlayWithoutRevealing()
        {
            Create(ChessSearchEngineTests.LadderMateIn2, 2, 3);
            machine.Begin();
            HintRequest request = null;
            machine.HintRequested += r => request = r;
            bool failed = false;
            machine.HintFailed += () => failed = true;
            machine.RequestHint();
            ChessHint unknown = ChessHintBuilder.Build(new ChessSearchEngine(new SearchBudget(2, 60)), request);
            Assert.That(machine.CompleteHint(unknown), Is.True);
            Assert.That(failed, Is.True);
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.PlayerTurn));
            Assert.That(machine.HintLevel, Is.EqualTo(0));
            Assert.That(machine.HintUsed, Is.False);
        }

        [Test]
        public void HintResultForAnOldRequestIsIgnored()
        {
            Create(ChessSearchEngineTests.LadderMateIn2, 2, 3);
            machine.Begin();
            HintRequest request = null;
            machine.HintRequested += r => request = r;
            machine.RequestHint();
            ChessHint hint = ChessHintBuilder.Build(engine, request);
            machine.Restart();
            Assert.That(machine.CompleteHint(hint), Is.False);
            Assert.That(machine.HintLevel, Is.EqualTo(0));
        }

        [Test]
        public void RestartKeepsTheSameSnapshotAndResetsTheAttempt()
        {
            Create(ChessSearchEngineTests.LadderMateIn2, 2, 3, 75);
            machine.Begin();
            Play("b1b7");
            machine.Restart();
            Assert.That(machine.Phase, Is.EqualTo(ChessAttemptPhase.Intro));
            Assert.That(machine.Position.ToFen(), Is.EqualTo(ChessPosition.FromFen(ChessSearchEngineTests.LadderMateIn2).ToFen()));
            Assert.That(machine.MovesUsed, Is.EqualTo(0));
            Assert.That(machine.Moves.Count, Is.EqualTo(0));
            Assert.That(machine.Clock.Limit, Is.EqualTo(75f));
            Assert.That(machine.Clock.Elapsed, Is.EqualTo(0f));
            Assert.That(machine.ExplicitHintUsed, Is.False);
            Assert.That(machine.EndReason, Is.EqualTo(ChessEndReason.None));
        }

        [Test]
        public void RepetitionEndsTheAttemptAsADraw()
        {
            // The attacker shuffles a rook while the lone king shuffles; the third occurrence must be a draw.
            Create("k7/8/8/8/8/8/8/K6R w - - 0 1", 3, 9);
            machine.Begin();
            string[] player = { "h1h2", "h2h1", "h1h2", "h2h1", "h1h2", "h2h1" };
            for (int i = 0; i < player.Length && machine.Phase == ChessAttemptPhase.PlayerTurn; i++)
            {
                if (!MoveGenerator.LegalMoves(machine.Position).Contains(Uci(player[i]))) break;
                machine.Submit(Uci(player[i]));
                Pump();
                if (machine.Phase == ChessAttemptPhase.BossTurn) machine.CompleteBossMove();
            }
            if (machine.Phase == ChessAttemptPhase.Failed)
                Assert.That(machine.EndReason, Is.EqualTo(ChessEndReason.Draw));
            else
                Assert.Inconclusive("the boss varied its king moves; repetition is covered in ChessDrawRulesTests");
        }

        [Test]
        public void EventsReportPhaseChangesAndCommits()
        {
            Create(ChessSearchEngineTests.LadderMateIn2, 2, 3);
            var phases = new System.Collections.Generic.List<ChessAttemptPhase>();
            var commits = new System.Collections.Generic.List<string>();
            machine.PhaseChanged += p => phases.Add(p);
            machine.MoveCommitted += (m, byPlayer) => commits.Add((byPlayer ? "P" : "B") + m.ToUci());
            machine.Begin();
            Play("b1b7");
            machine.CompleteBossMove();
            Assert.That(commits, Is.EqualTo(new[] { "Pb1b7", "Bh8g8" }));
            Assert.That(phases, Is.EqualTo(new[]
            {
                ChessAttemptPhase.PlayerTurn, ChessAttemptPhase.Analyzing, ChessAttemptPhase.BossTurn, ChessAttemptPhase.PlayerTurn
            }));
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Chess.ChessAttemptMachineTests" chess-machine`
Expected: compile error, `ChessAttemptMachine` not defined.

- [ ] **Step 3: Implement the machine**

```csharp
using System;
using System.Collections.Generic;

namespace KMA.Gameplay.Chess
{
    public enum ChessAttemptPhase { Intro, PlayerTurn, Analyzing, BossTurn, Paused, Completed, Failed, AnalysisError }

    public enum ChessEndReason { None, Mate, TimeUp, OutOfMoves, Mated, Draw }

    public enum SubmitResult { NotConsidered, Illegal, TimeUp, Accepted }

    public sealed class AttemptMove
    {
        public string Uci;
        public double Q;
        public int BestDistance;
        public int ChosenDistance;
        public int MovesLeftBefore;
    }

    /// Gameplay state of one attempt at one puzzle. It never searches: it asks for analysis, and the host feeds the
    /// result back on the main thread. Presentation listens to the events and calls CompleteBossMove after animating.
    public sealed class ChessAttemptMachine
    {
        enum Outcome { Continue, Mate, Draw, OutOfMoves }

        readonly ChessPosition start;
        readonly PositionHistory history = new PositionHistory();
        readonly List<AttemptMove> moves = new List<AttemptMove>();
        ChessAttemptPhase pausedFrom;
        Action deferred;
        int nextId;
        int activeId = -1;
        int hintId = -1;
        bool hintPending;
        int hintLevelRequested;
        AnalysisRequest pendingAnalysis;
        Outcome pendingOutcome;
        DrawReason pendingDraw;
        ChessMove pendingMove;
        ChessPosition pendingBefore;

        public ChessAttemptMachine(ChessPuzzleSnapshot snapshot)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.maxMoves < 1 || snapshot.optimalMoves < 1 || snapshot.timeSeconds <= 0)
                throw new ArgumentException("The snapshot has no move limit or time.", nameof(snapshot));
            start = snapshot.StartPosition;
            Clock = new ThinkClock(snapshot.timeSeconds);
            Reset();
        }

        public event Action<ChessAttemptPhase> PhaseChanged;
        public event Action<AnalysisRequest> AnalysisRequested;
        public event Action<HintRequest> HintRequested;
        public event Action<ChessMove, bool> MoveCommitted;
        public event Action<ChessMove> IllegalMoveRejected;
        public event Action<bool> Finished;
        public event Action HintFailed;

        public ChessPuzzleSnapshot Snapshot { get; }
        public ThinkClock Clock { get; }
        public ChessAttemptPhase Phase { get; private set; }
        public ChessPosition Position { get; private set; }
        public int MovesUsed { get; private set; }
        public int MaxMoves => Snapshot.maxMoves;
        public int OptimalMoves => Snapshot.optimalMoves;
        public ChessEndReason EndReason { get; private set; }
        public DrawReason DrawReason { get; private set; }
        public ChessMove? LastMove { get; private set; }
        public ChessMove? PendingBossReply { get; private set; }
        public AnalysisRequest PendingAnalysis => pendingAnalysis;
        public int HintLevel { get; private set; }
        public string HintText { get; private set; }
        public bool HintUsed { get; private set; }
        public bool ExplicitHintUsed { get; private set; }
        public int Revision { get; private set; }
        public IReadOnlyList<AttemptMove> Moves => moves;
        public ChessScore Result { get; private set; }
        public bool CanMove => Phase == ChessAttemptPhase.PlayerTurn;

        public void Begin()
        {
            if (Phase != ChessAttemptPhase.Intro) return;
            ReturnToPlayer();
        }

        public SubmitResult Submit(ChessMove move)
        {
            if (Phase != ChessAttemptPhase.PlayerTurn) return SubmitResult.NotConsidered;
            if (Clock.Expired)
            {
                Fail(ChessEndReason.TimeUp);
                return SubmitResult.TimeUp;
            }
            if (!MoveGenerator.LegalMoves(Position).Contains(move))
            {
                IllegalMoveRejected?.Invoke(move);
                return SubmitResult.Illegal;
            }

            Clock.Stop();
            ChessPosition before = Position;
            int movesLeftBefore = MaxMoves - MovesUsed;
            ChessPosition after = before.Apply(move);
            MovesUsed++;
            Revision++;
            HintLevel = 0;
            HintText = null;
            Commit(after, move, true);
            pendingOutcome = Judge(after, out pendingDraw);
            pendingBefore = before;
            pendingMove = move;
            pendingAnalysis = new AnalysisRequest
            {
                Id = activeId = ++nextId,
                BeforeFen = before.ToFen(),
                Move = move,
                MovesLeftBefore = movesLeftBefore,
                NeedDefence = pendingOutcome == Outcome.Continue,
                Seed = Snapshot.seed * 31 + MovesUsed
            };
            SetPhase(ChessAttemptPhase.Analyzing);
            AnalysisRequested?.Invoke(pendingAnalysis);
            return SubmitResult.Accepted;
        }

        /// False when the result is stale. A result that arrives while paused is applied on Resume.
        public bool CompleteAnalysis(AnalysisResult result)
        {
            if (result == null || result.Id != activeId || pendingAnalysis == null) return false;
            if (Phase != ChessAttemptPhase.Analyzing && !(Phase == ChessAttemptPhase.Paused &&
                                                           pausedFrom == ChessAttemptPhase.Analyzing)) return false;
            Deliver(() => ApplyAnalysis(result));
            return true;
        }

        public void RetryAnalysis()
        {
            if (Phase != ChessAttemptPhase.AnalysisError || pendingAnalysis == null) return;
            pendingAnalysis.Id = activeId = ++nextId;
            SetPhase(ChessAttemptPhase.Analyzing);
            AnalysisRequested?.Invoke(pendingAnalysis);
        }

        public bool CompleteBossMove()
        {
            if (Phase != ChessAttemptPhase.BossTurn || !PendingBossReply.HasValue) return false;
            ChessMove reply = PendingBossReply.Value;
            PendingBossReply = null;
            Revision++;
            HintLevel = 0;
            HintText = null;
            Commit(Position.Apply(reply), reply, false);
            if (MoveGenerator.IsCheckmate(Position)) { Fail(ChessEndReason.Mated); return true; }
            DrawReason draw = ChessDrawRules.Evaluate(Position, history.Record(Position));
            if (draw != DrawReason.None) { DrawReason = draw; Fail(ChessEndReason.Draw); return true; }
            ReturnToPlayer();
            return true;
        }

        public bool RequestHint()
        {
            if (Phase != ChessAttemptPhase.PlayerTurn || hintPending) return false;
            hintPending = true;
            hintLevelRequested = Math.Min(ChessHintBuilder.MaxLevel, HintLevel + 1);
            hintId = ++nextId;
            Clock.Stop();
            SetPhase(ChessAttemptPhase.Analyzing);
            HintRequested?.Invoke(new HintRequest
            {
                Id = hintId, Fen = Position.ToFen(), MovesLeft = MaxMoves - MovesUsed, Level = hintLevelRequested
            });
            return true;
        }

        public bool CompleteHint(ChessHint hint)
        {
            if (!hintPending || hint.Id != hintId) return false;
            if (Phase != ChessAttemptPhase.Analyzing && !(Phase == ChessAttemptPhase.Paused &&
                                                           pausedFrom == ChessAttemptPhase.Analyzing)) return false;
            Deliver(() => ApplyHint(hint));
            return true;
        }

        public void Pause()
        {
            if (Phase == ChessAttemptPhase.Paused || Phase == ChessAttemptPhase.Completed ||
                Phase == ChessAttemptPhase.Failed) return;
            pausedFrom = Phase;
            Clock.Stop();
            SetPhase(ChessAttemptPhase.Paused);
        }

        public void Resume()
        {
            if (Phase != ChessAttemptPhase.Paused) return;
            if (pausedFrom == ChessAttemptPhase.PlayerTurn) Clock.Start();
            SetPhase(pausedFrom);
            Action apply = deferred;
            deferred = null;
            apply?.Invoke();
        }

        public void Tick(float dt)
        {
            if (Phase == ChessAttemptPhase.PlayerTurn && Clock.Tick(dt)) Fail(ChessEndReason.TimeUp);
        }

        public void Restart() => Reset();

        void Reset()
        {
            Position = start;
            MovesUsed = 0;
            Revision = 0;
            HintLevel = 0;
            HintText = null;
            HintUsed = false;
            ExplicitHintUsed = false;
            EndReason = ChessEndReason.None;
            DrawReason = DrawReason.None;
            LastMove = null;
            PendingBossReply = null;
            pendingAnalysis = null;
            deferred = null;
            hintPending = false;
            activeId = -1;
            hintId = -1;
            moves.Clear();
            history.Clear();
            history.Record(start);
            Result = default;
            Clock.Reset();
            Phase = ChessAttemptPhase.Intro;
            PhaseChanged?.Invoke(Phase);
        }

        void Deliver(Action apply)
        {
            if (Phase == ChessAttemptPhase.Paused) deferred = apply;
            else apply();
        }

        void ApplyAnalysis(AnalysisResult result)
        {
            if (result.Status == SearchStatus.Unknown)
            {
                SetPhase(ChessAttemptPhase.AnalysisError);
                return;
            }
            moves.Add(new AttemptMove
            {
                Uci = pendingMove.ToUci(),
                Q = result.Quality.Q,
                BestDistance = result.Quality.BestDistance,
                ChosenDistance = result.Quality.ChosenDistance,
                MovesLeftBefore = pendingAnalysis.MovesLeftBefore
            });
            pendingAnalysis = null;
            switch (pendingOutcome)
            {
                case Outcome.Mate:
                    EndReason = ChessEndReason.Mate;
                    Clock.Stop();
                    Result = Score(true);
                    SetPhase(ChessAttemptPhase.Completed);
                    Finished?.Invoke(true);
                    break;
                case Outcome.Draw:
                    DrawReason = pendingDraw;
                    Fail(ChessEndReason.Draw);
                    break;
                case Outcome.OutOfMoves:
                    Fail(ChessEndReason.OutOfMoves);
                    break;
                default:
                    PendingBossReply = result.Defence.Move;
                    SetPhase(ChessAttemptPhase.BossTurn);
                    break;
            }
        }

        void ApplyHint(ChessHint hint)
        {
            hintPending = false;
            if (hint.Status == SearchStatus.Unknown)
            {
                ReturnToPlayer();
                HintFailed?.Invoke();
                return;
            }
            HintLevel = hint.Level;
            HintText = hint.Text;
            HintUsed = true;
            if (hint.NamesMove) ExplicitHintUsed = true;
            ReturnToPlayer();
        }

        /// Spec 6.3: checkmate, then draw, then the move limit.
        Outcome Judge(ChessPosition after, out DrawReason draw)
        {
            draw = DrawReason.None;
            if (MoveGenerator.IsCheckmate(after)) return Outcome.Mate;
            draw = ChessDrawRules.Evaluate(after, history.Record(after));
            if (draw != DrawReason.None) return Outcome.Draw;
            return MovesUsed >= MaxMoves ? Outcome.OutOfMoves : Outcome.Continue;
        }

        ChessScore Score(bool mated)
        {
            var qualities = new List<double>(moves.Count);
            foreach (AttemptMove move in moves) qualities.Add(move.Q);
            return ChessAttemptGrader.Score(mated, qualities, OptimalMoves, MovesUsed, ExplicitHintUsed);
        }

        void ReturnToPlayer()
        {
            Clock.Start();
            SetPhase(ChessAttemptPhase.PlayerTurn);
        }

        void Fail(ChessEndReason reason)
        {
            EndReason = reason;
            Clock.Stop();
            Result = Score(false);
            SetPhase(ChessAttemptPhase.Failed);
            Finished?.Invoke(false);
        }

        void Commit(ChessPosition next, ChessMove move, bool byPlayer)
        {
            Position = next;
            LastMove = move;
            MoveCommitted?.Invoke(move, byPlayer);
        }

        void SetPhase(ChessAttemptPhase phase)
        {
            if (Phase == phase) return;
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }
}
```

Implementation notes the implementer must respect:
- `Judge` calls `history.Record(after)` exactly once per player move; `CompleteBossMove` records once per boss move. `Reset` records the start position once.
- `RetryAnalysis` reuses `pendingBefore`/`pendingMove`/`pendingOutcome` unchanged (the board already shows the move).
- `pendingBefore` is stored for debugging only; remove the field if the compiler warns it is unused.

- [ ] **Step 4: Run to verify pass**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Chess.ChessAttemptMachineTests" chess-machine`
Expected: `result=Passed` (the repetition test may report Inconclusive, which the runner counts as skipped, not failed). If `ASuboptimalMoveIsStillPlayedAndCountsAMove` fails because `g1f1` yields q=1.0, pick another waiting move from `MoveGenerator.LegalMoves` whose `NaiveMate.DefenderDistance(position.Apply(m), 3)` is greater than 2 and use that instead; the behaviour under test (played, counted, q<1) must not change.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Gameplay/Chess/Core/ChessAttemptMachine.cs* Assets/Tests/EditMode/Gameplay/Chess/ChessAttemptMachineTests.cs*
git commit -m "feat(chess): add attempt state machine with analysis, hints, pause and draw handling"
```

---

### Task 5: Background worker and puzzle provider

**Files:**
- Create: `Assets/_Project/Scripts/Gameplay/Chess/ChessAnalysisWorker.cs`
- Create: `Assets/_Project/Scripts/Gameplay/Chess/ChessPuzzleProvider.cs`
- Create: `Assets/Tests/EditMode/Gameplay/Chess/ChessAnalysisWorkerTests.cs`
- Create: `Assets/Tests/EditMode/Gameplay/Chess/ChessPuzzleProviderTests.cs`

**Interfaces:**
- Consumes: `ChessSearchEngine`, `SearchBudget`, `ChessTurnAnalyzer`, `ChessHintBuilder`, `AnalysisRequest/Result`, `HintRequest/ChessHint` (Plans 1, 2), `ChessPuzzleGenerator`, `ChessGeneratorLimits`, `ChessFallbackLibrary`.
- Produces:
  - `sealed class ChessAnalysisWorker : IDisposable { ChessAnalysisWorker(); void Submit(AnalysisRequest); void SubmitHint(HintRequest); bool TryTakeAnalysis(out AnalysisResult); bool TryTakeHint(out ChessHint); void CancelAll(); void Dispose() }` — runs one request at a time on a pool thread with its own `ChessSearchEngine` (cache kept across requests); results are polled from the main thread.
  - `sealed class ChessPuzzleProvider : IDisposable { void Begin(ChessLevel level, int seed, string avoidFen); bool TryTake(out ChessPuzzleResult result); void Cancel(); static int GeneratedCount, FallbackCount }`
  - `sealed class ChessPuzzleResult { ChessPuzzleSnapshot Snapshot; GenerationStats Stats; }`

- [ ] **Step 1: Write the failing tests**

`ChessAnalysisWorkerTests.cs`:

```csharp
using System;
using System.Diagnostics;
using KMA.Gameplay.Chess;
using NUnit.Framework;
using static KMA.Tests.Gameplay.Chess.TestPuzzles;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessAnalysisWorkerTests
    {
        ChessAnalysisWorker worker;

        [SetUp] public void SetUp() => worker = new ChessAnalysisWorker();
        [TearDown] public void TearDown() => worker.Dispose();

        static T Wait<T>(TryTake<T> take, double seconds = 10)
        {
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed.TotalSeconds < seconds)
            {
                if (take(out T value)) return value;
                System.Threading.Thread.Sleep(5);
            }
            throw new TimeoutException("worker produced no result");
        }

        delegate bool TryTake<T>(out T value);

        [Test]
        public void AnalysisRunsOffThreadAndReturnsTheSameAnswerAsInline()
        {
            var request = new AnalysisRequest
            {
                Id = 11, BeforeFen = ChessSearchEngineTests.LadderMateIn2, Move = Uci("b1b7"),
                MovesLeftBefore = 3, NeedDefence = true, Seed = 1
            };
            worker.Submit(request);
            AnalysisResult result = Wait<AnalysisResult>(worker.TryTakeAnalysis);
            AnalysisResult inline = ChessTurnAnalyzer.Analyze(new ChessSearchEngine(SearchBudget.Default()), request);
            Assert.That(result.Id, Is.EqualTo(11));
            Assert.That(result.Status, Is.EqualTo(SearchStatus.Complete));
            Assert.That(result.Quality.Q, Is.EqualTo(inline.Quality.Q));
            Assert.That(result.Defence.Move, Is.EqualTo(inline.Defence.Move));
        }

        [Test]
        public void HintsComeBackOnTheirOwnQueue()
        {
            worker.SubmitHint(new HintRequest { Id = 3, Fen = ChessSearchEngineTests.LadderMateIn2, MovesLeft = 3, Level = 2 });
            ChessHint hint = Wait<ChessHint>(worker.TryTakeHint);
            Assert.That(hint.Id, Is.EqualTo(3));
            Assert.That(hint.Status, Is.EqualTo(SearchStatus.Complete));
            Assert.That(worker.TryTakeAnalysis(out _), Is.False);
        }

        [Test]
        public void RequestsAreAnsweredInOrderOneAtATime()
        {
            for (int id = 1; id <= 4; id++)
                worker.Submit(new AnalysisRequest
                {
                    Id = id, BeforeFen = ChessSearchEngineTests.BackRankMateIn1, Move = Uci("a1a8"), MovesLeftBefore = 2
                });
            for (int id = 1; id <= 4; id++)
                Assert.That(Wait<AnalysisResult>(worker.TryTakeAnalysis).Id, Is.EqualTo(id));
        }

        [Test]
        public void CancelAllDropsQueuedWorkAndNeverBlocks()
        {
            var clock = Stopwatch.StartNew();
            for (int id = 1; id <= 20; id++)
                worker.Submit(new AnalysisRequest
                {
                    Id = id, BeforeFen = "k7/8/8/8/8/8/8/K6R w - - 0 1", Move = Uci("h1h2"), MovesLeftBefore = 5, NeedDefence = true
                });
            worker.CancelAll();
            Assert.That(clock.Elapsed.TotalSeconds, Is.LessThan(1.0));
            System.Threading.Thread.Sleep(300);
            while (worker.TryTakeAnalysis(out AnalysisResult stale))
                Assert.That(stale.Status, Is.EqualTo(SearchStatus.Unknown), "a cancelled request is never reported as a real answer");
        }

        [Test]
        public void WorkerStillWorksAfterCancel()
        {
            worker.CancelAll();
            worker.Submit(new AnalysisRequest
            {
                Id = 99, BeforeFen = ChessSearchEngineTests.BackRankMateIn1, Move = Uci("a1a8"), MovesLeftBefore = 2
            });
            Assert.That(Wait<AnalysisResult>(worker.TryTakeAnalysis).Id, Is.EqualTo(99));
        }

        [Test]
        public void DisposeStopsTheWorker()
        {
            worker.Dispose();
            Assert.DoesNotThrow(() => worker.Submit(new AnalysisRequest
            {
                Id = 1, BeforeFen = ChessSearchEngineTests.BackRankMateIn1, Move = Uci("a1a8"), MovesLeftBefore = 2
            }));
            Assert.That(worker.TryTakeAnalysis(out _), Is.False);
        }
    }
}
```

`ChessPuzzleProviderTests.cs`:

```csharp
using System.Diagnostics;
using KMA.Gameplay.Chess;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessPuzzleProviderTests
    {
        static ChessPuzzleResult WaitFor(ChessPuzzleProvider provider)
        {
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed.TotalSeconds < 15)
            {
                if (provider.TryTake(out ChessPuzzleResult result)) return result;
                System.Threading.Thread.Sleep(5);
            }
            Assert.Fail("provider produced no puzzle");
            return null;
        }

        [TestCase(ChessLevel.Easy)]
        [TestCase(ChessLevel.Normal)]
        [TestCase(ChessLevel.Hard)]
        public void EveryLevelYieldsAPuzzleOfThatLevel(ChessLevel level)
        {
            using var provider = new ChessPuzzleProvider();
            provider.Begin(level, 123, null);
            ChessPuzzleSnapshot s = WaitFor(provider).Snapshot;
            ChessLevelRules rules = ChessLevelRules.For(level);
            Assert.That(s.Level, Is.EqualTo(level));
            Assert.That(s.optimalMoves, Is.EqualTo(rules.OptimalMoves));
            Assert.That(s.maxMoves, Is.EqualTo(rules.MaxMoves));
            Assert.That(s.source, Is.EqualTo(ChessPuzzleSnapshot.GeneratedSource).Or.EqualTo(ChessPuzzleSnapshot.FallbackSource));
            Assert.That(s.timeSeconds, Is.InRange(60, 240));
        }

        [Test]
        public void AvoidFenIsHonouredEvenViaFallback()
        {
            using var provider = new ChessPuzzleProvider();
            provider.Begin(ChessLevel.Normal, 5, null);
            string first = WaitFor(provider).Snapshot.fen;
            provider.Begin(ChessLevel.Normal, 5, first);
            Assert.That(WaitFor(provider).Snapshot.fen, Is.Not.EqualTo(first));
        }

        [Test]
        public void SourceCountersTrackGeneratedAndFallback()
        {
            int before = ChessPuzzleProvider.GeneratedCount + ChessPuzzleProvider.FallbackCount;
            using var provider = new ChessPuzzleProvider();
            provider.Begin(ChessLevel.Easy, 8, null);
            WaitFor(provider);
            Assert.That(ChessPuzzleProvider.GeneratedCount + ChessPuzzleProvider.FallbackCount, Is.EqualTo(before + 1));
        }

        [Test]
        public void CancelBeforeFinishYieldsNoResult()
        {
            using var provider = new ChessPuzzleProvider();
            provider.Begin(ChessLevel.Hard, 1, null);
            provider.Cancel();
            System.Threading.Thread.Sleep(400);
            Assert.That(provider.TryTake(out _), Is.False);
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Chess.ChessAnalysisWorkerTests" chess-worker` (compile error expected: `ChessAnalysisWorker` undefined).

- [ ] **Step 3: Implement worker and provider**

`ChessAnalysisWorker.cs`:

```csharp
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace KMA.Gameplay.Chess
{
    /// Runs analysis and hint requests one at a time on a pool thread. Only immutable request data goes in and plain
    /// result objects come out; the main thread polls the queues each frame and applies results itself.
    public sealed class ChessAnalysisWorker : IDisposable
    {
        readonly object gate = new object();
        readonly ConcurrentQueue<AnalysisResult> analyses = new ConcurrentQueue<AnalysisResult>();
        readonly ConcurrentQueue<ChessHint> hints = new ConcurrentQueue<ChessHint>();
        readonly ChessSearchEngine engine = new ChessSearchEngine(SearchBudget.Default());
        CancellationTokenSource cancel = new CancellationTokenSource();
        bool disposed;

        public void Submit(AnalysisRequest request) => Run(token =>
        {
            AnalysisResult result = ChessTurnAnalyzer.Analyze(engine, request);
            analyses.Enqueue(result);
        });

        public void SubmitHint(HintRequest request) => Run(token =>
        {
            hints.Enqueue(ChessHintBuilder.Build(engine, request));
        });

        public bool TryTakeAnalysis(out AnalysisResult result) => analyses.TryDequeue(out result);

        public bool TryTakeHint(out ChessHint hint) => hints.TryDequeue(out hint);

        /// Stops the running search at its next budget check and discards queued work. Safe to call from any thread.
        public void CancelAll()
        {
            CancellationTokenSource old;
            lock (gate)
            {
                old = cancel;
                cancel = new CancellationTokenSource();
            }
            old.Cancel();
            while (analyses.TryDequeue(out _)) { }
            while (hints.TryDequeue(out _)) { }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                cancel.Cancel();
            }
        }

        void Run(Action<CancellationToken> job)
        {
            CancellationToken token;
            lock (gate)
            {
                if (disposed) return;
                token = cancel.Token;
            }
            Task.Run(() =>
            {
                // One request at a time: the engine and its cache are not thread-safe.
                lock (engine)
                {
                    if (token.IsCancellationRequested) return;
                    engine.UseBudget(SearchBudget.Default(() => token.IsCancellationRequested));
                    job(token);
                }
            });
        }
    }
}
```

A request cancelled mid-search produces an `Unknown` result which is still enqueued after `CancelAll` drained the queues; consumers ignore it by id (Task 4) and `WorkerStillWorksAfterCancel`/`CancelAllDropsQueuedWork...` accept `Unknown` stale results.

`ChessPuzzleProvider.cs`:

```csharp
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace KMA.Gameplay.Chess
{
    public sealed class ChessPuzzleResult
    {
        public ChessPuzzleSnapshot Snapshot;
        public GenerationStats Stats;
    }

    /// Generates a puzzle on a pool thread within the central budget and falls back to a verified puzzle of the same
    /// level. TryTake must be called from the main thread because the fallback pool is read through Resources.
    public sealed class ChessPuzzleProvider : IDisposable
    {
        public static int GeneratedCount { get; private set; }
        public static int FallbackCount { get; private set; }

        readonly ConcurrentQueue<(ChessPuzzleSnapshot generated, GenerationStats stats, ChessLevel level, int seed, string avoid, int ticket)> done =
            new ConcurrentQueue<(ChessPuzzleSnapshot, GenerationStats, ChessLevel, int, string, int)>();
        CancellationTokenSource cancel = new CancellationTokenSource();
        int ticket;

        public void Begin(ChessLevel level, int seed, string avoidFen)
        {
            Cancel();
            int mine = ++ticket;
            CancellationToken token = cancel.Token;
            Task.Run(() =>
            {
                ChessPuzzleSnapshot snapshot = ChessPuzzleGenerator.TryGenerate(level, seed, ChessGeneratorLimits.Default,
                    avoidFen, () => token.IsCancellationRequested, out GenerationStats stats);
                if (!token.IsCancellationRequested) done.Enqueue((snapshot, stats, level, seed, avoidFen, mine));
            });
        }

        public bool TryTake(out ChessPuzzleResult result)
        {
            result = null;
            if (!done.TryDequeue(out var item) || item.ticket != ticket) return false;
            ChessPuzzleSnapshot snapshot = item.generated;
            if (snapshot == null)
            {
                snapshot = ChessFallbackLibrary.Pick(item.level, item.seed, item.avoid);
                FallbackCount++;
            }
            else GeneratedCount++;
            Debug.Log($"[KMA] Chess puzzle {snapshot.source} {snapshot.level} N={snapshot.optimalMoves} M={snapshot.maxMoves} " +
                      $"T={snapshot.timeSeconds}s candidates={item.stats.Candidates} nodes={item.stats.Nodes} " +
                      $"time={item.stats.ElapsedSeconds:0.00}s (generated {GeneratedCount}, fallback {FallbackCount})");
            result = new ChessPuzzleResult { Snapshot = snapshot, Stats = item.stats };
            return true;
        }

        public void Cancel()
        {
            cancel.Cancel();
            cancel = new CancellationTokenSource();
            while (done.TryDequeue(out _)) { }
        }

        public void Dispose() => cancel.Cancel();
    }
}
```

`Cancel()` bumps nothing; a cancelled run never enqueues. `Begin` increments `ticket` so a late item from an earlier `Begin` is ignored by `TryTake`.

- [ ] **Step 4: Run to verify pass**

Run both filters (`ChessAnalysisWorkerTests`, `ChessPuzzleProviderTests`). Expected: `result=Passed`. `Debug.Log` is fine in EditMode tests.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Gameplay/Chess/ChessAnalysisWorker.cs* Assets/_Project/Scripts/Gameplay/Chess/ChessPuzzleProvider.cs* Assets/Tests/EditMode/Gameplay/Chess/ChessAnalysisWorkerTests.cs* Assets/Tests/EditMode/Gameplay/Chess/ChessPuzzleProviderTests.cs*
git commit -m "feat(chess): run puzzle generation and analysis on a worker with fallback"
```

---

### Task 6: Controller, HUD and scene use the new loop

**Files:**
- Modify: `Assets/_Project/Scripts/Core/JourneyControllerAdapter.cs` (expose context)
- Modify: `Assets/_Project/Scripts/Gameplay/Chess/ChessFinalController.cs` (full rewrite of the flow)
- Modify: `Assets/_Project/Scripts/Gameplay/Chess/ChessFinalHud.cs`
- Modify: `Assets/_Project/Scripts/Gameplay/Chess/ChessGuide.cs`
- Modify: `Assets/Editor/ChessFinalSceneConfigurator.cs`
- Modify (regenerated): `Assets/_Project/Scenes/MG_ChessFinal.unity`
- Modify: `Assets/Tests/PlayMode/Gameplay/Chess/ChessFinalSceneTests.cs`
- Modify: `Assets/Tests/EditMode/Gameplay/Chess/ChessGuideTests.cs`

**Interfaces:**
- Consumes: Tasks 1–5.
- Produces:
  - `JourneyControllerAdapter.Context` (`ChallengeAttemptContext`, may be null) and `event Action Configured`.
  - `ChessFinalController.Machine` is now `ChessAttemptMachine` (null until the puzzle is ready), `bool PuzzleReady`, `event Action<ChessAttemptMachine> PuzzleReadyEvent`, `static ChessLevel LevelFor(ChallengeDifficulty)`, `MinigameResult BuildResult(bool solved)`.
  - `ChessFinalHud`: `SetObjective(int optimalMoves)`, `SetMoves(int used, int max)`, `SetIntro(string)`, `SetGenerating(bool)`, `ShowAnalysisError(bool)`, `Button RetryAnalysisButton`, `Button RestartPuzzleButton`, `string MovesText`.
  - `ChessGuide.Build(int optimalMoves, int maxMoves, int timeSeconds)` (seconds ≤ 0 means "not known yet").

Because this task edits large existing files and a serialized scene, follow these steps in order and keep the repo compiling after each one.

- [ ] **Step 1: Read the code you are about to change**

Read `ChessFinalController.cs`, `ChessFinalHud.cs`, `ChessGuide.cs`, `ChessGuideTests.cs`, `Assets/Editor/ChessFinalSceneConfigurator.cs` lines 76–200, `SceneRouter.Journey.cs` around `BindChallenge` and where it is called (search `BindChallenge(`), and `ChessFinalSceneTests.cs`. Confirm in `SceneRouter` that `ConfigureChallenge` runs within the first frames after the scene loads. If it can run later than 10 frames, raise `ContextWaitFrames` in Step 4 accordingly.

- [ ] **Step 2: Expose the Journey context on the adapter**

In `JourneyControllerAdapter.cs` add:

```csharp
public ChallengeAttemptContext Context => context;
public event Action Configured;
```

and at the end of `ConfigureChallenge`: `Configured?.Invoke();`.

- [ ] **Step 3: Update the guide and its tests first (failing test, then code)**

Replace the body of `ChessGuideTests.cs` assertions that mention mistakes with assertions on the new text, then change `ChessGuide.Build` to:

```csharp
public static IReadOnlyList<TutorialStep> Build(int optimalMoves, int maxMoves, int timeSeconds)
{
    string time = timeSeconds > 0
        ? $"Có {MinigameGuidePages.Number(timeSeconds)} giây suy nghĩ cho thế cờ này."
        : "Thời gian suy nghĩ được tính theo độ khó của thế cờ.";
    string goal = $"Chiếu hết trong tối đa {maxMoves} nước; có cách chiếu hết trong {optimalMoves} nước. {time} " +
                  "Trượt bài này không bị trừ lượt thi và không phải Bật cóc.";
    string rules = "Mọi nước đi hợp lệ đều được tính và không thể đi lại. Nước không hợp lệ không tính nước. " +
                   "Đồng hồ chỉ chạy khi đến lượt bạn nghĩ. Hết giờ, hết nước, bị chiếu hết hoặc hòa là chưa đạt. " +
                   "Điểm gồm chất lượng từng nước (70%) và số nước đã dùng (30%); dùng gợi ý chỉ rõ nước đi giới hạn hạng tối đa ở A.";
    return MinigameGuidePages.Pages(
        new TutorialStep("MỤC TIÊU", goal),
        new TutorialStep("ĐIỀU KHIỂN", Controls),
        new TutorialStep("LUẬT", rules));
}
```

Remove `TimeWords` only after Task 8 shows nothing else uses it (it is used by the old controller intro text). Update `Controls` to mention the hint levels unchanged. Test expectations: the goal page contains `"tối đa 3 nước"` and `"2 nước"` for `Build(2, 3, 90)`, mentions the seconds only when `timeSeconds > 0`, and no page contains `"sửa sai"` or `"lần sửa"`.

Run `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Chess.ChessGuideTests" chess-guide`; expect it to fail before the code change and pass after.

- [ ] **Step 4: Rewrite the controller around the machine**

Key behaviours to implement in `ChessFinalController` (keep the existing `MinigameBase` overrides, `Configure(...)`, `RequestMove`, `BossTurn` coroutine and `Resolve` structure; change what feeds them):

1. Fields: `ChessAttemptMachine machine`, `ChessAnalysisWorker worker`, `ChessPuzzleProvider provider`, `ChessPuzzleSnapshot snapshot`, `static string lastFen` (avoid immediate repeats in one app session), `const int ContextWaitFrames = 10`.
2. `public ChessAttemptMachine Machine => machine;` `public bool PuzzleReady => machine != null;`.
3. `Awake`: `base.Awake(); SetTutorialGate(true);` only. No machine here (spec: do not generate before the context is known).
4. `Start`: subscribe board/hud buttons that do not need the machine, show `hud.SetGenerating(true)`, disable Start and Hint buttons, set toast-free state, call `StartCoroutine(Boot())`.
5. `Boot()`:
   - `var adapter = GetComponent<JourneyControllerAdapter>();` wait up to `ContextWaitFrames` frames until `adapter == null || adapter.Context != null`.
   - `ChessLevel level = adapter?.Context != null ? LevelFor(adapter.Context.Difficulty) : ChessLevel.Normal;`
   - `provider = new ChessPuzzleProvider(); provider.Begin(level, Environment.TickCount, lastFen);` then `yield return new WaitUntil(() => provider.TryTake(out result))`; on a disabled/destroyed controller the coroutine stops and `OnDestroy` cancels the provider.
   - `lastFen = result.Snapshot.fen; StartAttempt(result.Snapshot);`
6. `StartAttempt(ChessPuzzleSnapshot s)`: create `machine = new ChessAttemptMachine(s)`, subscribe `PhaseChanged`, `MoveCommitted`, `AnalysisRequested` (→ `worker.Submit`), `HintRequested` (→ `worker.SubmitHint`), `IllegalMoveRejected` (→ toast "Nước đi không hợp lệ. Chọn ô khác."), `HintFailed` (→ toast "Chưa tính được gợi ý. Thử lại."), `Finished`; create `worker = new ChessAnalysisWorker()`; then the old `Start` UI initialisation using the snapshot: `hud.SetObjective(s.optimalMoves)`, `hud.SetMoves(0, s.maxMoves)`, `hud.SetClock(machine.Clock.Remaining)`, `hud.SetIntro($"Chiếu hết trong tối đa {s.maxMoves} nước.\nThời gian suy nghĩ {FormatSeconds(s.timeSeconds)}.")`, `hud.SetGenerating(false)`, `hud.ShowIntro(true)`, `board.Render(machine.Position, null)`, dialogue adapter configure (Task 7), `RefreshInput()`, enable the Start button (still gated by `ChessDialogueAdapter.OpeningReady`), raise `PuzzleReadyEvent`.
7. `BeginAttempt`: same as before but returns early when `machine == null`; calls `machine.Begin()`.
8. `Update`: `base.Update();` then if `machine == null` return; pause handling via `machine.Pause()/Resume()` exactly as before; `machine.Tick(Time.deltaTime)`; drain `worker.TryTakeAnalysis` → `machine.CompleteAnalysis(r)`, `worker.TryTakeHint` → `machine.CompleteHint(h)`; `hud.SetClock(...)`. After `CompleteAnalysis` returns, if `machine.Phase == ChessAttemptPhase.BossTurn && bossRoutine == null` start the boss coroutine (store the `Coroutine` and null it at the end). A result drained while `Paused` is buffered inside the machine, so the boss coroutine must be started from the `PhaseChanged(BossTurn)` handler instead of from `Update` to cover the buffered case. Do the latter (handler), not the former.
9. `OnPhaseChanged(phase)`: `hud.SetTurn(phase)`; `hud.SetHintAvailable(phase == PlayerTurn)`; `hud.ShowAnalysisError(phase == AnalysisError)`; on `BossTurn` start `StartCoroutine(BossTurn())` once; `RefreshInput()`.
10. `Submit(ChessMove move)`: `machine.Submit(move)`; on `SubmitResult.TimeUp` nothing else (the machine already failed); on `Accepted` update `hud.SetMoves(machine.MovesUsed, machine.MaxMoves)`.
11. `BossTurn()` coroutine: as before but reads `machine.PendingBossReply` and calls `machine.CompleteBossMove()`; guard every `WaitUntil` with `machine.Phase == ChessAttemptPhase.BossTurn` so a pause or restart cannot commit twice. If the phase left BossTurn (Restart) exit without committing.
12. `RevealHint()`: `machine.RequestHint()`; when `CompleteHint` succeeds the `PhaseChanged(PlayerTurn)` fires; show text by polling `machine.HintText` change in `OnPhaseChanged(PlayerTurn)`: if `machine.HintText != shownHint` then `dialogue.Hint(machine.HintText)` / fallback cast.Say, update `shownHint`. Reset `shownHint = null` on `MoveCommitted`.
13. Analysis-error UI: `hud.RetryAnalysisButton.onClick` → `machine.RetryAnalysis()`; `hud.RestartPuzzleButton.onClick` → `RestartPuzzle()` which does `worker.CancelAll(); machine.Restart(); hud.SetMoves(0, M); hud.SetClock(...); board.Render(machine.Position, null); hud.ShowIntro(true); resolving = false; cast poses reset`.
14. `OnFinished(bool solved)`: unchanged shape (`resolving` guard, `Resolve` coroutine). In `Resolve`, text for failures by `machine.EndReason`: TimeUp "Hết giờ", OutOfMoves "Hết số nước", Mated "Bị chiếu hết", Draw "Hòa cờ". Poses: success cheer; failure hurt/angry or handsOnHips for TimeUp.
15. `BuildResult(bool solved)`: `ChessScore s = machine.Result; return new MinigameResult(s.Pass, s.Score, ToRank(s.Grade));` where `static Rank ToRank(ChessGrade g)` maps `APlus → Rank.S` (compatibility value, spec §7; the A+ label is rendered in Plan 3) and F/D/C/B/A to the same-named `Rank`.
16. `BuildMetrics(...)`: `new ChallengeMetrics(elapsed: machine.Clock.Elapsed, completedTargets: machine.MovesUsed, mistakes: count of machine.Moves with Q < 1.0, hintUsed: machine.HintUsed, detail: Detail())` where `Detail()` returns `"Nước: U/M · Tối ưu: N · Thời gian mm:ss/mm:ss"` plus `" · Có dùng gợi ý"` when used, or the failure reason text. Plan 3 replaces this with the richer record.
17. `OnDisable`/`OnDestroy`: `worker?.CancelAll(); worker?.Dispose(); provider?.Cancel(); provider?.Dispose();` and unsubscribe machine events.
18. `IMinigameGuideSource.BuildGuide()`: `ChessGuide.Build(machine?.OptimalMoves ?? ChessLevelRules.For(currentLevel).OptimalMoves, machine?.MaxMoves ?? ChessLevelRules.For(currentLevel).MaxMoves, machine?.Clock.Limit is float l ? Mathf.RoundToInt(l) : 0)` (store `currentLevel` in `Boot`).
19. `static ChessLevel LevelFor(ChallengeDifficulty d) => d switch { Easy => ChessLevel.Easy, Hard => ChessLevel.Hard, _ => ChessLevel.Normal }`.

- [ ] **Step 5: Update the HUD**

In `ChessFinalHud.cs`: rename the `mistakes` label field to `moves`, add `TMP_Text introLabel`, `GameObject generatingLabel`... To avoid new scene objects for "generating", reuse the `turn` label: `SetGenerating(true)` → `SetTurnText("Đang tạo thế cờ…")` and `introCard.SetActive(false)`; `SetGenerating(false)` restores. Add the analysis-error card as a `GameObject analysisErrorCard` with `Button retryAnalysis, restartPuzzle`, shown by `ShowAnalysisError(bool)`. New/changed members:

```csharp
public string MovesText => moves.text;
public Button RetryAnalysisButton => retryAnalysis;
public Button RestartPuzzleButton => restartPuzzle;
public void SetObjective(int optimalMoves) => objective.text = VietText.Fix($"Chiếu hết, tối ưu {optimalMoves} nước");
public void SetMoves(int used, int max) => moves.text = VietText.Fix($"Nước: {used}/{max}");
public void SetIntro(string text) => introLabel.text = VietText.Fix(text);
public void SetGenerating(bool generating) { if (generating) { SetTurnText("Đang tạo thế cờ…"); introCard.SetActive(false); } }
public void ShowAnalysisError(bool visible) => analysisErrorCard.SetActive(visible);
```

`SetTurn(phase)` switch uses the new `ChessAttemptPhase`: PlayerTurn "Lượt của bạn", Analyzing "Đang phân tích…", BossTurn "Lượt giảng viên", Completed "Hoàn thành", Paused "Tạm dừng", AnalysisError "Chưa phân tích được", Failed `turn.text`, default "Đọc đề, sau đó bấm Bắt đầu". Extend `Configure(...)` with `TMP_Text introLabel, GameObject analysisErrorCard, Button retryAnalysis, Button restartPuzzle`.

- [ ] **Step 6: Update the scene configurator and regenerate the scene**

In `ChessFinalSceneConfigurator.cs`: change the `Objective` chip text to `"Chiếu hết"`, rename `Mistakes` chip text to `"Nước: 0/3"`, set the intro label initial text to `"Đang tạo thế cờ…"`, and add an analysis-error card next to the intro card:

```csharp
Image errorCard = UiKit.Panel(parent, "AnalysisErrorCard", alpha: 1f);
UiKit.Place(errorCard.rectTransform, centre, centre, new Vector2(0f, -30f), new Vector2(640f, 320f));
TMP_Text errorText = UiKit.Label(errorCard.transform, "Text",
    "Chưa phân tích được nước đi này.\nNước của bạn vẫn được giữ. Đồng hồ đã dừng.",
    MinigameUiTheme.BodyLarge, MinigameUiTheme.TextPrimary);
UiKit.Place(errorText.rectTransform, top, top, new Vector2(0f, -36f), new Vector2(580f, 140f));
ButtonHandle retry = UiKit.Button(errorCard.transform, "RetryAnalysisButton", "Thử lại", ButtonVariant.Primary);
UiKit.Place((RectTransform)retry.Button.transform, bottom, bottom, new Vector2(-170f, 36f), new Vector2(300f, MinigameUiTheme.ButtonHeight));
ButtonHandle restartPuzzle = UiKit.Button(errorCard.transform, "RestartPuzzleButton", "Chơi lại đề", ButtonVariant.Secondary);
UiKit.Place((RectTransform)restartPuzzle.Button.transform, bottom, bottom, new Vector2(170f, 36f), new Vector2(300f, MinigameUiTheme.ButtonHeight));
errorCard.gameObject.SetActive(false);
errorCard.transform.SetAsLastSibling();
```

and pass `introText, errorCard.gameObject, retry.Button, restartPuzzle.Button` to the new `hud.Configure(...)`. Then rebuild the scene: `"$UNITY" -batchmode -quit -projectPath "$(pwd -W)" -executeMethod KMA.EditorTools.ChessFinalSceneConfigurator.BuildScene -logFile Builds/chess-scene.log` (confirm the class's namespace and that `BuildScene` is the menu method `KMA/Chess Final/Build Scene`; if the class is not `public static`-reachable by `-executeMethod`, run it from the Editor menu instead). Check `git diff --stat Assets/_Project/Scenes/MG_ChessFinal.unity` shows only the expected HUD changes; if unrelated churn appears, revert the scene and apply the HUD wiring only.

- [ ] **Step 7: Rewrite the PlayMode scene tests for the new flow**

In `ChessFinalSceneTests.cs` replace tree-based tests. After `LoadScene`, wait for the puzzle: `yield return new WaitUntil(() => controller.PuzzleReady);` (timeout 20 s) and then for the opening flow as before. Tests (each a `[UnityTest]`):

- `BoardIsLockedAndClockStillUntilStart` — as before, using `Machine.Phase == ChessAttemptPhase.Intro`.
- `DirectSceneUsesNormalDifficulty` — `Machine.OptimalMoves == 2`, `Machine.MaxMoves == 3`, `Machine.Clock.Limit` in 60..240, HUD `MovesText` is `Nước: 0/3`.
- `OptimalLineSolvesAndReportsGrade` — compute the optimal first move with `ChessHintBuilder.Build(new ChessSearchEngine(SearchBudget.Default()), new HintRequest{ Fen = Machine.Position.ToFen(), MovesLeft = Machine.MaxMoves - Machine.MovesUsed, Level = 3 }).Move`, play it via `board.ClickSquare` (promotion pieces are never generated), wait for `Phase` to leave `Analyzing`/`BossTurn` back to `PlayerTurn` or `Completed`, repeat until `result != null`; assert `result.Pass`, `result.Score >= 8.9f` and `Machine.Moves.All(m => m.Q > 0.999)`.
- `SuboptimalMoveIsPlayedNotUndone` — play a legal non-mating move that is not optimal, wait for `Analyzing` to finish, assert `Machine.MovesUsed == 1`, `board` shows the new position (`Machine.Position.ToFen()` differs from the start) and `HUD.MovesText` contains `1/`.
- `IllegalClickChangesNothing` — click an empty square then another; `MovesUsed` stays 0 and the clock keeps `Running`.
- `PauseFreezesTheClockAndAnalysisResultWaits` — as the old pause test, plus submit a move, set `Time.timeScale = 0` before the result is drained, wait 0.3 s real time, assert `Phase == Paused`; restore `timeScale = 1`; assert it proceeds to `BossTurn` exactly once.
- `HintStopsTheClockUntilShown` — click hint, assert clock not running while `Phase == Analyzing`, then `PlayerTurn` with `Clock.Running` and `Machine.HintLevel == 1`.
- `RestartPuzzleKeepsTheSamePuzzle` — via `hud.RestartPuzzleButton` after forcing `AnalysisError` (call `Machine.CompleteAnalysis` with an Unknown result) assert `Machine.Snapshot.fen` unchanged, `MovesUsed == 0`, phase `Intro`.
- `DisablingTheControllerCancelsTheWorker` — disable the controller during analysis; after 1 s no exception/log errors (`LogAssert.NoUnexpectedReceived()`), then re-enable not required.
- Keep `PosesFollowTheTurn...` adapting the wrong-move step to a suboptimal legal move; `TheTeacherBubbleDoesNotExpireWhileTheGameIsFrozen` unchanged.

Run: `tools/run-unity-tests.sh PlayMode "KMA.Tests.Gameplay.Chess.ChessFinalSceneTests" chess-scene`. Expected `result=Passed`.

- [ ] **Step 8: Run the chess EditMode suite and commit**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Chess" chess-all` — expect pass (legacy tests still compile because the legacy classes still exist).

```bash
git add Assets/_Project/Scripts/Core/JourneyControllerAdapter.cs Assets/_Project/Scripts/Gameplay/Chess/ChessFinalController.cs Assets/_Project/Scripts/Gameplay/Chess/ChessFinalHud.cs Assets/_Project/Scripts/Gameplay/Chess/ChessGuide.cs Assets/Editor/ChessFinalSceneConfigurator.cs Assets/_Project/Scenes/MG_ChessFinal.unity Assets/Tests/PlayMode/Gameplay/Chess/ChessFinalSceneTests.cs Assets/Tests/EditMode/Gameplay/Chess/ChessGuideTests.cs
git commit -m "feat(chess): play the final exam against the AI with generated puzzles"
```

---

### Task 7: Dialogue follows the semantic state

**Files:**
- Modify: `Assets/_Project/Scripts/Gameplay/Chess/ChessDialogueAdapter.cs`
- Modify: `Assets/Editor/GameplayDialogueContentBuilder.cs` (chess lines)
- Modify (regenerated): `Assets/_Project/Resources/Journey/GameplayDialogues.asset`
- Modify: `Chay-tron-the-chat-loi-thoai-bien-tap.md` (chess sections that quote the changed lines)
- Modify: `Assets/Tests/PlayMode/Gameplay/Chess/ChessDialogueTests.cs`
- Modify: `Assets/Tests/EditMode/Progression/JourneyDialogueDataTests.cs` / `Assets/Tests/EditMode/Presentation/GameplayDialogueSchedulerTests.cs` only if they assert the old chess strings (search first).

**Interfaces:**
- Consumes: `ChessAttemptMachine` (Task 4), `ChessFinalController.Machine`/`PuzzleReadyEvent` (Task 6).
- Produces: `ChessDialogueAdapter.Configure(ChessFinalController, GameplayDialogueHost, ChessCastView)` unchanged signature; it subscribes to the machine once the puzzle is ready.

- [ ] **Step 1: Find every place that asserts or quotes the old chess lines**

Run: `grep -rn "chess\.\(start\|mistake\|failed\|timeout\)\|ch\.extra\|m2_001\|Hai nước, 90\|lần sửa" Assets docs Chay-tron-the-chat-loi-thoai-bien-tap.md --include=* -r | grep -v "\.meta"`. Write down each test and doc line that must change.

- [ ] **Step 2: Write failing tests for the new behaviour**

In `ChessDialogueTests.cs` (PlayMode) add tests that, with a ready puzzle:
- `chess.start` text contains neither a digit-based time (`90`) nor `sửa`.
- After a suboptimal move no `chess.mistake1`/`chess.mistake2` event plays.
- A level-2 hint played, then a board change (a committed move), then wait: the old hint line is not replayed (`GameplayDialogueHost.IsIdle` after `Clear`).
- `chess.failed` plays for `OutOfMoves`, `Draw` and `Mated`, `chess.timeout` for TimeUp, `chess.solved` for Mate (drive via `Machine` events or a forced `Finished`).
- `ch.extra.begin` does not play when the puzzle has no bishop on the board and no condition references an id string.

Run: `tools/run-unity-tests.sh PlayMode "KMA.Tests.Gameplay.Chess.ChessDialogueTests" chess-dialogue`. Expect failures.

- [ ] **Step 3: Update the adapter**

Replace the machine type and delete every `Puzzle.id == "m2_001"` condition:
- `Configure(...)`: store the controller; subscribe to `controller.PuzzleReadyEvent` and, if `controller.Machine != null` already, call `Bind(controller.Machine)`. `Bind` unsubscribes the previous machine, subscribes `MoveCommitted`.
- `Began()`: `host.Clear(); view.SetTeacher("strictLook"); view.SetStudent("think");` then emit `ch.extra.begin` only when `HasSecondAttacker(machine.Position)` (a white piece other than king/pawn besides the first, i.e. at least two white non-king pieces) and `machine.CanMove && machine.MovesUsed == 0`.
- Remove `Mistake(int)`; the controller no longer calls it. Keep the `chess.mistake*` events in the asset unused (do not delete assets that other tests enumerate; see Step 1 findings) or delete them together with those tests if Step 1 shows nothing depends on them.
- `Hint(string text)`: keep, but condition lambda uses `machine.Revision == revision && machine.HintLevel == level && machine.CanMove` where `revision`/`level` are captured when called.
- `FinishDialogue(bool solved)`: id = solved ? `chess.solved` : `machine.EndReason == ChessEndReason.TimeUp ? "chess.timeout" : "chess.failed"`.
- `Update`: `firstCorrect` condition becomes `machine.MovesUsed == 1 && machine.Moves.Count == 1 && machine.Moves[0].Q >= 0.999 && machine.CanMove`; low-time warning unchanged; `relief` unchanged.

- [ ] **Step 4: Update the dialogue content builder and regenerate the asset**

In `GameplayDialogueContentBuilder.cs` set these texts (poses and ids unchanged):
- `chess.start`: `"Thế cờ này có giới hạn số nước và thời gian riêng. Tôi không chấm điểm cho việc ngồi nhìn."`
- `chess.failed`: `"Chưa chiếu hết được. Làm lại bài."`
- `chess.timeout`: `"Hết giờ. Ngồi lâu không làm nước đi đúng hơn."` (unchanged)
- `ch.extra.begin`: `"Phải nhìn cả hai quân phối hợp..."`
- `ch.extra.firstcorrect`: student `"Nước đầu hợp lý rồi... còn phải ép cho xong."`, teacher unchanged `"Mới một nước. Đừng mừng sớm."`

Re-run the builder (find the exact menu/execute method with `grep -n "MenuItem\|public static void" Assets/Editor/GameplayDialogueContentBuilder.cs`) in batch mode and confirm `git diff` touches only `GameplayDialogues.asset` chess entries.

- [ ] **Step 5: Update the editorial dialogue document**

In `Chay-tron-the-chat-loi-thoai-bien-tap.md` update the quoted chess rows for `chess.start`, `chess.failed`, `ch.extra.begin`, `ch.extra.firstcorrect`, and the note at the end of section 4 ("câu hiện tại chỉ dành cho `m2_001`") to say the begin line plays when two attacking pieces are on the board. Remove rows for `chess.mistake1/2` or mark them "không còn phát". Do not change the Journey intro rows here (Plan 3).

- [ ] **Step 6: Run tests and commit**

Run the PlayMode dialogue filter and the EditMode filters named in Step 1. Expected: pass.

```bash
git add Assets/_Project/Scripts/Gameplay/Chess/ChessDialogueAdapter.cs Assets/Editor/GameplayDialogueContentBuilder.cs Assets/_Project/Resources/Journey/GameplayDialogues.asset* Chay-tron-the-chat-loi-thoai-bien-tap.md Assets/Tests
git commit -m "feat(chess): drive dialogue from semantic attempt state instead of puzzle id"
```

---

### Task 8: Remove the fixed-answer puzzle chain

**Files:**
- Delete: `Assets/_Project/Scripts/Gameplay/Chess/Core/PuzzleDefinition.cs`, `PuzzleGrader.cs`, `PuzzleHints.cs`, `PuzzleVerifier.cs`, `ChessFinalStateMachine.cs`
- Delete: `Assets/_Project/Scripts/Gameplay/Chess/ChessPuzzleLibrary.cs`
- Delete: `Assets/_Project/Resources/Chess/Puzzles/` (both JSON files and metas)
- Delete: `Assets/Tests/EditMode/Gameplay/Chess/PuzzleGraderTests.cs`, `PuzzleVerifierTests.cs`, `ShippedPuzzleTests.cs`, `ChessFinalStateMachineTests.cs`
- Modify: `Assets/Tests/EditMode/Gameplay/Chess/TestPuzzles.cs` (keep only `Uci`)
- Modify: any remaining reference found by the search in Step 1.

- [ ] **Step 1: Find remaining references**

Run: `grep -rn "PuzzleDefinition\|PuzzleGrader\|PuzzleVerifier\|PuzzleHints\|ChessFinalStateMachine\|ChessPuzzleLibrary\|ChessFinalPhase\|ChessFailReason\|GradeKind\|TimeWords" Assets --include=*.cs`. Expected remaining users: the files listed above plus `ChessGuide.TimeWords` (delete it if unused), `Assets/Editor/PlayModeScreenshotQaStates.cs`, `Assets/Tests/EditMode/EditorTools/DialoguePresentationAuthoringTests.cs`, `Assets/Tests/PlayMode/Presentation/*`. For each, replace the old API with the new one (`ChessAttemptPhase`, `ChessEndReason`, `Machine.Snapshot`), or delete the obsolete assertion with a comment-free removal.

- [ ] **Step 2: Reduce `TestPuzzles.cs`**

```csharp
using KMA.Gameplay.Chess;

namespace KMA.Tests.Gameplay.Chess
{
    internal static class TestPuzzles
    {
        public static ChessMove Uci(string text)
        {
            ChessMove.TryParseUci(text, out ChessMove move);
            return move;
        }
    }
}
```

(Match the signature the file already exposes; Plan 1/2 tests call `Uci(...)` through `using static`.)

- [ ] **Step 3: Delete the legacy files**

```bash
git rm Assets/_Project/Scripts/Gameplay/Chess/Core/PuzzleDefinition.cs* Assets/_Project/Scripts/Gameplay/Chess/Core/PuzzleGrader.cs* Assets/_Project/Scripts/Gameplay/Chess/Core/PuzzleHints.cs* Assets/_Project/Scripts/Gameplay/Chess/Core/PuzzleVerifier.cs* Assets/_Project/Scripts/Gameplay/Chess/Core/ChessFinalStateMachine.cs* Assets/_Project/Scripts/Gameplay/Chess/ChessPuzzleLibrary.cs*
git rm -r Assets/_Project/Resources/Chess/Puzzles
git rm Assets/Tests/EditMode/Gameplay/Chess/PuzzleGraderTests.cs* Assets/Tests/EditMode/Gameplay/Chess/PuzzleVerifierTests.cs* Assets/Tests/EditMode/Gameplay/Chess/ShippedPuzzleTests.cs* Assets/Tests/EditMode/Gameplay/Chess/ChessFinalStateMachineTests.cs*
```

`tools/chess/` (Python puzzle sourcing) is now unused by the game; leave it and mention it in the commit body rather than deleting a tool the user may still want.

- [ ] **Step 4: Compile and run every affected suite**

Run: `tools/run-unity-tests.sh EditMode "" editmode-all` then `tools/run-unity-tests.sh PlayMode "KMA.Tests" playmode-all` (full suites per project practice; allow the time). Expected: `result=Passed`. Any failure outside chess is investigated, not skipped; report it plainly.

- [ ] **Step 5: Commit**

```bash
git add -A Assets
git commit -m "refactor(chess): remove the fixed-answer puzzle chain replaced by generated puzzles"
```

---

### Task 9: Interaction check and screenshots

**Files:**
- Create: `docs/qa/2026-10-13-chess-game-loop.md`

- [ ] **Step 1: Load the `testing-unity-ui-with-screenshots` skill** and follow it to capture `MG_ChessFinal` at the project's target frame for: generating state, intro (shows moves limit and time), mid-game with `Nước: 1/3`, analysis-error card, a hint shown, and the failure/success end states. Use `tools/qa-screenshot.sh` as that skill directs.

- [ ] **Step 2: Check text fits and nothing overlaps** at the target frame (intro text, objective chip `Chiếu hết, tối ưu N nước`, moves chip, error card buttons). Fix layout in `ChessFinalSceneConfigurator.cs` and rebuild the scene if anything clips, then re-capture.

- [ ] **Step 3: Write `docs/qa/2026-10-13-chess-game-loop.md`** listing: tests run with their `result/total/passed/failed` lines, the screenshots taken (paths), what was verified in the Editor only, and what is not verified (Android device, performance of a worker on device). Do not claim Android coverage.

- [ ] **Step 4: Commit**

```bash
git add docs/qa/2026-10-13-chess-game-loop.md docs/qa/*.png
git commit -m "docs(qa): record the chess game loop checks and captures"
```

---

## Self-Review (Plan 2)

- **Spec coverage:** §1 rules (Tasks 4, 6); §3.2 difficulty from context, no pre-init (Task 6 steps 4–5); §4 runtime budget/Unknown/worker/generation ids/hint stops clock (Tasks 2, 4, 5); §5 clock states and Submit-first check (Task 4); §6.3 end order and draws (Tasks 1, 4); §7 hints three levels + cap + semantic dialogue + HUD (Tasks 3, 4, 6, 7); §8 components map to Tasks 4–6; §10 PlayMode list (Task 6 step 7, Task 7). Deferred to Plan 3: §7 result display and A+ formatter, §9 persistence/retry/migration, journey/guide copy elsewhere, perf on Android.
- **Placeholder scan:** none left as TBD; Task 6 steps 4 and 7 give behaviour per method rather than whole files because the existing controller and tests are large; the implementer must read them first (Step 1), which is stated.
- **Type consistency:** `ChessAttemptPhase`, `ChessEndReason`, `SubmitResult`, `AnalysisRequest/Result`, `HintRequest/ChessHint`, `MoveQuality` (Plan 1) and `ChessScore.Grade` are used with the same names across Tasks 2–7. `ChessFinalController.Machine` changes type in Task 6, so the adapter (Task 7) and PlayMode tests were written for `ChessAttemptMachine`.
- **Known soft spots:** the repetition test in Task 4 may be Inconclusive by design; Task 3's `FirstOrDefault` overload has a documented fallback; scene regeneration (Task 6 step 6) needs a Unity batch run and a diff check.
