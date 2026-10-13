# Chess AI Core Implementation Plan (Plan 1 of 3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Unity-free C# core that generates verified mate-in-N chess puzzles, proves best play, derives a per-puzzle time limit, and grades each move and the whole attempt from F to A+.

**Architecture:** A mate-proof search (`ChessSearchEngine`: iterative deepening, and/or search, state cache, node/time/cancel budget) is the single primitive. The generator, opponent, analyzer and move grader all call it. Everything lives in `KMA.Gameplay.Chess.Core` (`noEngineReferences: true`), so it can run on a worker thread and be tested in EditMode against an independent brute-force checker.

**Tech Stack:** C# (Unity 6000.3.23f1), NUnit/Unity Test Framework (EditMode), existing `ChessPosition`/`MoveGenerator`.

**Spec:** `docs/superpowers/specs/2026-10-12-chess-generated-mate-design.md` (sections 3, 4, 5, 6.1, 6.2, 10 EditMode, performance part of 10).

**Series:** Plan 1 (this) = core AI. Plan 2 = `2026-10-13-chess-game-loop.md` (state machine, hints, controller, HUD, dialogue, guide). Plan 3 = `2026-10-13-chess-persistence-results.md` (save data, result screens, A+ display, QA/perf report). Plan 1 changes no gameplay and is safe to merge alone.

## Global Constraints

- Core code goes in `Assets/_Project/Scripts/Gameplay/Chess/Core/`, namespace `KMA.Gameplay.Chess`, no `UnityEngine` reference (spec §4: "Core thuần C#").
- Difficulty table (spec §3): Easy N=1 M=2; Normal N=2 M=3; Hard N=3 M=5. N counts player moves only.
- Search budget per runtime request and for the generator: 2 seconds and 200,000 nodes, held in one central config (spec §3.2, §4).
- Time formula (spec §5): `T = roundUpTo5(clamp(25 + 25*N + 8*B + 15*Q, 60, 240))` seconds; `B` = mean `log2(1 + legalMoves)` over non-terminal states of the optimal proof tree, each normalized state once; `Q` = max quiet attacker moves on one line of the optimal tree. Store estimator version.
- Move quality (spec §6.1): `q = d_best / d_chosen` in [0,1]; chosen move that loses the mate = 0; no forced mate left = 0; Unknown is never 0.
- Score (spec §6.2): `rawScore = 10 * (0.7*Qmean + 0.3*clamp(N/U,0,1))`, one decimal; explicit-move hint caps at 8.9 after rounding. Grades: A+ >= 9, A >= 8, B >= 7, C >= 6, D >= 5, F < 5. Failure = score 0, F.
- Generated pieces: one king per side; white (player) extras from queen/rook/bishop/knight only; 4–6 pieces total; no castling, no en passant, halfmove 0, white moves first; kings not adjacent; black king not in check; white has a legal move.
- A candidate lacking a complete proof is discarded, never labelled verified.
- Project convention: commit straight to `master`; commit messages carry **no** `Co-Authored-By` or other AI trailer.
- Run tests with `tools/run-unity-tests.sh EditMode "<filter>" <name>` (Unity Editor must be closed). New files get `.meta` files from Unity; `git add` them with the sources.

## Review Focus

- A stalemating "mate" move (queen to g6 in `7k/5K2/8/6Q1/8/8/8/8 w`) must score q=0, never count as mate (tests in Tasks 1 and 4).
- Budget exhausted or cancelled mid-search must surface as `Unknown`, never as "no mate" or q=0 (Tasks 1, 4, 5).
- Same seed and node budget must reproduce the same puzzle; wall-clock time must not change the result when only node limits are set (Task 5).
- A position that already has a shorter mate than N must be rejected as an N-puzzle (Task 5 test with naive checker).
- Time estimate must stay within 60–240 and a multiple of 5 for extreme B/Q (Task 3).
- Score boundaries 8.9/9.0, 7.9/8.0 and float noise such as 8.999999 must land on the right grade (Task 4).

---

### Task 1: Levels, search budget and mate-proof engine

**Files:**
- Create: `Assets/_Project/Scripts/Gameplay/Chess/Core/ChessLevel.cs`
- Create: `Assets/_Project/Scripts/Gameplay/Chess/Core/SearchBudget.cs`
- Create: `Assets/_Project/Scripts/Gameplay/Chess/Core/ChessSearchEngine.cs`
- Create: `Assets/Tests/EditMode/Gameplay/Chess/NaiveMate.cs`
- Create: `Assets/Tests/EditMode/Gameplay/Chess/ChessSearchEngineTests.cs`

**Interfaces:**
- Produces:
  - `enum ChessLevel { Easy, Normal, Hard }`
  - `readonly struct ChessLevelRules { int OptimalMoves; int MaxMoves; static ChessLevelRules For(ChessLevel) }`
  - `enum SearchStop { None, Nodes, Time, Cancelled }`
  - `sealed class SearchBudget { SearchBudget(int maxNodes, double maxSeconds, Func<bool> isCancelled = null); int Nodes; SearchStop Stopped; void Charge(); static SearchBudget Default() }` (`Charge` throws `SearchAbortedException`)
  - `enum SearchStatus { Complete, Unknown }`
  - `readonly struct MateSearch { SearchStatus Status; int Distance; bool Forced; bool Unknown }` (`Distance` = attacker moves, 0 = no forced mate within the limit)
  - `sealed class ChessSearchEngine { ChessSearchEngine(SearchBudget); void UseBudget(SearchBudget); MateSearch MateDistance(ChessPosition attackerToMove, int maxMoves); MateSearch MoveDistance(ChessPosition afterAttackerMove, int maxMoves); static string PositionKey(ChessPosition) }`
  - Test helper `NaiveMate.AttackerDistance(ChessPosition, int limit)` / `DefenderDistance(ChessPosition, int limit)` returning `int.MaxValue` for "no mate within limit".

- [ ] **Step 1: Write `NaiveMate` (independent checker) and the failing engine tests**

`Assets/Tests/EditMode/Gameplay/Chess/NaiveMate.cs` — deliberately a different formulation (minimax over distance values, no cache, no move ordering) so it can cross-check the engine:

```csharp
using System;
using KMA.Gameplay.Chess;

namespace KMA.Tests.Gameplay.Chess
{
    /// Brute-force mate distance. Attacker moves needed to force mate, int.MaxValue when not within `limit`.
    internal static class NaiveMate
    {
        public const int None = int.MaxValue;

        /// Attacker to move.
        public static int AttackerDistance(ChessPosition position, int limit)
        {
            int best = None;
            foreach (ChessMove move in MoveGenerator.LegalMoves(position))
            {
                int d = DefenderDistance(position.Apply(move), limit);
                if (d < best) best = d;
            }
            return best;
        }

        /// Defender to move; `limit` counts the attacker move that produced `after`.
        public static int DefenderDistance(ChessPosition after, int limit)
        {
            var defences = MoveGenerator.LegalMoves(after);
            if (defences.Count == 0) return MoveGenerator.IsInCheck(after) ? 1 : None;
            if (limit <= 1) return None;
            int worst = 0;
            foreach (ChessMove defence in defences)
            {
                int inner = AttackerDistance(after.Apply(defence), limit - 1);
                if (inner == None) return None;
                worst = Math.Max(worst, 1 + inner);
            }
            return worst;
        }
    }
}
```

`Assets/Tests/EditMode/Gameplay/Chess/ChessSearchEngineTests.cs`:

```csharp
using System;
using KMA.Gameplay.Chess;
using NUnit.Framework;
using static KMA.Tests.Gameplay.Chess.TestPuzzles;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessSearchEngineTests
    {
        public const string BackRankMateIn1 = "6k1/5ppp/8/8/8/8/8/R3K3 w - - 0 1";
        public const string LadderMateIn2 = "7k/8/8/8/8/8/R7/1R4K1 w - - 0 1";
        public const string NoMateInTwo = "k7/8/8/8/8/8/8/K6R w - - 0 1";
        public const string StalemateTrap = "7k/5K2/8/6Q1/8/8/8/8 w - - 0 1";

        static ChessSearchEngine Engine() => new ChessSearchEngine(new SearchBudget(1_000_000, 60));

        [Test]
        public void LevelRulesMatchTheSpecTable()
        {
            Assert.That(ChessLevelRules.For(ChessLevel.Easy).OptimalMoves, Is.EqualTo(1));
            Assert.That(ChessLevelRules.For(ChessLevel.Easy).MaxMoves, Is.EqualTo(2));
            Assert.That(ChessLevelRules.For(ChessLevel.Normal).OptimalMoves, Is.EqualTo(2));
            Assert.That(ChessLevelRules.For(ChessLevel.Normal).MaxMoves, Is.EqualTo(3));
            Assert.That(ChessLevelRules.For(ChessLevel.Hard).OptimalMoves, Is.EqualTo(3));
            Assert.That(ChessLevelRules.For(ChessLevel.Hard).MaxMoves, Is.EqualTo(5));
        }

        [Test]
        public void FindsMateInOne()
        {
            MateSearch result = Engine().MateDistance(ChessPosition.FromFen(BackRankMateIn1), 3);
            Assert.That(result.Status, Is.EqualTo(SearchStatus.Complete));
            Assert.That(result.Distance, Is.EqualTo(1));
            Assert.That(result.Forced, Is.True);
        }

        [Test]
        public void MateInTwoCountsPlayerMovesNotPlies()
        {
            ChessPosition position = ChessPosition.FromFen(LadderMateIn2);
            Assert.That(Engine().MateDistance(position, 3).Distance, Is.EqualTo(2));
            MateSearch tooShort = Engine().MateDistance(position, 1);
            Assert.That(tooShort.Status, Is.EqualTo(SearchStatus.Complete));
            Assert.That(tooShort.Forced, Is.False);
        }

        [Test]
        public void ReportsNoForcedMateWhenNoneExists()
        {
            MateSearch result = Engine().MateDistance(ChessPosition.FromFen(NoMateInTwo), 2);
            Assert.That(result.Status, Is.EqualTo(SearchStatus.Complete));
            Assert.That(result.Distance, Is.EqualTo(0));
        }

        [Test]
        public void StalemateIsNotMate()
        {
            ChessPosition position = ChessPosition.FromFen(StalemateTrap);
            Assert.That(Engine().MoveDistance(position.Apply(Uci("g5g6")), 1).Forced, Is.False, "Qg6 stalemates");
            Assert.That(Engine().MoveDistance(position.Apply(Uci("g5g7")), 1).Distance, Is.EqualTo(1), "Qg7 mates");
            Assert.That(Engine().MoveDistance(position.Apply(Uci("g5h5")), 1).Distance, Is.EqualTo(1), "Qh5 mates");
        }

        [Test]
        public void AgreesWithTheBruteForceChecker()
        {
            foreach (string fen in new[] { BackRankMateIn1, LadderMateIn2, NoMateInTwo, StalemateTrap })
            {
                ChessPosition position = ChessPosition.FromFen(fen);
                int naive = NaiveMate.AttackerDistance(position, 2);
                MateSearch engine = Engine().MateDistance(position, 2);
                Assert.That(engine.Distance, Is.EqualTo(naive == NaiveMate.None ? 0 : naive), fen);
            }
        }

        [Test]
        public void ExhaustedNodeBudgetIsUnknownNotNoMate()
        {
            var engine = new ChessSearchEngine(new SearchBudget(3, 60));
            MateSearch result = engine.MateDistance(ChessPosition.FromFen(LadderMateIn2), 3);
            Assert.That(result.Status, Is.EqualTo(SearchStatus.Unknown));
            Assert.That(result.Forced, Is.False);
        }

        [Test]
        public void CancellationIsUnknown()
        {
            var engine = new ChessSearchEngine(new SearchBudget(1_000_000, 60, () => true));
            MateSearch result = engine.MateDistance(ChessPosition.FromFen(LadderMateIn2), 3);
            Assert.That(result.Status, Is.EqualTo(SearchStatus.Unknown));
        }

        [Test]
        public void CacheSurvivesBudgetSwapAndIsReusedWithoutNodes()
        {
            var engine = new ChessSearchEngine(new SearchBudget(1_000_000, 60));
            ChessPosition position = ChessPosition.FromFen(LadderMateIn2);
            Assert.That(engine.MateDistance(position, 3).Distance, Is.EqualTo(2));

            var tiny = new SearchBudget(1, 60);
            engine.UseBudget(tiny);
            Assert.That(engine.MateDistance(position, 3).Distance, Is.EqualTo(2));
            Assert.That(tiny.Stopped, Is.EqualTo(SearchStop.None));
        }

        [Test]
        public void PositionKeyIgnoresMoveCounters()
        {
            string a = ChessSearchEngine.PositionKey(ChessPosition.FromFen("6k1/8/8/8/8/8/8/R3K3 w - - 0 1"));
            string b = ChessSearchEngine.PositionKey(ChessPosition.FromFen("6k1/8/8/8/8/8/8/R3K3 w - - 7 40"));
            Assert.That(a, Is.EqualTo(b));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Chess.ChessSearchEngineTests" chess-search`
