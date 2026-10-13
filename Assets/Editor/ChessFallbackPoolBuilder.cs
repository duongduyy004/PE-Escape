using System.Collections.Generic;
using System.IO;
using KMA.Gameplay.Chess;
using UnityEditor;
using UnityEngine;

namespace KMA.EditorTools
{
    /// Builds Resources/Chess/fallback_pool.json offline with a large budget. Run once, commit the JSON.
    public static class ChessFallbackPoolBuilder
    {
        const string Path_ = "Assets/_Project/Resources/Chess/fallback_pool.json";
        const int PerLevel = 12;
        const int SeedBase = 90_000;

        [MenuItem("KMA/Chess/Build Fallback Pool")]
        public static void Build()
        {
            var limits = new ChessGeneratorLimits(5_000_000, 600_000, 50_000, 120);
            var puzzles = new List<ChessPuzzleSnapshot>();
            foreach (ChessLevel level in new[] { ChessLevel.Easy, ChessLevel.Normal, ChessLevel.Hard })
            {
                var seen = new HashSet<string>();
                for (int seed = SeedBase; seen.Count < PerLevel && seed < SeedBase + 400; seed++)
                {
                    ChessPuzzleSnapshot puzzle = ChessPuzzleGenerator.TryGenerate(level, seed, limits, null, null, out _);
                    if (puzzle == null || !seen.Add(puzzle.fen)) continue;
                    puzzle.source = ChessPuzzleSnapshot.FallbackSource;
                    puzzles.Add(puzzle);
                }
                Debug.Log($"[KMA] Fallback pool {level}: {seen.Count} puzzles");
            }
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path_));
            File.WriteAllText(Path_, JsonUtility.ToJson(new ChessFallbackFile { puzzles = puzzles.ToArray() }, true));
            AssetDatabase.ImportAsset(Path_);
        }
    }
}
