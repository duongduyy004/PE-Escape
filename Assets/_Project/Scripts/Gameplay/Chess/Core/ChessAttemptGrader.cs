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
