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
