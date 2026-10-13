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
            // Cache hits are free, so the defence search cannot be starved on its own. Sweep every node budget below
            // the full cost instead: a cut-off run is Unknown, or Complete with a real reply, never Complete without one.
            AnalysisRequest request = Request(ChessSearchEngineTests.LadderMateIn2, "b1b7", 3, true);
            var full = new SearchBudget(2_000_000, 60);
            Assert.That(ChessTurnAnalyzer.Analyze(new ChessSearchEngine(full), request).Defence.HasMove, Is.True);

            int unknown = 0;
            for (int nodes = 1; nodes < full.Nodes; nodes++)
            {
                AnalysisResult r = ChessTurnAnalyzer.Analyze(new ChessSearchEngine(new SearchBudget(nodes, 60)), request);
                if (r.Status == SearchStatus.Unknown) { unknown++; continue; }
                Assert.That(r.Defence.HasMove, Is.True, $"budget {nodes}: Complete result without a reply");
            }
            Assert.That(unknown, Is.GreaterThan(0));
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
