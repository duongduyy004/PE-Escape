using System;
using System.Collections.Generic;
using KMA.Gameplay.UI;
using UnityEngine;

namespace KMA.Gameplay.Core
{
    public sealed partial class SceneRouter
    {
        readonly Dictionary<IChallengeController, Action<ChallengeAttemptResult>> challengeHandlers =
            new Dictionary<IChallengeController, Action<ChallengeAttemptResult>>();
        JourneySaveCoordinator journeyCoordinator;
        JourneyPersistHandler journeyPersist;
        IChallengeResultPanel challengePanel;
        Action<JourneyResultAction> challengePanelHandler;
        ChallengeAttemptContext displayedChallenge;
        ChallengeAttemptResult displayedResult;
        JourneyCommitOutcome? displayedOutcome;
        string displayedSaveError;
        GameSession journeyMapIntentSession;
        string journeyMapIntentChallengeId;
        SaveData journeyContinuationRollback;
        bool journeyContinuationLoadPending;
        bool routingJourneyContinuation;
        SaveData pendingChallengeRouteRollback;
        string pendingChallengeRouteScene;
        SubjectId? pendingChallengeRoutePreviousSubject;
        bool pendingChallengeRoutePreviousAwaiting;

        public void ConfigureJourneyPersistence(JourneyPersistHandler persist)
        {
            journeyPersist = persist ?? throw new ArgumentNullException(nameof(persist));
            journeyCoordinator = new JourneySaveCoordinator(session, journeyPersist);
            SceneLoadFailed -= OnJourneyRouteFailed;
            SceneLoadFailed += OnJourneyRouteFailed;
        }

        /// With a frog jump still owed, every start is refused by the journey; send the player
        /// to the frog jump instead so a map reached in that state is never a dead end.
        public bool TryStartChallenge(string id, ChallengeAttemptMode mode = ChallengeAttemptMode.Journey,
            ChallengeDifficulty difficulty = ChallengeDifficulty.Normal)
            => session.PendingFrogJump != null ? StartFrogJump() : TryStartChallenge(id, mode, difficulty, null);

        bool TryStartChallenge(string id, ChallengeAttemptMode mode, ChallengeDifficulty difficulty,
            SaveData restartSnapshot)
        {
            lastRouteError = null;
            if (IsTransitioning || session.ActiveSubject.HasValue)
                return false;
            ChallengeDefinition definition;
            try { definition = session.Journey.Catalog.Get(id); }
            catch (Exception) { return false; }
            if (!TryGetSceneName(SessionRoute.Subject, definition.Subject, out string sceneName))
                return false;

            SaveData snapshot = restartSnapshot ?? session.ToSaveData();
            if (!session.TryStartChallenge(id, mode, difficulty, out _))
                return false;
            try
            {
                if (journeyPersist != null && !journeyPersist(out lastRouteError))
                {
                    session.RestoreSnapshot(snapshot);
                    return false;
                }
            }
            catch (Exception exception)
            {
                lastRouteError = exception.Message;
                session.RestoreSnapshot(snapshot);
                return false;
            }

            SubjectId? previousSubject = activeSubject;
            bool previousAwaitingScene = awaitingSubjectScene;
            pendingChallengeRouteRollback = snapshot;
            pendingChallengeRouteScene = sceneName;
            pendingChallengeRoutePreviousSubject = previousSubject;
            pendingChallengeRoutePreviousAwaiting = previousAwaitingScene;
            PrepareSceneBinding(SessionRoute.Subject, definition.Subject);
            try
            {
                if (transitioner.TryRoute(SessionRoute.Subject, definition.Subject, sceneName))
                {
                    SessionChanged?.Invoke();
                    return true;
                }
            }
            catch (Exception exception)
            {
                lastRouteError = exception.Message;
            }

            // A rejected load must leave the displayed result or paused attempt usable.
            // Persist the rollback too: the new attempt was saved before routing.
            session.RestoreSnapshot(snapshot);
            activeSubject = previousSubject;
            awaitingSubjectScene = previousAwaitingScene;
            ClearPendingChallengeRouteRollback();
            if (journeyPersist != null)
            {
                try
                {
                    if (!journeyPersist(out string rollbackError))
                        lastRouteError = rollbackError ?? lastRouteError;
                }
                catch (Exception exception) { lastRouteError = exception.Message; }
            }
            return false;
        }

        public bool RetryActiveChallenge()
        {
            ChallengeAttemptContext context = session.Journey.ActiveAttempt;
            if (context == null || IsTransitioning || !TryGetSceneName(SessionRoute.Subject,
                session.ActiveSubject, out string sceneName))
                return false;
            PrepareSceneBinding(SessionRoute.Subject, session.ActiveSubject);
            return transitioner.TryRoute(SessionRoute.Subject, session.ActiveSubject, sceneName);
        }

