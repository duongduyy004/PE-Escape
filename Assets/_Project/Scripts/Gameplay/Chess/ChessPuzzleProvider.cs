using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace KMA.Gameplay.Chess
{
    public sealed class ChessPuzzleResult
    {
        public ChessPuzzleSnapshot Snapshot;
        public GenerationStats Stats;
    }

    /// Generates a puzzle on a pool thread within the central budget and falls back to a verified puzzle of the same
    /// level. TryTake must be called from the main thread because the fallback pool is read through Resources.
    public sealed class ChessPuzzleProvider : IDisposable
    {
        public static int GeneratedCount { get; private set; }
        public static int FallbackCount { get; private set; }

        readonly ConcurrentQueue<(ChessPuzzleSnapshot generated, GenerationStats stats, ChessLevel level, int seed, string avoid, int ticket)> done =
            new ConcurrentQueue<(ChessPuzzleSnapshot, GenerationStats, ChessLevel, int, string, int)>();
        /// The generator seam: tests can swap in one that crashes.
        public delegate ChessPuzzleSnapshot Generator(ChessLevel level, int seed, string avoidFen, Func<bool> isCancelled,
            out GenerationStats stats);

        readonly Generator generate;
        CancellationTokenSource cancel = new CancellationTokenSource();
        int ticket;

        public ChessPuzzleProvider(Generator generator = null) => generate = generator ?? DefaultGenerator;

        static ChessPuzzleSnapshot DefaultGenerator(ChessLevel level, int seed, string avoidFen, Func<bool> isCancelled,
            out GenerationStats stats) =>
            ChessPuzzleGenerator.TryGenerate(level, seed, ChessGeneratorLimits.Default, avoidFen, isCancelled, out stats);

        public void Begin(ChessLevel level, int seed, string avoidFen)
        {
            Cancel();
            int mine = ++ticket;
            CancellationToken token = cancel.Token;
            Task.Run(() =>
            {
                ChessPuzzleSnapshot snapshot = null;
                var stats = new GenerationStats();
                try { snapshot = generate(level, seed, avoidFen, () => token.IsCancellationRequested, out stats); }
                catch (Exception exception)
                {
                    // A crashed generator must not leave the exam on "generating" forever: take the fallback.
                    Debug.LogError("[KMA] Chess puzzle generator crashed: " + exception);
                    snapshot = null;
                    stats = stats ?? new GenerationStats();
                }
                if (!token.IsCancellationRequested) done.Enqueue((snapshot, stats, level, seed, avoidFen, mine));
            });
        }

        public bool TryTake(out ChessPuzzleResult result)
        {
            result = null;
            if (!done.TryDequeue(out var item) || item.ticket != ticket) return false;
            ChessPuzzleSnapshot snapshot = item.generated;
            if (snapshot == null)
            {
                snapshot = ChessFallbackLibrary.Pick(item.level, item.seed, item.avoid);
                FallbackCount++;
            }
            else GeneratedCount++;
            Debug.Log($"[KMA] Chess puzzle {snapshot.source} {snapshot.level} N={snapshot.optimalMoves} M={snapshot.maxMoves} " +
                      $"T={snapshot.timeSeconds}s candidates={item.stats.Candidates} nodes={item.stats.Nodes} " +
                      $"time={item.stats.ElapsedSeconds:0.00}s (generated {GeneratedCount}, fallback {FallbackCount})");
            result = new ChessPuzzleResult { Snapshot = snapshot, Stats = item.stats };
            return true;
        }

        public void Cancel()
        {
            cancel.Cancel();
            cancel = new CancellationTokenSource();
            while (done.TryDequeue(out _)) { }
        }

        public void Dispose() => cancel.Cancel();
    }
}
