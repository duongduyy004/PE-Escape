using UnityEngine;

namespace KMA.Gameplay
{
    /// The lecturer who starts every sports minigame. She is never still: during the tutorial she
    /// folds her arms, points at the field, adjusts her cap and counts on her fingers; in the
    /// countdown she counts down and then raises the whistle; play opens with the blast (on the same
    /// frame the shared Whistle sound plays) and a point down the field, then she keeps supervising;
    /// at the result she cheers a pass and points down at a fail.
    /// The art faces right, so a lecturer standing to the right of the athletes sets <c>faceLeft</c>.
    public sealed class StartLecturer : MonoBehaviour
    {
        public enum Pose
        {
            Waiting, WhistleRaised, WhistleBlown,
            Count, Command, StrictLook, Taunt,
            Cheer, CheerBothArms, Penalty, PenaltySide,
            ScoreHold, ScoreWrite
        }

        /// How long the blast pose is held before she points the athletes off.
        public const float BlastSeconds = .7f;

        /// How long she counts on her fingers before raising the whistle for the last beat.
        public const float CountSeconds = 1.8f;

        readonly struct Beat
        {
            public readonly Pose Pose;
            public readonly float Seconds;

            public Beat(Pose pose, float seconds)
            {
                Pose = pose;
                Seconds = seconds;
            }
        }

        /// A sequence of poses; once it runs out it repeats from <c>LoopFrom</c>, or holds the last
        /// pose when <c>LoopFrom</c> is negative.
        sealed class Routine
        {
            public readonly Beat[] Beats;
            public readonly int LoopFrom;

            public Routine(int loopFrom, params Beat[] beats)
            {
                Beats = beats;
                LoopFrom = loopFrom;
            }
        }

        static readonly Routine Explaining = new Routine(0,
            new Beat(Pose.Waiting, 2.2f), new Beat(Pose.Command, 1.2f), new Beat(Pose.Waiting, 1.4f),
            new Beat(Pose.Count, 1.1f), new Beat(Pose.StrictLook, 1.8f), new Beat(Pose.Taunt, 1f));

        static readonly Routine CountingDown = new Routine(-1,
            new Beat(Pose.Count, CountSeconds), new Beat(Pose.WhistleRaised, 0f));

        static readonly Routine Supervising = new Routine(2,
            new Beat(Pose.WhistleBlown, BlastSeconds), new Beat(Pose.Command, .9f),
            new Beat(Pose.Waiting, 2.4f), new Beat(Pose.StrictLook, 2f), new Beat(Pose.Taunt, 1f),
            new Beat(Pose.Waiting, 1.8f), new Beat(Pose.Command, 1f), new Beat(Pose.StrictLook, 1.6f));

        static readonly Routine Whistling = new Routine(1,
            new Beat(Pose.WhistleBlown, BlastSeconds), new Beat(Pose.Command, .8f),
            new Beat(Pose.StrictLook, 1.2f), new Beat(Pose.Waiting, 1.8f));

        static readonly Routine WritingScore = new Routine(-1,
            new Beat(Pose.ScoreHold, .35f), new Beat(Pose.ScoreWrite, 1.4f),
            new Beat(Pose.Waiting, 0f));

        static readonly Routine PointingThenWriting = new Routine(-1,
            new Beat(Pose.Penalty, .7f), new Beat(Pose.ScoreWrite, 1.4f),
            new Beat(Pose.Waiting, 0f));

        static readonly Routine Judging = new Routine(-1, new Beat(Pose.Waiting, 0f));

        static readonly Routine Cheering = new Routine(0,
            new Beat(Pose.Cheer, .35f), new Beat(Pose.CheerBothArms, .35f));

        static readonly Routine Scolding = new Routine(0,
            new Beat(Pose.Penalty, .6f), new Beat(Pose.PenaltySide, .6f));

        [SerializeField] SpriteRenderer body;
        [SerializeField] Sprite waiting;
        [SerializeField] Sprite whistleRaised;
        [SerializeField] Sprite whistleBlown;
        [SerializeField] Sprite count;
        [SerializeField] Sprite command;
        [SerializeField] Sprite strictLook;
        [SerializeField] Sprite taunt;
        [SerializeField] Sprite cheer;
        [SerializeField] Sprite cheerBothArms;
        [SerializeField] Sprite penalty;
        [SerializeField] Sprite penaltySide;
        [SerializeField] Sprite scoreHold;
        [SerializeField] Sprite scoreWrite;
        [SerializeField] bool faceLeft;
        [SerializeField] float leaveAfterSeconds;

        MinigameBase source;
        Routine routine;
        int beat;
        float beatRemaining;
        float leaveRemaining;
        float lastWhistleSoundTime = -10f;

        public Pose Current { get; private set; }
        public bool FacesLeft => body != null && body.flipX;
        public float Alpha => body == null ? 0f : body.color.a;