Expected: compile errors (`ChessLevelRules`, `SearchBudget`, `ChessSearchEngine` not defined) so no results file; the script prints diagnostics with `error CS0246`.

- [ ] **Step 3: Implement levels, budget and engine**

`ChessLevel.cs`:

```csharp
namespace KMA.Gameplay.Chess
{
    public enum ChessLevel { Easy, Normal, Hard }

    /// N = player moves of the optimal mate, M = player moves allowed. Spec section 3.
    public readonly struct ChessLevelRules
    {
        ChessLevelRules(int optimalMoves, int maxMoves)
        {
            OptimalMoves = optimalMoves;
            MaxMoves = maxMoves;
        }

        public int OptimalMoves { get; }
        public int MaxMoves { get; }

        public static ChessLevelRules For(ChessLevel level) => level switch
        {
            ChessLevel.Easy => new ChessLevelRules(1, 2),
            ChessLevel.Normal => new ChessLevelRules(2, 3),
            _ => new ChessLevelRules(3, 5)
        };
    }
}
```

`SearchBudget.cs`:

```csharp
using System;
using System.Diagnostics;

namespace KMA.Gameplay.Chess
{
    public enum SearchStop { None, Nodes, Time, Cancelled }

    public sealed class SearchAbortedException : Exception
    {
        public SearchAbortedException(SearchStop reason) : base("Search stopped: " + reason) => Reason = reason;
        public SearchStop Reason { get; }
    }

    /// One budget per request. Charge is called once per search node and throws when the budget is spent.
    public sealed class SearchBudget
    {
        public const int DefaultNodes = 200_000;
        public const double DefaultSeconds = 2.0;

        readonly int maxNodes;
        readonly double maxSeconds;
        readonly Func<bool> isCancelled;
        readonly Stopwatch clock = Stopwatch.StartNew();

        public SearchBudget(int maxNodes, double maxSeconds, Func<bool> isCancelled = null)
        {
            this.maxNodes = maxNodes;
            this.maxSeconds = maxSeconds;
            this.isCancelled = isCancelled;
        }

        public static SearchBudget Default(Func<bool> isCancelled = null) =>
            new SearchBudget(DefaultNodes, DefaultSeconds, isCancelled);

        public int Nodes { get; private set; }
        public SearchStop Stopped { get; private set; }
        public double ElapsedSeconds => clock.Elapsed.TotalSeconds;

        public void Charge()
        {
            if (Stopped != SearchStop.None) throw new SearchAbortedException(Stopped);
            Nodes++;
            if (Nodes > maxNodes) Stop(SearchStop.Nodes);
            if (Nodes == 1 || (Nodes & 63) == 0)
            {
                if (isCancelled != null && isCancelled()) Stop(SearchStop.Cancelled);
                if (clock.Elapsed.TotalSeconds > maxSeconds) Stop(SearchStop.Time);
            }
        }

        void Stop(SearchStop reason)
        {
            Stopped = reason;
            throw new SearchAbortedException(reason);
        }
    }
}
```

`ChessSearchEngine.cs`:

```csharp
using System.Collections.Generic;

namespace KMA.Gameplay.Chess
{
    public enum SearchStatus { Complete, Unknown }

    public readonly struct MateSearch
    {
        public MateSearch(SearchStatus status, int distance)
        {
            Status = status;
            Distance = distance;
        }

        public SearchStatus Status { get; }
        /// Attacker moves needed to force mate against best defence; 0 when none within the limit.
        public int Distance { get; }
        public bool Forced => Status == SearchStatus.Complete && Distance > 0;
        public bool Unknown => Status == SearchStatus.Unknown;
    }

    /// And/or proof search for forced mate. Attacker nodes need one winning move, defender nodes need every
    /// reply to lose. Only finished results enter the cache, so an aborted search never poisons it.
    /// Not thread-safe: use one engine per worker.
    public sealed class ChessSearchEngine
    {
        readonly Dictionary<string, bool> cache = new Dictionary<string, bool>();
        SearchBudget budget;

        public ChessSearchEngine(SearchBudget budget) => this.budget = budget;

        public void UseBudget(SearchBudget next) => budget = next;

        /// `position.SideToMove` is the attacker. Smallest k in 1..maxMoves with a forced mate in k attacker moves.
        public MateSearch MateDistance(ChessPosition position, int maxMoves)
        {
            try
            {
                for (int k = 1; k <= maxMoves; k++)
                    if (AttackerWins(position, k)) return new MateSearch(SearchStatus.Complete, k);
                return new MateSearch(SearchStatus.Complete, 0);
            }
            catch (SearchAbortedException)
            {
                return new MateSearch(SearchStatus.Unknown, 0);
            }
        }

        /// `after` is the position after an attacker move, defender to move. `maxMoves` counts that move.
        public MateSearch MoveDistance(ChessPosition after, int maxMoves)
        {
            try
            {
                for (int k = 1; k <= maxMoves; k++)
                    if (DefenderLoses(after, k)) return new MateSearch(SearchStatus.Complete, k);
                return new MateSearch(SearchStatus.Complete, 0);
            }
            catch (SearchAbortedException)
            {
                return new MateSearch(SearchStatus.Unknown, 0);
            }
        }

        /// Board, side, castling and en passant; move counters are left out so transpositions share entries.
        public static string PositionKey(ChessPosition position)
        {
            string fen = position.ToFen();
            int cut = fen.LastIndexOf(' ');
            cut = fen.LastIndexOf(' ', cut - 1);
            return fen.Substring(0, cut);
        }

        bool AttackerWins(ChessPosition position, int moves)
        {
            budget.Charge();
            string key = PositionKey(position) + "|A" + moves;
            if (cache.TryGetValue(key, out bool known)) return known;

            var quiet = new List<ChessPosition>();
            bool result = false;
            foreach (ChessMove move in MoveGenerator.LegalMoves(position))
            {
                ChessPosition after = position.Apply(move);
                if (MoveGenerator.IsInCheck(after))
                {
                    if (DefenderLoses(after, moves)) { result = true; break; }
                }
                else quiet.Add(after);
            }
            // Checkmate needs a check, so quiet moves are only useful with a move to spare.
            if (!result && moves > 1)
                foreach (ChessPosition after in quiet)
                    if (DefenderLoses(after, moves)) { result = true; break; }

            cache[key] = result;
            return result;
        }

        bool DefenderLoses(ChessPosition after, int moves)
        {
            budget.Charge();
            string key = PositionKey(after) + "|D" + moves;
            if (cache.TryGetValue(key, out bool known)) return known;

            List<ChessMove> defences = MoveGenerator.LegalMoves(after);
            bool result;
            if (defences.Count == 0) result = MoveGenerator.IsInCheck(after); // checkmate, not stalemate
            else if (moves <= 1) result = false;
            else
            {
                result = true;
                foreach (ChessMove defence in defences)
                    if (!AttackerWins(after.Apply(defence), moves - 1)) { result = false; break; }
            }

            cache[key] = result;
            return result;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Chess.ChessSearchEngineTests" chess-search`
