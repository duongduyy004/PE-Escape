using UnityEngine;

namespace KMA.Gameplay
{
    /// <summary>Reads race state for presentation; never advances or changes the race rules.</summary>
    public sealed class RunnerVisualPresenter : MonoBehaviour
    {
        [SerializeField] SprintController controller;
        [SerializeField] Animator animator;
        [SerializeField] Transform raceRoot;
        // Hysteresis, in m/s, so the runner does not flicker between running and resting around one speed.
        const float RestBelowSpeed = .8f;
        const float RunAboveSpeed = 2f;

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
            RefreshPosition(controller.PlayerPosition);
            UpdateResting();
            bool falling = controller.Phase == MinigamePhase.Play && controller.IsFalling;
            string state = controller.Phase == MinigamePhase.Resolve
                ? (controller.LastResult != null && controller.LastResult.Pass ? "Celebrate" : "Fail")
                : falling ? "Stumble"
                : controller.Phase != MinigamePhase.Play || resting ? "Idle"
                : !controller.IsLearnChallenge && !controller.IsPlayerFinished &&
                  controller.Snapshot.Distance >= controller.TargetDistance * .7f
                    ? "Burst" : "Run";
            animator.speed = state == "Run" || state == "Burst"
                ? RunnerStride.Playback(controller.PlayerMetersPerSecond) : 1f;
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
                float speed = controller.PlayerMetersPerSecond;
                resting = !controller.IsFalling && (resting ? speed < RunAboveSpeed : speed < RestBelowSpeed);
            }

            if (breathing != null) breathing.SetResting(resting);
        }

        void RefreshPosition(float position)
        {
            if (raceRoot == null) return;
            Vector3 world = raceRoot.position;
            world.x = SprintTrackLayout.WorldX(position);
            raceRoot.position = world;
        }
    }
}
