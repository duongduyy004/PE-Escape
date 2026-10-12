using UnityEngine;

namespace KMA.Gameplay
{
    /// <summary>Reads race state for presentation; never advances or changes the race rules.</summary>
    public sealed class RunnerVisualPresenter : MonoBehaviour
    {
        [SerializeField] SprintController controller;
        [SerializeField] Animator animator;
        [SerializeField] Transform raceRoot;
        [SerializeField] float trackStartX = -9.6f;
        // Hysteresis so the runner does not flicker between running and resting around one speed.
        const float RestBelowSpeed = 10f;
        const float RunAboveSpeed = 25f;

        RunnerBreathing breathing;
        bool resting;
        string lastState;

        void Awake()
        {
            if (controller == null) controller = FindFirstObjectByType<SprintController>();
            if (animator == null) animator = GetComponent<Animator>();
            if (raceRoot == null) raceRoot = transform.parent == null ? transform : transform.parent;
            breathing = RunnerBreathing.Attach(transform);
        }

        void Update()
        {
            if (controller == null || animator == null) return;
            RefreshPosition(controller.Snapshot.Distance);
            UpdateResting();
            string state = controller.Phase == MinigamePhase.Resolve
                ? (controller.LastResult != null && controller.LastResult.Pass ? "Celebrate" : "Fail")
                : controller.Phase != MinigamePhase.Play || resting ? "Idle"
                : !controller.IsLearnChallenge && controller.Snapshot.Distance >= SprintRules.RaceDistance * .7f
                    ? "Burst" : "Run";
            if (state == lastState) return;
            animator.Play(state, 0, 0f);
            lastState = state;
        }

        // Stopped tapping (or not yet started): stand still and breathe until the runner gets going again.
        void UpdateResting()
        {
            if (controller.Phase == MinigamePhase.Resolve)
                resting = false;
            else if (controller.Phase != MinigamePhase.Play)
                resting = true;
            else
            {
                float speed = controller.Snapshot.Speed;
                resting = resting ? speed < RunAboveSpeed : speed < RestBelowSpeed;
            }

            if (breathing != null) breathing.SetResting(resting);
        }

        void RefreshPosition(float distance)
        {
            if (raceRoot == null) return;
            var position = raceRoot.localPosition;
            // Learn is a cadence drill. Hold the runner in the middle of the track so repeated
            // taps never look like the timed race has reached its finish.
            float shownDistance = controller != null && controller.IsLearnChallenge
                ? Mathf.Min(distance, SprintRules.RaceDistance * .66f) : distance;
            position.x = trackStartX + SprintTrackLayout.TrackLength * Mathf.Clamp01(shownDistance / SprintRules.RaceDistance);
            raceRoot.localPosition = position;
        }
    }
}
