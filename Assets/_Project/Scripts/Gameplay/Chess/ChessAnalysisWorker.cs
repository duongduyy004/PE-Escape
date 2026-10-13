using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace KMA.Gameplay.Chess
{
    /// Runs analysis and hint requests one at a time on a pool thread. Only immutable request data goes in and plain
    /// result objects come out; the main thread polls the queues each frame and applies results itself.
    public sealed class ChessAnalysisWorker : IDisposable
    {
        readonly object gate = new object();
        readonly ConcurrentQueue<AnalysisResult> analyses = new ConcurrentQueue<AnalysisResult>();
        readonly ConcurrentQueue<ChessHint> hints = new ConcurrentQueue<ChessHint>();
        readonly ChessSearchEngine engine = new ChessSearchEngine(SearchBudget.Default());
        CancellationTokenSource cancel = new CancellationTokenSource();
        bool disposed;

        public void Submit(AnalysisRequest request) => Run(token =>
        {
            AnalysisResult result = ChessTurnAnalyzer.Analyze(engine, request);
            analyses.Enqueue(result);
        });

        public void SubmitHint(HintRequest request) => Run(token =>
        {
            hints.Enqueue(ChessHintBuilder.Build(engine, request));
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

        void Run(Action<CancellationToken> job)
        {
            CancellationToken token;
            lock (gate)
            {
                if (disposed) return;
                token = cancel.Token;
            }
            Task.Run(() =>
            {
                // One request at a time: the engine and its cache are not thread-safe.
                lock (engine)
                {
                    if (token.IsCancellationRequested) return;
                    engine.UseBudget(SearchBudget.Default(() => token.IsCancellationRequested));
                    job(token);
                }
            });
        }
    }
}
