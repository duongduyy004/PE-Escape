using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace KMA.Gameplay.Chess
{
    /// Runs analysis and hint requests one at a time, in submission order, on pool threads. Only immutable request
    /// data goes in and plain result objects come out; the main thread polls the queues each frame and applies
    /// results itself.
    public sealed class ChessAnalysisWorker : IDisposable
    {
        readonly object gate = new object();
        readonly ConcurrentQueue<AnalysisResult> analyses = new ConcurrentQueue<AnalysisResult>();
        readonly ConcurrentQueue<ChessHint> hints = new ConcurrentQueue<ChessHint>();
        readonly ChessSearchEngine engine = new ChessSearchEngine(SearchBudget.Default());
        CancellationTokenSource cancel = new CancellationTokenSource();
        Task tail = Task.CompletedTask;
        bool disposed;

        public void Submit(AnalysisRequest request) => Run(() =>
        {
            AnalysisResult result;
            try { result = ChessTurnAnalyzer.Analyze(engine, request); }
            catch (Exception exception)
            {
                // A crashed job must not leave the game waiting forever; Unknown shows the retryable error.
                Debug.LogException(exception);
                result = new AnalysisResult
                {
                    Id = request.Id, Status = SearchStatus.Unknown,
                    Quality = new MoveQuality(SearchStatus.Unknown, 0, 0, 0)
                };
            }
            analyses.Enqueue(result);
        });

        public void SubmitHint(HintRequest request) => Run(() =>
        {
            ChessHint hint;
            try { hint = ChessHintBuilder.Build(engine, request); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                hint = new ChessHint(request.Id, SearchStatus.Unknown, request.Level, false, false, default, null);
            }
            hints.Enqueue(hint);
        });

        public bool TryTakeAnalysis(out AnalysisResult result) => analyses.TryDequeue(out result);

        public bool TryTakeHint(out ChessHint hint) => hints.TryDequeue(out hint);

        /// Stops the running search at its next budget check and discards queued work. Safe to call from any thread.
        public void CancelAll()
        {
            CancellationTokenSource old;
            lock (gate)
            {
                old = cancel;
                cancel = new CancellationTokenSource();
            }
            old.Cancel();
            while (analyses.TryDequeue(out _)) { }
            while (hints.TryDequeue(out _)) { }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                cancel.Cancel();
            }
        }

        void Run(Action job)
        {
            lock (gate)
            {
                if (disposed) return;
                CancellationToken token = cancel.Token;
                // Chained, so requests run strictly one at a time in order: the engine and its cache are not
                // thread-safe, and a lock alone would not keep the order.
                tail = tail.ContinueWith(_ =>
                {
                    if (token.IsCancellationRequested) return;
                    engine.UseBudget(SearchBudget.Default(() => token.IsCancellationRequested));
                    job();
                }, TaskScheduler.Default);
            }
        }
    }
}
