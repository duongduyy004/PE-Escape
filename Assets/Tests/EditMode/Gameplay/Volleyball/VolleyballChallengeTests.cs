using KMA.Gameplay;
using KMA.Gameplay.Volleyball;
using NUnit.Framework;
using UnityEngine;

namespace KMA.Tests.Gameplay.Volleyball
{
    public sealed class VolleyballChallengeTests
    {
        const float Step = 1f / 60f;

        [Test]
        public void LearnAssetDefinesAllSevenContactsAcrossThreeDrills()
        {
            var rules = new VolleyballChallengeRules(ChallengeCatalog.LoadDefault().Get("volleyball_learn"));
            Assert.That(rules.Match.WinningPoints, Is.EqualTo(0));
            Assert.That(rules.Match.ClockLimit, Is.EqualTo(0f));
            Assert.That(rules.CompletedTargets, Is.EqualTo(0));
            Assert.That(ChallengeCatalog.LoadDefault().Get("volleyball_learn").TargetCount, Is.EqualTo(VolleyballChallengeRules.LearnTargetTotal));
            Assert.That(rules.LearnStage, Is.EqualTo(VolleyballLearnStage.Receive));
            Assert.That(rules.Match.Server, Is.EqualTo(CourtSide.Opponent));
        }


        [Test]
        public void LearnCountsOnlyResolverContactsAndWaitsForEachPresentationBeforeRefeding()
        {
            var rules = new VolleyballChallengeRules(ChallengeCatalog.LoadDefault().Get("volleyball_learn"));
            VolleyballMatch match = rules.Match;

            // A button press without a reachable ball is not a successful receive.
            match.Tick(1f / 60f);
            Assert.That(rules.PressAction().Kind, Is.EqualTo(ActionKind.None));
            Assert.That(rules.CompletedTargets, Is.Zero);

            for (int i = 0; i < VolleyballChallengeRules.LearnReceiveTarget; i++)
            {
                ActionDecision receive = Receive(rules);
                Assert.That(receive.Kind, Is.EqualTo(ActionKind.Receive));
                Assert.That(rules.CompletedTargets, Is.EqualTo(i + 1));
                Assert.That(rules.IsPresentationPending, Is.True);
                Assert.That(rules.IsComplete, Is.False);
                Assert.That(match.BallState, Is.EqualTo(BallState.InPlay), "contact flight remains visible");
                Assert.That(rules.PressAction().Kind, Is.EqualTo(ActionKind.None), "pending contacts cannot spam the target");
                rules.Tick(VolleyballChallengeRules.LearnPresentationSeconds);
                Assert.That(rules.IsPresentationPending, Is.False);
            }

            Assert.That(rules.LearnStage, Is.EqualTo(VolleyballLearnStage.Serve));
            Assert.That(match.Server, Is.EqualTo(CourtSide.Player));
            Assert.That(rules.LearnProgressText, Is.EqualTo("PHÁT BÓNG 0/2"));

            for (int i = 0; i < VolleyballChallengeRules.LearnServeTarget; i++)
            {
                Assert.That(rules.PressAction().Kind, Is.EqualTo(ActionKind.ServeToss));
                Assert.That(MatchDriver.AdvanceUntil(match, () => match.BallState == BallState.Toss, 1f), Is.True);
                MatchDriver.AdvanceToFlightTime(match, match.Flight.ApexTime);
                Assert.That(rules.PressAction().Kind, Is.EqualTo(ActionKind.ServeHit));
                Assert.That(rules.CompletedTargets, Is.EqualTo(VolleyballChallengeRules.LearnReceiveTarget + i + 1));
                Assert.That(rules.IsPresentationPending, Is.True);
                rules.Tick(VolleyballChallengeRules.LearnPresentationSeconds);
            }

            Assert.That(rules.LearnStage, Is.EqualTo(VolleyballLearnStage.JumpHit));
            Assert.That(match.Server, Is.EqualTo(CourtSide.Opponent));
            Assert.That(rules.LearnProgressText, Is.EqualTo("NHẢY & ĐÁNH 0/2"));

            for (int i = 0; i < VolleyballChallengeRules.LearnJumpHitTarget; i++)
            {
                Assert.That(MatchDriver.AdvanceUntil(match, () => match.BallState == BallState.InPlay, 3f), Is.True);
                float receiveIdeal = match.Flight.TimeAtHeightDescending(ActionResolver.ReceiveContactHeight);
                match.Player.PlaceAt(match.Flight.GroundAt(receiveIdeal));
                MatchDriver.AdvanceToFlightTime(match, receiveIdeal);
                Assert.That(rules.PressAction().Kind, Is.EqualTo(ActionKind.Receive),
                    "the drill feed needs a legal first touch before the jump hit");

                float smashIdeal = match.Flight.TimeAtHeightDescending(ActionResolver.SmashContactHeight);
                match.Player.PlaceAt(match.Flight.GroundAt(smashIdeal));
                MatchDriver.AdvanceToFlightTime(match, smashIdeal - .35f);
                Assert.That(rules.PressJump(), Is.True);
                MatchDriver.AdvanceToFlightTime(match, smashIdeal);
                Assert.That(rules.PressAction().Kind, Is.EqualTo(ActionKind.Smash));
                Assert.That(rules.CompletedTargets, Is.EqualTo(VolleyballChallengeRules.LearnReceiveTarget +
                    VolleyballChallengeRules.LearnServeTarget + i + 1));
                Assert.That(rules.IsComplete, Is.False, "final contact still needs visible follow-through and landing");
                rules.Tick(VolleyballChallengeRules.LearnPresentationSeconds);
            }

            Assert.That(rules.LearnStage, Is.EqualTo(VolleyballLearnStage.Complete));
            Assert.That(rules.IsComplete, Is.True);
            Assert.That(rules.BuildResult(new ChallengeAttemptContext("learn", "volleyball_learn",
                ChallengeAttemptMode.Journey, ChallengeDifficulty.Easy)).Pass, Is.True);
        }

