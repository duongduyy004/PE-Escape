using System.Linq;
using KMA.Gameplay;
using KMA.Gameplay.Celebration;
using KMA.Gameplay.Chess;
using KMA.Gameplay.FrogJump;
using KMA.Gameplay.UI;
using UnityEngine;

namespace KMA.EditorTools
{
    /// QA-only: puts a scene into a frog-jump-penalty state for a screenshot through the same public
    /// seams production uses (ResultPanel.ShowFrogJump/ShowChallenge, FrogJumpRules.Stop, GameSession).
    /// Never runs outside a PlayModeScreenshot capture.
    ///   frog-fall          MG_FrogJump: stop the needle on a red end shortly before the capture
    ///   frog-win-keep      frog result card, reached the finish with lives left (THI LẠI)
    ///   frog-lose-zero     frog result card, timed out with 0 lives (VỀ BẢN ĐỒ)
    ///   exam-fail-1        challenge result panel, first failure (saves the life)
    ///   exam-fail-2        challenge result panel, second failure (life already lost)
    ///   map-lives-3        Map with 3 lives (regen countdown running)
    ///   map-lives-0        Map with 0 lives
    ///   map-chess-locked   Map with the course done through soccer_practice (final stop locked)
    ///   map-chess-open     Map with the course done through soccer_exam (final exam is the checkpoint)
    ///   map-complete       Map with the whole course done (summary and replay button)
    ///   map-lessons        Map with the Sprint stop tapped, so the HỌC/LUYỆN/THI popup is open
    ///   chess-select       MG_ChessFinal: start the attempt and select the piece of the optimal first move
    ///   chess-wrong        MG_ChessFinal: one suboptimal move played (Nước: 1/3)
    ///   chess-analysis-error MG_ChessFinal: the analysis-error card after a forced Unknown result
    ///   chess-boss         MG_ChessFinal: first correct move played, boss turn
    ///   chess-hint         MG_ChessFinal: hint level 2 shown
    ///   chess-promotion    MG_ChessFinal: promotion picker opened directly (the demo puzzle has no promotion)
    ///   chess-win          MG_ChessFinal: optimal line played, result panel (use a long wait)
    ///   chess-timeout      MG_ChessFinal: clock ticked out, result panel (use a long wait)
    ///   celebration-cheer  Celebration: wait until the timeline reaches 3 s (cheer beat)
    ///   celebration-teacher Celebration: wait until the timeline reaches 6 s (teacher and bubble)
    ///   celebration-skip   Celebration: press Skip and wait for the summary to fade in
    public static class PlayModeScreenshotQaStates
    {
        public static bool CaptureSoonAfterApply { get; private set; }
        public static float CaptureDelaySeconds {get;private set;}=.5f;

        public static bool IsKnown(string state) => DialogueStates.Contains(state) || state == "" || state == "frog-fall" ||
            state == "frog-win-keep" || state == "frog-lose-zero" || state == "exam-fail-1" ||
            state == "exam-fail-2" || state == "map-lives-3" || state == "map-lives-0" || state == "chess-select" ||
            state == "chess-wrong" || state == "chess-analysis-error" || state == "chess-boss" || state == "chess-hint" ||
            state == "chess-promotion" || state == "chess-win" || state == "chess-timeout" ||
            state == "map-chess-locked" || state == "map-chess-open" || state == "map-complete" || state == "map-lessons" ||
            state == "celebration-cheer" || state == "celebration-teacher" || state == "celebration-skip";

