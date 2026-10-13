using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KMA.Gameplay.Core
{
    /// The mandatory frog jump after a failed journey practice/exam: route to its scene, apply its
    /// result once, then retry the failed challenge (or return to the map with no lives left, or
    /// when the player chooses Quay về).
    public sealed partial class SceneRouter
    {
        [SerializeField] string frogJumpScene = "MG_FrogJump";

        MinigameBase boundFrogJump;
        Action<MinigameResult> frogJumpHandler;
        IFrogJumpResultPanel frogPanel;
        FrogJumpResultView frogView;
        string frogRetryChallengeId;
        bool frogSavePending;
        bool frogContinueUsed;
        bool frogRetryIsPractice;

        public string FrogJumpScene => frogJumpScene;

        public bool StartFrogJump()
        {
            lastRouteError = null;
            if (IsTransitioning || session.PendingFrogJump == null ||
                !TryGetSceneName(SessionRoute.FrogJump, null, out string sceneName))
                return false;
            PrepareSceneBinding(SessionRoute.FrogJump, null);
            if (!transitioner.TryRoute(SessionRoute.FrogJump, null, sceneName))
                return false;
            // The failed challenge's panel dies with its scene; a later load failure must not
            // reach for it.
            UnbindChallengePanel();
            displayedChallenge = null;
            displayedResult = null;
            displayedOutcome = null;
            displayedSaveError = null;
            SessionChanged?.Invoke();
            return true;
        }

        internal void CompleteFrogJumpForTests(bool reachedFinish)
        {
            if (session.PendingFrogJump != null)
                OnFrogJumpCompleted(session.PendingFrogJump.Id, new MinigameResult(reachedFinish, 0f,
                    reachedFinish ? Rank.C : Rank.F));
        }

        void BindFrogJump(Scene scene)
        {
            FrogJumpPending pending = session.PendingFrogJump;
            if (pending == null || !string.Equals(scene.name, frogJumpScene, StringComparison.Ordinal))
                return;
            MinigameBase minigame = FindFirstObjectByType<MinigameBase>(FindObjectsInactive.Exclude);
            if (minigame == null)
                return;
            string id = pending.Id;
            boundFrogJump = minigame;
            frogJumpHandler = result => OnFrogJumpCompleted(id, result);
            minigame.Completed += frogJumpHandler;
        }

        void UnbindFrogJump()
        {
            if (boundFrogJump != null && frogJumpHandler != null)
                boundFrogJump.Completed -= frogJumpHandler;
            boundFrogJump = null;
            frogJumpHandler = null;
        }

        void OnFrogJumpCompleted(string frogJumpId, MinigameResult result)
        {
            FrogJumpPending pending = session.PendingFrogJump;
            if (pending == null || pending.Id != frogJumpId || result == null)
                return;
            string retryId = pending.FailedChallengeId;
            bool savesLife = pending.SavesLife;
            if (!session.TryApplyFrogJump(frogJumpId, result.Pass))
                return;

            frogRetryChallengeId = retryId;
            frogRetryIsPractice = session.Journey.Catalog.Get(retryId).Kind == ChallengeKind.Practice;
            frogContinueUsed = false;
            frogSavePending = !TryPersistJourney(out string saveError);
            SessionChanged?.Invoke();

            UnbindFrogPanel();
            frogPanel = FindFrogJumpPanel() ??
                throw new InvalidOperationException("A frog jump result panel is required.");
            frogPanel.FrogJumpContinueRequested += OnFrogJumpContinue;
            frogPanel.FrogJumpBackRequested += OnFrogJumpBack;
            frogView = new FrogJumpResultView(result.Pass, savesLife, session.Lives, saveError,
                frogRetryIsPractice);
            frogPanel.ShowFrogJump(frogView);
        }

        void OnFrogJumpContinue()
        {
            if (frogPanel == null || frogContinueUsed || !FlushFrogSave())
                return;

            frogContinueUsed = true;
            bool routed;
            if (session.Lives > 0 && !string.IsNullOrEmpty(frogRetryChallengeId))
            {
                bool canRetryFromMap = !session.Journey.CourseComplete &&
                    string.Equals(session.Journey.CheckpointChallengeId, frogRetryChallengeId,
                        StringComparison.Ordinal);
                routed = canRetryFromMap
                    ? RouteToMapForJourney(frogRetryChallengeId)
                    : Route(SessionRoute.Map);
            }
            else
                routed = Route(SessionRoute.Map);

            if (!routed)
            {
                frogContinueUsed = false;
                ShowFrogError(lastRouteError ?? "Không thể chuyển cảnh. Hãy thử lại.");
            }
        }

        /// QUAY VỀ on the frog result: the map without the retry; the failed challenge stays the checkpoint.
        void OnFrogJumpBack()
        {
            if (frogPanel == null || frogContinueUsed || !FlushFrogSave())
                return;

            frogContinueUsed = true;
            if (!Route(SessionRoute.Map))
            {
                frogContinueUsed = false;
                ShowFrogError(lastRouteError ?? "Không thể chuyển cảnh. Hãy thử lại.");
            }
        }

        // The frog result must be saved before its scene is left.
        bool FlushFrogSave()
        {
            if (!frogSavePending)
                return true;
            if (!TryPersistJourney(out string saveError))
            {
                ShowFrogError(saveError);
                return false;
            }
            frogSavePending = false;
            return true;
        }

        void ShowFrogError(string error)
        {
            frogView = new FrogJumpResultView(frogView.ReachedFinish, frogView.SavesLife, session.Lives, error,
                frogView.RetryIsPractice);
            frogPanel?.ShowFrogJump(frogView);
        }

        bool TryPersistJourney(out string error)
        {
            error = null;
            if (journeyPersist == null)
                return true;
            try { return journeyPersist(out error); }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        void UnbindFrogPanel()
        {
            if (frogPanel != null)
            {
                frogPanel.FrogJumpContinueRequested -= OnFrogJumpContinue;
                frogPanel.FrogJumpBackRequested -= OnFrogJumpBack;
            }
            frogPanel = null;
        }

        static IFrogJumpResultPanel FindFrogJumpPanel()
        {
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (behaviour is IFrogJumpResultPanel panel)
                    return panel;
            return null;
        }
    }
}