        static ActionDecision Receive(VolleyballChallengeRules rules)
        {
            VolleyballMatch match = rules.Match;
            Assert.That(MatchDriver.AdvanceUntil(match, () => match.BallState == BallState.InPlay, 3f), Is.True);
            float ideal = match.Flight.TimeAtHeightDescending(ActionResolver.ReceiveContactHeight);
            match.Player.PlaceAt(match.Flight.GroundAt(ideal));
            MatchDriver.AdvanceToFlightTime(match, ideal);
            return rules.PressAction();
        }

        [Test]
        public void PracticeHasNoClockAndFailsOnceTheOpponentReachesFivePoints()
        {
            var rules = new VolleyballChallengeRules(ChallengeCatalog.LoadDefault().Get("volleyball_practice"));
            Assert.That(rules.Match.WinningPoints, Is.EqualTo(VolleyballMatch.PointsToWin));
            Assert.That(rules.Match.ClockLimit, Is.Zero);

            // An idle player hands every rally to the opponent.
            for (float t = 0f; t < 300f && !rules.IsComplete; t += Step) rules.Tick(Step);

            Assert.That(rules.IsComplete, Is.True);
            Assert.That(rules.Match.OpponentPoints, Is.EqualTo(VolleyballMatch.PointsToWin));
            Assert.That(rules.BuildResult(Practice()).Pass, Is.False);
        }

        [Test]
        public void PracticeContinuesBeyondTwoMinutesUntilAPointTargetIsReached()
        {
            var rules = new VolleyballChallengeRules(ChallengeCatalog.LoadDefault().Get("volleyball_practice"));
            for (int i = 0; i < 150 * 60; i++)
            {
                rules.Match.SetScoreForTest(4, 0);
                rules.Tick(Step);
            }
            Assert.That(rules.IsComplete, Is.False);
            Assert.That(rules.Match.Elapsed, Is.GreaterThan(120f));
        }

        [Test]
        public void ExamHasNoClockAndContinuesUntilAPointTargetIsReached()
        {
            var rules = new VolleyballChallengeRules(ChallengeCatalog.LoadDefault().Get("volleyball_exam"));
            for (int i = 0; i < 150 * 60; i++)
            {
                rules.Match.SetScoreForTest(9, 0);
                rules.Tick(Step);
            }
            Assert.That(rules.IsComplete, Is.False);
            Assert.That(rules.Match.Elapsed, Is.GreaterThan(120f));
        }

        static ChallengeAttemptContext Practice() => new ChallengeAttemptContext("attempt", "volleyball_practice",
            ChallengeAttemptMode.Journey, ChallengeDifficulty.Easy);

        [Test]
        public void ExamEndsEarlyWhenTheOpponentReachesTenPoints()
        {
            var rules = new VolleyballChallengeRules(ChallengeCatalog.LoadDefault().Get("volleyball_exam"));
            Assert.That(rules.Match.WinningPoints, Is.EqualTo(10));
            Assert.That(rules.Match.ClockLimit, Is.Zero);

            // The player serves first; standing still must not hold the ball until a deadline.
            for (float t = 0f; t < 600f && !rules.IsComplete; t += Step) rules.Tick(Step);

            Assert.That(rules.Match.IsOver, Is.True);
            Assert.That(rules.Match.OpponentPoints, Is.EqualTo(10), "The match ends on points, not on a clock.");
            Assert.That(rules.BuildResult(new ChallengeAttemptContext("attempt", "volleyball_exam",
                ChallengeAttemptMode.Journey, ChallengeDifficulty.Normal)).Pass, Is.False);
        }

        [TestCase("volleyball_practice")]
        [TestCase("volleyball_exam")]
        public void OpponentGetsTheNextServeAfterScoring(string id)
        {
            var rules = new VolleyballChallengeRules(ChallengeCatalog.LoadDefault().Get(id));
            Assert.That(rules.Match.Server, Is.EqualTo(CourtSide.Player));
            Assert.That(MatchDriver.AdvanceUntil(rules.Match,
                () => rules.Match.OpponentPoints == 1, 10f), Is.True);
            Assert.That(rules.Match.Server, Is.EqualTo(CourtSide.Opponent));
            Assert.That(MatchDriver.AdvanceUntil(rules.Match,
                () => rules.Match.BallState == BallState.Held, 2f), Is.True);
            Assert.That(rules.Match.Server, Is.EqualTo(CourtSide.Opponent));
        }