        public void Configure(SpriteRenderer renderer, Sprite waitingPose, Sprite raisedPose, Sprite blownPose,
            bool facingLeft, float leaveAfter)
        {
            body = renderer;
            waiting = waitingPose;
            whistleRaised = raisedPose;
            whistleBlown = blownPose;
            faceLeft = facingLeft;
            leaveAfterSeconds = leaveAfter;
            Play(Judging);
        }

        /// The gestures between the whistle poses; any left unset falls back to the waiting pose.
        public void ConfigureGestures(Sprite countPose, Sprite commandPose, Sprite strictLookPose, Sprite tauntPose,
            Sprite cheerPose, Sprite cheerBothArmsPose, Sprite penaltyPose, Sprite penaltySidePose)
        {
            count = countPose;
            command = commandPose;
            strictLook = strictLookPose;
            taunt = tauntPose;
            cheer = cheerPose;
            cheerBothArms = cheerBothArmsPose;
            penalty = penaltyPose;
            penaltySide = penaltySidePose;
            Apply(Current);
        }

        public void ConfigureScore(Sprite holdPose, Sprite writePose)
        {
            scoreHold = holdPose;
            scoreWrite = writePose;
            Apply(Current);
        }

        public void Whistle()
        {
            Show();
            Play(Whistling);
            if (Time.time - lastWhistleSoundTime > .3f)
            {
                lastWhistleSoundTime = Time.time;
                GameAudio.Play(GameSound.Whistle);
            }
        }

        public void WriteScore()
        {
            Show();
            Play(WritingScore);
        }

        public void PointThenWrite()
        {
            Show();
            Play(PointingThenWriting);
        }

        void OnEnable()
        {
            if (body != null)
                body.flipX = faceLeft;
            source = FindFirstObjectByType<MinigameBase>();
            if (source == null)
            {
                Play(Judging);
                return;
            }

            source.PhaseChanged += OnPhaseChanged;
            source.ResultDecided += OnResultDecided;
            OnPhaseChanged(source.PresentationPhase);
        }

        void OnDisable()
        {
            if (source != null)
            {
                source.PhaseChanged -= OnPhaseChanged;
                source.ResultDecided -= OnResultDecided;
            }
            source = null;
        }

        void Update() => Tick(Time.deltaTime);

        public void OnPhaseChanged(MinigamePhase phase)
        {
            switch (phase)
            {
                case MinigamePhase.Tutorial:
                    Show();
                    Play(Explaining);
                    break;
                case MinigamePhase.Countdown:
                    Show();
                    Play(CountingDown);
                    break;
                case MinigamePhase.Play:
                    leaveRemaining = leaveAfterSeconds;
                    lastWhistleSoundTime = Time.time;
                    Play(Supervising);
                    break;
                default:
                    Show();
                    Play(Judging);
                    break;
            }
        }

        public void OnResultDecided(bool passed)
        {
            Show();
            Play(passed ? Cheering : Scolding);
        }

        public void Tick(float dt)
        {
            if (routine != null && beatRemaining > 0f)
            {
                beatRemaining -= dt;
                while (beatRemaining <= 0f && Advance())
                    beatRemaining += routine.Beats[beat].Seconds;
            }

            if (leaveRemaining > 0f)
            {
                leaveRemaining = Mathf.Max(0f, leaveRemaining - dt);
                SetAlpha(Mathf.Clamp01(leaveRemaining / .4f));
            }
        }

        void Play(Routine next)
        {
            routine = next;
            beat = 0;
            beatRemaining = next.Beats[0].Seconds;
            Apply(next.Beats[0].Pose);
        }

        /// Steps to the next beat; false when the routine holds its last pose.
        bool Advance()
        {
            if (beat + 1 < routine.Beats.Length)
                beat++;
            else if (routine.LoopFrom >= 0)
                beat = routine.LoopFrom;
            else
                return false;
            Apply(routine.Beats[beat].Pose);
            return routine.Beats[beat].Seconds > 0f;
        }

        void Apply(Pose pose)
        {
            Current = pose;
            if (body == null)
                return;
            Sprite sprite = pose switch
            {
                Pose.WhistleRaised => whistleRaised,
                Pose.WhistleBlown => whistleBlown,
                Pose.Count => count,
                Pose.Command => command,
                Pose.StrictLook => strictLook,
                Pose.Taunt => taunt,
                Pose.Cheer => cheer,
                Pose.CheerBothArms => cheerBothArms,
                Pose.Penalty => penalty,
                Pose.PenaltySide => penaltySide,
                Pose.ScoreHold => scoreHold != null ? scoreHold : cheer,
                Pose.ScoreWrite => scoreWrite != null ? scoreWrite : cheer,
                _ => waiting
            };
            body.sprite = sprite != null ? sprite : waiting;
        }

        void Show()
        {
            leaveRemaining = 0f;
            if (body != null)
                SetAlpha(1f);
        }

        void SetAlpha(float alpha)
        {
            Color color = body.color;
            color.a = alpha;
            body.color = color;
        }
    }
}
