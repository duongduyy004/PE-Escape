using System;
using System.Linq;
using KMA.Gameplay;
using NUnit.Framework;
using UnityEditor;

namespace KMA.Tests.Gameplay.Running
{
    public sealed class RivalPaceProfileTests
    {
        const float MinSpeed = SprintController.DefaultMinRivalSpeed;
        const float MaxSpeed = SprintController.DefaultMaxRivalSpeed;
        const float FastestFinish = 12f;
        const float SlowestFinish = 28f;

        // Each race rolls the rivals' pace, but never so fast that a rival runs away from a
        // passing player, nor so slow that one is still crawling long after the field.
        [Test]
        public void RolledAuthoredRivalsStayWithinTheSpeedRangeAndFinishWindow()
        {
            RivalPaceProfile[] authored = LoadAuthoredProfiles();
            for (int seed = 0; seed < 200; seed++)
            {
                RivalPaceProfile[] field = RivalPaceRandomizer.RollField(authored, MinSpeed, MaxSpeed, new Random(seed));
                for (int i = 0; i < authored.Length; i++)
                {
                    RivalPaceProfile profile = authored[i];
                    RivalPaceProfile rolled = field[i];
                    Assert.That(rolled.SustainedSpeed, Is.InRange(MinSpeed, MaxSpeed));
                    Assert.That(rolled.Name, Is.EqualTo(profile.Name));
                    float finish = FinishTime(rolled);
                    Assert.That(finish, Is.InRange(FastestFinish, SlowestFinish), profile.Name + " seed " + seed);
                }
            }
        }

        [Test]
        public void ExtremeRollsHitTheFinishWindowEdges()
        {
            RivalPaceProfile[] authored = LoadAuthoredProfiles();
            float fastest = authored.Min(profile => FinishTime(AtSpeed(profile, MaxSpeed)));
            float slowest = authored.Max(profile => FinishTime(AtSpeed(profile, MinSpeed)));

            // With a real start the field crosses 150 m in about 15–17 s (fastest) to 18–20 s (slowest).
            Assert.That(fastest, Is.InRange(13.5f, 17f));
            Assert.That(slowest, Is.InRange(17f, SlowestFinish));
        }

        [Test]
        public void RollKeepsTheProfileShape()
        {
            var fastStart = new RivalPaceProfile("Fast Start", 9f, 6f);
            RivalPaceProfile rolled = RivalPaceRandomizer.Roll(fastStart, 5f, 7f, new Random(3));

            Assert.That(rolled.OpeningSpeed / rolled.SustainedSpeed, Is.EqualTo(1.5f).Within(1e-4f));
        }

        [Test]
        public void SameSeedRollsTheSamePace()
        {
            var profile = new RivalPaceProfile("Steady", 6f, 6f);
            RivalPaceProfile a = RivalPaceRandomizer.Roll(profile, 5f, 7f, new Random(42));
            RivalPaceProfile b = RivalPaceRandomizer.Roll(profile, 5f, 7f, new Random(42));

            Assert.That(a.SustainedSpeed, Is.EqualTo(b.SustainedSpeed));
            Assert.That(a.OpeningSpeed, Is.EqualTo(b.OpeningSpeed));
            for (float t = 0f; t < 30f; t += .25f)
                Assert.That(a.SpeedAt(t), Is.EqualTo(b.SpeedAt(t)));
        }

        [Test]
        public void InvertedRangeIsSwappedAndNullProfileStaysNull()
        {
            var profile = new RivalPaceProfile("Steady", 6f, 6f);
            RivalPaceProfile rolled = RivalPaceRandomizer.Roll(profile, 7f, 5f, new Random(1));

            Assert.That(rolled.SustainedSpeed, Is.InRange(5f, 7f));
            Assert.That(RivalPaceRandomizer.Roll(null, 5f, 7f, new Random(1)), Is.Null);
        }

        [Test]
        public void FieldRollGivesEveryRivalTheirOwnSpeedBand()
        {
            RivalPaceProfile[] authored = LoadAuthoredProfiles();
            float minGap = (MaxSpeed - MinSpeed) / authored.Length * .3f;
            for (int seed = 0; seed < 200; seed++)
            {
                float[] speeds = RivalPaceRandomizer.RollField(authored, MinSpeed, MaxSpeed, new Random(seed))
                    .Select(profile => profile.SustainedSpeed).OrderBy(speed => speed).ToArray();
                Assert.That(speeds, Has.All.InRange(MinSpeed, MaxSpeed));
                for (int i = 1; i < speeds.Length; i++)
                    Assert.That(speeds[i] - speeds[i - 1], Is.GreaterThanOrEqualTo(minGap - 1e-4f), "seed " + seed);
            }
        }

        [Test]
        public void FieldRollLeavesEmptySlotsEmptyAndSpreadsTheRest()
        {
            var steady = new RivalPaceProfile("Steady", 6f, 6f);
            RivalPaceProfile[] field = RivalPaceRandomizer.RollField(new[] { steady, null, steady }, 5f, 7f,
                new Random(9));

            Assert.That(field[1], Is.Null);
            Assert.That(Math.Abs(field[0].SustainedSpeed - field[2].SustainedSpeed), Is.GreaterThan(.3f));
        }

