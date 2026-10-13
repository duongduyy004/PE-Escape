using System;
using System.Collections.Generic;

namespace KMA.Gameplay.Chess
{
    public enum ChessAttemptPhase { Intro, PlayerTurn, Analyzing, BossTurn, Paused, Completed, Failed, AnalysisError }

    public enum ChessEndReason { None, Mate, TimeUp, OutOfMoves, Mated, Draw }

    public enum SubmitResult { NotConsidered, Illegal, TimeUp, Accepted }

    public sealed class AttemptMove
    {
        public string Uci;
        public double Q;
        public int BestDistance;
        public int ChosenDistance;
        public int MovesLeftBefore;
    }

    /// Gameplay state of one attempt at one puzzle. It never searches: it asks for analysis, and the host feeds the
    /// result back on the main thread. Presentation listens to the events and calls CompleteBossMove after animating.
    public sealed class ChessAttemptMachine
    {
        enum Outcome { Continue, Mate, Draw, OutOfMoves }

        readonly ChessPosition start;
        readonly PositionHistory history = new PositionHistory();
        readonly List<AttemptMove> moves = new List<AttemptMove>();
        ChessAttemptPhase pausedFrom;
        Action deferred;
        int nextId;
        int activeId = -1;
        int hintId = -1;
        bool hintPending;
        int hintLevelRequested;
        AnalysisRequest pendingAnalysis;
        Outcome pendingOutcome;
        DrawReason pendingDraw;
        ChessMove pendingMove;

