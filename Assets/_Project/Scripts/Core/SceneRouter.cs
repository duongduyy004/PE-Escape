using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: InternalsVisibleTo("KMA.Gameplay.UI.PlayMode.Tests")]
[assembly: InternalsVisibleTo("KMA.Gameplay.Progression.PlayMode.Tests")]

namespace KMA.Gameplay.Core
{
    public interface ISceneRouteTransitionSink
    {
        bool Begin(SceneRouteTransition transition, Action onCompleted);
    }

    public readonly struct SceneRouteTransition
    {
        public SceneRouteTransition(SessionRoute route, SubjectId? subject, GameSession session,
            string sceneName)
        {
            Route = route;
            Subject = subject;
            Session = session ?? throw new ArgumentNullException(nameof(session));
            SceneName = sceneName ?? string.Empty;
        }

        public SessionRoute Route { get; }
        public SubjectId? Subject { get; }
        public GameSession Session { get; }
        public string SceneName { get; }
    }

    public sealed class SessionRouteTransitioner
    {
        readonly GameSession session;
        readonly ISceneRouteTransitionSink sink;
        bool transitioning;

        public SessionRouteTransitioner(GameSession session, ISceneRouteTransitionSink sink)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public bool IsTransitioning => transitioning;

        public bool TryRoute(SessionRoute route, SubjectId? subject, string sceneName = null)
        {
            if (transitioning)
                return false;

            if (RequiresSubject(route) && !subject.HasValue)
                throw new ArgumentNullException(nameof(subject));
            transitioning = true;
            try
            {
                bool accepted = sink.Begin(
                    new SceneRouteTransition(route, subject, session, sceneName ?? route.ToString()),
                    CompleteTransition);
                if (!accepted)
                    transitioning = false;
                return accepted;
            }
            catch
            {
                transitioning = false;
                throw;
            }
        }

        void CompleteTransition() => transitioning = false;

        static bool RequiresSubject(SessionRoute route) => route == SessionRoute.Subject ||
            route == SessionRoute.RetrySubject;
    }

    [DefaultExecutionOrder(-1000)]
    public sealed partial class SceneRouter : MonoBehaviour, ISceneRouteTransitionSink
    {
        [Serializable]
        public struct SubjectScene
        {
            public SubjectId Subject;
            public string SceneName;
        }

        static SceneRouter instance;

        [SerializeField] string mapScene = "Map";
        [SerializeField] string gameOverScene = "GameOver";
        [SerializeField] SubjectScene[] subjectScenes = DefaultSubjectScenes();

        readonly Dictionary<MinigameBase, Action<MinigameResult>> subjectCompletionHandlers =
            new Dictionary<MinigameBase, Action<MinigameResult>>();
        IResultPreviewPanel pendingResultPanel;
        Action<string> pendingResultPanelHandler;
        SubjectId? activeSubject;
        SubjectId? pendingResultSubject;
        MinigameResult pendingSubjectResult;
        Func<SceneRouteTransition, Action, bool> routeAcceptanceForTests;
        string lastRouteError;
        bool awaitingSubjectScene;
        bool menuLoading;
        GameSession session;
        SessionRouteTransitioner transitioner;
        Func<string, AsyncOperation> sceneLoader;

        public event Action<SceneRouteTransition> TransitionStarted;
        public event Action SessionChanged;
        public event Action<SubjectId, MinigameResult> SubjectCompleted;
        public event Action<int> LifeLost;
        public event Action SceneLoadStarted;
        public event Action<float> SceneLoadProgressChanged;
        public event Action SceneLoadCompleted;
        public event Action<string> SceneLoadFailed;

        public static SceneRouter Instance => instance;
        public GameSession Session => session;
        public bool IsTransitioning => menuLoading || (transitioner != null && transitioner.IsTransitioning);

        public static SceneRouter EnsurePersistentInstance()
        {
            if (instance != null)
                return instance;

            var existing = FindFirstObjectByType<SceneRouter>();
            if (existing != null)
                return existing;

            return new GameObject(nameof(SceneRouter)).AddComponent<SceneRouter>();
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            session = new GameSession();
            transitioner = new SessionRouteTransitioner(session, this);
            sceneLoader ??= LoadSingleScene;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnbindSubjects();
            UnbindResultPanel();
            UnbindJourneyControllers();
            UnbindChallengePanel();
            UnbindFrogJump();
            UnbindFrogPanel();
        }