        void ArmJourneyMapContinuation(string challengeId)
        {
            journeyMapIntentSession = session;
            journeyMapIntentChallengeId = challengeId;
            journeyContinuationRollback = null;
            journeyContinuationLoadPending = false;
        }

        bool RouteToMapForJourney(string challengeId)
        {
            ArmJourneyMapContinuation(challengeId);
            routingJourneyContinuation = true;
            try { return Route(SessionRoute.Map); }
            finally { routingJourneyContinuation = false; }
        }

        public bool CompleteJourneyMapDialogue(GameSession sourceSession)
        {
            if (IsTransitioning)
                return false;
            if (sourceSession == null || journeyMapIntentSession != sourceSession || sourceSession != session ||
                string.IsNullOrEmpty(journeyMapIntentChallengeId))
                return false;
            if (sourceSession.Journey.ActiveAttempt != null || sourceSession.PendingFrogJump != null ||
                !string.Equals(sourceSession.Journey.CheckpointChallengeId, journeyMapIntentChallengeId,
                    StringComparison.Ordinal))
            {
                ClearJourneyMapContinuation();
                return false;
            }

            ChallengeDefinition definition;
            try { definition = sourceSession.Journey.Catalog.Get(journeyMapIntentChallengeId); }
            catch (Exception) { ClearJourneyMapContinuation(); return false; }

            UnbindFrogPanel();
            journeyContinuationRollback = sourceSession.ToSaveData();
            journeyContinuationLoadPending = true;
            if (TryStartChallenge(journeyMapIntentChallengeId, ChallengeAttemptMode.Journey,
                definition.Difficulty))
                return true;

            journeyContinuationLoadPending = false;
            journeyContinuationRollback = null;
            return false;
        }

        internal void CancelJourneyMapContinuation() => ClearJourneyMapContinuation();

        public bool HasPendingJourneyMapContinuation => journeyMapIntentSession == session &&
            !string.IsNullOrEmpty(journeyMapIntentChallengeId);

        void ClearJourneyMapContinuation()
        {
            journeyMapIntentSession = null;
            journeyMapIntentChallengeId = null;
            journeyContinuationRollback = null;
            journeyContinuationLoadPending = false;
        }

        void ClearPendingChallengeRouteRollback()
        {
            pendingChallengeRouteRollback = null;
            pendingChallengeRouteScene = null;
            pendingChallengeRoutePreviousSubject = null;
            pendingChallengeRoutePreviousAwaiting = false;
        }

        internal void ReportChallengeResultForTests(ChallengeAttemptResult result) =>
            PreviewChallengeResult(session.Journey.ActiveAttempt, result);

        void BindChallenge(IChallengeController controller)
        {
            if (controller == null || challengeHandlers.ContainsKey(controller))
                return;
            ChallengeAttemptContext context = session.Journey.ActiveAttempt;
            if (context == null || (!(controller is JourneyControllerAdapter) &&
                session.Journey.Catalog.Get(context.ChallengeId).Subject != controller.Subject))
                return;
            ChallengeDefinition definition = session.Journey.Catalog.Get(context.ChallengeId);
            controller.ConfigureChallenge(definition, context);
            Action<ChallengeAttemptResult> handler = result => PreviewChallengeResult(context, result);
            challengeHandlers.Add(controller, handler);
            controller.ChallengeCompleted += handler;
        }

        void PreviewChallengeResult(ChallengeAttemptContext context, ChallengeAttemptResult result)
        {
            if (result == null || context.AttemptId != result.Context.AttemptId)
                return;
            UnbindChallengePanel();
            challengePanel = FindChallengePanel();
            if (challengePanel == null)
                throw new InvalidOperationException("A challenge result panel is required.");
            displayedChallenge = context;
            displayedResult = result;
            displayedOutcome = null;
            displayedSaveError = null;
            journeyCoordinator ??= new JourneySaveCoordinator(session, (out string error) =>
            {
                error = null;
                return true;
            });
            if (journeyCoordinator.TryCommit(result, out JourneyCommitOutcome committed, out string saveError))
            {
                displayedOutcome = committed;
                SessionChanged?.Invoke();
            }
            else
                displayedSaveError = saveError;
            challengePanelHandler = HandleChallengeAction;
            challengePanel.JourneyActionRequested += challengePanelHandler;
            challengePanel.ShowChallenge(context, result, displayedOutcome, displayedSaveError);
        }

