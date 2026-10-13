using System;
using UnityEngine;

namespace KMA.Gameplay
{
    public enum Side
    {
        Left,
        Right
    }

    public readonly struct SprintSnapshot : System.IEquatable<SprintSnapshot>
    {
        public float Distance { get; }
        public float Speed { get; }
        public float Elapsed { get; }

        public SprintSnapshot(float distance, float speed, float elapsed)
        {
            Distance = distance;
            Speed = speed;
            Elapsed = elapsed;
        }

        public bool Equals(SprintSnapshot other) =>
            Distance.Equals(other.Distance) && Speed.Equals(other.Speed) && Elapsed.Equals(other.Elapsed);

        public override bool Equals(object obj) => obj is SprintSnapshot other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Distance.GetHashCode();
                hash = hash * 397 ^ Speed.GetHashCode();
                return hash * 397 ^ Elapsed.GetHashCode();
            }
        }
    }

    public sealed class SprintRules
    {
        public const float RaceDistance = 150f;
        public const float DefaultTimeLimit = 22f;
        // A wrong or off-beat tap trips the runner: they stop, lie stumbled, then get up.
        public const float FallSeconds = .9f;
        // After getting up the runner cannot trip again at once, so a fumbling player is not locked down.
        public const float FallCooldownSeconds = 1.2f;
        // How far past the line the player runs before standing still, unless the controller rolls one.
        public const float DefaultRunOutDistance = 4f;

        readonly float timeLimit;
        readonly float goalDistance;
        readonly Side[] authoredSequence;
        readonly RivalPaceProfile[] rivalProfiles;
        readonly RivalMotion[] rivals;
        readonly float runOutDistance;
        // Race time for the whole field; it keeps running after the player's own clock stops on the line.
        float raceClock;
        float finishTime = -1f;
        // The player's few metres past the line, and how fast they are still covering them.
        float runOut;
        float runOutSpeed;
        float runOutBrake;
        readonly SprintBalanceParameters? balance;
        int sequenceIndex;
        int valid;
        int total;
        int correctStreak;
        int currentRank = 1;
        float lastTapElapsed;
        float fallEndsAt = -1f;
        float fallCooldownEndsAt = -1f;
        bool hasTapped;
        float distance;
        float speed;
        float elapsed;
        bool finished;

        public SprintRules(float timeLimit = DefaultTimeLimit, RivalPaceProfile[] rivalProfiles = null,
            Side[] authoredSequence = null, SprintBalanceParameters? balance = null,
            float goalDistance = RaceDistance, float runOutDistance = DefaultRunOutDistance)
        {
            this.timeLimit = timeLimit;
            this.balance = balance;
            this.goalDistance = goalDistance;
            this.authoredSequence = authoredSequence == null ? new[] { Side.Left, Side.Right } : (Side[])authoredSequence.Clone();
            if (this.authoredSequence.Length == 0)
                throw new System.ArgumentException("Sprint authored sequence must contain at least one side.", nameof(authoredSequence));

            this.rivalProfiles = rivalProfiles == null ? Array.Empty<RivalPaceProfile>() : (RivalPaceProfile[])rivalProfiles.Clone();
            this.runOutDistance = runOutDistance;
            rivals = new RivalMotion[this.rivalProfiles.Length];
            for (int i = 0; i < rivals.Length; i++)
                rivals[i] = this.rivalProfiles[i] == null ? null : new RivalMotion(this.rivalProfiles[i]);
        }

        public static SprintRules Default() => new SprintRules(DefaultTimeLimit);

        public static SprintRules ForTest(float distance, float elapsed, int rank,
            RivalPaceProfile[] rivalProfiles = null, Side[] authoredSequence = null, float speed = 0f,
            float goalDistance = RaceDistance)
        {
            _ = rank;
            var value = new SprintRules(DefaultTimeLimit, rivalProfiles, authoredSequence, goalDistance: goalDistance);
            value.distance = distance;
            value.elapsed = elapsed;
            value.raceClock = elapsed;
            value.speed = speed;
            value.UpdateRank();
            return value;
        }

        public float Distance => distance;
        public float Speed => speed;
        public float Elapsed => elapsed;
        /// The player crossed the line: their numbers and rank are frozen while rivals keep running.
        public bool IsFinished => finished;
        public float TimeLimit => timeLimit;
        public int CorrectStreak => correctStreak;
        /// The runner tripped on a wrong or off-beat tap and is stopped until they get up.
        public bool IsFalling => !finished && elapsed < fallEndsAt;
        public float ComboBoost => ComboBoostFor(correctStreak, Tuning);
        public bool IsComboBoosting => ComboBoost > 0f;
        public float ValidTapRatio => total == 0 ? 0f : (float)valid / total;
        public int Rank => currentRank;
        public Side ExpectedSide => authoredSequence[sequenceIndex];
        public Side[] AuthoredSequence => (Side[])authoredSequence.Clone();
        public RivalPaceProfile[] RivalProfiles => (RivalPaceProfile[])rivalProfiles.Clone();
        public float GoalDistance => goalDistance;
        /// Where the player actually is, including the metres they run on past the line.
        public float Position => distance + runOut;
        public float MetersPerSecond => finished || runOutSpeed > 0f ? runOutSpeed : speed * Tuning.DistanceScale;
        /// Race time at which the player crossed the line; -1 until they do.
        public float FinishTime => finishTime;
        public float[] RivalDistances
        {
            get
            {
                var values = new float[rivals.Length];
                for (int i = 0; i < values.Length; i++) values[i] = GetRivalDistance(i);
                return values;
            }
        }
        public int RivalCount => rivals.Length;
        RivalMotion RivalAt(int index) => index < 0 || index >= rivals.Length ? null : rivals[index];
        /// Race distance covered, stopping on the line; see <see cref="GetRivalPosition"/> for the body.
        public float GetRivalDistance(int index) => RivalAt(index) == null ? 0f : Mathf.Min(RivalAt(index).Position, goalDistance);
        public float GetRivalPosition(int index) => RivalAt(index)?.Position ?? 0f;
        public float GetRivalSpeed(int index) => RivalAt(index)?.Velocity ?? 0f;
        public bool HasRivalFinished(int index) => RivalAt(index) != null && RivalAt(index).HasFinished;
        public float GetRivalFinishTime(int index) => RivalAt(index)?.FinishTime ?? -1f;
        public bool IsRivalSurging(int index) => RivalAt(index) != null && RivalAt(index).IsSurgingAt(raceClock);
        public bool IsRivalSlowing(int index) => RivalAt(index) != null && RivalAt(index).IsSlowingAt(raceClock);
        public SprintSnapshot Snapshot => new SprintSnapshot(distance, speed, elapsed);

        // Races without an authored balance asset use the default tuning.
        SprintBalanceParameters Tuning => balance ?? SprintBalanceParameters.Default;

        // A long unbroken alternation lifts both the impulse and the speed cap, up to ComboBoostMax.
        static float ComboBoostFor(int streak, SprintBalanceParameters tuning)
        {
            if (tuning.ComboBoostMax <= 0f || streak <= tuning.ComboBoostStartStreak) return 0f;
            int span = tuning.ComboBoostFullStreak - tuning.ComboBoostStartStreak;
            float t = span <= 0 ? 1f : Mathf.Clamp01((float)(streak - tuning.ComboBoostStartStreak) / span);
            return tuning.ComboBoostMax * t;
        }

        public void FinishRace()
        {
            if (finished) return;
            finished = true;
            finishTime = elapsed;
            StartRunOut(speed * Tuning.DistanceScale);
        }

        public bool AllRivalsReached(float goalDistance)
        {
            for (int i = 0; i < rivals.Length; i++)
                if (rivals[i] != null && rivals[i].Position < goalDistance) return false;
            return true;
        }

        public void Tap(Side side)
        {
            if (finished) return;
            bool correct = side == ExpectedSide;
            total++;
            // Taps while lying on the ground do nothing.
            if (IsFalling)
            {
                if (!correct) correctStreak = 0;
                return;
            }
            if (correct)
            {
                valid++;
                correctStreak++;
                sequenceIndex = (sequenceIndex + 1) % authoredSequence.Length;
            }
            else
                correctStreak = 0;

            SprintBalanceParameters tuning = Tuning;
            if (!correct && elapsed >= fallCooldownEndsAt)
            {
                speed = 0f;
                fallEndsAt = elapsed + FallSeconds;
                fallCooldownEndsAt = fallEndsAt + FallCooldownSeconds;
                lastTapElapsed = elapsed;
                hasTapped = true;
                return;
            }
            float boost = 1f + ComboBoostFor(correctStreak, tuning);
            float impulse = (correct ? tuning.CorrectImpulse : tuning.CorrectImpulse * tuning.WrongImpulseFactor) * boost;
            float cap = tuning.SpeedCap * boost;
            // Losing the combo stops further gains but lets drag bleed off the extra speed.
            speed = Mathf.Max(speed, Mathf.Min(cap, speed + impulse));
            lastTapElapsed = elapsed;
            hasTapped = true;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            float clock = raceClock;
            raceClock += dt;
            if (finished)
            {
                AdvanceRunOut(dt);
                AdvanceRivals(clock, dt);
                return;
            }

            float startElapsed = elapsed;
            elapsed += dt;
            SprintBalanceParameters tuning = Tuning;
            if (IsFalling || IsIdle) speed = 0f;
            else speed = Mathf.Max(0f, speed - tuning.DragPerSecond * dt);
            float before = distance;
            float step = speed * dt * tuning.DistanceScale;
            distance += step;
            AdvanceRivals(clock, dt);

            if (before < goalDistance && distance >= goalDistance)
            {
                float crossing = dt * (step > 0f ? (goalDistance - before) / step : 1f);
                finishTime = startElapsed + crossing;
                elapsed = finishTime;
                runOut = distance - goalDistance;
                distance = goalDistance;
                finished = true;
                // Rivals who crossed earlier, in this tick or while the player was held, still beat
                // the player: compare on the race clock, which never stops.
                float finishClock = clock + crossing;
                currentRank = 1;
                for (int i = 0; i < rivals.Length; i++)
                    if (rivals[i] != null && rivals[i].HasFinished && rivals[i].FinishTime < finishClock) currentRank++;
                StartRunOut(speed * tuning.DistanceScale);
                return;
            }

            UpdateRank();
        }

        /// Moves the field on while the player is held (a Learn teacher line pauses only the learner):
        /// the player's position, speed and clock stay put.
        public void TickRivals(float dt)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            float clock = raceClock;
            raceClock += dt;
            if (finished) AdvanceRunOut(dt);
            AdvanceRivals(clock, dt);
        }

        /// After the result is decided the runners still pull up and the field still runs in;
        /// the race numbers do not change.
        public void TickAfterRace(float dt)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            float clock = raceClock;
            raceClock += dt;
            // A player stopped short of the line (time out) still pulls up rather than freezing;
            // the run-out only moves the body, never Distance, Elapsed or Rank.
            if (!finished && runOutSpeed <= 0f && speed > 0f) StartRunOut(speed * Tuning.DistanceScale);
            AdvanceRunOut(dt);
            AdvanceRivals(clock, dt);
        }

        void AdvanceRivals(float clock, float dt)
        {
            for (int i = 0; i < rivals.Length; i++)
            {
                if (rivals[i] == null) continue;
                rivals[i].StepRace(clock, dt, goalDistance);
            }
        }

        void StartRunOut(float metersPerSecond)
        {
            speed = 0f;
            runOutSpeed = metersPerSecond;
            float room = runOutDistance - runOut;
            runOutBrake = room > .01f ? metersPerSecond * metersPerSecond / (2f * room) : 0f;
            if (room <= .01f) runOutSpeed = 0f;
        }

        void AdvanceRunOut(float dt)
        {
            if (runOutSpeed <= 0f) return;
            runOutSpeed = Mathf.Max(0f, runOutSpeed - runOutBrake * dt);
            runOut += runOutSpeed * dt;
        }

        public MinigameResult BuildResult() => BuildResult(RaceDistance, timeLimit);

        public MinigameResult BuildResult(float goalDistance, float deadline)
        {
            bool pass = distance >= Mathf.Max(0f, goalDistance) && elapsed <= deadline;
            float accuracy = total == 0 ? 0f : 2f * valid / total;
            // 1st place earns the full bonus and last place none.
            int rivalTotal = rivals.Length;
            float placement = rivalTotal == 0 ? 1f : Mathf.Clamp01((float)(rivalTotal + 1 - currentRank) / rivalTotal);
            float mastery = deadline <= 0f ? 0f : Mathf.Clamp01((deadline - elapsed) / 3f);
            return ScoreUtil.Build(pass, accuracy, placement, mastery);
        }

        // Once the grace window after the last tap has passed the runner stands still: no sliding.
        bool IsIdle => hasTapped && elapsed - lastTapElapsed > Tuning.IdleGraceSeconds;

        void UpdateRank()
        {
            currentRank = 1;
            for (int i = 0; i < rivals.Length; i++)
                if (rivals[i] != null && (rivals[i].HasFinished || rivals[i].Position > distance)) currentRank++;
        }
    }
}