Expected: `result=Passed`, `failed=0`. If `FindsMateInOne`/ladder asserts fail, the FEN constant is wrong: re-derive it by hand before touching the engine (the engine is cross-checked by `AgreesWithTheBruteForceChecker`).

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Gameplay/Chess/Core/ChessLevel.cs* Assets/_Project/Scripts/Gameplay/Chess/Core/SearchBudget.cs* Assets/_Project/Scripts/Gameplay/Chess/Core/ChessSearchEngine.cs* Assets/Tests/EditMode/Gameplay/Chess/NaiveMate.cs* Assets/Tests/EditMode/Gameplay/Chess/ChessSearchEngineTests.cs*
git commit -m "feat(chess): add mate-proof search engine with node/time/cancel budget"
```

---

### Task 2: Defending opponent

**Files:**
- Create: `Assets/_Project/Scripts/Gameplay/Chess/Core/ChessOpponent.cs`
- Create: `Assets/Tests/EditMode/Gameplay/Chess/ChessOpponentTests.cs`

**Interfaces:**
- Consumes: `ChessSearchEngine.MateDistance(ChessPosition, int)`, `MateSearch`, `SearchStatus` (Task 1); `NaiveMate` (Task 1 test helper).
- Produces: `readonly struct DefenceChoice { SearchStatus Status; bool HasMove; ChessMove Move; }` and `static class ChessOpponent { static DefenceChoice Choose(ChessSearchEngine engine, ChessPosition defenderToMove, int attackerMovesLeft, int seed) }`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Linq;
using KMA.Gameplay.Chess;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessOpponentTests
    {
        const string AfterRb7 = "7k/1R6/8/8/8/8/R7/6K1 b - - 1 1";
        const string KingAndRook = "7k/8/5K2/8/8/8/8/R7 b - - 0 1";
        const string LoneRook = "k7/8/8/8/8/8/8/K6R b - - 0 1";

        static ChessSearchEngine Engine() => new ChessSearchEngine(new SearchBudget(1_000_000, 60));

        [Test]
        public void OnlyDefenceIsPlayed()
        {
            DefenceChoice choice = ChessOpponent.Choose(Engine(), ChessPosition.FromFen(AfterRb7), 1, 1);
            Assert.That(choice.Status, Is.EqualTo(SearchStatus.Complete));
            Assert.That(choice.Move.ToUci(), Is.EqualTo("h8g8"));
        }

        [Test]
        public void ChosenDefenceIsNeverWorseThanAnotherLegalOne()
        {
            foreach (string fen in new[] { KingAndRook, LoneRook, AfterRb7 })
            foreach (int movesLeft in new[] { 1, 2 })
            {
                ChessPosition position = ChessPosition.FromFen(fen);
                DefenceChoice choice = ChessOpponent.Choose(Engine(), position, movesLeft, 7);
                Assert.That(choice.HasMove, Is.True, fen);
                int chosen = NaiveMate.AttackerDistance(position.Apply(choice.Move), movesLeft);
                foreach (ChessMove other in MoveGenerator.LegalMoves(position))
                    Assert.That(chosen, Is.GreaterThanOrEqualTo(NaiveMate.AttackerDistance(position.Apply(other), movesLeft)),
                        $"{fen} movesLeft={movesLeft} chose {choice.Move} over {other}");
            }
        }

        [Test]
        public void SameSeedSameMoveAndMovesAreLegal()
        {
            ChessPosition position = ChessPosition.FromFen(LoneRook);
            ChessMove first = ChessOpponent.Choose(Engine(), position, 1, 42).Move;
            ChessMove second = ChessOpponent.Choose(Engine(), position, 1, 42).Move;
            Assert.That(second, Is.EqualTo(first));
            Assert.That(MoveGenerator.LegalMoves(position).Contains(first), Is.True);
        }

        [Test]
        public void SeedBreaksTiesBetweenEqualDefences()
        {
            ChessPosition position = ChessPosition.FromFen(LoneRook);
            var picked = Enumerable.Range(0, 40)
                .Select(seed => ChessOpponent.Choose(Engine(), position, 1, seed).Move.ToUci()).Distinct().ToList();
            Assert.That(picked.Count, Is.GreaterThan(1), "equal defences should vary with the seed");
        }

        [Test]
        public void ExhaustedBudgetIsUnknownWithNoMove()
        {
            var engine = new ChessSearchEngine(new SearchBudget(2, 60));
            DefenceChoice choice = ChessOpponent.Choose(engine, ChessPosition.FromFen(KingAndRook), 2, 1);
            Assert.That(choice.Status, Is.EqualTo(SearchStatus.Unknown));
            Assert.That(choice.HasMove, Is.False);
        }

        [Test]
        public void TakesAMateAgainstTheAttackerWhenOneExists()
        {
            // Black (the defender) to play: Qh2# is available (king g3 guards h2) and must be preferred.
            ChessPosition position = ChessPosition.FromFen("8/8/8/8/8/6k1/4q3/7K b - - 0 1");
            DefenceChoice choice = ChessOpponent.Choose(Engine(), position, 2, 3);
            Assert.That(MoveGenerator.IsCheckmate(position.Apply(choice.Move)), Is.True, choice.Move.ToUci());
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Chess.ChessOpponentTests" chess-opponent`
Expected: compile error, `ChessOpponent`/`DefenceChoice` not defined.

