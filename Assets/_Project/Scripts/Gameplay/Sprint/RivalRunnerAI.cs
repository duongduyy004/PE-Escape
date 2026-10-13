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

        RivalPaceProfile profile;

        public RivalPaceProfileAsset ProfileAsset => profileAsset;
        public RivalPaceProfile Profile => profile;
        public int Lane => lane;
        public int RivalIndex => rivalIndex;
        public RivalRunnerState State { get; private set; }
        public Animator Animator => animator;
        public SpriteRenderer Sprite => visual == null ? null : visual.GetComponent<SpriteRenderer>();

        static readonly int RunHash = Animator.StringToHash("Run");
        static readonly int BurstHash = Animator.StringToHash("Burst");
        static readonly int StumbleHash = Animator.StringToHash("Stumble");
        static readonly int CelebrateHash = Animator.StringToHash("Celebrate");
        static readonly int FailHash = Animator.StringToHash("Fail");
        static readonly int IdleHash = Animator.StringToHash("Idle");
        // A rival kicks into its finishing burst once this much of the track is behind them.
        public const float RivalBurstFromProgress = .8f;
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
            if (controller == null || rivalIndex < 0 || rivalIndex >= controller.RivalCount)
                return;
            Refresh(controller.GetRivalPosition(rivalIndex), controller.GetRivalSpeed(rivalIndex),
                controller.HasRivalFinished(rivalIndex), controller.RaceGoal, controller.Phase,
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

        public void RefreshForTest(float position, float speed, bool finished, float goal, MinigamePhase phase,
            MinigameResult result, bool surging = false, bool slowing = false) =>
            Refresh(position, speed, finished, goal, phase, result, surging, slowing);

        void Refresh(float position, float speed, bool finished, float goal, MinigamePhase phase,
            MinigameResult result, bool surging, bool slowing)
        {
            // Place the visual in world space so the scaled lane root never stretches the distance.
            if (visual != null)
            {
                Vector3 world = visual.position;
                world.x = SprintTrackLayout.WorldX(position);
                visual.position = world;
            }

            bool standing = speed <= .05f;
            if (phase == MinigamePhase.Resolve)
                State = result != null && result.Pass ? RivalRunnerState.Celebrate : RivalRunnerState.Fail;
            else if (phase != MinigamePhase.Play || standing)
                State = RivalRunnerState.Idle;
            else if (!finished && (surging || position >= goal * RivalBurstFromProgress))
                State = RivalRunnerState.Burst;
            else
                State = RivalRunnerState.Run;

            // Waiting on the grid, flagging mid-race or stood past the line: catch a breath.
            if (breathing != null)
                breathing.SetResting(phase == MinigamePhase.Tutorial || phase == MinigamePhase.Countdown ||
                    (phase == MinigamePhase.Play && (slowing || (finished && standing))));

            if (animator != null)
            {
                animator.speed = State == RivalRunnerState.Run || State == RivalRunnerState.Burst
                    ? RunnerStride.Playback(speed) : 1f;
                if (!hasPlayedState || lastPlayedState != State)
                {
                    animator.Play(StateHash(State), 0, 0f);
                    lastPlayedState = State;
                    hasPlayedState = true;
                }
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