        public bool StartSubject(SubjectId subject)
        {
            if (IsTransitioning || session.ActiveSubject.HasValue)
                return false;
            string checkpoint = session.Journey.CheckpointChallengeId;
            if (!string.IsNullOrEmpty(checkpoint) &&
                session.Journey.Catalog.Get(checkpoint).Subject != subject)
                return false;
            if (string.IsNullOrEmpty(checkpoint) && !session.Journey.CourseComplete)
                return false;

            EnsureRouteIsConfigured(SessionRoute.Subject, subject);
            SaveData previous = session.ToSaveData();
            SessionRoute route = session.StartSubject(subject);
            if (!TryRouteMutatedSession(previous, route, subject))
                return false;
            if (route != SessionRoute.GameOver)
                SessionChanged?.Invoke();
            return true;
        }

        public bool ResumeCampaign()
        {
            if (IsTransitioning)
                return false;
            SessionRoute route = session.ResumeRoute();
            string checkpoint = route == SessionRoute.Map && !session.Journey.CourseComplete
                ? session.Journey.CheckpointChallengeId : null;
            return !string.IsNullOrEmpty(checkpoint)
                ? RouteToMapForJourney(checkpoint)
                : Route(route, session.ActiveSubject);
        }

        public bool ResetCampaign()
        {
            if (IsTransitioning)
                return false;

            EnsureRouteIsConfigured(SessionRoute.Map, null);
            SaveData previous = session.ToSaveData();
            session.ResetCampaign();
            ArmJourneyMapContinuation(session.Journey.CheckpointChallengeId);
            routingJourneyContinuation = true;
            bool routed;
            try { routed = TryRouteMutatedSession(previous, SessionRoute.Map, null); }
            finally { routingJourneyContinuation = false; }
            if (!routed)
            {
                ClearJourneyMapContinuation();
                return false;
            }
            SessionChanged?.Invoke();
            return true;
        }

        public void LoadSession(GameSession restoredSession)
        {
            if (restoredSession == null)
                throw new ArgumentNullException(nameof(restoredSession));

            UnbindSubjects();
            UnbindResultPanel();
            ClearJourneyMapContinuation();
            activeSubject = null;
            awaitingSubjectScene = false;
            session = restoredSession;
            transitioner = new SessionRouteTransitioner(session, this);
            if (journeyPersist != null)
                journeyCoordinator = new JourneySaveCoordinator(session, journeyPersist);
        }

        public bool SubmitSubjectResult(SubjectId subject, MinigameResult result) =>
            TrySubmitSubjectResult(subject, result, false);

        bool TrySubmitSubjectResult(SubjectId subject, MinigameResult result, bool retry)
        {
            if (IsTransitioning || result == null)
                return false;
            if (retry && (subject != SubjectId.Football || result.Pass))
                return false;

            int livesBefore = session.Lives;
            SaveData previous = session.ToSaveData();
            lastRouteError = null;
            SessionRoute route;
            try
            {
                route = session.SubmitResult(subject, result);
                if (retry && session.Lives > 0)
                    route = session.StartSubject(subject);
            }
            catch
            {
                session.RestoreSnapshot(previous);
                throw;
            }

            if (!TryRouteMutatedSession(previous, route, route == SessionRoute.Subject ? subject : null))
                return false;
            SessionChanged?.Invoke();

            if (result.Pass)
                SubjectCompleted?.Invoke(subject, result);
            else if (session.Lives < livesBefore)
                LifeLost?.Invoke(session.Lives);
            return true;
        }

        public bool RouteToMenu()
        {
            if (IsTransitioning)
                return false;
            if (!CanLoadScene("Menu"))
                return TryLoadScene("Menu");

            if (!TryLoadScene("Menu"))
                return false;

            UnbindSubjects();
            UnbindResultPanel();
            ClearJourneyMapContinuation();
            activeSubject = null;
            awaitingSubjectScene = false;
            return true;
        }

        public bool TryLoadScene(string sceneName)
        {
            if (IsTransitioning)
                return false;

            ClearJourneyMapContinuation();

            menuLoading = true;
            return StartSceneLoad(sceneName, () => menuLoading = false);
        }

