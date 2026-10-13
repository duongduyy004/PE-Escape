using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KMA.Gameplay.Chess
{
    [Serializable]
    public sealed class ChessFallbackFile
    {
        public ChessPuzzleSnapshot[] puzzles;
    }

    /// Verified puzzles used when the generator does not finish in budget (spec section 3.2).
    /// Never substitutes another level: a Hard request without a Hard puzzle throws.
    public static class ChessFallbackLibrary
    {
        public const string Resource = "Chess/fallback_pool";
        static IReadOnlyList<ChessPuzzleSnapshot> cached;

        public static IReadOnlyList<ChessPuzzleSnapshot> All()
        {
            if (cached != null) return cached;
            var asset = Resources.Load<TextAsset>(Resource) ??
                throw new InvalidOperationException($"Missing Resources/{Resource}.json; run KMA/Chess/Build Fallback Pool.");
            cached = JsonUtility.FromJson<ChessFallbackFile>(asset.text).puzzles;
            return cached;
        }

        public static ChessPuzzleSnapshot Pick(ChessLevel level, int seed, string avoidFen)
        {
            List<ChessPuzzleSnapshot> own = All().Where(p => p.Level == level).ToList();
            if (own.Count == 0) throw new InvalidOperationException($"No {level} fallback puzzle in the pool.");
            List<ChessPuzzleSnapshot> pool = own.Where(p => p.fen != avoidFen).ToList();
            if (pool.Count == 0) pool = own;
            return pool[new System.Random(seed).Next(pool.Count)];
        }
    }
}
