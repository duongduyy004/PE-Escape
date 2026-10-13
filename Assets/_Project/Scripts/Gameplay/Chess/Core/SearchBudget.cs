using System;
using System.Diagnostics;

namespace KMA.Gameplay.Chess
{
    public enum SearchStop { None, Nodes, Time, Cancelled }

    public sealed class SearchAbortedException : Exception
    {
        public SearchAbortedException(SearchStop reason) : base("Search stopped: " + reason) => Reason = reason;
        public SearchStop Reason { get; }
    }

    /// One budget per request. Charge is called once per search node and throws when the budget is spent.
    public sealed class SearchBudget
    {
        public const int DefaultNodes = 200_000;
        public const double DefaultSeconds = 2.0;

        readonly int maxNodes;
        readonly double maxSeconds;
        readonly Func<bool> isCancelled;
        readonly Stopwatch clock = Stopwatch.StartNew();

        public SearchBudget(int maxNodes, double maxSeconds, Func<bool> isCancelled = null)
        {
            this.maxNodes = maxNodes;
            this.maxSeconds = maxSeconds;
            this.isCancelled = isCancelled;
        }

        public static SearchBudget Default(Func<bool> isCancelled = null) =>
            new SearchBudget(DefaultNodes, DefaultSeconds, isCancelled);

        public int Nodes { get; private set; }
        public SearchStop Stopped { get; private set; }
        public double ElapsedSeconds => clock.Elapsed.TotalSeconds;

        public void Charge()
        {
            if (Stopped != SearchStop.None) throw new SearchAbortedException(Stopped);
            Nodes++;
            if (Nodes > maxNodes) Stop(SearchStop.Nodes);
            if (Nodes == 1 || (Nodes & 63) == 0)
            {
                if (isCancelled != null && isCancelled()) Stop(SearchStop.Cancelled);
                if (clock.Elapsed.TotalSeconds > maxSeconds) Stop(SearchStop.Time);
            }
        }

        void Stop(SearchStop reason)
        {
            Stopped = reason;
            throw new SearchAbortedException(reason);
        }
    }
}
