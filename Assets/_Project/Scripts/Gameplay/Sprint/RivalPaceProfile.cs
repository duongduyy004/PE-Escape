using System;

namespace KMA.Gameplay
{
    public sealed class RivalPaceProfile
    {
        public const float OpeningSeconds = 3f;
        // A rival counts as surging once their pace runs this far above their sustained speed.
        const float SurgingRatio = 1.06f;
        // Below this fraction of their sustained speed a rival is flagging and visibly catching breath.
        const float SlowingRatio = .9f;

        public string Name { get; }
        public float OpeningSpeed { get; }
        public float SustainedSpeed { get; }
        public float SurgeAmplitude { get; }
        public float SurgePeriod { get; }
        public float SurgePhase { get; }

        // Seconds a pace change takes to blend in, so a rival accelerates rather than teleports.
        const float PaceBlendSeconds = .4f;
        readonly PaceSegment[] paceSegments;
        readonly float paceLength;

        public RivalPaceProfile(string name, float openingSpeed, float sustainedSpeed,
            float surgeAmplitude = 0f, float surgePeriod = 0f, float surgePhase = 0f)
        {
            Name = name;
            OpeningSpeed = openingSpeed;
            SustainedSpeed = sustainedSpeed;
            SurgeAmplitude = surgeAmplitude;
            SurgePeriod = surgePeriod;
            SurgePhase = surgePhase;
        }

        // An irregular pace: each segment holds a multiplier of the sustained speed, and the
        // schedule repeats if a race outlasts it.
        public RivalPaceProfile(string name, float openingSpeed, float sustainedSpeed, PaceSegment[] segments)
            : this(name, openingSpeed, sustainedSpeed)
        {
            if (segments == null || segments.Length == 0) return;
            paceSegments = (PaceSegment[])segments.Clone();
            foreach (PaceSegment segment in paceSegments) paceLength += segment.Seconds;
        }

        public int PaceSegmentCount => paceSegments == null ? 0 : paceSegments.Length;
        public PaceSegment GetPaceSegment(int index) => paceSegments[index];

        // After the opening burst the pace changes around the sustained speed, so rivals visibly
        // surge, fade and swap places instead of gliding at one constant speed.
        public float SpeedAt(float elapsed)
        {
            if (elapsed <= OpeningSeconds) return OpeningSpeed;
            if (paceSegments != null && paceLength > 0f)
                return SustainedSpeed * ScheduledMultiplier(elapsed - OpeningSeconds);
            if (SurgeAmplitude <= 0f || SurgePeriod <= 0f) return SustainedSpeed;
            float cycle = 2f * MathF.PI * (elapsed - OpeningSeconds) / SurgePeriod + SurgePhase;
            return SustainedSpeed * (1f + SurgeAmplitude * MathF.Sin(cycle));
        }

        float ScheduledMultiplier(float sincePace)
        {
            float t = sincePace % paceLength;
            float previous = 1f;
            for (int i = 0; i < paceSegments.Length; i++)
            {
                PaceSegment segment = paceSegments[i];
                if (t < segment.Seconds || i == paceSegments.Length - 1)
                {
                    float blend = Math.Min(PaceBlendSeconds, segment.Seconds * .5f);
                    if (blend <= 0f || t >= blend) return segment.Multiplier;
                    float x = t / blend;
                    return previous + (segment.Multiplier - previous) * x * x * (3f - 2f * x);
                }
                t -= segment.Seconds;
                previous = segment.Multiplier;
            }
            return 1f;
        }

        public bool IsSurgingAt(float elapsed) =>
            elapsed > OpeningSeconds && SpeedAt(elapsed) > SustainedSpeed * SurgingRatio;

        public bool IsSlowingAt(float elapsed) =>
            elapsed > OpeningSeconds && SpeedAt(elapsed) < SustainedSpeed * SlowingRatio;
    }

    public readonly struct PaceSegment
    {
        public float Seconds { get; }
        public float Multiplier { get; }

        public PaceSegment(float seconds, float multiplier)
        {
            Seconds = seconds;
            Multiplier = multiplier;
        }

        public bool IsSurge => Multiplier > 1f;
    }
}
