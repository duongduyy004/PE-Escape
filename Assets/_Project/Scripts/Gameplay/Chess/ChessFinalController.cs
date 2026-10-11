using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KMA.Gameplay.UI;
using UnityEngine;

namespace KMA.Gameplay.Chess
{
    public sealed class ChessFinalController : MinigameBase, IChallengeMetricsSource, IMinigameGuideSource
    {
        [SerializeField] ChessBoardView board;
        [SerializeField] ChessFinalHud hud;
        [SerializeField] ChessCastView cast;
        [SerializeField] PromotionPicker promotion;
        [SerializeField] float bossThinkSeconds = .5f;
        [SerializeField] float bossMoveSeconds = .6f;
        [SerializeField] float resultDelaySeconds = .9f;
        bool resolving;

        public ChessFinalStateMachine Machine { get; private set; }
        public override bool UsesSharedTutorial => false;
        public override bool UsesSharedCountdown => false;
        public override bool OwnsStartGate => true;
        public override bool OwnsCameraBackground => true;
        public string GuideKey => ChessGuide.Key;
        public IReadOnlyList<TutorialStep> BuildGuide() =>
            ChessGuide.Build(Machine.MaxPlayerMoves, Machine.Clock.Limit, Machine.MaxMistakes);

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
            Machine = new ChessFinalStateMachine(ChessPuzzleLibrary.ForDifficulty(ChallengeDifficulty.Normal));
            Machine.PhaseChanged += OnPhaseChanged;
            Machine.MoveCommitted += OnMoveCommitted;
            Machine.MistakeMade += OnMistake;
            Machine.IllegalMoveRejected += _ => hud.Toast("Nước đi không hợp lệ. Chọn ô khác.");
            Machine.GradingFailed += OnGradingFailed;
            Machine.Finished += OnFinished;
        }

        void Start()
        {
            board.MoveRequested += RequestMove;
            hud.StartButton.onClick.AddListener(BeginAttempt);
            hud.HintButton.onClick.AddListener(RevealHint);
            var panel = FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
            if (panel != null) panel.SetTitles("CHIẾU HẾT", "CHƯA ĐẠT");
            hud.SetObjective(Machine.MaxPlayerMoves);
            hud.SetMistakes(0, Machine.MaxMistakes);
            hud.SetClock(Machine.Clock.Remaining);
            hud.SetTurn(Machine.Phase);
            hud.SetHintAvailable(false);
            hud.ShowIntro(true);
            promotion.Close();
            board.Render(Machine.Position, null);
            cast.SetStudent("idle");
            cast.SetTeacher("idleBoss");
            var dialogue=GetComponent<ChessDialogueAdapter>();
            if(dialogue!=null)dialogue.Configure(this,GetComponent<GameplayDialogueHost>(),cast);
            else cast.Say($"{CountWord(Machine.MaxPlayerMoves)} nước, {ChessGuide.TimeWords(Machine.Clock.Limit)} suy nghĩ. Tôi không chấm điểm cho việc ngồi nhìn.",6f);
            RefreshInput();
        }

        public void BeginAttempt()
        {
            if (Machine.Phase != ChessFinalPhase.Intro) return;
            var dialogue=GetComponent<ChessDialogueAdapter>();
            if(dialogue!=null && !dialogue.OpeningReady)return;
            hud.ShowIntro(false);
            SetTutorialGate(false);
            Machine.Begin();
            dialogue?.Began();
        }

        protected override void Update()
        {
            base.Update();
            bool paused = Time.timeScale == 0f;
            if (paused) Machine.Pause();
            else if (Machine.Phase == ChessFinalPhase.Paused) Machine.Resume();
            Machine.Tick(Time.deltaTime);
            hud.SetClock(Machine.Clock.Remaining);
        }

        protected override void TickPlay(float dt) { }

        public void RequestMove(int from, int to)
        {
            if (!Machine.CanMove || promotion.IsOpen) return;
            List<ChessMove> options = MoveGenerator.LegalMoves(Machine.Position)
                .Where(m => m.From == from && m.To == to).ToList();
            if (options.Count <= 1)
            {
                Submit(options.Count == 1 ? options[0] : new ChessMove(from, to));
                return;
            }
            board.SetInteractable(false, Machine.Puzzle.PlayerColor, null);
            promotion.Open(type =>
            {
                RefreshInput();
                if (Machine.CanMove) Submit(new ChessMove(from, to, type));
            });
        }

        public void RevealHint()
        {
            string text = Machine.RevealNextHint();
            if (text == null) return;
            var dialogue=GetComponent<ChessDialogueAdapter>();
            if(dialogue!=null)dialogue.Hint(text);
            else {cast.SetTeacher("taunt");cast.Say(text,8f);}
            hud.SetHintAvailable(Machine.HintLevel < PuzzleHints.MaxLevel);
        }

        void Submit(ChessMove move)
        {
            if (Machine.Submit(move) == GradeKind.Accepted) StartCoroutine(BossTurn());
        }

