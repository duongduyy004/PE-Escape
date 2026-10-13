using System;
using UnityEngine;

namespace KMA.Gameplay
{
    /// One rival's body on the track: position, velocity and acceleration in metres and seconds.
    /// The pace profile says how fast they want to run; this decides how fast they actually go.
    public sealed class RivalMotion
    {
        public const float MaxAcceleration = 7f;
        public const float FinishStretchFrom = .8f;
        public const float SurgingRatio = 1.06f;
        public const float SlowingRatio = .9f;
        // A runner who barely has room left before their stop point pulls up at once.
        const float MinBrakeRoom = .01f;

        readonly RivalPaceProfile profile;
        readonly RivalMotionTraits traits;
        float brake;

        public RivalMotion(RivalPaceProfile profile)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            traits = profile.Traits;
            FinishTime = -1f;
        }

        public RivalPaceProfile Profile => profile;
        public float Position { get; private set; }
        public float Velocity { get; private set; }
        public float Acceleration { get; private set; }
        /// Race time at which the rival crossed the line, interpolated inside the tick; −1 before.
        public float FinishTime { get; private set; }
        public bool HasFinished => FinishTime >= 0f;

        public bool IsSurgingAt(float clock) => !HasFinished && clock > RivalPaceProfile.OpeningSeconds &&
            Velocity > profile.SustainedSpeed * SurgingRatio;

        public bool IsSlowingAt(float clock) => !HasFinished && clock > RivalPaceProfile.OpeningSeconds &&
            Velocity < profile.SustainedSpeed * SlowingRatio;

        /// One tick of a race to <paramref name="goal"/>; <paramref name="clock"/> is the race time at its start.
        public void StepRace(float clock, float dt, float goal)
        {
            if (dt <= 0f) return;
            if (HasFinished)
            {
                Acceleration = -brake;
                Velocity = Mathf.Max(0f, Velocity - brake * dt);
                Position += Velocity * dt;
                return;
            }

            float end = clock + dt;
            if (end <= traits.ReactionSeconds) { Acceleration = 0f; return; }
            float target = profile.SpeedAt(end);
            if (Position >= goal * FinishStretchFrom) target *= 1f + traits.FinishStyle;
            Chase(target, dt);

            float before = Position;
            Position += Velocity * dt;
            if (before < goal && Position >= goal)
            {
                FinishTime = clock + dt * ((goal - before) / (Position - before));
                float room = goal + StopDistance - Position;
                brake = room > MinBrakeRoom ? Velocity * Velocity / (2f * room) : float.PositiveInfinity;
                if (float.IsPositiveInfinity(brake)) Velocity = 0f;
            }
        }

        float StopDistance => traits.StopDistance > 0f ? traits.StopDistance : 4f;

        void Chase(float target, float dt)
        {
            if (traits.AccelerationTau <= 0f)
            {
                Acceleration = 0f;
                Velocity = Mathf.Max(0f, target);
                return;
            }
            Acceleration = Mathf.Min((target - Velocity) / traits.AccelerationTau, MaxAcceleration);
            float next = Velocity + Acceleration * dt;
            // A long tick must not carry the runner past the pace they were chasing.
            Velocity = Mathf.Max(0f, Acceleration >= 0f ? Mathf.Min(next, target) : Mathf.Max(next, target));
        }
    }
}
