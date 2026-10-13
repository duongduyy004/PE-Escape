using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KMA.Gameplay.Core;
using KMA.Gameplay.UI;
using UnityEngine;

namespace KMA.Gameplay.Chess
{
    public sealed class ChessFinalController : MinigameBase, IChallengeMetricsSource, IMinigameGuideSource
    {
        const int ContextWaitFrames = 10;
        // Avoids serving the same puzzle twice in a row within one app session.
        static string lastFen;

        [SerializeField] ChessBoardView board;
        [SerializeField] ChessFinalHud hud;
        [SerializeField] ChessCastView cast;
        [SerializeField] PromotionPicker promotion;
        [SerializeField] float bossThinkSeconds = .5f;
        [SerializeField] float bossMoveSeconds = .6f;
        [SerializeField] float resultDelaySeconds = .9f;
        ChessAttemptMachine machine;
        ChessAnalysisWorker worker;
        ChessPuzzleProvider provider;
        ChessLevel currentLevel = ChessLevel.Normal;
        Coroutine bossRoutine;
        int epoch;
        bool resolving;
        bool awaitingHint;

        public ChessAttemptMachine Machine => machine;
        public bool PuzzleReady => machine != null;
        public event Action<ChessAttemptMachine> PuzzleReadyEvent;
        public override bool UsesSharedTutorial => false;
        public override bool UsesSharedCountdown => false;
        public override bool OwnsStartGate => true;
        public override bool OwnsCameraBackground => true;
        public string GuideKey => ChessGuide.Key;

        public IReadOnlyList<TutorialStep> BuildGuide()
        {
            ChessLevelRules rules = ChessLevelRules.For(currentLevel);
            return ChessGuide.Build(machine?.OptimalMoves ?? rules.OptimalMoves, machine?.MaxMoves ?? rules.MaxMoves,
                machine != null ? Mathf.RoundToInt(machine.Clock.Limit) : 0);
        }

        public static ChessLevel LevelFor(ChallengeDifficulty difficulty) => difficulty switch
        {
            ChallengeDifficulty.Easy => ChessLevel.Easy,
            ChallengeDifficulty.Hard => ChessLevel.Hard,
            _ => ChessLevel.Normal
        };

        public void Configure(ChessBoardView boardView, ChessFinalHud hudView, ChessCastView castView,
            PromotionPicker picker)
        {
            board = boardView;
            hud = hudView;
            cast = castView;
            promotion = picker;
        }

        protected override void Awake()
        {
            base.Awake();
            SetTutorialGate(true);
        }

        void Start()
        {
            board.MoveRequested += RequestMove;
            hud.StartButton.onClick.AddListener(BeginAttempt);
            hud.HintButton.onClick.AddListener(RevealHint);
            hud.RetryAnalysisButton.onClick.AddListener(() => machine?.RetryAnalysis());
            hud.RestartPuzzleButton.onClick.AddListener(RestartPuzzle);
            var panel = FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
            if (panel != null)
            {
                panel.SetTitles("CHIẾU HẾT", "CHƯA ĐẠT");
                panel.UseChessRanks(true);
            }
            hud.SetGenerating(true);
            hud.StartButton.interactable = false;
            hud.SetHintAvailable(false);
            hud.ShowAnalysisError(false);
            promotion.Close();
            cast.SetStudent("idle");
            cast.SetTeacher("idleBoss");
            var dialogue = GetComponent<ChessDialogueAdapter>();
            if (dialogue != null) dialogue.Configure(this, GetComponent<GameplayDialogueHost>(), cast);
            StartCoroutine(Boot());
        }