- [ ] **Step 3: Implement `ChessOpponent`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace KMA.Gameplay.Chess
{
    public readonly struct DefenceChoice
    {
        public DefenceChoice(SearchStatus status, bool hasMove, ChessMove move)
        {
            Status = status;
            HasMove = hasMove;
            Move = move;
        }

        public SearchStatus Status { get; }
        public bool HasMove { get; }
        public ChessMove Move { get; }
    }

    /// Picks the defender's reply (spec section 4): mate the attacker if possible; else a reply that escapes the
    /// mate; else the reply that delays it longest. Equal replies fall back to material, then a seeded tie-break.
    public static class ChessOpponent
    {
        static readonly int[] Values = { 0, 1, 3, 3, 5, 9, 0 };

        /// `attackerMovesLeft` is how many more moves the attacker may make (player moves left after the one just made).
        public static DefenceChoice Choose(ChessSearchEngine engine, ChessPosition position, int attackerMovesLeft,
            int seed)
        {
            List<ChessMove> legal = MoveGenerator.LegalMoves(position);
            if (legal.Count == 0) return new DefenceChoice(SearchStatus.Complete, false, default);

            var scored = new List<(ChessMove move, int rank, int material)>(legal.Count);
            foreach (ChessMove move in legal)
            {
                ChessPosition after = position.Apply(move);
                if (MoveGenerator.IsCheckmate(after)) return new DefenceChoice(SearchStatus.Complete, true, move);
                MateSearch search = engine.MateDistance(after, attackerMovesLeft);
                if (search.Unknown) return new DefenceChoice(SearchStatus.Unknown, false, default);
                int rank = search.Forced ? search.Distance : int.MaxValue; // escaping the mate is best
                scored.Add((move, rank, Material(after, position.SideToMove)));
            }

            int bestRank = scored.Max(s => s.rank);
            List<(ChessMove move, int rank, int material)> best = scored.Where(s => s.rank == bestRank).ToList();
            int bestMaterial = best.Max(s => s.material);
            List<ChessMove> ties = best.Where(s => s.material == bestMaterial).Select(s => s.move)
                .OrderBy(m => m.ToUci(), StringComparer.Ordinal).ToList();
            int pick = new Random(seed ^ Fnv(ChessSearchEngine.PositionKey(position))).Next(ties.Count);
            return new DefenceChoice(SearchStatus.Complete, true, ties[pick]);
        }

        static int Material(ChessPosition position, PieceColor side)
        {
            int total = 0;
            for (int square = 0; square < 64; square++)
            {
                sbyte piece = position[square];
                if (piece == 0) continue;
                int value = Values[Piece.TypeOf(piece)];
                total += Piece.ColorOf(piece) == side ? value : -value;
            }
            return total;
        }

        /// string.GetHashCode is not stable across runtimes; tests need a reproducible seed.
        static int Fnv(string text)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in text) hash = (hash ^ c) * 16777619;
                return (int)hash;
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Chess.ChessOpponentTests" chess-opponent`
Expected: `result=Passed`.
- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Gameplay/Chess/Core/ChessOpponent.cs* Assets/Tests/EditMode/Gameplay/Chess/ChessOpponentTests.cs*
git commit -m "feat(chess): add proof-based defending opponent"
```

---

### Task 3: Puzzle analyzer and time estimator

**Files:**
- Create: `Assets/_Project/Scripts/Gameplay/Chess/Core/ChessPuzzleAnalyzer.cs`
- Create: `Assets/_Project/Scripts/Gameplay/Chess/Core/ChessTimeEstimator.cs`
- Create: `Assets/Tests/EditMode/Gameplay/Chess/ChessPuzzleAnalyzerTests.cs`
- Create: `Assets/Tests/EditMode/Gameplay/Chess/ChessTimeEstimatorTests.cs`

**Interfaces:**
- Consumes: `ChessSearchEngine` (`MateDistance`, `MoveDistance`, `PositionKey`) from Task 1.
- Produces:
  - `readonly struct PuzzleAnalysis { SearchStatus Status; int OptimalMoves; double Branching; int QuietMoves }` (`OptimalMoves` 0 = no forced mate within the limit)
  - `static class ChessPuzzleAnalyzer { static PuzzleAnalysis Analyze(ChessSearchEngine engine, ChessPosition start, int maxMoves) }`
  - `static class ChessTimeEstimator { const int Version = 1; static int Seconds(int optimalMoves, double branching, int quietMoves) }`

- [ ] **Step 1: Write the failing tests**

`ChessTimeEstimatorTests.cs`:

```csharp
using KMA.Gameplay.Chess;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessTimeEstimatorTests
    {
        [TestCase(1, 1.0, 0, 60)]    // 58 clamps up to 60
        [TestCase(1, 4.0, 0, 85)]    // 25+25+32 = 82 -> 85
        [TestCase(2, 4.0, 0, 110)]   // 25+50+32 = 107 -> 110
        [TestCase(3, 5.0, 2, 170)]   // 25+75+40+30 = 170 exact
        [TestCase(3, 30.0, 9, 240)]  // far above the cap
        public void FormulaClampsAndRoundsUpToFive(int n, double b, int q, int expected) =>
            Assert.That(ChessTimeEstimator.Seconds(n, b, q), Is.EqualTo(expected));

        [Test]
        public void SameOptimalMovesCanGiveDifferentTimes() =>
            Assert.That(ChessTimeEstimator.Seconds(2, 3.0, 0), Is.Not.EqualTo(ChessTimeEstimator.Seconds(2, 5.5, 1)));

        [Test]
        public void ResultIsAlwaysMultipleOfFiveInRange()
        {
            for (int n = 1; n <= 3; n++)
            for (double b = 1; b <= 8; b += .37)
            for (int q = 0; q <= 4; q++)
            {
                int t = ChessTimeEstimator.Seconds(n, b, q);
                Assert.That(t % 5, Is.EqualTo(0));
                Assert.That(t, Is.InRange(60, 240));
            }
        }

        [Test]
        public void VersionIsOne() => Assert.That(ChessTimeEstimator.Version, Is.EqualTo(1));
    }
}
```

`ChessPuzzleAnalyzerTests.cs`:

```csharp
using System;
using KMA.Gameplay.Chess;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessPuzzleAnalyzerTests
    {
        static ChessSearchEngine Engine() => new ChessSearchEngine(new SearchBudget(1_000_000, 60));

        [Test]
        public void MateInOneHasOneProofStateAndNoQuietMoves()
        {
            // 10 rook moves + 5 king moves = 15 legal moves; the mating move leaves only a terminal state.
            PuzzleAnalysis result = ChessPuzzleAnalyzer.Analyze(Engine(),
                ChessPosition.FromFen(ChessSearchEngineTests.BackRankMateIn1), 3);
            Assert.That(result.Status, Is.EqualTo(SearchStatus.Complete));
            Assert.That(result.OptimalMoves, Is.EqualTo(1));
            Assert.That(result.QuietMoves, Is.EqualTo(0));
            Assert.That(result.Branching, Is.EqualTo(Math.Log(16, 2)).Within(1e-9));
        }

        [Test]
        public void LadderMateNeedsExactlyOneQuietMove()
        {
            PuzzleAnalysis result = ChessPuzzleAnalyzer.Analyze(Engine(),
                ChessPosition.FromFen(ChessSearchEngineTests.LadderMateIn2), 3);
            Assert.That(result.OptimalMoves, Is.EqualTo(2));
            Assert.That(result.QuietMoves, Is.EqualTo(1));
            Assert.That(result.Branching, Is.GreaterThan(1.0));
        }

        [Test]
        public void NoForcedMateReportsZeroOptimalMoves()
        {
            PuzzleAnalysis result = ChessPuzzleAnalyzer.Analyze(Engine(),
                ChessPosition.FromFen(ChessSearchEngineTests.NoMateInTwo), 2);
            Assert.That(result.Status, Is.EqualTo(SearchStatus.Complete));
            Assert.That(result.OptimalMoves, Is.EqualTo(0));
        }

        [Test]
        public void LimitBelowTheRealDistanceReportsNoMate()
        {
            PuzzleAnalysis result = ChessPuzzleAnalyzer.Analyze(Engine(),
                ChessPosition.FromFen(ChessSearchEngineTests.LadderMateIn2), 1);
            Assert.That(result.OptimalMoves, Is.EqualTo(0));
        }

        [Test]
        public void TinyBudgetIsUnknown()
        {
            var engine = new ChessSearchEngine(new SearchBudget(5, 60));
            PuzzleAnalysis result = ChessPuzzleAnalyzer.Analyze(engine,
                ChessPosition.FromFen(ChessSearchEngineTests.LadderMateIn2), 3);
            Assert.That(result.Status, Is.EqualTo(SearchStatus.Unknown));
        }

        [Test]
        public void AnalysisIsDeterministic()
        {
            ChessPosition position = ChessPosition.FromFen(ChessSearchEngineTests.LadderMateIn2);
            PuzzleAnalysis a = ChessPuzzleAnalyzer.Analyze(Engine(), position, 3);
            PuzzleAnalysis b = ChessPuzzleAnalyzer.Analyze(Engine(), position, 3);
            Assert.That(b.Branching, Is.EqualTo(a.Branching));
            Assert.That(b.QuietMoves, Is.EqualTo(a.QuietMoves));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Chess.ChessTimeEstimatorTests" chess-estimator` and the same for `ChessPuzzleAnalyzerTests`.
Expected: compile error, `ChessTimeEstimator`/`ChessPuzzleAnalyzer` not defined.

- [ ] **Step 3: Implement estimator and analyzer**

`ChessTimeEstimator.cs`:

```csharp
using System;

namespace KMA.Gameplay.Chess
{
    /// Spec section 5. Coefficients are a first configuration to be tuned by playtesting; bump Version when they change.
    public static class ChessTimeEstimator
    {
        public const int Version = 1;
        const double Base = 25, PerMove = 25, PerBranching = 8, PerQuiet = 15;
        const double Min = 60, Max = 240;

        public static int Seconds(int optimalMoves, double branching, int quietMoves)
        {
            double raw = Base + PerMove * optimalMoves + PerBranching * branching + PerQuiet * quietMoves;
            double clamped = Math.Min(Max, Math.Max(Min, raw));
            // Round first so 65.0000000001 from float noise does not jump a whole step.
            return (int)(Math.Ceiling(Math.Round(clamped, 6) / 5.0) * 5.0);
        }
    }
}
```

`ChessPuzzleAnalyzer.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace KMA.Gameplay.Chess
{
    public readonly struct PuzzleAnalysis
    {
        public PuzzleAnalysis(SearchStatus status, int optimalMoves, double branching, int quietMoves)
        {
            Status = status;
            OptimalMoves = optimalMoves;
            Branching = branching;
            QuietMoves = quietMoves;
        }

        public SearchStatus Status { get; }
        /// Player moves of the optimal forced mate; 0 when none exists within the limit.
        public int OptimalMoves { get; }
        /// B in spec section 5: mean log2(1 + legal moves) over non-terminal states of the optimal proof tree.
        public double Branching { get; }
        /// Q in spec section 5: most quiet (no capture, no check) attacker moves on one optimal line.
        public int QuietMoves { get; }
    }

    public static class ChessPuzzleAnalyzer
    {
        sealed class UnknownException : Exception { }

        public static PuzzleAnalysis Analyze(ChessSearchEngine engine, ChessPosition start, int maxMoves)
        {
            MateSearch root = engine.MateDistance(start, maxMoves);
            if (root.Unknown) return new PuzzleAnalysis(SearchStatus.Unknown, 0, 0, 0);
            if (!root.Forced) return new PuzzleAnalysis(SearchStatus.Complete, 0, 0, 0);

            var logs = new List<double>();
            var seen = new HashSet<string>();
            var memo = new Dictionary<string, int>();
            try
            {
                int quiet = Walk(engine, start, root.Distance, seen, logs, memo);
                return new PuzzleAnalysis(SearchStatus.Complete, root.Distance, logs.Average(), quiet);
            }
            catch (UnknownException)
            {
                return new PuzzleAnalysis(SearchStatus.Unknown, 0, 0, 0);
            }
        }

        /// Attacker to move with a known shortest mate `distance`; returns the most quiet attacker moves on a line.
        static int Walk(ChessSearchEngine engine, ChessPosition position, int distance, HashSet<string> seen,
            List<double> logs, Dictionary<string, int> memo)
        {
            string key = ChessSearchEngine.PositionKey(position);
            if (memo.TryGetValue(key, out int known)) return known;

            List<ChessMove> legal = MoveGenerator.LegalMoves(position);
            if (seen.Add("A" + key)) logs.Add(Math.Log(1 + legal.Count, 2));

            int best = 0;
            foreach (ChessMove move in legal)
            {
                ChessPosition after = position.Apply(move);
                MateSearch result = engine.MoveDistance(after, distance);
                if (result.Unknown) throw new UnknownException();
                if (result.Distance != distance) continue; // not an optimal move

                int line = IsQuiet(position, move, after) ? 1 : 0;
                if (!MoveGenerator.IsCheckmate(after))
                {
                    List<ChessMove> defences = MoveGenerator.LegalMoves(after);
                    if (seen.Add("D" + ChessSearchEngine.PositionKey(after))) logs.Add(Math.Log(1 + defences.Count, 2));
                    int deepest = 0;
                    foreach (ChessMove defence in defences)
                    {
                        ChessPosition next = after.Apply(defence);
                        MateSearch follow = engine.MateDistance(next, distance - 1);
                        if (follow.Unknown) throw new UnknownException();
                        if (!follow.Forced)
                            throw new InvalidOperationException("Optimal move has a defence that escapes: " + next.ToFen());
                        deepest = Math.Max(deepest, Walk(engine, next, follow.Distance, seen, logs, memo));
                    }
                    line += deepest;
                }
                best = Math.Max(best, line);
            }

            memo[key] = best;
            return best;
        }

        static bool IsQuiet(ChessPosition before, ChessMove move, ChessPosition after)
        {
            bool capture = before[move.To] != 0 ||
                (Piece.TypeOf(before[move.From]) == Piece.Pawn && move.To == before.EnPassant);
            return !capture && !MoveGenerator.IsInCheck(after);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run both filters from Step 2. Expected: `result=Passed`. If `MateInOneHasOneProofStateAndNoQuietMoves` fails on `Branching`, recount the white legal moves in `6k1/5ppp/8/8/8/8/8/R3K3 w` (expected 15) before changing code.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Gameplay/Chess/Core/ChessPuzzleAnalyzer.cs* Assets/_Project/Scripts/Gameplay/Chess/Core/ChessTimeEstimator.cs* Assets/Tests/EditMode/Gameplay/Chess/ChessPuzzleAnalyzerTests.cs* Assets/Tests/EditMode/Gameplay/Chess/ChessTimeEstimatorTests.cs*
git commit -m "feat(chess): derive puzzle difficulty metrics and per-puzzle time estimate"
```

---

### Task 4: Move quality and attempt scoring

**Files:**
- Create: `Assets/_Project/Scripts/Gameplay/Chess/Core/ChessAttemptGrader.cs`
- Create: `Assets/Tests/EditMode/Gameplay/Chess/ChessAttemptGraderTests.cs`

**Interfaces:**
- Consumes: `ChessSearchEngine`, `MateSearch`, `SearchStatus` (Task 1).
- Produces:
  - `enum ChessGrade { F, D, C, B, A, APlus }`
  - `readonly struct MoveQuality { SearchStatus Status; double Q; int BestDistance; int ChosenDistance; bool Unknown }`
  - `readonly struct ChessScore { bool Pass; float Score; ChessGrade Grade; bool HintCapped; double QualityMean; double Efficiency }`
  - `static class ChessAttemptGrader { static MoveQuality Evaluate(ChessSearchEngine engine, ChessPosition before, ChessMove move, int movesLeft); static ChessScore Score(bool mated, IReadOnlyList<double> qualities, int optimalMoves, int movesUsed, bool explicitHintUsed); static ChessGrade GradeFromTenths(int tenths); static string Label(ChessGrade grade); const int HintCapTenths = 89; }`

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using System.Collections.Generic;
using KMA.Gameplay.Chess;
using NUnit.Framework;
using static KMA.Tests.Gameplay.Chess.TestPuzzles;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessAttemptGraderTests
    {
        static ChessSearchEngine Engine() => new ChessSearchEngine(new SearchBudget(2_000_000, 60));

        [Test]
        public void EveryOptimalMoveScoresOne()
        {
            ChessPosition position = ChessPosition.FromFen(ChessSearchEngineTests.LadderMateIn2);
            foreach (string uci in new[] { "b1b7", "a2a7" })
            {
                MoveQuality q = ChessAttemptGrader.Evaluate(Engine(), position, Uci(uci), 3);
                Assert.That(q.Status, Is.EqualTo(SearchStatus.Complete), uci);
                Assert.That(q.Q, Is.EqualTo(1.0).Within(1e-9), uci);
            }
        }

        [Test]
        public void MoveQualityMatchesTheBruteForceChecker()
        {
            ChessPosition position = ChessPosition.FromFen(ChessSearchEngineTests.LadderMateIn2);
            int best = NaiveMate.AttackerDistance(position, 2);
            foreach (ChessMove move in MoveGenerator.LegalMoves(position))
            {
                int chosen = NaiveMate.DefenderDistance(position.Apply(move), 2);
                double expected = chosen == NaiveMate.None ? 0.0 : (double)best / chosen;
                MoveQuality q = ChessAttemptGrader.Evaluate(Engine(), position, move, 2);
                Assert.That(q.Q, Is.EqualTo(expected).Within(1e-9), move.ToUci());
            }
        }

        [Test]
        public void SlowerMateGetsAFraction()
        {
            // With three moves left the ladder still mates in 2; a waiting king move that keeps a mate in 3 scores 2/3.
            ChessPosition position = ChessPosition.FromFen(ChessSearchEngineTests.LadderMateIn2);
            ChessMove waiting = Uci("g1f1");
            int chosen = NaiveMate.DefenderDistance(position.Apply(waiting), 3);
            double expected = chosen == NaiveMate.None ? 0.0 : 2.0 / chosen;
            Assert.That(ChessAttemptGrader.Evaluate(Engine(), position, waiting, 3).Q, Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void StalematingMoveScoresZeroAndMatingMovesScoreOne()
        {
            ChessPosition position = ChessPosition.FromFen(ChessSearchEngineTests.StalemateTrap);
            Assert.That(ChessAttemptGrader.Evaluate(Engine(), position, Uci("g5g6"), 2).Q, Is.EqualTo(0.0));
            Assert.That(ChessAttemptGrader.Evaluate(Engine(), position, Uci("g5g7"), 2).Q, Is.EqualTo(1.0));
        }

        [Test]
        public void NoForcedMateLeftScoresZeroButIsComplete()
        {
            ChessPosition position = ChessPosition.FromFen(ChessSearchEngineTests.NoMateInTwo);
            MoveQuality q = ChessAttemptGrader.Evaluate(Engine(), position, Uci("h1h8"), 2);
            Assert.That(q.Status, Is.EqualTo(SearchStatus.Complete));
            Assert.That(q.Q, Is.EqualTo(0.0));
            Assert.That(q.BestDistance, Is.EqualTo(0));
        }

        [Test]
        public void UnknownIsNeverReportedAsZero()
        {
            var engine = new ChessSearchEngine(new SearchBudget(2, 60));
            MoveQuality q = ChessAttemptGrader.Evaluate(engine,
                ChessPosition.FromFen(ChessSearchEngineTests.LadderMateIn2), Uci("b1b7"), 3);
            Assert.That(q.Unknown, Is.True);
            Assert.That(q.Status, Is.EqualTo(SearchStatus.Unknown));
        }

        [TestCase(100, ChessGrade.APlus)]
        [TestCase(90, ChessGrade.APlus)]
        [TestCase(89, ChessGrade.A)]
        [TestCase(80, ChessGrade.A)]
        [TestCase(79, ChessGrade.B)]
        [TestCase(70, ChessGrade.B)]
        [TestCase(69, ChessGrade.C)]
        [TestCase(60, ChessGrade.C)]
        [TestCase(59, ChessGrade.D)]
        [TestCase(50, ChessGrade.D)]
        [TestCase(49, ChessGrade.F)]
        [TestCase(0, ChessGrade.F)]
        public void GradeBoundaries(int tenths, ChessGrade expected) =>
            Assert.That(ChessAttemptGrader.GradeFromTenths(tenths), Is.EqualTo(expected));

        [Test]
        public void LabelsUseAPlus()
        {
            Assert.That(ChessAttemptGrader.Label(ChessGrade.APlus), Is.EqualTo("A+"));
            Assert.That(ChessAttemptGrader.Label(ChessGrade.F), Is.EqualTo("F"));
        }

        [Test]
        public void PerfectPlayInOptimalMovesIsTenAndAPlus()
        {
            ChessScore s = ChessAttemptGrader.Score(true, new[] { 1.0, 1.0 }, 2, 2, false);
            Assert.That(s.Pass, Is.True);
            Assert.That(s.Score, Is.EqualTo(10f));
            Assert.That(s.Grade, Is.EqualTo(ChessGrade.APlus));
        }

        [Test]
        public void ExtraMovesLowerEfficiencyOnlyThirtyPercent()
        {
            ChessScore s = ChessAttemptGrader.Score(true, new[] { 1.0, 1.0, 1.0, 1.0 }, 2, 4, false);
            Assert.That(s.Score, Is.EqualTo(8.5f)); // 10 * (0.7*1 + 0.3*0.5)
            Assert.That(s.Grade, Is.EqualTo(ChessGrade.A));
        }

        [Test]
        public void FloatNoiseAtNineStillMakesAPlus()
        {
            // N=2, U=3 gives efficiency 0.6667 and exactly 9.0 mathematically.
            ChessScore s = ChessAttemptGrader.Score(true, new[] { 1.0, 1.0, 1.0 }, 2, 3, false);
            Assert.That(s.Score, Is.EqualTo(9.0f));
            Assert.That(s.Grade, Is.EqualTo(ChessGrade.APlus));
        }

        [Test]
        public void ExplicitHintCapsAtEightPointNine()
        {
            ChessScore s = ChessAttemptGrader.Score(true, new[] { 1.0, 1.0 }, 2, 2, true);
            Assert.That(s.Score, Is.EqualTo(8.9f));
            Assert.That(s.Grade, Is.EqualTo(ChessGrade.A));
            Assert.That(s.HintCapped, Is.True);
        }

        [Test]
        public void HintDoesNotChangeALowerScore()
        {
            ChessScore plain = ChessAttemptGrader.Score(true, new[] { 0.5, 1.0 }, 2, 2, false);
            ChessScore hinted = ChessAttemptGrader.Score(true, new[] { 0.5, 1.0 }, 2, 2, true);
            Assert.That(hinted.Score, Is.EqualTo(plain.Score));
            Assert.That(hinted.HintCapped, Is.False);
        }

        [Test]
        public void FailureIsZeroAndF()
        {
            ChessScore s = ChessAttemptGrader.Score(false, new[] { 1.0, 1.0 }, 2, 2, false);
            Assert.That(s.Pass, Is.False);
            Assert.That(s.Score, Is.EqualTo(0f));
            Assert.That(s.Grade, Is.EqualTo(ChessGrade.F));
        }

        [Test]
        public void OutOfRangeInputsAreClamped()
        {
            ChessScore s = ChessAttemptGrader.Score(true, new[] { 2.0, -1.0 }, 3, 0, false);
            Assert.That(s.Score, Is.InRange(0f, 10f));
            Assert.That(s.Efficiency, Is.EqualTo(0.0));
        }

        [Test]
        public void NoRecordedMovesCountsAsZeroQuality() =>
            Assert.That(ChessAttemptGrader.Score(true, new List<double>(), 1, 1, false).QualityMean, Is.EqualTo(0.0));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Chess.ChessAttemptGraderTests" chess-grader`
Expected: compile error, `ChessAttemptGrader` not defined.

- [ ] **Step 3: Implement the grader**

```csharp
using System;
using System.Collections.Generic;

namespace KMA.Gameplay.Chess
{
    public enum ChessGrade { F, D, C, B, A, APlus }

    public readonly struct MoveQuality
    {
        public MoveQuality(SearchStatus status, double q, int bestDistance, int chosenDistance)
        {
            Status = status;
            Q = q;
            BestDistance = bestDistance;
            ChosenDistance = chosenDistance;
        }

        public SearchStatus Status { get; }
        /// In [0,1]. Meaningless when Unknown.
        public double Q { get; }
        /// Shortest forced mate before the move (player moves), 0 when none was left.
        public int BestDistance { get; }
        /// Shortest forced mate after the move including the move itself, 0 when the mate was lost.
        public int ChosenDistance { get; }
        public bool Unknown => Status == SearchStatus.Unknown;
    }

    public readonly struct ChessScore
    {
        public ChessScore(bool pass, float score, ChessGrade grade, bool hintCapped, double qualityMean,
            double efficiency)
        {
            Pass = pass;
            Score = score;
            Grade = grade;
            HintCapped = hintCapped;
            QualityMean = qualityMean;
            Efficiency = efficiency;
        }

        public bool Pass { get; }
        public float Score { get; }
        public ChessGrade Grade { get; }
        public bool HintCapped { get; }
        public double QualityMean { get; }
        public double Efficiency { get; }
    }

    /// Spec sections 6.1 and 6.2.
    public static class ChessAttemptGrader
    {
        public const int HintCapTenths = 89;

        /// `before` has the player to move with `movesLeft` player moves remaining, this one included.
        public static MoveQuality Evaluate(ChessSearchEngine engine, ChessPosition before, ChessMove move, int movesLeft)
        {
            MateSearch best = engine.MateDistance(before, movesLeft);
            if (best.Unknown) return new MoveQuality(SearchStatus.Unknown, 0, 0, 0);
            if (!best.Forced) return new MoveQuality(SearchStatus.Complete, 0, 0, 0);

            MateSearch chosen = engine.MoveDistance(before.Apply(move), movesLeft);
            if (chosen.Unknown) return new MoveQuality(SearchStatus.Unknown, 0, best.Distance, 0);
            if (!chosen.Forced) return new MoveQuality(SearchStatus.Complete, 0, best.Distance, 0);

            double q = Math.Min(1.0, (double)best.Distance / chosen.Distance);
            return new MoveQuality(SearchStatus.Complete, q, best.Distance, chosen.Distance);
        }

        public static ChessScore Score(bool mated, IReadOnlyList<double> qualities, int optimalMoves, int movesUsed,
            bool explicitHintUsed)
        {
            double mean = 0;
            if (qualities != null && qualities.Count > 0)
            {
                foreach (double q in qualities) mean += Math.Min(1.0, Math.Max(0.0, q));
                mean /= qualities.Count;
            }
            double efficiency = movesUsed <= 0 || optimalMoves <= 0
                ? 0.0
                : Math.Min(1.0, Math.Max(0.0, (double)optimalMoves / movesUsed));
            if (!mated) return new ChessScore(false, 0f, ChessGrade.F, false, mean, efficiency);

            double raw = 10.0 * (0.7 * mean + 0.3 * efficiency);
            int tenths = (int)Math.Round(raw * 10.0 + 1e-9, MidpointRounding.AwayFromZero);
            bool capped = false;
            if (explicitHintUsed && tenths > HintCapTenths)
            {
                tenths = HintCapTenths;
                capped = true;
            }
            return new ChessScore(true, tenths / 10f, GradeFromTenths(tenths), capped, mean, efficiency);
        }

        public static ChessGrade GradeFromTenths(int tenths) =>
            tenths >= 90 ? ChessGrade.APlus :
            tenths >= 80 ? ChessGrade.A :
            tenths >= 70 ? ChessGrade.B :
            tenths >= 60 ? ChessGrade.C :
            tenths >= 50 ? ChessGrade.D : ChessGrade.F;

        public static string Label(ChessGrade grade) => grade == ChessGrade.APlus ? "A+" : grade.ToString();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Chess.ChessAttemptGraderTests" chess-grader`
Expected: `result=Passed`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Gameplay/Chess/Core/ChessAttemptGrader.cs* Assets/Tests/EditMode/Gameplay/Chess/ChessAttemptGraderTests.cs*
git commit -m "feat(chess): grade each move against proven best play and score F to A+"
```

---

### Task 5: Puzzle snapshot and generator

**Files:**
- Create: `Assets/_Project/Scripts/Gameplay/Chess/Core/ChessPuzzleSnapshot.cs`
- Create: `Assets/_Project/Scripts/Gameplay/Chess/Core/ChessPuzzleGenerator.cs`
- Create: `Assets/Tests/EditMode/Gameplay/Chess/ChessPuzzleGeneratorTests.cs`

**Interfaces:**
- Consumes: `ChessLevel`/`ChessLevelRules` (Task 1), `SearchBudget`, `ChessSearchEngine` (Task 1), `ChessPuzzleAnalyzer` and `ChessTimeEstimator` (Task 3).
- Produces:
  - `[Serializable] sealed class ChessPuzzleSnapshot { string fen; string level; int seed; int optimalMoves; int maxMoves; int timeSeconds; float branching; int quietMoves; int estimatorVersion; string source; string playerColor; ChessLevel Level {get;} ChessPosition StartPosition {get;} const string GeneratedSource, FallbackSource }`
  - `readonly struct ChessGeneratorLimits { int TotalNodes; int PerCandidateNodes; int MaxCandidates; double TotalSeconds; static ChessGeneratorLimits Default }`
  - `sealed class GenerationStats { int Candidates; int Nodes; double ElapsedSeconds; bool Success }`
  - `static class ChessPuzzleGenerator { static ChessPuzzleSnapshot TryGenerate(ChessLevel level, int seed, ChessGeneratorLimits limits, string avoidFen, Func<bool> isCancelled, out GenerationStats stats); static ChessPosition RandomPosition(ChessLevel level, Random rng) }`

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using System.Linq;
using KMA.Gameplay.Chess;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessPuzzleGeneratorTests
    {
        // Node-only limits: wall-clock must not change the outcome of a seeded run.
        static readonly ChessGeneratorLimits Generous = new ChessGeneratorLimits(2_000_000, 300_000, 5_000, 1e9);

        [TestCase(ChessLevel.Easy)]
        [TestCase(ChessLevel.Normal)]
        [TestCase(ChessLevel.Hard)]
        public void RandomPositionsAreLegalSetups(ChessLevel level)
        {
            var rng = new Random(11);
            int made = 0;
            for (int i = 0; i < 300; i++)
            {
                ChessPosition p = ChessPuzzleGenerator.RandomPosition(level, rng);
                if (p == null) continue;
                made++;
                int wk = p.KingSquare(PieceColor.White), bk = p.KingSquare(PieceColor.Black);
                Assert.That(Math.Abs(Square.File(wk) - Square.File(bk)) > 1 || Math.Abs(Square.Rank(wk) - Square.Rank(bk)) > 1,
                    "kings adjacent: " + p.ToFen());
                Assert.That(MoveGenerator.IsSquareAttacked(p, bk, PieceColor.White), Is.False, p.ToFen());
                Assert.That(MoveGenerator.LegalMoves(p).Count, Is.GreaterThan(0), p.ToFen());
                Assert.That(p.SideToMove, Is.EqualTo(PieceColor.White));
                Assert.That(p.Castling, Is.EqualTo(CastlingRights.None));
                Assert.That(p.EnPassant, Is.EqualTo(-1));
                Assert.That(p.HalfmoveClock, Is.EqualTo(0));
                int pieces = Enumerable.Range(0, 64).Count(s => p[s] != 0);
                Assert.That(pieces, Is.InRange(4, 6), p.ToFen());
                for (int s = 0; s < 64; s++)
                    if (p[s] != 0 && Piece.ColorOf(p[s]) == PieceColor.Black)
                        Assert.That(Piece.TypeOf(p[s]), Is.EqualTo(Piece.King), "black has only a king: " + p.ToFen());
            }
            Assert.That(made, Is.GreaterThan(150), "setup rejection rate is far too high");
        }

        [TestCase(ChessLevel.Easy)]
        [TestCase(ChessLevel.Normal)]
        public void GeneratedPuzzleMatchesItsLevelAndIndependentCheck(ChessLevel level)
        {
            ChessLevelRules rules = ChessLevelRules.For(level);
            for (int seed = 1; seed <= 4; seed++)
            {
                ChessPuzzleSnapshot s = ChessPuzzleGenerator.TryGenerate(level, seed, Generous, null, null, out GenerationStats stats);
                Assert.That(s, Is.Not.Null, $"seed {seed}");
                Assert.That(stats.Success, Is.True);
                Assert.That(s.optimalMoves, Is.EqualTo(rules.OptimalMoves));
                Assert.That(s.maxMoves, Is.EqualTo(rules.MaxMoves));
                Assert.That(s.source, Is.EqualTo(ChessPuzzleSnapshot.GeneratedSource));
                ChessPosition p = ChessPosition.FromFen(s.fen);
                Assert.That(NaiveMate.AttackerDistance(p, rules.OptimalMoves), Is.EqualTo(rules.OptimalMoves),
                    $"mate in exactly N: {s.fen}");
            }
        }

        [Test, Timeout(300000)]
        public void HardPuzzleHasNoShorterMateAndAMateInThree()
        {
            ChessPuzzleSnapshot s = ChessPuzzleGenerator.TryGenerate(ChessLevel.Hard, 3, Generous, null, null, out _);
            Assert.That(s, Is.Not.Null);
            ChessPosition p = ChessPosition.FromFen(s.fen);
            Assert.That(NaiveMate.AttackerDistance(p, 2), Is.EqualTo(NaiveMate.None), "a shorter mate exists: " + s.fen);
            Assert.That(NaiveMate.AttackerDistance(p, 3), Is.EqualTo(3), s.fen);
        }

        [Test]
        public void SnapshotCarriesDerivedMetrics()
        {
            ChessPuzzleSnapshot s = ChessPuzzleGenerator.TryGenerate(ChessLevel.Normal, 5, Generous, null, null, out _);
            Assert.That(s.estimatorVersion, Is.EqualTo(ChessTimeEstimator.Version));
            Assert.That(s.timeSeconds, Is.EqualTo(ChessTimeEstimator.Seconds(s.optimalMoves, s.branching, s.quietMoves)));
            Assert.That(s.timeSeconds, Is.InRange(60, 240));
            Assert.That(s.seed, Is.EqualTo(5));
            Assert.That(s.Level, Is.EqualTo(ChessLevel.Normal));
            Assert.That(s.StartPosition.ToFen(), Is.EqualTo(s.fen));
            Assert.That(s.playerColor, Is.EqualTo("w"));
        }

        [Test]
        public void SameSeedAndNodeBudgetReproducesThePuzzle()
        {
            ChessPuzzleSnapshot a = ChessPuzzleGenerator.TryGenerate(ChessLevel.Normal, 9, Generous, null, null, out _);
            ChessPuzzleSnapshot b = ChessPuzzleGenerator.TryGenerate(ChessLevel.Normal, 9, Generous, null, null, out _);
            Assert.That(b.fen, Is.EqualTo(a.fen));
            Assert.That(b.timeSeconds, Is.EqualTo(a.timeSeconds));
        }

        [Test]
        public void DifferentSeedsGiveDifferentPuzzles()
        {
            var fens = Enumerable.Range(20, 5).Select(seed =>
                ChessPuzzleGenerator.TryGenerate(ChessLevel.Easy, seed, Generous, null, null, out _).fen).Distinct();
            Assert.That(fens.Count(), Is.GreaterThan(1));
        }

        [Test]
        public void AvoidFenIsNotReturned()
        {
            ChessPuzzleSnapshot first = ChessPuzzleGenerator.TryGenerate(ChessLevel.Easy, 4, Generous, null, null, out _);
            ChessPuzzleSnapshot second = ChessPuzzleGenerator.TryGenerate(ChessLevel.Easy, 4, Generous, first.fen, null, out _);
            Assert.That(second, Is.Not.Null);
            Assert.That(second.fen, Is.Not.EqualTo(first.fen));
        }

        [Test]
        public void TinyNodeBudgetReturnsNullInsteadOfAnUnverifiedPuzzle()
        {
            var tiny = new ChessGeneratorLimits(10, 10, 5_000, 1e9);
            ChessPuzzleSnapshot s = ChessPuzzleGenerator.TryGenerate(ChessLevel.Hard, 1, tiny, null, null, out GenerationStats stats);
            Assert.That(s, Is.Null);
            Assert.That(stats.Success, Is.False);
            Assert.That(stats.Nodes, Is.LessThanOrEqualTo(10 + 1));
        }

        [Test]
        public void CancellationStopsGenerationQuickly()
        {
            ChessPuzzleSnapshot s = ChessPuzzleGenerator.TryGenerate(ChessLevel.Hard, 1, Generous, null, () => true, out GenerationStats stats);
            Assert.That(s, Is.Null);
            Assert.That(stats.ElapsedSeconds, Is.LessThan(1.0));
        }

        [Test]
        public void StatsReportCandidatesAndNodes()
        {
            ChessPuzzleGenerator.TryGenerate(ChessLevel.Easy, 2, Generous, null, null, out GenerationStats stats);
            Assert.That(stats.Candidates, Is.GreaterThan(0));
            Assert.That(stats.Nodes, Is.GreaterThan(0));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Chess.ChessPuzzleGeneratorTests" chess-generator`
Expected: compile error, `ChessPuzzleSnapshot`/`ChessGeneratorLimits`/`ChessPuzzleGenerator` not defined.

- [ ] **Step 3: Implement snapshot and generator**

`ChessPuzzleSnapshot.cs`:

```csharp
using System;

namespace KMA.Gameplay.Chess
{
    /// Everything needed to rebuild a puzzle and its grading context (spec sections 5 and 9).
    /// Plain public fields so JsonUtility can store it in a save file.
    [Serializable]
    public sealed class ChessPuzzleSnapshot
    {
        public const string GeneratedSource = "Generated";
        public const string FallbackSource = "Fallback";

        public string fen;
        public string level;
        public int seed;
        public int optimalMoves;
        public int maxMoves;
        public int timeSeconds;
        public float branching;
        public int quietMoves;
        public int estimatorVersion;
        public string source;
        public string playerColor = "w";

        public ChessLevel Level => Enum.TryParse(level, out ChessLevel parsed) ? parsed : ChessLevel.Normal;
        public ChessPosition StartPosition => ChessPosition.FromFen(fen);
    }
}
```

`ChessPuzzleGenerator.cs`:

```csharp
using System;
using System.Diagnostics;
using System.Text;

namespace KMA.Gameplay.Chess
{
    public readonly struct ChessGeneratorLimits
    {
        public ChessGeneratorLimits(int totalNodes, int perCandidateNodes, int maxCandidates, double totalSeconds)
        {
            TotalNodes = totalNodes;
            PerCandidateNodes = perCandidateNodes;
            MaxCandidates = maxCandidates;
            TotalSeconds = totalSeconds;
        }

        public int TotalNodes { get; }
        public int PerCandidateNodes { get; }
        public int MaxCandidates { get; }
        public double TotalSeconds { get; }

        /// Spec section 3.2: 2 seconds and 200,000 nodes in total.
        public static ChessGeneratorLimits Default =>
            new ChessGeneratorLimits(SearchBudget.DefaultNodes, 30_000, 5_000, SearchBudget.DefaultSeconds);
    }

    public sealed class GenerationStats
    {
        public int Candidates;
        public int Nodes;
        public double ElapsedSeconds;
        public bool Success;
    }

    public static class ChessPuzzleGenerator
    {
        // Piece letters for the player's extras. The black side is a lone king.
        static readonly string[] EasySets = { "QR", "RR", "QB", "QN", "RB" };
        static readonly string[] NormalSets = { "Q", "Q", "QN", "QB", "RR" };
        static readonly string[] HardSets = { "Q", "Q", "QN", "QB", "QR" };
        static readonly int[] EdgeSquares = BuildEdgeSquares();

        /// Null when the budget ran out, the run was cancelled, or nothing verified. A candidate is only returned when
        /// the proof search finished and the shortest mate is exactly N.
        public static ChessPuzzleSnapshot TryGenerate(ChessLevel level, int seed, ChessGeneratorLimits limits,
            string avoidFen, Func<bool> isCancelled, out GenerationStats stats)
        {
            ChessLevelRules rules = ChessLevelRules.For(level);
            var rng = new Random(seed);
            var clock = Stopwatch.StartNew();
            stats = new GenerationStats();

            while (stats.Candidates < limits.MaxCandidates && stats.Nodes < limits.TotalNodes &&
                   clock.Elapsed.TotalSeconds < limits.TotalSeconds && !(isCancelled?.Invoke() ?? false))
            {
                stats.Candidates++;
                ChessPosition position = RandomPosition(level, rng);
                if (position == null) continue;
                string fen = position.ToFen();
                if (fen == avoidFen) continue;

                var budget = new SearchBudget(Math.Min(limits.PerCandidateNodes, limits.TotalNodes - stats.Nodes),
                    Math.Max(0.001, limits.TotalSeconds - clock.Elapsed.TotalSeconds), isCancelled);
                PuzzleAnalysis analysis = ChessPuzzleAnalyzer.Analyze(new ChessSearchEngine(budget), position,
                    rules.OptimalMoves);
                stats.Nodes += budget.Nodes;
                if (analysis.Status != SearchStatus.Complete || analysis.OptimalMoves != rules.OptimalMoves) continue;

                float branching = (float)analysis.Branching;
                stats.Success = true;
                stats.ElapsedSeconds = clock.Elapsed.TotalSeconds;
                return new ChessPuzzleSnapshot
                {
                    fen = fen,
                    level = level.ToString(),
                    seed = seed,
                    optimalMoves = rules.OptimalMoves,
                    maxMoves = rules.MaxMoves,
                    branching = branching,
                    quietMoves = analysis.QuietMoves,
                    timeSeconds = ChessTimeEstimator.Seconds(rules.OptimalMoves, branching, analysis.QuietMoves),
                    estimatorVersion = ChessTimeEstimator.Version,
                    source = ChessPuzzleSnapshot.GeneratedSource,
                    playerColor = "w"
                };
            }

            stats.ElapsedSeconds = clock.Elapsed.TotalSeconds;
            return null;
        }

        /// A legal setup with white to move, or null when the random placement is unusable.
        public static ChessPosition RandomPosition(ChessLevel level, Random rng)
        {
            string[] sets = level == ChessLevel.Easy ? EasySets : level == ChessLevel.Normal ? NormalSets : HardSets;
            string extras = sets[rng.Next(sets.Length)];

            var board = new sbyte[64];
            // Mates are far more common against a king on the edge.
            int blackKing = rng.NextDouble() < .6 ? EdgeSquares[rng.Next(EdgeSquares.Length)] : rng.Next(64);
            board[blackKing] = Piece.Make(Piece.King, PieceColor.Black);
            int whiteKing = FreeSquare(board, rng);
            if (Adjacent(whiteKing, blackKing)) return null;
            board[whiteKing] = Piece.Make(Piece.King, PieceColor.White);
            foreach (char letter in extras)
                board[FreeSquare(board, rng)] = Piece.Make(TypeOf(letter), PieceColor.White);

            ChessPosition position = ChessPosition.FromFen(ToFen(board));
            if (MoveGenerator.IsSquareAttacked(position, blackKing, PieceColor.White)) return null;
            return MoveGenerator.LegalMoves(position).Count == 0 ? null : position;
        }

        static int TypeOf(char letter) => letter switch
        {
            'Q' => Piece.Queen,
            'R' => Piece.Rook,
            'B' => Piece.Bishop,
            _ => Piece.Knight
        };

        static int FreeSquare(sbyte[] board, Random rng)
        {
            int square;
            do square = rng.Next(64); while (board[square] != 0);
            return square;
        }

        static bool Adjacent(int a, int b) =>
            Math.Abs(Square.File(a) - Square.File(b)) <= 1 && Math.Abs(Square.Rank(a) - Square.Rank(b)) <= 1;

        static string ToFen(sbyte[] board)
        {
            var text = new StringBuilder(64);
            for (int rank = 7; rank >= 0; rank--)
            {
                int empty = 0;
                for (int file = 0; file < 8; file++)
                {
                    sbyte piece = board[Square.At(file, rank)];
                    if (piece == 0) { empty++; continue; }
                    if (empty > 0) { text.Append(empty); empty = 0; }
                    text.Append(Piece.ToFen(piece));
                }
                if (empty > 0) text.Append(empty);
                if (rank > 0) text.Append('/');
            }
            return text.Append(" w - - 0 1").ToString();
        }

        static int[] BuildEdgeSquares()
        {
            var edge = new System.Collections.Generic.List<int>();
            for (int s = 0; s < 64; s++)
            {
                int f = Square.File(s), r = Square.Rank(s);
                if (f == 0 || f == 7 || r == 0 || r == 7) edge.Add(s);
            }
            return edge.ToArray();
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Chess.ChessPuzzleGeneratorTests" chess-generator`
Expected: `result=Passed`. If a `TryGenerate(...) Is.Not.Null` assert fails for a seed, the generator is not finding puzzles inside `Generous`; do not loosen the test, instead print `stats` and adjust piece sets or the edge bias in `ChessPuzzleGenerator`, then record the finding for Task 6's measurement. If `HardPuzzleHasNoShorterMateAndAMateInThree` is too slow (>5 min), lower the naive limit check to `NaiveMate.AttackerDistance(p, 2)` plus the engine's own distance, and note the reduction in the commit message.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Gameplay/Chess/Core/ChessPuzzleSnapshot.cs* Assets/_Project/Scripts/Gameplay/Chess/Core/ChessPuzzleGenerator.cs* Assets/Tests/EditMode/Gameplay/Chess/ChessPuzzleGeneratorTests.cs*
git commit -m "feat(chess): generate verified mate-in-N puzzles from a seed"
```

---

### Task 6: Fallback pool, library and generator measurement

**Files:**
- Create: `Assets/Editor/ChessFallbackPoolBuilder.cs`
- Create (generated by the tool): `Assets/_Project/Resources/Chess/fallback_pool.json`
- Create: `Assets/_Project/Scripts/Gameplay/Chess/ChessFallbackLibrary.cs`
- Create: `Assets/Tests/EditMode/Gameplay/Chess/ChessFallbackLibraryTests.cs`
- Create: `Assets/Tests/EditMode/Gameplay/Chess/ChessGeneratorMeasurement.cs`
- Create: `docs/qa/2026-10-13-chess-generator-performance.md`

**Interfaces:**
- Consumes: `ChessPuzzleGenerator.TryGenerate`, `ChessGeneratorLimits`, `ChessPuzzleSnapshot`, `ChessSearchEngine`, `ChessPuzzleAnalyzer` (Tasks 1–5).
- Produces:
  - `static class ChessFallbackLibrary { const string Resource = "Chess/fallback_pool"; static IReadOnlyList<ChessPuzzleSnapshot> All(); static ChessPuzzleSnapshot Pick(ChessLevel level, int seed, string avoidFen) }` (in `KMA.Gameplay.Chess`, Unity side)
  - `[Serializable] sealed class ChessFallbackFile { public ChessPuzzleSnapshot[] puzzles; }`
  - Menu `KMA/Chess/Build Fallback Pool` and `KMA.EditorTools.ChessFallbackPoolBuilder.Build()` for `-executeMethod`.

Rationale: spec §3.2 requires a fallback pool with all three levels. Hand-written FENs would be unverified, so the pool is produced by the generator with a large offline budget and then re-verified by tests.

- [ ] **Step 1: Write the pool builder**

`Assets/Editor/ChessFallbackPoolBuilder.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using KMA.Gameplay.Chess;
using UnityEditor;
using UnityEngine;

namespace KMA.EditorTools
{
    /// Builds Resources/Chess/fallback_pool.json offline with a large budget. Run once, commit the JSON.
    public static class ChessFallbackPoolBuilder
    {
        const string Path_ = "Assets/_Project/Resources/Chess/fallback_pool.json";
        const int PerLevel = 12;
        const int SeedBase = 90_000;

        [MenuItem("KMA/Chess/Build Fallback Pool")]
        public static void Build()
        {
            var limits = new ChessGeneratorLimits(5_000_000, 600_000, 50_000, 120);
            var puzzles = new List<ChessPuzzleSnapshot>();
            foreach (ChessLevel level in new[] { ChessLevel.Easy, ChessLevel.Normal, ChessLevel.Hard })
            {
                var seen = new HashSet<string>();
                for (int seed = SeedBase; seen.Count < PerLevel && seed < SeedBase + 400; seed++)
                {
                    ChessPuzzleSnapshot puzzle = ChessPuzzleGenerator.TryGenerate(level, seed, limits, null, null, out _);
                    if (puzzle == null || !seen.Add(puzzle.fen)) continue;
                    puzzle.source = ChessPuzzleSnapshot.FallbackSource;
                    puzzles.Add(puzzle);
                }
                Debug.Log($"[KMA] Fallback pool {level}: {seen.Count} puzzles");
            }
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path_));
            File.WriteAllText(Path_, JsonUtility.ToJson(new ChessFallbackFile { puzzles = puzzles.ToArray() }, true));
            AssetDatabase.ImportAsset(Path_);
        }
    }
}
```

(`ChessFallbackFile` is defined in Step 3; the builder will not compile until then, which is fine because Steps 2–3 come first in one compile.)

- [ ] **Step 2: Write the failing library tests**

`ChessFallbackLibraryTests.cs`:

```csharp
using System.Linq;
using KMA.Gameplay.Chess;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Chess
{
    public sealed class ChessFallbackLibraryTests
    {
        [TestCase(ChessLevel.Easy)]
        [TestCase(ChessLevel.Normal)]
        [TestCase(ChessLevel.Hard)]
        public void EveryLevelHasAtLeastSixPuzzlesOfItsOwnLevel(ChessLevel level)
        {
            var own = ChessFallbackLibrary.All().Where(p => p.Level == level).ToList();
            Assert.That(own.Count, Is.GreaterThanOrEqualTo(6));
            Assert.That(own.Select(p => p.fen).Distinct().Count(), Is.EqualTo(own.Count), "duplicate FEN");
            Assert.That(own.All(p => p.source == ChessPuzzleSnapshot.FallbackSource), Is.True);
        }

        [Test]
        public void EveryPuzzleIsReVerifiedByTheEngine()
        {
            foreach (ChessPuzzleSnapshot p in ChessFallbackLibrary.All())
            {
                ChessLevelRules rules = ChessLevelRules.For(p.Level);
                var engine = new ChessSearchEngine(new SearchBudget(2_000_000, 120));
                PuzzleAnalysis analysis = ChessPuzzleAnalyzer.Analyze(engine, p.StartPosition, rules.OptimalMoves);
                Assert.That(analysis.Status, Is.EqualTo(SearchStatus.Complete), p.fen);
                Assert.That(analysis.OptimalMoves, Is.EqualTo(rules.OptimalMoves), p.fen);
                Assert.That(p.optimalMoves, Is.EqualTo(rules.OptimalMoves), p.fen);
                Assert.That(p.maxMoves, Is.EqualTo(rules.MaxMoves), p.fen);
                Assert.That(p.timeSeconds, Is.EqualTo(ChessTimeEstimator.Seconds(p.optimalMoves, p.branching, p.quietMoves)), p.fen);
                Assert.That(p.estimatorVersion, Is.EqualTo(ChessTimeEstimator.Version), p.fen);
            }
        }

        [Test]
        public void EasyAndNormalPuzzlesPassTheIndependentChecker()
        {
            foreach (ChessPuzzleSnapshot p in ChessFallbackLibrary.All().Where(p => p.Level != ChessLevel.Hard))
                Assert.That(NaiveMate.AttackerDistance(p.StartPosition, p.optimalMoves), Is.EqualTo(p.optimalMoves), p.fen);
        }

        [Test, Timeout(300000)]
        public void TwoHardPuzzlesPassTheIndependentChecker()
        {
            foreach (ChessPuzzleSnapshot p in ChessFallbackLibrary.All().Where(p => p.Level == ChessLevel.Hard).Take(2))
            {
                Assert.That(NaiveMate.AttackerDistance(p.StartPosition, 2), Is.EqualTo(NaiveMate.None), p.fen);
                Assert.That(NaiveMate.AttackerDistance(p.StartPosition, 3), Is.EqualTo(3), p.fen);
            }
        }

        [Test]
        public void PickNeverCrossesLevelsAndIsSeedStable()
        {
            foreach (ChessLevel level in new[] { ChessLevel.Easy, ChessLevel.Normal, ChessLevel.Hard })
            {
                ChessPuzzleSnapshot a = ChessFallbackLibrary.Pick(level, 7, null);
                ChessPuzzleSnapshot b = ChessFallbackLibrary.Pick(level, 7, null);
                Assert.That(a.Level, Is.EqualTo(level));
                Assert.That(b.fen, Is.EqualTo(a.fen));
            }
        }

        [Test]
        public void PickAvoidsTheLastFenWhenAnotherExists()
        {
            ChessPuzzleSnapshot first = ChessFallbackLibrary.Pick(ChessLevel.Hard, 1, null);
            for (int seed = 0; seed < 20; seed++)
                Assert.That(ChessFallbackLibrary.Pick(ChessLevel.Hard, seed, first.fen).fen, Is.Not.EqualTo(first.fen));
        }
    }
}
```

- [ ] **Step 3: Implement the library**

`Assets/_Project/Scripts/Gameplay/Chess/ChessFallbackLibrary.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KMA.Gameplay.Chess
{
    [Serializable]
    public sealed class ChessFallbackFile
    {
        public ChessPuzzleSnapshot[] puzzles;
    }

    /// Verified puzzles used when the generator does not finish in budget (spec section 3.2).
    /// Never substitutes another level: a Hard request without a Hard puzzle throws.
    public static class ChessFallbackLibrary
    {
        public const string Resource = "Chess/fallback_pool";
        static IReadOnlyList<ChessPuzzleSnapshot> cached;

        public static IReadOnlyList<ChessPuzzleSnapshot> All()
        {
            if (cached != null) return cached;
            var asset = Resources.Load<TextAsset>(Resource) ??
                throw new InvalidOperationException($"Missing Resources/{Resource}.json; run KMA/Chess/Build Fallback Pool.");
            cached = JsonUtility.FromJson<ChessFallbackFile>(asset.text).puzzles;
            return cached;
        }

        public static ChessPuzzleSnapshot Pick(ChessLevel level, int seed, string avoidFen)
        {
            List<ChessPuzzleSnapshot> own = All().Where(p => p.Level == level).ToList();
            if (own.Count == 0) throw new InvalidOperationException($"No {level} fallback puzzle in the pool.");
            List<ChessPuzzleSnapshot> pool = own.Where(p => p.fen != avoidFen).ToList();
            if (pool.Count == 0) pool = own;
            return pool[new System.Random(seed).Next(pool.Count)];
        }
    }
}
```

- [ ] **Step 4: Build the pool and run the library tests**

Run (Unity Editor closed):

```bash
UNITY="${UNITY:-<path to Unity.exe for 6000.3.23f1>}"
"$UNITY" -batchmode -quit -projectPath "$(pwd -W)" -executeMethod KMA.EditorTools.ChessFallbackPoolBuilder.Build -logFile Builds/fallback-pool.log
grep "Fallback pool" Builds/fallback-pool.log
```

Expected: three lines `Fallback pool Easy: 12 puzzles`, `Normal: 12`, `Hard: 12` (at least 6 each). If Hard yields fewer than 6, raise `PerCandidateNodes` in the builder first; if still short, change Hard's piece sets in `ChessPuzzleGenerator` (and re-run Task 5 tests) rather than lowering the test minimum.

Then: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Chess.ChessFallbackLibraryTests" chess-fallback`
Expected: `result=Passed`.

- [ ] **Step 5: Add the measurement test and record results**

`ChessGeneratorMeasurement.cs` (explicit, not part of normal runs; spec section 10 "Hiệu năng"):

```csharp
using System.Diagnostics;
using KMA.Gameplay.Chess;
using NUnit.Framework;

namespace KMA.Tests.Gameplay.Chess
{
    [Explicit("Measurement for docs/qa; run with the Unity test filter on purpose."), Category("Measurement")]
    public sealed class ChessGeneratorMeasurement
    {
        const int Seeds = 30;

        [TestCase(ChessLevel.Easy)]
        [TestCase(ChessLevel.Normal)]
        [TestCase(ChessLevel.Hard)]
        public void MeasureDefaultBudget(ChessLevel level)
        {
            int generated = 0, nodes = 0;
            double seconds = 0, worst = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                ChessPuzzleGenerator.TryGenerate(level, seed, ChessGeneratorLimits.Default, null, null, out GenerationStats stats);
                if (stats.Success) generated++;
                nodes += stats.Nodes;
                seconds += stats.ElapsedSeconds;
                if (stats.ElapsedSeconds > worst) worst = stats.ElapsedSeconds;
            }
            UnityEngine.Debug.Log($"[MEASURE] {level}: generated {generated}/{Seeds} ({100.0 * generated / Seeds:0}%), " +
                $"fallback {Seeds - generated}/{Seeds}, avg nodes {nodes / Seeds}, avg {seconds / Seeds:0.00}s, worst {worst:0.00}s");
            Assert.Pass();
        }
    }
}
```

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Chess.ChessGeneratorMeasurement" chess-measure` then `grep MEASURE Builds/TestResults/chess-measure.log`.

Create `docs/qa/2026-10-13-chess-generator-performance.md` containing: date, machine/Unity version, "Editor, not Android", the three `[MEASURE]` lines, and a verdict against spec §10 (target >= 80% Generated per level). Spec §11 forbids calling it a pass if it only works through fallback: if any level is below 80%, tune (piece-set weights, edge bias, `PerCandidateNodes`), re-run Task 5 tests and this measurement, and record both the before and after numbers. Android numbers stay "not measured" until Plan 3.

- [ ] **Step 6: Run the whole chess EditMode suite**

Run: `tools/run-unity-tests.sh EditMode "KMA.Tests.Gameplay.Chess" chess-all`
Expected: `result=Passed`; existing tests (`PuzzleGraderTests`, `ChessFinalStateMachineTests`, `PuzzleVerifierTests`, `ShippedPuzzleTests`) still pass because Plan 1 changed no existing file.

- [ ] **Step 7: Commit**

```bash
git add Assets/Editor/ChessFallbackPoolBuilder.cs* Assets/_Project/Resources/Chess/fallback_pool.json* Assets/_Project/Scripts/Gameplay/Chess/ChessFallbackLibrary.cs* Assets/Tests/EditMode/Gameplay/Chess/ChessFallbackLibraryTests.cs* Assets/Tests/EditMode/Gameplay/Chess/ChessGeneratorMeasurement.cs* docs/qa/2026-10-13-chess-generator-performance.md
git commit -m "feat(chess): add verified fallback puzzle pool and measure generator success rate"
```

---

## Self-Review (Plan 1)

- **Spec coverage:** §3 level table and generation (Tasks 1, 5); §3.2 budget and fallback with per-level pool and Generated/Fallback rates (Tasks 5, 6); §4 search, Unknown vs Complete, opponent preferences (Tasks 1, 2); §5 time formula and metrics (Task 3); §6.1 q and §6.2 score/cap/grades (Task 4); §10 EditMode items for puzzles, seeds, estimator, scoring, independent checker (Tasks 1–6). Not in this plan by design: state machine, hints, controller, worker threading, HUD, dialogue (Plan 2); persistence, result/A+ display, Android numbers (Plan 3).
- **Placeholder scan:** no TBD/TODO; the hand-written FEN constants (back-rank, ladder, stalemate trap, lone rook, `8/8/8/8/8/6k1/4q3/7K b`) were derived by reasoning, not by running Unity; if a test fails on a FEN constant, re-derive the position by hand before touching the engine.
- **Type consistency:** `MateSearch.Distance`/`Forced`/`Unknown`, `SearchStatus`, `SearchBudget(int, double, Func<bool>)`, `ChessSearchEngine.UseBudget`/`PositionKey`, `PuzzleAnalysis`, `ChessPuzzleSnapshot` fields, `ChessGeneratorLimits` ctor order (totalNodes, perCandidateNodes, maxCandidates, totalSeconds) are used identically in Tasks 1–6.
- **Risks carried forward:** Hard (mate-in-3) generation yield inside 2 s/200k nodes is unproven until Task 6 measures it; `ChessSearchEngine` is not thread-safe, so Plan 2 must give each worker request its own access to one engine at a time.