        public bool RestartActiveSubject()
        {
            if (IsTransitioning)
                return false;
            if (!activeSubject.HasValue || !session.ActiveSubject.HasValue)
                return false;

            var subject = activeSubject.Value;
            EnsureRouteIsConfigured(SessionRoute.Subject, subject);
            SaveData previous = session.ToSaveData();
            ChallengeAttemptContext context = session.Journey.ActiveAttempt;
            if (context != null)
            {
                session.AbandonActiveChallenge();
                if (!TryStartChallenge(context.ChallengeId, context.Mode, context.Difficulty, previous))
                {
                    session.RestoreSnapshot(previous);
                    return false;
                }
                return true;
            }
            session.AbandonActiveSubject();
            SessionRoute route = session.StartSubject(subject);
            if (!TryRouteMutatedSession(previous, route, subject)) return false;
            SessionChanged?.Invoke();
            return true;
        }

        public bool ExitActiveSubjectToMap()
        {
            if (pendingResultPanel != null && pendingResultSubject == SubjectId.Football && pendingSubjectResult != null)
                return HandlePendingResultAction(ResultPanelActions.Continue);
            return Route(SessionRoute.Map);
        }

        public void BindSubject(MinigameBase controller, SubjectId subject)
        {
            if (controller == null)
                throw new ArgumentNullException(nameof(controller));
            if (session.Journey.ActiveAttempt != null)
            {
                var adapter = controller.GetComponent<JourneyControllerAdapter>() ??
                    controller.gameObject.AddComponent<JourneyControllerAdapter>();
                BindChallenge(adapter);
                return;
            }
            if (subjectCompletionHandlers.ContainsKey(controller))
                return;

            Action<MinigameResult> handler = result => PreviewSubjectResult(subject, result);
            subjectCompletionHandlers.Add(controller, handler);
            controller.Completed += handler;
        }

        void PreviewSubjectResult(SubjectId subject, MinigameResult result)
        {
            SessionRoute previewRoute = session.PreviewRoute(subject, result);
            var panel = FindResultPanel();
            if (panel == null)
                throw new InvalidOperationException("A ResultPanel is required to continue from a subject result.");

            UnbindResultPanel();
            if (subject == SubjectId.Football && panel is IRetryResultPreviewPanel retryPanel)
                retryPanel.ConfigureRetry(result.Pass ? session.Lives : Mathf.Max(0, session.Lives - 1));

            Action<string> handler = action => { HandlePendingResultAction(action); };

            pendingResultPanel = panel;
            pendingResultPanelHandler = handler;
            pendingResultSubject = subject;
            pendingSubjectResult = result;
            panel.ActionRequested += handler;
            panel.Show(result, previewRoute.ToString());
        }

        bool HandlePendingResultAction(string action)
        {
            if (pendingResultPanel == null || !pendingResultSubject.HasValue || pendingSubjectResult == null)
                return false;

            var subject = pendingResultSubject.Value;
            var result = pendingSubjectResult;
            bool retry = action == ResultPanelActions.Retry && subject == SubjectId.Football && !result.Pass &&
                pendingResultPanel is IRetryResultPreviewPanel retryPanel && retryPanel.RetryAvailable;
            if (action == ResultPanelActions.Retry && !retry)
                return false;

            var actionablePanel = pendingResultPanel as IRetryResultPreviewPanel;
            actionablePanel?.SetActionPending(true, null);
            try
            {
                bool committed = TrySubmitSubjectResult(subject, result, retry);
                if (committed)
                    UnbindResultPanel();
                else
                    actionablePanel?.SetActionPending(false, lastRouteError ?? "Không thể chuyển cảnh. Vui lòng thử lại.");
                return committed;
            }
            catch (Exception exception)
            {
                lastRouteError = exception.Message;
                actionablePanel?.SetActionPending(false, "Không thể chuyển cảnh. Vui lòng thử lại.");
                return false;
            }
        }