        IEnumerator BossTurn()
        {
            cast.SetTeacher("chessThink");
            yield return new WaitForSeconds(bossThinkSeconds);
            if (Machine.Phase != ChessFinalPhase.BossTurn && Machine.Phase != ChessFinalPhase.Paused) yield break;
            yield return new WaitUntil(() => Machine.Phase == ChessFinalPhase.BossTurn);
            ChessMove reply = Machine.PendingBossReply.Value;
            ChessPosition before = Machine.Position;
            // The chessMove pose holds a rook, so it is only shown for rook moves.
            cast.SetTeacher(Piece.TypeOf(before[reply.From]) == Piece.Rook ? "chessMove" : "chessThink");
            yield return board.AnimateMove(reply, before.Apply(reply), bossMoveSeconds);
            // A pause can land on the frame the slide ends; the move is committed only after resume.
            yield return new WaitUntil(() => Machine.Phase == ChessFinalPhase.BossTurn);
            if (Machine.CompleteBossMove()) cast.SetTeacher("strictLook");
        }

        void OnPhaseChanged(ChessFinalPhase phase)
        {
            if (hud == null) return;
            hud.SetTurn(phase);
            hud.SetHintAvailable(phase == ChessFinalPhase.PlayerTurn && Machine.HintLevel < PuzzleHints.MaxLevel);
            // Each submit clears the hurt pose; a mistake sets it again right after.

            // The whistle sequence after a mistake already ends on strictLook, so it is left to finish.

            RefreshInput();
        }

        void OnMoveCommitted(ChessMove move, bool byPlayer)
        {
            if (byPlayer) { board.Render(Machine.Position, move); if(!resolving)cast.SetStudent("idle"); }
        }

        void OnMistake(int mistakes)
        {
            hud.SetMistakes(Mathf.Min(mistakes, Machine.MaxMistakes), Machine.MaxMistakes);
            board.Render(Machine.Position, Machine.LastMove);
            cast.SetStudent("hurt");
            GameAudio.Play(GameSound.Whistle);
            int left = Machine.MaxMistakes - mistakes;
            if (left < 0) return;
            var dialogue=GetComponent<ChessDialogueAdapter>();
            if(dialogue!=null)dialogue.Mistake(mistakes);
            else {cast.PlayTeacher("whistle0","whistle1",mistakes==1?"strictLook":"angry");cast.Say(mistakes==1?"Sai rồi. Còn một lần sửa, nghĩ kỹ trước khi đi.":"Hai lần sai rồi. Sai thêm một lần là dừng bài.");}
        }

        void OnGradingFailed(string message)
        {
            Debug.LogError("[KMA] Chess puzzle data error: " + message);
            hud.Toast("Không thể chấm nước đi này do lỗi dữ liệu. Bàn cờ giữ nguyên.");
            board.Render(Machine.Position, Machine.LastMove);
        }

        void OnFinished(bool solved)
        {
            if (resolving) return;
            resolving = true;
            StartCoroutine(Resolve(solved));
        }

        IEnumerator Resolve(bool solved)
        {
            promotion.Close();
            board.SetInteractable(false, Machine.Puzzle.PlayerColor, null);
            if (solved)
            {
                cast.SetStudent("cheer0");
                cast.SetTeacher("cheer0");

            }
            else
            {
                bool timeUp = Machine.FailReason == ChessFailReason.TimeUp;
                hud.SetTurnText(timeUp ? "Hết giờ" : "Quá số lần sửa");
                cast.SetStudent("hurt");
                cast.SetTeacher(timeUp?"handsOnHips":"angry");
            }
            GetComponent<ChessDialogueAdapter>()?.FinishDialogue(solved);
            yield return new WaitForSeconds(resultDelaySeconds);
            Finish(BuildResult(solved));
        }

        public MinigameResult BuildResult(bool solved) => ScoreUtil.Build(solved,
            accuracy: 2f - Machine.Mistakes,
            efficiency: Machine.Clock.Remaining / Machine.Clock.Limit,
            mastery: Machine.HintUsed ? 0f : 1f);

        public ChallengeMetrics BuildMetrics(ChallengeDefinition definition, MinigameResult result) =>
            new ChallengeMetrics(elapsed: Machine.Clock.Elapsed, completedTargets: Machine.PlayerMovesMade,
                mistakes: Machine.Mistakes, hintUsed: Machine.HintUsed, detail: Detail(result != null && result.Pass));

        string Detail(bool solved)
        {
            if (solved)
                return $"Thời gian {ChessFinalHud.FormatClock(Machine.Clock.Elapsed)} · " +
                       $"Sai: {Machine.Mistakes}/{Machine.MaxMistakes}" + (Machine.HintUsed ? " · Có dùng gợi ý" : string.Empty);
            return Machine.FailReason == ChessFailReason.TimeUp ? "Hết giờ" : $"Sai quá {Machine.MaxMistakes} lần";
        }

        void RefreshInput()
        {
            if (board == null || promotion == null) return;
            bool open = Machine.CanMove && !promotion.IsOpen;
            board.SetInteractable(open, Machine.Puzzle.PlayerColor,
                open ? MoveGenerator.LegalMoves(Machine.Position) : null);
        }

        static string CountWord(int n) => n switch { 1 => "một", 2 => "hai", 3 => "ba", _ => n.ToString(CultureInfo.InvariantCulture) };
    }
}
