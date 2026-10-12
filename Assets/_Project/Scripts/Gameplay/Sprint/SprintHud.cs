using KMA.UI.Kit;
using TMPro;
using UnityEngine;

namespace KMA.Gameplay
{
    public sealed class SprintHud : MonoBehaviour
    {
        [SerializeField] SprintController controller;
        [SerializeField] Transform metricsRoot;
        [SerializeField] TMP_Text distanceLabel;
        [SerializeField] TMP_Text rankLabel;
        [SerializeField] TMP_Text cadenceLabel;
        [SerializeField] TMP_Text timerLabel;
        [SerializeField] KitBar distanceBar;

        public string DistanceText { get; private set; } = string.Empty;
        public string RankText { get; private set; } = string.Empty;
        public string CadenceText { get; private set; } = string.Empty;
        public string TimerText { get; private set; } = string.Empty;
        public float PipProgress { get; private set; }

        void Awake()
        {
            if (controller == null)
                controller = Object.FindFirstObjectByType<SprintController>();
            SprintFestivalPresentation.Build();
            CacheVisuals();
        }

        void OnEnable() => Refresh();
        void Update() => Refresh();

        public void Refresh()
        {
            if (controller == null)
                return;

            var snapshot = controller.Snapshot;
            float progress = controller.IsLearnChallenge
                ? controller.LearnProgress01
                : Mathf.Clamp01(snapshot.Distance / Mathf.Max(1f, controller.TargetDistance));

            DistanceText = controller.IsLearnChallenge
                ? $"HỌC · {FormatClock(controller.LearnTimeRemaining)}"
                : $"{Mathf.RoundToInt(snapshot.Distance)} / {controller.TargetDistance:0} m";
            RankText = controller.IsLearnChallenge ? string.Empty : controller.RankText;
            CadenceText = controller.IsWaitingForRivals ? SprintController.WaitForRivalsText
                : controller.IsLearnChallenge
                    ? $"CHUỖI TỐT NHẤT {controller.BestStreak}/{controller.TargetCount}"
                    : controller.IsComboBoosting
                    ? $"BỨT TỐC ×{controller.CadenceCombo}" : $"CHUỖI ×{controller.CadenceCombo}";
            TimerText = controller.IsLearnChallenge ? string.Empty
                : $"{Mathf.CeilToInt(Mathf.Max(0f, controller.TargetTime - snapshot.Elapsed))}s";
            PipProgress = progress;

            if (distanceLabel != null) distanceLabel.text = VietText.Fix(DistanceText);
            if (rankLabel != null) rankLabel.text = VietText.Fix(RankText);
            if (rankLabel != null && rankLabel.transform.parent != null)
                rankLabel.transform.parent.gameObject.SetActive(!controller.IsLearnChallenge);
            if (cadenceLabel != null) cadenceLabel.text = VietText.Fix(CadenceText);
            if (timerLabel != null) timerLabel.text = VietText.Fix(TimerText);
            if (distanceBar != null) distanceBar.SetValue(progress);
        }

        static string FormatClock(float seconds)
        {
            int remaining = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{remaining / 60:00}:{remaining % 60:00}";
        }


        public bool HasBoundVisuals => metricsRoot != null && distanceLabel != null && rankLabel != null &&
            cadenceLabel != null && distanceBar != null;

        void CacheVisuals()
        {
            var hud = GameObject.Find("S2_HUD_Minigame");
            if (hud == null)
                return;

            var chrome = hud.transform.Find("SafeAreaRoot/SprintBroadcastChrome");
            if (chrome == null)
                return;

            metricsRoot = chrome.Find("Scoreboard");
            if (metricsRoot == null)
                return;

            distanceLabel = metricsRoot.Find("Distance")?.GetComponent<TMP_Text>();
            rankLabel = metricsRoot.Find("RankBadge/RankLabel")?.GetComponent<TMP_Text>();
            cadenceLabel = metricsRoot.Find("Combo")?.GetComponent<TMP_Text>();
            timerLabel = metricsRoot.Find("RaceTimer")?.GetComponent<TMP_Text>();
            distanceBar = chrome.Find("ProgressRail")?.GetComponent<KitBar>();
        }
    }
}