        public bool Route(SessionRoute route, SubjectId? subject = null)
        {
            if (IsTransitioning)
                return false;

            if (!routingJourneyContinuation)
                ClearJourneyMapContinuation();

            if (!TryGetSceneName(route, subject, out var sceneName))
                throw new InvalidOperationException($"No loadable scene is configured for {route}" +
                    (subject.HasValue ? $" ({subject.Value})." : "."));

            bool abandonActiveSubject = route == SessionRoute.Map && session.ActiveSubject.HasValue;
            SaveData routeSnapshot = abandonActiveSubject ? session.ToSaveData() : null;
            if (abandonActiveSubject)
            {
                session.AbandonActiveSubject();
                if (journeyPersist != null && !journeyPersist(out lastRouteError))
                {
                    session.RestoreSnapshot(routeSnapshot);
                    return false;
                }
            }
            if (!transitioner.TryRoute(route, subject, sceneName))
            {
                if (journeyPersist == null && routeSnapshot != null)
                    session.RestoreSnapshot(routeSnapshot);
                return false;
            }
            if (abandonActiveSubject)
                SessionChanged?.Invoke();
            PrepareSceneBinding(route, subject);
            return true;
        }

        public bool TryGetSceneName(SessionRoute route, SubjectId? subject, out string sceneName)
        {
            sceneName = route switch
            {
                SessionRoute.Map => mapScene,
                SessionRoute.GameOver => gameOverScene,
                SessionRoute.FrogJump => frogJumpScene,
                SessionRoute.Celebration => celebrationScene,
                SessionRoute.Subject or SessionRoute.RetrySubject => SceneFor(subject),
                _ => null
            };

            if (string.IsNullOrWhiteSpace(sceneName))
                return false;

            return CanLoadScene(sceneName);
        }

        public bool Begin(SceneRouteTransition transition, Action onCompleted)
        {
            TransitionStarted?.Invoke(transition);
            if (routeAcceptanceForTests != null)
                return routeAcceptanceForTests(transition, onCompleted);
            return StartSceneLoad(transition.SceneName, onCompleted);
        }

        internal void ConfigureSceneLoaderForTests(Func<string, AsyncOperation> loader)
        {
            routeAcceptanceForTests = null;
            sceneLoader = loader ?? LoadSingleScene;
        }

        internal void ConfigureRouteAcceptanceForTests(Func<SceneRouteTransition, Action, bool> accept)
        {
            routeAcceptanceForTests = accept;
            transitioner = new SessionRouteTransitioner(session, this);
        }

        // A throw here would skip every later sceneLoaded handler (the pause exit among them),
        // so each binding step logs its failure and lets the rest run.
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RunBindingStep(UnbindChallengePanel);
            RunBindingStep(UnbindFrogJump);
            if (!string.Equals(scene.name, frogJumpScene, StringComparison.Ordinal))
                RunBindingStep(UnbindFrogPanel);
            RunBindingStep(UnbindSubjects);
            RunBindingStep(() => BindSubjectScene(scene));
            RunBindingStep(BindJourneyControllers);
            RunBindingStep(() => BindFrogJump(scene));
            if (pendingChallengeRouteRollback != null &&
                string.Equals(scene.name, pendingChallengeRouteScene, StringComparison.Ordinal))
            {
                bool completedMapIntent = journeyContinuationLoadPending && journeyMapIntentSession == session;
                ClearPendingChallengeRouteRollback();
                if (completedMapIntent) ClearJourneyMapContinuation();
            }
        }

        void BindSubjectScene(Scene scene)
        {
            if (session.Journey.ActiveAttempt == null && awaitingSubjectScene && activeSubject.HasValue &&
                string.Equals(scene.name, SceneFor(activeSubject), StringComparison.Ordinal))
            {
                var boundController = false;
                foreach (var controller in FindObjectsByType<MinigameBase>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    BindSubject(controller, activeSubject.Value);
                    boundController = true;
                }
                awaitingSubjectScene = !boundController;
            }
        }

