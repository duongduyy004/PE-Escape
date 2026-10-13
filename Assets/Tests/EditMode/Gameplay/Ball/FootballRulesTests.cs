using KMA.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace KMA.Tests.Gameplay.Ball
{
    public sealed class FootballRulesTests
    {
        static FootballRules NewRules() => new FootballRules(FootballTuning.For(FootballDifficulty.Normal));
        [Test]
        public void PreviewContract_ChargeStartsDirectlyFromAiming()
        {
            var rules = NewRules();
            Assert.That(rules.BeginCharge(), Is.False);
            rules.Start();
            Assert.That(rules.BeginCharge(), Is.True);
            Assert.That(rules.PreviewVisible, Is.True);
        }
        [Test]
        public void AimIsManualAndLockedOnlyWhileCharging()
        {
            var rules = NewRules(); rules.Start(); rules.SetAim(-.6f); rules.Tick(.5f);
            Assert.That(rules.AimX, Is.EqualTo(-.6f));
            rules.BeginCharge(); Assert.That(rules.SetAim(.9f), Is.False);
            rules.Tick(FootballTuning.For(FootballDifficulty.Normal).PowerRiseSeconds);
            Assert.That(rules.Power, Is.EqualTo(1f).Within(.0001f));
            rules.CancelCharge(); Assert.That(rules.State, Is.EqualTo(FootballState.Aiming));
            Assert.That(rules.PreviewVisible, Is.False); Assert.That(rules.Power, Is.Zero);
            Assert.That(rules.Kicks, Is.Zero); Assert.That(rules.SetAim(.4f), Is.True);
        }
        [Test]
        public void ShotWindowRunsAcrossAimingAndChargingAndTimesOut()
        {
            var rules = NewRules();
            rules.Start();
            rules.Tick(2.2f);
            Assert.That(rules.State, Is.EqualTo(FootballState.Aiming));
            Assert.That(rules.ShotTimeRemaining, Is.EqualTo(.8f).Within(.001f));
            Assert.That(rules.BeginCharge(), Is.True);
            rules.Tick(.8f);
            Assert.That(rules.State, Is.EqualTo(FootballState.ShotResult));
            Assert.That(rules.LastOutcome, Is.EqualTo(FootballOutcome.Timeout));
            Assert.That(rules.Kicks, Is.EqualTo(1));
        }
        [Test]
        public void ReleasingBeforeShotDeadlineProducesNormalShot()
        {
            var rules = NewRules();
            rules.Start();
            rules.Tick(1.5f);
            rules.SetAim(-.75f);
            rules.BeginCharge();
            rules.Tick(.5f);
            Assert.That(rules.ReleaseShot(), Is.True);
            Assert.That(rules.State, Is.EqualTo(FootballState.Kicking));
            Assert.That(rules.LastOutcome, Is.Null);
        }
        [Test]
        public void PredictionIsAvailableOnlyWhileHeld()
        {
            var rules = NewRules(); var points = new Vector3[140]; rules.Start();
            Assert.That(rules.GetPreview(points), Is.Zero);
            rules.BeginCharge();rules.Tick(1f);Assert.That(rules.GetPreview(points), Is.GreaterThan(2));
            Assert.That(rules.ReleaseShot(), Is.True);Assert.That(rules.GetPreview(points), Is.Zero);
            Assert.That(rules.ReleaseShot(), Is.False);
        }
        [TestCase(2,false,0f)] [TestCase(3,true,6f)] [TestCase(4,true,8f)] [TestCase(5,true,10f)]
        public void FiveKicksRequireThreeGoals(int goals, bool passed, float score)
        {
            // Very slow keeper isolates scoring from opponent difficulty.
            var rules = new FootballRules(new FootballTuning(2f, 10f, .1f)); rules.Start();
            for(int i=0;i<5;i++)
            {
                rules.SetAim(.55f);rules.BeginCharge();if(i<goals)rules.Tick(1f);
                rules.ReleaseShot();rules.Tick(20f);
                Assert.That(rules.Kicks, Is.EqualTo(i+1));
                Assert.That(rules.State, Is.EqualTo(i==4?FootballState.MatchResult:FootballState.Aiming));
            }
            Assert.That(rules.Goals, Is.EqualTo(goals));Assert.That(rules.BuildResult().Pass, Is.EqualTo(passed));
            Assert.That(rules.BuildResult().Score, Is.EqualTo(score));
            rules.Tick(50f);Assert.That(rules.Kicks, Is.EqualTo(5));
        }
        [Test]
        public void LargeAndSmallTicksProduceSamePowerAndFlight()
        {
            var a=NewRules();var b=NewRules();a.Start();b.Start();a.BeginCharge();b.BeginCharge();
            a.Tick(1f);for(int i=0;i<100;i++)b.Tick(.01f);
            Assert.That(a.Power,Is.EqualTo(b.Power).Within(.0001f));a.ReleaseShot();b.ReleaseShot();
            a.Tick(.68f);for(int i=0;i<68;i++)b.Tick(.01f);
            Assert.That(Vector3.Distance(a.Flight.Position,b.Flight.Position),Is.LessThan(.05f));
            a.Tick(20f);b.Tick(20f);Assert.That(a.Outcomes[0],Is.EqualTo(b.Outcomes[0]));
        }
        [Test]
        public void InvalidInputsDoNotMutateState()
        {
            var rules=NewRules();rules.Start();float aim=rules.AimX;
            Assert.That(rules.SetAim(float.NaN),Is.False);Assert.That(rules.SetAim(2f),Is.False);
            rules.Tick(float.NaN);rules.Tick(float.PositiveInfinity);rules.Tick(-1f);
            Assert.That(rules.AimX,Is.EqualTo(aim));Assert.That(rules.State,Is.EqualTo(FootballState.Aiming));
            Assert.That(()=>rules.BuildResult(),Throws.TypeOf<System.InvalidOperationException>());
        }
    }
}
