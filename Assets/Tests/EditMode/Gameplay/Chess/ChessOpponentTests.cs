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
        public void ProvenEscapeSurvivesALaterReplyRunningOutOfBudget()
        {
            ChessPosition position = ChessPosition.FromFen(LoneRook);
            var full = new SearchBudget(1_000_000, 60);
            ChessOpponent.Choose(new ChessSearchEngine(full), position, 1, 1);

            int complete = 0;
            for (int nodes = 1; nodes < full.Nodes; nodes++)
            {
                DefenceChoice choice = ChessOpponent.Choose(
                    new ChessSearchEngine(new SearchBudget(nodes, 60)), position, 1, 1);
                if (choice.Status != SearchStatus.Complete) continue;
                complete++;
                Assert.That(NaiveMate.AttackerDistance(position.Apply(choice.Move), 1), Is.EqualTo(NaiveMate.None),
                    $"budget {nodes}: partial choice must be a proven escape");
            }
            Assert.That(complete, Is.GreaterThan(0), "an escape proven before the budget ran out must be played");
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
