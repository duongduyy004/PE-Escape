using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KMA.Gameplay.UI
{
    /// Opens a minigame's how-to-play guide on its first visit and lets the pause menu reopen it.
    /// Installed on every scene load that has an IMinigameGuideSource.
    public sealed class MinigameGuideHost : MonoBehaviour
    {
        /// PlayMode test assemblies turn this off: the first-run guide freezes Time.timeScale.
        public static bool AutoOpenEnabled = true;
        public static MinigameGuideHost Current { get; private set; }

        IMinigameGuideSource source;
        ITutorialSeenStore seenStore;
        MinigameGuidePanel panel;

        public MinigameGuidePanel Panel => panel;
        public IMinigameGuideSource Source => source;
        public event System.Action<GuideMode> Closed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            AutoOpenEnabled = true;
            Current = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= Ensure;
            SceneManager.sceneLoaded += Ensure;
            Ensure(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void Ensure(Scene scene, LoadSceneMode mode)
        {
            if (FindFirstObjectByType<MinigameGuideHost>() != null)
                return;
            IMinigameGuideSource found = FindSource();
            if (found == null)
                return;
            new GameObject(nameof(MinigameGuideHost)).AddComponent<MinigameGuideHost>()
                .Configure(found, new SaveDataTutorialSeenStore());
        }

        static IMinigameGuideSource FindSource()
        {
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (behaviour is IMinigameGuideSource guide && behaviour.isActiveAndEnabled)
                    return guide;
            return null;
        }

        public void Configure(IMinigameGuideSource guideSource, ITutorialSeenStore store)
        {
            source = guideSource;
            seenStore = store ?? new SaveDataTutorialSeenStore();
        }

        void Awake()
        {
            Current = this;
            panel = MinigameGuidePanel.Create(transform);
            panel.Closed += OnClosed;
        }

        // Start runs after SceneRouter's sceneLoaded binding, so the lesson numbers are in place.
        void Start()
        {
            if (AutoOpenEnabled)
                TryOpenFirstRun();
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
            if (panel != null)
                panel.Closed -= OnClosed;
        }

        public bool TryOpenFirstRun() =>
            source != null && seenStore != null && !seenStore.HasSeen(source.GuideKey) && Open(GuideMode.FirstRun);

        public bool OpenReview() => source != null && Open(GuideMode.Review);

        bool Open(GuideMode mode)
        {
            if (panel.IsOpen)
                return false;
            IReadOnlyList<TutorialStep> pages = source.BuildGuide();
            if (pages == null || pages.Count == 0)
                return false;
            panel.Open(pages, mode);
            return panel.IsOpen;
        }

        void OnClosed(GuideMode mode)
        {
            if (mode == GuideMode.FirstRun && source != null)
                seenStore.MarkSeen(source.GuideKey);
            Closed?.Invoke(mode);
        }
    }
}
