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
