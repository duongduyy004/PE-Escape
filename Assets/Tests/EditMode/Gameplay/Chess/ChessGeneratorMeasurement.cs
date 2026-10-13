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

        /// The request the game sends for the first move of a fresh puzzle: grade it and pick the reply, default budget.
        [TestCase(ChessLevel.Easy)]
        [TestCase(ChessLevel.Normal)]
        [TestCase(ChessLevel.Hard)]
        public void MeasureFirstMoveAnalysis(ChessLevel level)
        {
            int unknown = 0, runs = 0;
            double total = 0, worst = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                ChessPuzzleSnapshot puzzle = ChessPuzzleGenerator.TryGenerate(level, seed, ChessGeneratorLimits.Default, null, null, out _);
                if (puzzle == null) continue;
                ChessMove optimal = ChessHintBuilder.Build(new ChessSearchEngine(SearchBudget.Default()),
                    new HintRequest { Id = 0, Fen = puzzle.fen, MovesLeft = puzzle.maxMoves, Level = 3 }).Move;
                var budget = SearchBudget.Default();
                AnalysisResult result = ChessTurnAnalyzer.Analyze(new ChessSearchEngine(budget), new AnalysisRequest
                {
                    Id = 1, BeforeFen = puzzle.fen, Move = optimal, MovesLeftBefore = puzzle.maxMoves,
                    NeedDefence = true, Seed = seed
                });
                runs++;
                if (result.Status == SearchStatus.Unknown) unknown++;
                total += budget.ElapsedSeconds;
                if (budget.ElapsedSeconds > worst) worst = budget.ElapsedSeconds;
            }
            UnityEngine.Debug.Log($"[MEASURE-FIRST] {level}: unknown {unknown}/{runs}, mean {total / System.Math.Max(1, runs):0.000}s, worst {worst:0.000}s");
            Assert.Pass();
        }

        /// Runtime cost of grading every first move and answering it, one fresh default budget per request.
        [TestCase(ChessLevel.Normal)]
        [TestCase(ChessLevel.Hard)]
        public void MeasureRuntimeAnalysis(ChessLevel level)
        {
            ChessLevelRules rules = ChessLevelRules.For(level);
            int gradeCalls = 0, gradeUnknown = 0, replyCalls = 0, replyUnknown = 0;
            double worst = 0;
            for (int seed = 1; seed <= 5; seed++)
            {
                ChessPuzzleSnapshot puzzle = ChessPuzzleGenerator.TryGenerate(level, seed, ChessGeneratorLimits.Default, null, null, out _);
                ChessPosition start = puzzle.StartPosition;
                foreach (ChessMove move in MoveGenerator.LegalMoves(start))
                {
                    var grade = SearchBudget.Default();
                    MoveQuality q = ChessAttemptGrader.Evaluate(new ChessSearchEngine(grade), start, move, rules.MaxMoves);
                    gradeCalls++;
                    if (q.Unknown) gradeUnknown++;
                    worst = System.Math.Max(worst, grade.ElapsedSeconds);

                    ChessPosition after = start.Apply(move);
                    if (MoveGenerator.IsCheckmate(after)) continue;
                    var reply = SearchBudget.Default();
                    DefenceChoice choice = ChessOpponent.Choose(new ChessSearchEngine(reply), after, rules.MaxMoves - 1, seed);
                    replyCalls++;
                    if (choice.Status == SearchStatus.Unknown) replyUnknown++;
                    worst = System.Math.Max(worst, reply.ElapsedSeconds);
                }
            }
            UnityEngine.Debug.Log($"[MEASURE-RT] {level}: grade unknown {gradeUnknown}/{gradeCalls}, " +
                $"reply unknown {replyUnknown}/{replyCalls}, worst request {worst:0.00}s");
            Assert.Pass();
        }
    }
}