        IEnumerator Boot()
        {
            // The puzzle must not be generated before the Journey difficulty is known.
            var adapter = GetComponent<JourneyControllerAdapter>();
            for (int frame = 0; adapter != null && adapter.Context == null && frame < ContextWaitFrames; frame++)
                yield return null;
            currentLevel = adapter != null && adapter.Context != null ? LevelFor(adapter.Context.Difficulty) : ChessLevel.Normal;

            // A Journey retry of the Final repeats the failed puzzle; every other start gets a new one.
            bool retry = ChallengeRetryIntent.Consume(adapter?.Context?.ChallengeId);
            ChessPuzzleSnapshot reuse = ChessRetryStore.TakeIfRetry(retry);
            if (reuse != null)
            {
                currentLevel = reuse.Level;
                lastFen = reuse.fen;
                StartAttempt(reuse);
                yield break;
            }

            provider = new ChessPuzzleProvider();
            provider.Begin(currentLevel, Environment.TickCount, lastFen);
            ChessPuzzleResult result = null;
            yield return new WaitUntil(() => provider.TryTake(out result));
            lastFen = result.Snapshot.fen;
            StartAttempt(result.Snapshot);
        }

        void StartAttempt(ChessPuzzleSnapshot snapshot)
        {
            machine = new ChessAttemptMachine(snapshot);
            worker = new ChessAnalysisWorker();
            machine.PhaseChanged += OnPhaseChanged;
            machine.MoveCommitted += OnMoveCommitted;
            machine.AnalysisRequested += request => worker.Submit(request);
            machine.HintRequested += request => worker.SubmitHint(request);
            machine.IllegalMoveRejected += _ => hud.Toast("Nước đi không hợp lệ. Chọn ô khác.");
            machine.HintFailed += () => { awaitingHint = false; hud.Toast("Chưa tính được gợi ý. Thử lại."); };
            machine.Finished += OnFinished;

            ShowFreshAttempt();
            var dialogue = GetComponent<ChessDialogueAdapter>();
            hud.StartButton.interactable = dialogue == null || dialogue.OpeningReady;
            PuzzleReadyEvent?.Invoke(machine);
        }

        /// Shows the intro for the machine's puzzle in its initial state (also used by Restart).
        void ShowFreshAttempt()
        {
            hud.SetObjective(machine.OptimalMoves);
            hud.SetMoves(0, machine.MaxMoves);
            hud.SetClock(machine.Clock.Remaining);
            hud.SetTurn(machine.Phase);
            hud.SetHintAvailable(false);
            hud.ShowAnalysisError(false);
            hud.SetIntro($"Tối đa {machine.MaxMoves} nước.\nThời gian suy nghĩ {ChessGuide.TimeWords(machine.Clock.Limit)}.");
            hud.ShowIntro(true);
            board.Render(machine.Position, null);
            RefreshInput();
        }

        public void BeginAttempt()
        {
            if (machine == null || machine.Phase != ChessAttemptPhase.Intro) return;
            var dialogue = GetComponent<ChessDialogueAdapter>();
            if (dialogue != null && !dialogue.OpeningReady) return;
            hud.ShowIntro(false);
            SetTutorialGate(false);
            machine.Begin();
            dialogue?.Began();
        }

        protected override void Update()
        {
            base.Update();
            if (machine == null) return;
            bool paused = Time.timeScale == 0f;
            if (paused) machine.Pause();
            else if (machine.Phase == ChessAttemptPhase.Paused) machine.Resume();
            machine.Tick(Time.deltaTime);
            if (worker != null)
            {
                while (worker.TryTakeAnalysis(out AnalysisResult analysis)) machine.CompleteAnalysis(analysis);
                while (worker.TryTakeHint(out ChessHint hint)) machine.CompleteHint(hint);
            }
            hud.SetClock(machine.Clock.Remaining);
        }

        protected override void TickPlay(float dt) { }

        public void RequestMove(int from, int to)
        {
            if (machine == null || !machine.CanMove || promotion.IsOpen) return;
            List<ChessMove> options = MoveGenerator.LegalMoves(machine.Position)
                .Where(m => m.From == from && m.To == to).ToList();
            if (options.Count <= 1)
            {
                Submit(options.Count == 1 ? options[0] : new ChessMove(from, to));
                return;
            }
            board.SetInteractable(false, PieceColor.White, null);
            promotion.Open(type =>
            {
                RefreshInput();
                if (machine.CanMove) Submit(new ChessMove(from, to, type));
            });
        }