        static readonly System.Collections.Generic.HashSet<string> DialogueStates=new System.Collections.Generic.HashSet<string>{"frog-first-fall","frog-first-command","frog-repeat-fall","frog-charged-result","chess-student-speech","sprint-lowtime","volley-lostpoint","football-lastkick","gameover-comment","gameover-teacher"};
        static bool dialoguePrepared;
        static float firstFallAt;
        static bool volleyServePrepared;
        static bool PrepareDialogue(string state)
        {
            var guide=MinigameGuideHost.Current?.Panel;while(guide!=null && guide.IsOpen)guide.PressPrimary();
            if(dialoguePrepared)return true;
            var game=Object.FindFirstObjectByType<MinigameBase>();if(game==null){dialoguePrepared=true;return true;}
            var host=Object.FindFirstObjectByType<GameplayDialogueHost>();var opening=Object.FindFirstObjectByType<GameplayOpeningFlow>();
            string id=state=="sprint-lowtime"?"sprint_exam":state=="volley-lostpoint"?"volleyball_exam":state=="football-lastkick"?"soccer_exam":null;
            if(id!=null)
            {
                var definition=ChallengeCatalog.LoadDefault().Get(id);var context=new ChallengeAttemptContext("qa-dialogue",id,ChallengeAttemptMode.Journey,definition.Difficulty);
                ((IChallengeController)game).ConfigureChallenge(definition,context);
                if(game is SprintController sp)sp.GetComponent<SprintDialogueAdapter>().Configure(sp,host,opening);
                if(game is KMA.Gameplay.Volleyball.VolleyballController vb)vb.GetComponent<KMA.Gameplay.Volleyball.VolleyballDialogueAdapter>().Configure(vb,host,opening);
                if(game is FootballController fb)fb.GetComponent<FootballDialogueAdapter>().Configure(fb,host,opening);
            }
            dialoguePrepared=true;return true;
        }
        static bool DialogueState(string state)
        {
            if(!PrepareDialogue(state))return false;
            var host=Object.FindFirstObjectByType<GameplayDialogueHost>();
            if(state=="gameover-teacher" || state=="gameover-comment")
            {
                bool ready=host!=null && !host.IsIdle && (state=="gameover-comment" || Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).Any(t=>t.name=="SpeakerName" && t.text=="Giảng viên thể chất"));
                return ready;
            }
            if(state=="frog-charged-result")
            {
                var frog=Object.FindFirstObjectByType<FrogJumpController>();if(frog==null || frog.PresentationPhase==MinigamePhase.Tutorial || frog.PresentationPhase==MinigamePhase.Countdown)return false;
                if(!frog.Rules.IsOver){frog.Rules.Tick(frog.Rules.TimeRemaining+.1f);return false;}
                var panel=Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);if(panel==null || frog.PresentationPhase!=MinigamePhase.Resolve)return false;
                panel.ShowFrogJump(new FrogJumpResultView(false,false,0,null));return true;
            }
            if(state=="chess-student-speech")
            {
                var chess=ReadyChess();return chess!=null && chess.Machine.Phase==ChessAttemptPhase.PlayerTurn && !host.IsIdle;
            }
            var game=Object.FindFirstObjectByType<MinigameBase>();
            if(game is FootballController football)football.BeginMatch(FootballDifficulty.Normal);
            if(game==null || game.PresentationPhase!=MinigamePhase.Play)return false;
            if(state=="frog-first-fall" || state=="frog-first-command")return FrogFall();
            if(state=="frog-repeat-fall")
            {
                var frog=game as FrogJumpController;
                if(frog.Rules.Falls==0){if(FrogFall())firstFallAt=Time.time;return false;}
                if(Time.time-firstFallAt<10.1f || frog.Rules.State!=FrogJumpState.Aiming)return false;
                return FrogFall();
            }
            if(state=="sprint-lowtime")
            {
                var sprint=game as SprintController;return sprint.Snapshot.Elapsed>=10 && !host.IsIdle;
            }
            if(state=="volley-lostpoint")
            {
                var volley=game as KMA.Gameplay.Volleyball.VolleyballController;
                if(!volleyServePrepared){volleyServePrepared=true;volley.Match.ForceServerForTest(KMA.Gameplay.Volleyball.CourtSide.Player);volley.Match.ResetRally(KMA.Gameplay.Volleyball.CourtSide.Player);}
                return !host.IsIdle;
            }
            if(state=="football-lastkick")
            {
                var footballGame=game as FootballController;var rules=footballGame.Rules;
                if(rules.Kicks<4 && rules.State==FootballState.Aiming){rules.SetAim(.9f);rules.BeginCharge();}
                else if(rules.Kicks<4 && rules.State==FootballState.Charging){rules.Tick(.1f);rules.ReleaseShot();}
                else if(rules.State==FootballState.Kicking)rules.Tick(.2f);
                else if(rules.State==FootballState.Flying)rules.Tick(5f);
                else if(rules.State==FootballState.ShotResult)rules.Tick(1f);
                return rules.Kicks==4 && rules.State==FootballState.Aiming && !host.IsIdle;
            }
            return false;
        }

        /// Called every editor tick while a capture is active. Returns true once the state is applied.
        public static bool Apply(string state, float secondsToCapture)
        {
            CaptureSoonAfterApply = false;CaptureDelaySeconds=state.StartsWith("gameover-")?0f:state=="frog-first-command"?1.0f:.5f;
            if(DialogueStates.Contains(state))return CaptureSoonAfterApply=DialogueState(state);
            switch (state)
            {
                case "frog-fall": return CaptureSoonAfterApply = FrogFall();
                case "frog-win-keep": return ShowFrog(new FrogJumpResultView(true, true, 3, null));
                case "frog-lose-zero": return ShowFrog(new FrogJumpResultView(false, true, 0, null));
                case "exam-fail-1": return ShowExamFailure(3, true);
                case "exam-fail-2": return ShowExamFailure(2, false);
                case "map-lives-3": return SetMapLives(3);
                case "map-lives-0": return SetMapLives(0);
                case "map-chess-locked": return SetMapProgress("soccer_practice");
                case "map-chess-open": return SetMapProgress("soccer_exam");
                case "map-complete": return SetMapProgress("chess_final");
                case "map-lessons": return OpenMapLessons();
                case "chess-select": return CaptureSoonAfterApply = ChessSelect();
                case "chess-wrong": return CaptureSoonAfterApply = ChessWrong();
                case "chess-analysis-error": return CaptureSoonAfterApply = ChessAnalysisError();
                case "chess-boss": return CaptureSoonAfterApply = ChessBoss();
                case "chess-hint": return ChessHint();
                case "chess-promotion": return ChessPromotion();
                case "chess-win": return CaptureSoonAfterApply = ChessWin();
                case "chess-timeout": return CaptureSoonAfterApply = ChessTimeout();
                case "celebration-cheer": return CaptureSoonAfterApply = CelebrationAt(3f);
                case "celebration-teacher": return CaptureSoonAfterApply = CelebrationAt(6f);
                case "celebration-skip": return CaptureSoonAfterApply = CelebrationSkip();
                default: return true;
            }
        }

        static bool FrogFall()
        {
            var controller = Object.FindFirstObjectByType<FrogJumpController>();
            if (controller == null || controller.Rules == null) return false;
            if (controller.PresentationPhase != MinigamePhase.Play) return false;
            float needle = controller.Rules.Needle01;
            if (!FrogJumpRules.IsFall(needle, controller.Rules.Tuning)) return false;
            return controller.Rules.Stop() || controller.Rules.State != FrogJumpState.Aiming;
        }

        static bool ChessSelect()
        {
            ReadyChess();
            var controller = Object.FindFirstObjectByType<ChessFinalController>();
            var board = Object.FindFirstObjectByType<ChessBoardView>();
            if (controller == null || board == null || controller.Machine == null) return false;
            if (controller.Machine.Phase == ChessAttemptPhase.Intro) controller.BeginAttempt();
            if (!board.Interactable) return false;
            ChessMove move = OptimalMove(controller.Machine);
            board.ClickSquare(move.From);
            return board.SelectedSquare == move.From;
        }

        static ChessFinalController ReadyChess()
        {
            var guide = MinigameGuideHost.Current?.Panel;
            while (guide != null && guide.IsOpen) guide.PressPrimary();
            var controller = Object.FindFirstObjectByType<ChessFinalController>();
            if (controller == null || controller.Machine == null) return null;
            if (controller.Machine.Phase == ChessAttemptPhase.Intro) controller.BeginAttempt();
            return controller;
        }

        /// The first optimal move of the live board, from the same builder the level-3 hint uses.
        static ChessMove OptimalMove(ChessAttemptMachine machine) => ChessHintBuilder.Build(
            new ChessSearchEngine(SearchBudget.Default()),
            new HintRequest { Id = 0, Fen = machine.Position.ToFen(), MovesLeft = machine.MaxMoves - machine.MovesUsed, Level = 3 }).Move;

        /// A legal, non-mating move that loses quality, so the captures show a real suboptimal move.
        static ChessMove SuboptimalMove(ChessAttemptMachine machine)
        {
            var engine = new ChessSearchEngine(SearchBudget.Default());
            int movesLeft = machine.MaxMoves - machine.MovesUsed;
            ChessMove fallback = default;
            foreach (ChessMove move in MoveGenerator.LegalMoves(machine.Position))
            {
                ChessPosition after = machine.Position.Apply(move);
                if (MoveGenerator.IsCheckmate(after) || MoveGenerator.LegalMoves(after).Count == 0) continue;
                fallback = move;
                if (ChessAttemptGrader.Evaluate(engine, machine.Position, move, movesLeft).Q < .999) return move;
            }
            return fallback;
        }

        static void PlayOptimalMove(ChessFinalController controller)
        {
            ChessMove move = OptimalMove(controller.Machine);
            controller.RequestMove(move.From, move.To);
        }

        static double finishSeenAt;

        // The standalone scene has no SceneRouter, so show the card the router would: the final-mode challenge card.
        // Waits (wall clock) past the controller's result delay so the card replaces the phase banner as in play.
        static bool ShowChessResult(ChessFinalController controller, bool solved)
        {
            if (finishSeenAt == 0) finishSeenAt = UnityEditor.EditorApplication.timeSinceStartup;
            if (UnityEditor.EditorApplication.timeSinceStartup - finishSeenAt < 2.5) return false;
            var panel = Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
            if (panel == null) return false;
            ChallengeDefinition definition = new GameSession().Journey.Catalog.Get("chess_final");
            var context = new ChallengeAttemptContext("qa", "chess_final", ChallengeAttemptMode.Journey,
                ChallengeDifficulty.Normal);
            MinigameResult mini = controller.BuildResult(solved);
            var result = new ChallengeAttemptResult(context, solved, controller.BuildMetrics(definition, mini), mini);
            var outcome = new JourneyCommitOutcome(true, null, 0, false, false, solved, true);
            panel.ShowChallenge(context, result, outcome, null);
            return true;
        }

        static bool ChessWrong()
        {
            var controller = ReadyChess();
            if (controller == null) return false;
            ChessAttemptMachine machine = controller.Machine;
            if (machine.Phase == ChessAttemptPhase.PlayerTurn && machine.MovesUsed == 0)
            {
                ChessMove move = SuboptimalMove(machine);
                controller.RequestMove(move.From, move.To);
            }
            return machine.MovesUsed == 1 && machine.Phase != ChessAttemptPhase.Analyzing;
        }

        static bool ChessAnalysisError()
        {
            var controller = ReadyChess();
            if (controller == null) return false;
            ChessAttemptMachine machine = controller.Machine;
            if (machine.Phase == ChessAttemptPhase.PlayerTurn && machine.MovesUsed == 0)
            {
                ChessMove move = OptimalMove(machine);
                controller.RequestMove(move.From, move.To);
                // The worker result is applied on the next Update, so a failed analysis can be forced before it.
                machine.CompleteAnalysis(new AnalysisResult
                {
                    Id = machine.PendingAnalysis.Id, Status = SearchStatus.Unknown,
                    Quality = new MoveQuality(SearchStatus.Unknown, 0, 0, 0)
                });
            }
            return machine.Phase == ChessAttemptPhase.AnalysisError;
        }

        static bool ChessBoss()
        {
            var controller = ReadyChess();
            if (controller == null) return false;
            if (controller.Machine.Phase == ChessAttemptPhase.PlayerTurn && controller.Machine.MovesUsed == 0)
                PlayOptimalMove(controller);
            return controller.Machine.Phase == ChessAttemptPhase.BossTurn;
        }

        static bool ChessHint()
        {
            var controller = ReadyChess();
            if (controller == null || controller.Machine.Phase != ChessAttemptPhase.PlayerTurn) return false;
            if (controller.Machine.HintLevel < 2) controller.RevealHint();
            return controller.Machine.HintLevel == 2;
        }

        static bool ChessPromotion()
        {
            var controller = ReadyChess();
            var picker = Object.FindFirstObjectByType<PromotionPicker>(FindObjectsInactive.Include);
            if (controller == null || picker == null) return false;
            if (!picker.IsOpen) picker.Open(_ => { });
            return picker.IsOpen;
        }

        static bool ChessWin()
        {
            var controller = ReadyChess();
            if (controller == null) return false;
            if (controller.Machine.Phase == ChessAttemptPhase.PlayerTurn) PlayOptimalMove(controller);
            return controller.Machine.Phase == ChessAttemptPhase.Completed && ShowChessResult(controller, true);
        }

        static bool ChessTimeout()
        {
            var controller = ReadyChess();
            if (controller == null) return false;
            ChessAttemptMachine machine = controller.Machine;
            if (machine.Phase == ChessAttemptPhase.PlayerTurn) machine.Tick(machine.Clock.Limit);
            return machine.Phase == ChessAttemptPhase.Failed && ShowChessResult(controller, false);
        }

        // Game time lags wall time in a background Editor, so the beats are reached on the timeline clock.
        static bool CelebrationAt(float seconds)
        {
            var controller = Object.FindFirstObjectByType<CelebrationSceneController>();
            return controller != null && controller.Timeline.Time >= seconds;
        }

        static bool CelebrationSkip()
        {
            var controller = Object.FindFirstObjectByType<CelebrationSceneController>();
            if (controller == null || controller.Summary == null) return false;
            controller.Skip();
            return controller.SummaryVisible;
        }

        static bool ShowFrog(FrogJumpResultView view)
        {
            var panel = Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
            if (panel == null) return false;
            panel.ShowFrogJump(view);
            return true;
        }

        static bool ShowExamFailure(int attemptsRemaining, bool savesLife)
        {
            var panel = Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
            if (panel == null) return false;
            var context = new ChallengeAttemptContext("qa", "sprint_exam", ChallengeAttemptMode.Journey,
                ChallengeDifficulty.Normal);
            var result = new ChallengeAttemptResult(context, false, new ChallengeMetrics(),
                new MinigameResult(false, 0f, Rank.F));
            var outcome = new JourneyCommitOutcome(true, null, attemptsRemaining, true, savesLife, false);
            panel.ShowChallenge(context, result, outcome, null);
            return true;
        }

        static bool SetMapLives(int lives)
        {
            var map = Object.FindFirstObjectByType<MapScreen>(FindObjectsInactive.Include);
            if (map == null || map.Hearts == null) return false;
            // A throwaway session (never persisted) with sprint learn+practice done, so the exam card is the checkpoint.
            var session = new GameSession();
            foreach (string id in new[] { "sprint_learn", "sprint_practice" })
            {
                ChallengeDefinition definition = session.Journey.Catalog.Get(id);
                if (!session.TryStartChallenge(id, ChallengeAttemptMode.Journey, definition.Difficulty, out var context))
                    continue;
                session.SubmitChallengeResult(new ChallengeAttemptResult(context, true, new ChallengeMetrics()));
            }
            session.Journey.SetAttemptsRemaining(lives);
            session.RefreshLives();
            map.BindPresentation(map.Nodes, map.Hearts, session, map.LessonList);
            return true;
        }

        static bool OpenMapLessons()
        {
            var map = Object.FindFirstObjectByType<MapScreen>(FindObjectsInactive.Include);
            if (map == null || map.LessonList == null) return false;
            map.SelectSubject(SubjectId.Sprint);
            return true;
        }

        // A throwaway session (never persisted) with every challenge passed up to and including lastId.
        static bool SetMapProgress(string lastId)
        {
            var map = Object.FindFirstObjectByType<MapScreen>(FindObjectsInactive.Include);
            if (map == null || map.Hearts == null) return false;
            var session = new GameSession();
            foreach (ChallengeDefinition definition in session.Journey.Catalog.Ordered)
            {
                if (session.TryStartChallenge(definition.Id, ChallengeAttemptMode.Journey, definition.Difficulty,
                        out var context))
                    session.SubmitChallengeResult(new ChallengeAttemptResult(context, true,
                        new ChallengeMetrics(completedTargets: definition.TargetCount),
                        ChallengeDefinition.IsScored(definition.Kind) ? new MinigameResult(true, 8f, Rank.A) : null));
                if (definition.Id == lastId) break;
            }
            session.RefreshLives();
            map.BindPresentation(map.Nodes, map.Hearts, session, map.LessonList);
            return true;
        }
    }
}