        [Test]
        public void TenPointsCanBeWonWithinNinetySecondsWithOneReceiveThenSmash()
        {
            var match = new VolleyballMatch(options: new VolleyballMatchOptions(10, 90f,
                requirePointsToWin: true));
            const float step = .005f;
            for (int frame = 0; frame < 90 * 200 && !match.IsOver; frame++)
            {
                match.SetMove(Vector2.zero);
                if (match.Server == CourtSide.Player && match.BallState == BallState.Held)
                    match.PressAction();
                else if (match.Server == CourtSide.Player && match.BallState == BallState.Toss &&
                    Mathf.Abs(match.FlightTime - match.Flight.ApexTime) <= step)
                    match.PressAction();
                else if (match.BallState == BallState.InPlay && match.Rally.Possession == CourtSide.Player)
                {
                    bool smash = match.Rally.Touches >= 1;
                    float ideal = match.Flight.TimeAtHeightDescending(smash
                        ? ActionResolver.SmashContactHeight : ActionResolver.ReceiveContactHeight);
                    if (Mathf.Abs(match.FlightTime - ideal) <= step)
                    {
                        // Deterministic court position, as in the existing rally tests;
                        // ball flight, touches, scoring and deadline remain production behavior.
                        match.Player.PlaceAt(match.Flight.GroundAt(ideal));
                        if (smash)
                        {
                            match.SetMove(new Vector2(1f, 1f));
                            match.PressJump();
                        }
                        match.PressAction();
                    }
                }
                match.Tick(step);
            }
            Assert.That(match.PlayerPoints, Is.EqualTo(10),
                $"Score {match.PlayerPoints}:{match.OpponentPoints} at {match.Elapsed:F2}s");
            Assert.That(match.Elapsed, Is.LessThan(90f));
            Assert.That(match.BuildResult().Pass, Is.True);
        }

        [Test]
        public void AnUnplayedPlayerServeTossesItselfAndCostsThePoint()
        {
            var match = new VolleyballMatch(options: new VolleyballMatchOptions(5, 0f));
            Assert.That(match.Server, Is.EqualTo(CourtSide.Player));
            for (float t = 0f; t < VolleyballMatch.PlayerServeTimeout - .1f; t += Step) match.Tick(Step);
            Assert.That(match.BallState, Is.EqualTo(BallState.Held), "The player gets time to serve.");

            for (float t = 0f; t < 10f && match.OpponentPoints == 0; t += Step) match.Tick(Step);
            Assert.That(match.OpponentPoints, Is.EqualTo(1));
        }

        [TestCase("volleyball_practice", 5)]
        [TestCase("volleyball_exam", 10)]
        public void PlayerWinsOnTargetPointAndGetsTheNextServe(string id, int target)
        {
            var definition = ChallengeCatalog.LoadDefault().Get(id);
            var rules = new VolleyballChallengeRules(definition);
            VolleyballMatch match = rules.Match;
            Assert.That(match.Server, Is.EqualTo(CourtSide.Player));
            match.SetScoreForTest(target - 1, 0);
            match.ForceServerForTest(CourtSide.Opponent);
            Assert.That(MatchDriver.AdvanceUntil(match, () => match.BallState == BallState.InPlay, 3f), Is.True);
            for (int touch = 0; touch < 2; touch++)
            {
                float ideal = match.Flight.TimeAtHeightDescending(ActionResolver.ReceiveContactHeight);
                match.Player.PlaceAt(match.Flight.GroundAt(ideal));
                MatchDriver.AdvanceToFlightTime(match, ideal);
                Assert.That(rules.PressAction().Kind, Is.EqualTo(ActionKind.Receive), $"touch {touch + 1}");
            }

            float smashIdeal = match.Flight.TimeAtHeightDescending(ActionResolver.SmashContactHeight);
            match.Player.PlaceAt(match.Flight.GroundAt(smashIdeal));
            MatchDriver.AdvanceToFlightTime(match, smashIdeal);
            Assert.That(rules.PressJump(), Is.True);
            rules.SetMove(Vector2.up);
            Assert.That(rules.PressAction().Kind, Is.EqualTo(ActionKind.Smash));
            rules.SetMove(Vector2.zero);

            Assert.That(MatchDriver.AdvanceUntil(match, () => match.BallState == BallState.Dead, 3f), Is.True);
            Assert.That(match.PlayerPoints, Is.EqualTo(target));
            Assert.That(match.Server, Is.EqualTo(CourtSide.Player));
            if (definition.Kind == ChallengeKind.Practice)
                Assert.That(rules.CompletedTargets, Is.EqualTo(target));
            Assert.That(rules.IsComplete, Is.True);
            Assert.That(rules.BuildResult(new ChallengeAttemptContext("attempt", id,
                ChallengeAttemptMode.Journey, definition.Difficulty)).Pass, Is.True);
        }
    }
}