        public void RevealHint()
        {
            if (machine == null) return;
            awaitingHint = machine.RequestHint();
        }

        void Submit(ChessMove move)
        {
            if (machine.Submit(move) == SubmitResult.Accepted) hud.SetMoves(machine.MovesUsed, machine.MaxMoves);
        }

        IEnumerator BossTurnRoutine()
        {
            int mine = epoch;
            cast.SetTeacher("chessThink");
            yield return new WaitForSeconds(bossThinkSeconds);
            yield return new WaitUntil(() => machine.Phase != ChessAttemptPhase.Paused);
            if (mine != epoch || machine.Phase != ChessAttemptPhase.BossTurn || !machine.PendingBossReply.HasValue)
            {
                bossRoutine = null;
                yield break;
            }
            ChessMove reply = machine.PendingBossReply.Value;
            ChessPosition before = machine.Position;
            // The chessMove pose holds a rook, so it is only shown for rook moves.
            cast.SetTeacher(Piece.TypeOf(before[reply.From]) == Piece.Rook ? "chessMove" : "chessThink");
            yield return board.AnimateMove(reply, before.Apply(reply), bossMoveSeconds);
            // A pause can land on the frame the slide ends; the move is committed only after resume.
            yield return new WaitUntil(() => machine.Phase != ChessAttemptPhase.Paused);
            if (mine != epoch || machine.Phase != ChessAttemptPhase.BossTurn)
            {
                // Restarted during the slide: the animation drew the old boss move, so draw the real board again.
                board.Render(machine.Position, machine.LastMove);
                bossRoutine = null;
                yield break;
            }
            if (machine.CompleteBossMove()) cast.SetTeacher("strictLook");
            bossRoutine = null;
        }

        void RestartPuzzle()
        {
            if (machine == null) return;
            epoch++;
            worker.CancelAll();
            promotion.Close();
            machine.Restart();
            resolving = false;
            awaitingHint = false;
            cast.SetStudent("idle");
            cast.SetTeacher("idleBoss");
            ShowFreshAttempt();
            hud.StartButton.interactable = true;
            SetTutorialGate(true);
        }

        void OnPhaseChanged(ChessAttemptPhase phase)
        {
            if (hud == null) return;
            hud.SetTurn(phase);
            hud.SetHintAvailable(phase == ChessAttemptPhase.PlayerTurn);
            hud.ShowAnalysisError(phase == ChessAttemptPhase.AnalysisError);
            if (phase == ChessAttemptPhase.BossTurn && bossRoutine == null)
                bossRoutine = StartCoroutine(BossTurnRoutine());
            if (phase == ChessAttemptPhase.PlayerTurn && awaitingHint && machine.HintText != null)
            {
                awaitingHint = false;
                var dialogue = GetComponent<ChessDialogueAdapter>();
                if (dialogue != null) dialogue.Hint(machine.HintText);
                else { cast.SetTeacher("taunt"); cast.Say(machine.HintText, 8f); }
            }
            RefreshInput();
        }

        void OnMoveCommitted(ChessMove move, bool byPlayer)
        {
            awaitingHint = false;
            if (byPlayer)
            {
                board.Render(machine.Position, move);
                if (!resolving) cast.SetStudent("idle");
            }
        }

        void OnFinished(bool solved)
        {
            if (resolving) return;
            resolving = true;
            if (solved) ChessRetryStore.Forget();
            else ChessRetryStore.Remember(machine.Snapshot);
            StartCoroutine(Resolve(solved));
        }

