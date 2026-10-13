using UnityEngine;

namespace KMA.Gameplay.Chess
{
    /// Keeps the puzzle of a failed Final attempt in memory so "retry" repeats it. It is not saved: an unfinished or
    /// failed puzzle is not resumed after the app is closed (spec section 9).
    public static class ChessRetryStore
    {
        static ChessPuzzleSnapshot remembered;

        public static bool HasSnapshot => remembered != null;

        public static void Remember(ChessPuzzleSnapshot failed) =>
            remembered = failed == null ? null : JsonUtility.FromJson<ChessPuzzleSnapshot>(JsonUtility.ToJson(failed));

        public static void Forget() => remembered = null;

        /// The remembered puzzle when this start is a retry, otherwise null. Either way the store is emptied.
        public static ChessPuzzleSnapshot TakeIfRetry(bool retryRequested)
        {
            ChessPuzzleSnapshot taken = retryRequested ? remembered : null;
            remembered = null;
            return taken;
        }
    }
}