        void RunBindingStep(Action step)
        {
            try { step(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        void EnsureRouteIsConfigured(SessionRoute route, SubjectId? subject)
        {
            if (!TryGetSceneName(route, subject, out _))
            {
                throw new InvalidOperationException($"No loadable scene is configured for {route}" +
                    (subject.HasValue ? $" ({subject.Value})." : "."));
            }
        }

        void PrepareSceneBinding(SessionRoute route, SubjectId? subject)
        {
            switch (route)
            {
                case SessionRoute.Subject:
                case SessionRoute.RetrySubject:
                    activeSubject = subject;
                    awaitingSubjectScene = true;
                    break;
                case SessionRoute.Map:
                case SessionRoute.GameOver:
                case SessionRoute.FrogJump:
                case SessionRoute.Celebration:
                    activeSubject = null;
                    awaitingSubjectScene = false;
                    break;
            }
        }

        void UnbindSubjects()
        {
            foreach (var binding in subjectCompletionHandlers)
                binding.Key.Completed -= binding.Value;
            subjectCompletionHandlers.Clear();
        }

        void UnbindResultPanel()
        {
            if (pendingResultPanel != null && pendingResultPanelHandler != null)
                pendingResultPanel.ActionRequested -= pendingResultPanelHandler;
            pendingResultPanel = null;
            pendingResultPanelHandler = null;
            pendingResultSubject = null;
            pendingSubjectResult = null;
        }

        static IResultPreviewPanel FindResultPanel()
        {
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour is IResultPreviewPanel panel)
                    return panel;
            }

            return null;
        }

        string SceneFor(SubjectId? subject)
        {
            if (!subject.HasValue)
                return null;

            foreach (var entry in subjectScenes)
            {
                if (entry.Subject == subject.Value)
                    return entry.SceneName;
            }

            return null;
        }

        bool StartSceneLoad(string sceneName, Action onCompleted)
        {
            SceneLoadStarted?.Invoke();
            SceneLoadProgressChanged?.Invoke(0f);

            if (!CanLoadScene(sceneName))
            {
                FailSceneLoad(sceneName, onCompleted, "The scene is not enabled in Build Settings.");
                return false;
            }

            AsyncOperation operation;
            try
            {
                operation = sceneLoader(sceneName);
            }
            catch (Exception exception)
            {
                FailSceneLoad(sceneName, onCompleted, exception.Message);
                return false;
            }

            if (operation == null)
            {
                FailSceneLoad(sceneName, onCompleted, "Unity did not create an async load operation.");
                return false;
            }

            StartCoroutine(ObserveSceneLoad(operation, onCompleted));
            return true;
        }

        IEnumerator ObserveSceneLoad(AsyncOperation operation, Action onCompleted)
        {
            while (!operation.isDone)
            {
                SceneLoadProgressChanged?.Invoke(Mathf.Min(0.99f,
                    NormalizeLoadProgress(operation.progress)));
                yield return null;
            }

            SceneLoadProgressChanged?.Invoke(1f);
            onCompleted?.Invoke();
            SceneLoadCompleted?.Invoke();
        }

        void FailSceneLoad(string sceneName, Action onCompleted, string reason)
        {
            string message = $"Could not load scene '{sceneName}': {reason}";
            lastRouteError = message;
            Debug.LogError(message, this);
            SceneLoadFailed?.Invoke(message);
            onCompleted?.Invoke();
            SceneLoadCompleted?.Invoke();
        }

        internal static float NormalizeLoadProgress(float progress) =>
            Mathf.Clamp01(progress / 0.9f);

        bool TryRouteMutatedSession(SaveData previous, SessionRoute route, SubjectId? subject)
        {
            if (journeyPersist != null && !journeyPersist(out lastRouteError))
            {
                session.RestoreSnapshot(previous);
                return false;
            }
            try
            {
                if (Route(route, subject))
                    return true;
            }
            catch
            {
                if (journeyPersist == null)
                    session.RestoreSnapshot(previous);
                throw;
            }

            if (journeyPersist == null)
                session.RestoreSnapshot(previous);
            return false;
        }

        static AsyncOperation LoadSingleScene(string sceneName) =>
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

        static bool CanLoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return false;
            if (Application.CanStreamedLevelBeLoaded(sceneName))
                return true;
#if UNITY_EDITOR
            foreach (var buildScene in UnityEditor.EditorBuildSettings.scenes)
            {
                if (buildScene.enabled && string.Equals(
                        System.IO.Path.GetFileNameWithoutExtension(buildScene.path), sceneName,
                        StringComparison.Ordinal))
                    return true;
            }
#endif
            return false;
        }

        static SubjectScene[] DefaultSubjectScenes() => new[]
        {
            new SubjectScene { Subject = SubjectId.Sprint, SceneName = "MG_Sprint" },
            new SubjectScene { Subject = SubjectId.Volleyball, SceneName = "MG_Volleyball" },
            new SubjectScene { Subject = SubjectId.Football, SceneName = "MG_Football" },
            new SubjectScene { Subject = SubjectId.Chess, SceneName = "MG_ChessFinal" }
        };
    }
}
