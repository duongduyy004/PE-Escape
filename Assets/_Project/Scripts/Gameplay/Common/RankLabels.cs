namespace KMA.Gameplay
{
    /// The stored Rank.S means "top grade". For chess the top grade is shown as A+ (spec section 7).
    public static class RankLabels
    {
        public static string For(Rank rank, bool chess) => chess && rank == Rank.S ? "A+" : rank.ToString();
    }
}
