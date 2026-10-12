using UnityEngine;

namespace KMA.Gameplay
{
    public enum RivalRunnerState
    {
        Idle,
        Run,
        Burst,
        Stumble,
        Celebrate,
        Fail
    }

    public sealed class RivalRunnerAI : MonoBehaviour
    {
        [SerializeField] SprintController controller;
        [SerializeField] RivalPaceProfileAsset profileAsset;
        [SerializeField] int lane = 1;
        [SerializeField] int rivalIndex;
        [SerializeField] Transform visual;
        [SerializeField] Animator animator;
        [SerializeField] float trackStartX;

        RivalPaceProfile profile;

        public RivalPaceProfileAsset ProfileAsset => profileAsset;
        public RivalPaceProfile Profile => profile;
        public int Lane => lane;
        public int RivalIndex => rivalIndex;
        public RivalRunnerState State { get; private set; }
        public float VisualProgress01 { get; private set; }
        public Animator Animator => animator;
        public SpriteRenderer Sprite => visual == null ? null : visual.GetComponent<SpriteRenderer>();

        static readonly int RunHash = Animator.StringToHash("Run");
        static readonly int BurstHash = Animator.StringToHash("Burst");
        static readonly int StumbleHash = Animator.StringToHash("Stumble");
        static readonly int CelebrateHash = Animator.StringToHash("Celebrate");
        static readonly int FailHash = Animator.StringToHash("Fail");
        static readonly int IdleHash = Animator.StringToHash("Idle");
        RunnerBreathing breathing;
        RivalRunnerState lastPlayedState;
        bool hasPlayedState;

        void Awake()
        {
            if (visual == null) visual = transform;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (profileAsset != null) profile = profileAsset.ToRuntime();
            if (controller == null) controller = Object.FindFirstObjectByType<SprintController>();
            breathing = RunnerBreathing.Attach(visual);
        }

        void Update()
        {
            if (controller == null || rivalIndex < 0)
                return;

            if (rivalIndex >= controller.RivalCount)
                return;
            Refresh(controller.GetRivalDistance(rivalIndex), controller.Snapshot.Distance, controller.Phase,
                controller.LastResult, controller.IsRivalSurging(rivalIndex), controller.IsRivalSlowing(rivalIndex));
        }

        public void Configure(RivalPaceProfileAsset value, int valueLane, int valueRivalIndex, SprintController owner)
        {
            profileAsset = value;
            profile = value == null ? null : value.ToRuntime();
            lane = valueLane;
            rivalIndex = valueRivalIndex;
            controller = owner;
            if (visual == null) visual = transform;
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        public void RefreshForTest(float rivalDistance, float playerDistance, MinigamePhase phase, MinigameResult result,
            bool surging = false, bool slowing = false) =>
            Refresh(rivalDistance, playerDistance, phase, result, surging, slowing);

        void Refresh(float rivalDistance, float playerDistance, MinigamePhase phase, MinigameResult result,
            bool surging, bool slowing = false)
        {
            bool learning = controller != null && controller.IsLearnChallenge;
            float shownDistance = learning ? Mathf.Min(rivalDistance, SprintRules.RaceDistance * .66f) : rivalDistance;
            VisualProgress01 = Mathf.Clamp01(shownDistance / SprintRules.RaceDistance);
            if (visual != null)
            {
                // The rival root is scaled, which stretches a child's local offset. The player moves its
                // own root instead, so divide the scale out or the rival would out-run the same distance.
                float parentScaleX = visual.parent == null ? 1f : Mathf.Abs(visual.parent.lossyScale.x);
                var position = visual.localPosition;
                position.x = (trackStartX + SprintTrackLayout.TrackLength * VisualProgress01) / Mathf.Max(.0001f, parentScaleX);
                visual.localPosition = position;
            }

            if (phase == MinigamePhase.Resolve)
                State = result != null && result.Pass ? RivalRunnerState.Celebrate : RivalRunnerState.Fail;
            else if (phase != MinigamePhase.Play || (!learning && VisualProgress01 >= 1f))
                State = RivalRunnerState.Idle;
            else if (surging || (!learning && playerDistance >= SprintRules.RaceDistance * .7f))
                State = RivalRunnerState.Burst;
            else
                State = RivalRunnerState.Run;

            // Waiting on the grid, flagging mid-race or already across the line: catch a breath.
            if (breathing != null)
                breathing.SetResting(phase == MinigamePhase.Tutorial || phase == MinigamePhase.Countdown ||
                    (phase == MinigamePhase.Play && (slowing || (!learning && rivalDistance >= SprintRules.RaceDistance))));

            if (animator != null && (!hasPlayedState || lastPlayedState != State))
            {
                animator.Play(StateHash(State), 0, 0f);
                lastPlayedState = State;
                hasPlayedState = true;
            }
        }

        static int StateHash(RivalRunnerState state) => state switch
        {
            RivalRunnerState.Run => RunHash,
            RivalRunnerState.Burst => BurstHash,
            RivalRunnerState.Stumble => StumbleHash,
            RivalRunnerState.Celebrate => CelebrateHash,
            RivalRunnerState.Fail => FailHash,
            _ => IdleHash
        };
    }
}
