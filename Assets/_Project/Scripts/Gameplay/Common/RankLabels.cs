namespace KMA.Gameplay
{
    /// The stored Rank.S means "top grade"; every subject shows it as A+. Saved data keeps the enum value.
    public static class RankLabels
    {
        public static string For(Rank rank) => rank == Rank.S ? "A+" : rank.ToString();
    }
}