        [Test]
        public void RolledPaceSurgesAndFadesAroundTheSustainedSpeedAfterTheStart()
        {
            RivalPaceProfile rolled = RivalPaceRandomizer.Roll(new RivalPaceProfile("Steady", 6f, 6f), 6f, 6f,
                new Random(5));
            float fastest = float.MinValue, slowest = float.MaxValue;
            for (float t = RivalPaceProfile.OpeningSeconds + .01f; t < 12f; t += .05f)
            {
                fastest = Math.Max(fastest, rolled.SpeedAt(t));
                slowest = Math.Min(slowest, rolled.SpeedAt(t));
            }

            Assert.That(rolled.SpeedAt(1f), Is.EqualTo(6f));
            Assert.That(fastest, Is.GreaterThan(6f * 1.08f));
            Assert.That(slowest, Is.LessThan(6f * .85f));
        }

        // The pace is irregular rather than a smooth wave, and rivals surge more often than they fade.
        [Test]
        public void RolledPaceSurgesMoreOftenThanItFades()
        {
            int surges = 0, fades = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                RivalPaceProfile rolled = RivalPaceRandomizer.Roll(new RivalPaceProfile("Steady", 6f, 6f), 6f, 6f,
                    new Random(seed));
                Assert.That(rolled.PaceSegmentCount, Is.GreaterThan(1));
                float drift = 0f;
                for (int i = 0; i < rolled.PaceSegmentCount; i++)
                {
                    PaceSegment segment = rolled.GetPaceSegment(i);
                    Assert.That(segment.Seconds, Is.InRange(RivalPaceRandomizer.MinSegmentSeconds,
                        RivalPaceRandomizer.MaxSegmentSeconds));
                    if (segment.IsSurge) surges++;
                    else fades++;
                    drift += (segment.Multiplier - 1f) * segment.Seconds;
                    Assert.That(Math.Abs(drift), Is.LessThan(1.5f), "seed " + seed);
                }
            }
            Assert.That(surges, Is.GreaterThan(fades * 1.4f));
        }

        [Test]
        public void EveryRivalCanBeTheFastestAcrossSeeds()
        {
            RivalPaceProfile[] authored = LoadAuthoredProfiles();
            var fastest = new int[authored.Length];
            for (int seed = 0; seed < 300; seed++)
            {
                RivalPaceProfile[] field = RivalPaceRandomizer.RollField(authored, MinSpeed, MaxSpeed, new Random(seed));
                int best = 0;
                for (int i = 1; i < field.Length; i++)
                    if (field[i].SustainedSpeed > field[best].SustainedSpeed) best = i;
                fastest[best]++;
            }
            Assert.That(fastest, Has.All.GreaterThan(30), string.Join(",", fastest));
        }

        [Test]
        public void OpeningSpeedDiffersBetweenSeeds()
        {
            RivalPaceProfile[] authored = LoadAuthoredProfiles();
            var openings = Enumerable.Range(0, 20).Select(seed =>
                RivalPaceRandomizer.RollField(authored, MinSpeed, MaxSpeed, new Random(seed))[0].OpeningSpeed)
                .Distinct().Count();
            Assert.That(openings, Is.GreaterThan(10));
        }

        static RivalPaceProfile[] LoadAuthoredProfiles() => SprintRivalMappings.Required
            .Select(mapping => AssetDatabase.LoadAssetAtPath<RivalPaceProfileAsset>(mapping.ProfilePath))
            .Select(asset =>
            {
                Assert.That(asset, Is.Not.Null);
                return asset.ToRuntime();
            })
            .ToArray();

        static RivalPaceProfile AtSpeed(RivalPaceProfile profile, float sustained) =>
            RivalPaceRandomizer.Roll(profile, sustained, sustained, new Random(0));

        static float FinishTime(RivalPaceProfile profile)
        {
            var rules = new SprintRules(60f, new[] { profile });
            while (!rules.HasRivalFinished(0) && rules.Elapsed < 60f)
                rules.Tick(1f / 120f);
            return rules.GetRivalFinishTime(0);
        }

        [Test]
        public void RolledRivalsCarryTheirOwnMotionTraits()
        {
            RivalPaceProfile[] field = RivalPaceRandomizer.RollField(LoadAuthoredProfiles(), MinSpeed, MaxSpeed,
                new Random(7));
            foreach (RivalPaceProfile rolled in field)
                Assert.That(rolled.Traits.AccelerationTau, Is.InRange(RivalMotionTraits.MinTau, RivalMotionTraits.MaxTau));
            Assert.That(field.Select(rolled => rolled.Traits.ReactionSeconds).Distinct().Count(), Is.EqualTo(field.Length),
                "each rival reacts to the gun on their own");
        }
    }
}
