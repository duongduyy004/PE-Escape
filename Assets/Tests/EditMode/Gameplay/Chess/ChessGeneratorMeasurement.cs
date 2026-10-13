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