        public ChessAttemptMachine(ChessPuzzleSnapshot snapshot)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.maxMoves < 1 || snapshot.optimalMoves < 1 || snapshot.timeSeconds <= 0)
                throw new ArgumentException("The snapshot has no move limit or time.", nameof(snapshot));
            start = snapshot.StartPosition;
            Clock = new ThinkClock(snapshot.timeSeconds);
            Reset();
        }

        public event Action<ChessAttemptPhase> PhaseChanged;
        public event Action<AnalysisRequest> AnalysisRequested;
        public event Action<HintRequest> HintRequested;
        public event Action<ChessMove, bool> MoveCommitted;
        public event Action<ChessMove> IllegalMoveRejected;
        public event Action<bool> Finished;
        public event Action HintFailed;

        public ChessPuzzleSnapshot Snapshot { get; }
        public ThinkClock Clock { get; }
        public ChessAttemptPhase Phase { get; private set; }
        public ChessPosition Position { get; private set; }
        public int MovesUsed { get; private set; }
        public int MaxMoves => Snapshot.maxMoves;
        public int OptimalMoves => Snapshot.optimalMoves;
        public ChessEndReason EndReason { get; private set; }
        public DrawReason DrawReason { get; private set; }
        public ChessMove? LastMove { get; private set; }
        public ChessMove? PendingBossReply { get; private set; }
        public AnalysisRequest PendingAnalysis => pendingAnalysis;
        public int HintLevel { get; private set; }
        public string HintText { get; private set; }
        public bool HintUsed { get; private set; }
        public bool ExplicitHintUsed { get; private set; }
        public int Revision { get; private set; }
        public IReadOnlyList<AttemptMove> Moves => moves;
        public ChessScore Result { get; private set; }
        public bool CanMove => Phase == ChessAttemptPhase.PlayerTurn;

        public void Begin()
        {
            if (Phase != ChessAttemptPhase.Intro) return;
            ReturnToPlayer();
        }

        public SubmitResult Submit(ChessMove move)
        {
            if (Phase != ChessAttemptPhase.PlayerTurn) return SubmitResult.NotConsidered;
            if (Clock.Expired)
            {
                Fail(ChessEndReason.TimeUp);
                return SubmitResult.TimeUp;
            }
            if (!MoveGenerator.LegalMoves(Position).Contains(move))
            {
                IllegalMoveRejected?.Invoke(move);
                return SubmitResult.Illegal;
            }

            Clock.Stop();
            ChessPosition before = Position;
            int movesLeftBefore = MaxMoves - MovesUsed;
            ChessPosition after = before.Apply(move);
            MovesUsed++;
            Revision++;
            HintLevel = 0;
            HintText = null;
            Commit(after, move, true);
            pendingOutcome = Judge(after, out pendingDraw);
            pendingMove = move;
            pendingAnalysis = new AnalysisRequest
            {
                Id = activeId = ++nextId,
                BeforeFen = before.ToFen(),
                Move = move,
                MovesLeftBefore = movesLeftBefore,
                NeedDefence = pendingOutcome == Outcome.Continue,
                Seed = Snapshot.seed * 31 + MovesUsed
            };
            SetPhase(ChessAttemptPhase.Analyzing);
            AnalysisRequested?.Invoke(pendingAnalysis);
            return SubmitResult.Accepted;
        }

        /// False when the result is stale. A result that arrives while paused is applied on Resume.
        public bool CompleteAnalysis(AnalysisResult result)
        {
            if (result == null || result.Id != activeId || pendingAnalysis == null) return false;
            if (Phase != ChessAttemptPhase.Analyzing && !(Phase == ChessAttemptPhase.Paused &&
                                                           pausedFrom == ChessAttemptPhase.Analyzing)) return false;
            Deliver(() => ApplyAnalysis(result));
            return true;
        }

        public void RetryAnalysis()
        {
            if (Phase != ChessAttemptPhase.AnalysisError || pendingAnalysis == null) return;
            pendingAnalysis.Id = activeId = ++nextId;
            SetPhase(ChessAttemptPhase.Analyzing);
            AnalysisRequested?.Invoke(pendingAnalysis);
        }

        public bool CompleteBossMove()
        {
            if (Phase != ChessAttemptPhase.BossTurn || !PendingBossReply.HasValue) return false;
            ChessMove reply = PendingBossReply.Value;
            PendingBossReply = null;
            Revision++;
            HintLevel = 0;
            HintText = null;
            Commit(Position.Apply(reply), reply, false);
            if (MoveGenerator.IsCheckmate(Position)) { Fail(ChessEndReason.Mated); return true; }
            DrawReason draw = ChessDrawRules.Evaluate(Position, history.Record(Position));
            if (draw != DrawReason.None) { DrawReason = draw; Fail(ChessEndReason.Draw); return true; }
            ReturnToPlayer();
            return true;
        }

        public bool RequestHint()
        {
            if (Phase != ChessAttemptPhase.PlayerTurn || hintPending) return false;
            hintPending = true;
            hintLevelRequested = Math.Min(ChessHintBuilder.MaxLevel, HintLevel + 1);
            hintId = ++nextId;
            Clock.Stop();
            SetPhase(ChessAttemptPhase.Analyzing);
            HintRequested?.Invoke(new HintRequest
            {
                Id = hintId, Fen = Position.ToFen(), MovesLeft = MaxMoves - MovesUsed, Level = hintLevelRequested
            });
            return true;
        }

        public bool CompleteHint(ChessHint hint)
        {
            if (!hintPending || hint.Id != hintId) return false;
            if (Phase != ChessAttemptPhase.Analyzing && !(Phase == ChessAttemptPhase.Paused &&
                                                           pausedFrom == ChessAttemptPhase.Analyzing)) return false;
            Deliver(() => ApplyHint(hint));
            return true;
        }

        public void Pause()
        {
            if (Phase == ChessAttemptPhase.Paused || Phase == ChessAttemptPhase.Completed ||
                Phase == ChessAttemptPhase.Failed) return;
            pausedFrom = Phase;
            Clock.Stop();
            SetPhase(ChessAttemptPhase.Paused);
        }

        public void Resume()
        {
            if (Phase != ChessAttemptPhase.Paused) return;
            if (pausedFrom == ChessAttemptPhase.PlayerTurn) Clock.Start();
            SetPhase(pausedFrom);
            Action apply = deferred;
            deferred = null;
            apply?.Invoke();
        }

        public void Tick(float dt)
        {
            if (Phase == ChessAttemptPhase.PlayerTurn && Clock.Tick(dt)) Fail(ChessEndReason.TimeUp);
        }

        public void Restart() => Reset();

        void Reset()
        {
            Position = start;
            MovesUsed = 0;
            Revision = 0;
            HintLevel = 0;
            HintText = null;
            HintUsed = false;
            ExplicitHintUsed = false;
            EndReason = ChessEndReason.None;
            DrawReason = DrawReason.None;
            LastMove = null;
            PendingBossReply = null;
            pendingAnalysis = null;
            deferred = null;
            hintPending = false;
            activeId = -1;
            hintId = -1;
            moves.Clear();
            history.Clear();
            history.Record(start);
            Result = default;
            Clock.Reset();
            Phase = ChessAttemptPhase.Intro;
            PhaseChanged?.Invoke(Phase);
        }

        void Deliver(Action apply)
        {
            if (Phase == ChessAttemptPhase.Paused) deferred = apply;
            else apply();
        }

        void ApplyAnalysis(AnalysisResult result)
        {
            if (result.Status == SearchStatus.Unknown)
            {
                SetPhase(ChessAttemptPhase.AnalysisError);
                return;
            }
            moves.Add(new AttemptMove
            {
                Uci = pendingMove.ToUci(),
                Q = result.Quality.Q,
                BestDistance = result.Quality.BestDistance,
                ChosenDistance = result.Quality.ChosenDistance,
                MovesLeftBefore = pendingAnalysis.MovesLeftBefore
            });
            pendingAnalysis = null;
            switch (pendingOutcome)
            {
                case Outcome.Mate:
                    EndReason = ChessEndReason.Mate;
                    Clock.Stop();
                    Result = Score(true);
                    SetPhase(ChessAttemptPhase.Completed);
                    Finished?.Invoke(true);
                    break;
                case Outcome.Draw:
                    DrawReason = pendingDraw;
                    Fail(ChessEndReason.Draw);
                    break;
                case Outcome.OutOfMoves:
                    Fail(ChessEndReason.OutOfMoves);
                    break;
                default:
                    PendingBossReply = result.Defence.Move;
                    SetPhase(ChessAttemptPhase.BossTurn);
                    break;
            }
        }

        void ApplyHint(ChessHint hint)
        {
            hintPending = false;
            if (hint.Status == SearchStatus.Unknown)
            {
                ReturnToPlayer();
                HintFailed?.Invoke();
                return;
            }
            HintLevel = hint.Level;
            HintText = hint.Text;
            HintUsed = true;
            if (hint.NamesMove) ExplicitHintUsed = true;
            ReturnToPlayer();
        }

        /// Spec 6.3: checkmate, then draw, then the move limit.
        Outcome Judge(ChessPosition after, out DrawReason draw)
        {
            draw = DrawReason.None;
            if (MoveGenerator.IsCheckmate(after)) return Outcome.Mate;
            draw = ChessDrawRules.Evaluate(after, history.Record(after));
            if (draw != DrawReason.None) return Outcome.Draw;
            return MovesUsed >= MaxMoves ? Outcome.OutOfMoves : Outcome.Continue;
        }

        ChessScore Score(bool mated)
        {
            var qualities = new List<double>(moves.Count);
            foreach (AttemptMove move in moves) qualities.Add(move.Q);
            return ChessAttemptGrader.Score(mated, qualities, OptimalMoves, MovesUsed, ExplicitHintUsed);
        }

        void ReturnToPlayer()
        {
            Clock.Start();
            SetPhase(ChessAttemptPhase.PlayerTurn);
        }

        void Fail(ChessEndReason reason)
        {
            EndReason = reason;
            Clock.Stop();
            Result = Score(false);
            SetPhase(ChessAttemptPhase.Failed);
            Finished?.Invoke(false);
        }

        void Commit(ChessPosition next, ChessMove move, bool byPlayer)
        {
            Position = next;
            LastMove = move;
            MoveCommitted?.Invoke(move, byPlayer);
        }

        void SetPhase(ChessAttemptPhase phase)
        {
            if (Phase == phase) return;
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }
}
