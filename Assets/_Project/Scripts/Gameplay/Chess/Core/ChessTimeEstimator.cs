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
