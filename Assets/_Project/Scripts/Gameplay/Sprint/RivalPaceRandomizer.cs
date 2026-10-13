using System;

namespace KMA.Gameplay
{
    public static class RivalPaceRandomizer
    {
        // Mid-race the pace is a run of random segments: surges are frequent but mild, fades are
        // rarer but deeper, so rivals speed up more often than they slow down while their
        // average stays on the rolled sustained speed.
        public const float SurgeChance = .65f;
        public const float MinSurge = .08f, MaxSurge = .22f;
        public const float MinFade = .2f, MaxFade = .35f;
        public const float MinSegmentSeconds = .8f, MaxSegmentSeconds = 2.2f;
        // Longer than any race plus the wait for stragglers; the schedule repeats after this.
        public const float ScheduleSeconds = 40f;
        // Once a rival is this many "seconds at sustained speed" ahead of or behind their average,
        // the next segment pulls them back, so luck never decides a whole race.
        const float MaxDrift = .6f;
        // Each rival rolls inside the middle of their own band, so neighbouring bands never touch.
        const float BandMargin = .15f;

        // Rolls a sustained speed in [minSpeed, maxSpeed] and scales the opening speed with it,
        // so a fast starter still bursts off the line and a finisher still closes strong.
        public static RivalPaceProfile Roll(RivalPaceProfile authored, float minSpeed, float maxSpeed, Random random)
        {
            if (authored == null) return null;
            if (minSpeed > maxSpeed) (minSpeed, maxSpeed) = (maxSpeed, minSpeed);

            float openingRatio = authored.SustainedSpeed > 0f ? authored.OpeningSpeed / authored.SustainedSpeed : 1f;
            float sustained = minSpeed + (maxSpeed - minSpeed) * (float)random.NextDouble();
            return new RivalPaceProfile(authored.Name, sustained * openingRatio, sustained, RollSchedule(random));
        }

        public static PaceSegment[] RollSchedule(Random random)
        {
            var segments = new System.Collections.Generic.List<PaceSegment>();
            float total = 0f, drift = 0f;
            while (total < ScheduleSeconds)
            {
                bool surge = drift > MaxDrift ? false
                    : drift < -MaxDrift ? true
                    : random.NextDouble() < SurgeChance;
                float change = surge
                    ? Range(random, MinSurge, MaxSurge)
                    : -Range(random, MinFade, MaxFade);
                float seconds = Range(random, MinSegmentSeconds, MaxSegmentSeconds);
                segments.Add(new PaceSegment(seconds, 1f + change));
                drift += change * seconds;
                total += seconds;
            }
            return segments.ToArray();
        }

        static float Range(Random random, float min, float max) => min + (max - min) * (float)random.NextDouble();

        // Rolls the whole field at once: the speed range is split into one band per rival and the
        // bands are shuffled, so every race has a clearly fast, middle and slow rival.
        public static RivalPaceProfile[] RollField(RivalPaceProfile[] authored, float minSpeed, float maxSpeed,
            Random random)
        {
            if (authored == null || authored.Length == 0) return Array.Empty<RivalPaceProfile>();
            if (minSpeed > maxSpeed) (minSpeed, maxSpeed) = (maxSpeed, minSpeed);

            // Empty slots stay empty and take no band, so the real rivals still spread across the range.
            int count = 0;
            foreach (RivalPaceProfile profile in authored)
                if (profile != null) count++;
            var rolled = new RivalPaceProfile[authored.Length];
            if (count == 0) return rolled;

            var bands = new int[count];
            for (int i = 0; i < count; i++) bands[i] = i;
            for (int i = count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (bands[i], bands[j]) = (bands[j], bands[i]);
            }

            float width = (maxSpeed - minSpeed) / count;
            for (int i = 0, band = 0; i < authored.Length; i++)
            {
                if (authored[i] == null) continue;
                float low = minSpeed + width * (bands[band] + BandMargin);
                float high = minSpeed + width * (bands[band] + 1 - BandMargin);
                rolled[i] = Roll(authored[i], low, high, random);
                band++;
            }
            return rolled;
        }
    }
}