        IEnumerator Resolve(bool solved)
        {
            int mine = epoch;
            promotion.Close();
            board.SetInteractable(false, PieceColor.White, null);
            if (solved)
            {
                cast.SetStudent("cheer0");
                cast.SetTeacher("cheer0");
            }
            else
            {
                bool timeUp = machine.EndReason == ChessEndReason.TimeUp;
                hud.SetTurnText(EndReasonText(machine.EndReason));
                cast.SetStudent("hurt");
                cast.SetTeacher(timeUp ? "handsOnHips" : "angry");
            }
            // Shown when the chess scene is opened directly; a Journey result card replaces it with the same text.
            UnityEngine.Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include)
                ?.ShowChessDetail(ChessResultText.Detail(BuildSummary()));
            GetComponent<ChessDialogueAdapter>()?.FinishDialogue(solved);
            yield return new WaitForSeconds(resultDelaySeconds);
            if (mine != epoch) yield break;
            Finish(BuildResult(solved));
        }

        public MinigameResult BuildResult(bool solved)
        {
            ChessScore score = machine.Result;
            return new MinigameResult(score.Pass, score.Score, ToRank(score.Grade));
        }

        // A+ is stored as the existing top rank so saved data and other subjects keep working; the chess screens
        // render it as A+ (spec section 7).
        static Rank ToRank(ChessGrade grade) => grade switch
        {
            ChessGrade.APlus => Rank.S,
            ChessGrade.A => Rank.A,
            ChessGrade.B => Rank.B,
            ChessGrade.C => Rank.C,
            ChessGrade.D => Rank.D,
            _ => Rank.F
        };

        ChessResultSummary BuildSummary() => new ChessResultSummary
        {
            scoringVersion = ChessResultSummary.CurrentScoringVersion,
            movesUsed = machine.MovesUsed,
            maxMoves = machine.MaxMoves,
            optimalMoves = machine.OptimalMoves,
            allowedSeconds = Mathf.RoundToInt(machine.Clock.Limit),
            usedSeconds = machine.Clock.Elapsed,
            hintUsed = machine.HintUsed,
            explicitHintUsed = machine.ExplicitHintUsed,
            hintCapped = machine.Result.HintCapped,
            endReason = machine.EndReason.ToString(),
            gradeLabel = ChessAttemptGrader.Label(machine.Result.Grade),
            score = machine.Result.Score,
            fen = machine.Snapshot.fen,
            source = machine.Snapshot.source,
            seed = machine.Snapshot.seed,
            estimatorVersion = machine.Snapshot.estimatorVersion,
            moves = machine.Moves.Select(m => new ChessMoveEntry
                { uci = m.Uci, q = (float)m.Q, best = m.BestDistance, chosen = m.ChosenDistance }).ToArray()
        };

        public ChallengeMetrics BuildMetrics(ChallengeDefinition definition, MinigameResult result)
        {
            ChessResultSummary summary = BuildSummary();
            return new ChallengeMetrics(elapsed: machine.Clock.Elapsed, completedTargets: machine.MovesUsed,
                mistakes: machine.Moves.Count(m => m.Q < 0.999), hintUsed: machine.HintUsed,
                detail: ChessResultText.Detail(summary), chess: summary);
        }

        static string EndReasonText(ChessEndReason reason) => reason switch
        {
            ChessEndReason.TimeUp => "Hết giờ",
            ChessEndReason.OutOfMoves => "Hết số nước",
            ChessEndReason.Mated => "Bị chiếu hết",
            ChessEndReason.Draw => "Hòa cờ",
            _ => "Chưa đạt"
        };

        void RefreshInput()
        {
            if (board == null || promotion == null || machine == null) return;
            bool open = machine.CanMove && !promotion.IsOpen;
            board.SetInteractable(open, PieceColor.White, open ? MoveGenerator.LegalMoves(machine.Position) : null);
        }

        void OnDisable()
        {
            worker?.CancelAll();
            provider?.Cancel();
        }

        void OnDestroy()
        {
            worker?.CancelAll();
            worker?.Dispose();
            provider?.Cancel();
            provider?.Dispose();
        }
    }
}