        void HandleChallengeAction(JourneyResultAction action)
        {
            if (displayedResult == null || journeyCoordinator == null)
                return;
            if (action == JourneyResultAction.RetrySave)
            {
                if (!journeyCoordinator.RetrySave(out JourneyCommitOutcome retried, out string retryError))
                {
                    displayedSaveError = retryError;
                    challengePanel?.ShowChallenge(displayedChallenge, displayedResult, null, retryError);
                    return;
                }
                displayedOutcome = retried;
                // Once saved, a failure that owes a frog jump goes there instead of the map.
                if (retried.FrogJumpRequired)
                    action = JourneyResultAction.FrogJump;
                else
                    action = JourneyResultAction.Continue;
            }
            else if (!displayedOutcome.HasValue)
                return;

            if (displayedOutcome.HasValue && displayedOutcome.Value.FrogJumpRequired &&
                (action == JourneyResultAction.Continue || action == JourneyResultAction.Retry))
                action = JourneyResultAction.FrogJump;

            if (action == JourneyResultAction.Retry ||
                (action == JourneyResultAction.Continue && displayedChallenge.Mode == ChallengeAttemptMode.Journey &&
                    !displayedResult.Pass && !displayedOutcome.Value.FrogJumpRequired))
            {
                ChallengeRetryIntent.Arm(displayedChallenge.ChallengeId);
                if (!TryStartChallenge(displayedChallenge.ChallengeId, displayedChallenge.Mode,
                    displayedChallenge.Difficulty))
                {
                    ChallengeRetryIntent.Clear();
                    RestoreChallengeActions();
                }
            }
            else if (action == JourneyResultAction.FrogJump)
            {
                if (!StartFrogJump()) RestoreChallengeActions();
            }
            else
            {
                // Journey wins continue from the committed checkpoint after Map dialogue. Review
                // and free-play results return to Map without changing the checkpoint.
                bool journeyWin = displayedChallenge.Mode == ChallengeAttemptMode.Journey &&
                    displayedResult.Pass && displayedOutcome.HasValue;
                bool celebrate = journeyWin && displayedOutcome.Value.FinalChallenge &&
                    displayedOutcome.Value.CourseComplete && !session.Journey.CelebrationSeen;
                string nextChallenge = journeyWin ? displayedOutcome.Value.NextChallengeId : null;
                bool routed = celebrate ? Route(SessionRoute.Celebration)
                    : journeyWin && !string.IsNullOrEmpty(nextChallenge) &&
                        string.Equals(nextChallenge, session.Journey.CheckpointChallengeId, StringComparison.Ordinal)
                        ? RouteToMapForJourney(nextChallenge)
                        : Route(SessionRoute.Map);
                if (!routed)
                    challengePanel?.ShowChallenge(displayedChallenge, displayedResult, displayedOutcome,
                        lastRouteError ?? "Không thể chuyển cảnh. Hãy thử lại.");
            }
        }

        void RestoreChallengeActions() => challengePanel?.ShowChallenge(displayedChallenge,
            displayedResult, displayedOutcome, lastRouteError ?? "Không thể bắt đầu bài. Hãy thử lại.");

        void OnJourneyRouteFailed(string message)
        {
            if (pendingChallengeRouteRollback != null)
            {
                session.RestoreSnapshot(pendingChallengeRouteRollback);
                activeSubject = pendingChallengeRoutePreviousSubject;
                awaitingSubjectScene = pendingChallengeRoutePreviousAwaiting;
                UnbindJourneyControllers();
                if (journeyPersist != null)
                {
                    try { journeyPersist(out _); }
                    catch (Exception exception) { Debug.LogException(exception, this); }
                }
                journeyContinuationLoadPending = false;
                journeyContinuationRollback = null;
                ClearPendingChallengeRouteRollback();
            }
            if (frogPanel != null && frogContinueUsed)
            {
                frogContinueUsed = false;
                ShowFrogError(message);
            }
            if (displayedOutcome.HasValue && displayedResult != null && challengePanel != null)
                challengePanel.ShowChallenge(displayedChallenge, displayedResult, displayedOutcome, message);
        }

        void BindJourneyControllers()
        {
            if (session.Journey.ActiveAttempt != null)
            {
                foreach (MinigameBase minigame in FindObjectsByType<MinigameBase>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var adapter = minigame.GetComponent<JourneyControllerAdapter>();
                    if (adapter == null) adapter = minigame.gameObject.AddComponent<JourneyControllerAdapter>();
                    RunBindingStep(() => BindChallenge(adapter));
                }
            }
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (behaviour is IChallengeController controller)
                    RunBindingStep(() => BindChallenge(controller));
        }

        void UnbindJourneyControllers()
        {
            foreach (var binding in challengeHandlers)
                binding.Key.ChallengeCompleted -= binding.Value;
            challengeHandlers.Clear();
        }

        bool RetryDisplayedChallenge() => displayedChallenge != null && TryStartChallenge(
            displayedChallenge.ChallengeId, displayedChallenge.Mode, displayedChallenge.Difficulty);

        void UnbindChallengePanel()
        {
            if (challengePanel != null && challengePanelHandler != null)
                challengePanel.JourneyActionRequested -= challengePanelHandler;
            challengePanel = null;
            challengePanelHandler = null;
        }

        static IChallengeResultPanel FindChallengePanel()
        {
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (behaviour is IChallengeResultPanel panel)
                    return panel;
            return null;
        }
    }
}
