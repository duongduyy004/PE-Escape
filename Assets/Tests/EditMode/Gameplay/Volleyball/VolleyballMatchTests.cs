using KMA.Gameplay;
using KMA.Gameplay.Volleyball;
using NUnit.Framework;
using UnityEngine;

namespace KMA.Tests.Gameplay.Volleyball
{
    public sealed class VolleyballMatchTests
    {
        static VolleyballMatch FrozenOpponentMatch() => new VolleyballMatch(null, new OpponentTuning(0f, .25f));
        static VolleyballMatch TimedMatch() =>
            new VolleyballMatch(options: new VolleyballMatchOptions(VolleyballMatch.PointsToWin, VolleyballMatch.TimeLimit));

        // Opponent serve received perfectly; returns with the set in the air and the player
        // standing on the smash contact point.
        static VolleyballMatch ReceivedAndUnderTheSet(out float smashIdeal)
        {
            var match = FrozenOpponentMatch();
            match.ForceServerForTest(CourtSide.Opponent);
            Assert.That(MatchDriver.AdvanceUntil(match, () => match.BallState == BallState.InPlay, 3f), Is.True);
            float receiveIdeal = match.Flight.TimeAtHeightDescending(ActionResolver.ReceiveContactHeight);
            match.Player.PlaceAt(match.Flight.GroundAt(receiveIdeal));
            MatchDriver.AdvanceToFlightTime(match, receiveIdeal);
            Assert.That(match.PressAction().Kind, Is.EqualTo(ActionKind.Receive));
            smashIdeal = match.Flight.TimeAtHeightDescending(ActionResolver.SmashContactHeight);
            match.Player.PlaceAt(match.Flight.GroundAt(smashIdeal));
            return match;
        }

        [Test]
        public void MatchStartsWithThePlayerHoldingTheServe()
        {
            var match = new VolleyballMatch();
            Assert.That(match.Server, Is.EqualTo(CourtSide.Player));
            Assert.That(match.BallState, Is.EqualTo(BallState.Held));
            Assert.That(match.Player.Position, Is.EqualTo(VolleyballMatch.PlayerServeSpot));
            Assert.That(match.Opponent.Position, Is.EqualTo(VolleyballMatch.OpponentReadySpot));
            Assert.That(match.BallGround, Is.EqualTo(VolleyballMatch.PlayerServeSpot));
            Assert.That(match.BallHeight, Is.EqualTo(VolleyballMatch.TossStartHeight));
            Assert.That(match.ClockLimit, Is.Zero, "A match is won on points, not on a clock.");
        }

        [Test]
        public void ServerMovesOnlyAlongTheBaseline()
        {
            var match = new VolleyballMatch();
            match.SetMove(new Vector2(1f, 1f));
            MatchDriver.Advance(match, .2f);
            Assert.That(match.Player.Position.x, Is.EqualTo(VolleyballMatch.PlayerServeSpot.x));
            Assert.That(match.Player.Position.y, Is.GreaterThan(0f));
        }

        [Test]
        public void PerfectServeFliesFastToTheAimedSpot()
        {
            var match = FrozenOpponentMatch();
            match.PressAction();
            Assert.That(match.BallState, Is.EqualTo(BallState.Toss));
            MatchDriver.AdvanceToFlightTime(match, match.Flight.ApexTime);
            match.SetMove(Vector2.left);
            ActionDecision serve = match.PressAction();

            Assert.That(serve.Kind, Is.EqualTo(ActionKind.ServeHit));
            Assert.That(serve.Grade, Is.EqualTo(TimingGrade.Perfect));
            Assert.That(match.BallState, Is.EqualTo(BallState.InPlay));
            Assert.That(match.Flight.Target, Is.EqualTo(new Vector2(3f, 0f)));
            Assert.That(match.Flight.ClearsNet, Is.True);
            Assert.That(match.Rally.Possession, Is.EqualTo(CourtSide.Opponent));
            Assert.That(match.TimedPresses, Is.EqualTo(1));
            Assert.That(match.QualitySum, Is.EqualTo(1f));
        }

        [Test]
        public void GoodServeIsTheSafeHighServe()
        {
            var match = FrozenOpponentMatch();
            ActionDecision serve = MatchDriver.ServeGood(match);
            Assert.That(serve.Grade, Is.EqualTo(TimingGrade.Good));
            Assert.That(match.Flight.Target, Is.EqualTo(new Vector2(5f, 0f)));
        }

        [Test]
        public void DroppedTossGivesThePointAndServeAway()
        {
            var match = new VolleyballMatch();
            match.PressAction();
            MatchDriver.Advance(match, match.Flight.Duration + .05f);
            Assert.That(match.OpponentPoints, Is.EqualTo(1));
            Assert.That(match.Server, Is.EqualTo(CourtSide.Opponent));
            Assert.That(match.BallState, Is.EqualTo(BallState.Dead));
        }

        [Test]
        public void UnreturnedServeScoresForTheServer()
        {
            var match = FrozenOpponentMatch();
            Assert.That(MatchDriver.ServePerfectAce(match).Grade, Is.EqualTo(TimingGrade.Perfect));
            Assert.That(MatchDriver.AdvanceUntil(match, () => match.BallState == BallState.Dead, 3f), Is.True);
            Assert.That(match.PlayerPoints, Is.EqualTo(1));
            Assert.That(match.Winners, Is.Zero);
            Assert.That(match.Server, Is.EqualTo(CourtSide.Player));
        }

        [Test]
        public void OpponentServesItsPlanTargetAfterTheDelay()
        {
            var match = FrozenOpponentMatch();
            match.ForceServerForTest(CourtSide.Opponent);
            MatchDriver.Advance(match, VolleyballMatch.OpponentServeDelay + .01f);
            Assert.That(match.BallState, Is.EqualTo(BallState.Toss));
            Assert.That(MatchDriver.AdvanceUntil(match, () => match.BallState == BallState.InPlay, 2f), Is.True);
            Assert.That(match.Flight.Target, Is.EqualTo(OpponentPlan.Authored().Current.ServeTarget));
            Assert.That(match.Flight.Hitter, Is.EqualTo(CourtSide.Opponent));
            Assert.That(match.Plan.Index, Is.EqualTo(1));

            Assert.That(MatchDriver.AdvanceUntil(match, () => match.BallState == BallState.Dead, 3f), Is.True);
            Assert.That(match.OpponentPoints, Is.EqualTo(1));
        }

        [Test]
        public void NextPointStartsAfterThePause()
        {
            var match = FrozenOpponentMatch();
            match.ForceServerForTest(CourtSide.Opponent);
            Assert.That(MatchDriver.AdvanceUntil(match, () => match.BallState == BallState.Dead, 6f), Is.True);
            MatchDriver.Advance(match, VolleyballMatch.PointPause + .01f);
            Assert.That(match.BallState, Is.EqualTo(BallState.Held));
            Assert.That(match.Opponent.Position, Is.EqualTo(VolleyballMatch.OpponentServeSpot));
            Assert.That(match.Player.Position, Is.EqualTo(VolleyballMatch.PlayerReadySpot));
        }

        [Test]
        public void ReceiveThenSmashWinsThePointAsAWinner()
        {
            var match = FrozenOpponentMatch();
            match.ForceServerForTest(CourtSide.Opponent);
            Assert.That(MatchDriver.AdvanceUntil(match, () => match.BallState == BallState.InPlay, 3f), Is.True);

            float receiveIdeal = match.Flight.TimeAtHeightDescending(ActionResolver.ReceiveContactHeight);
            Vector2 contact = match.Flight.GroundAt(receiveIdeal);
            match.Player.PlaceAt(contact);
            MatchDriver.AdvanceToFlightTime(match, receiveIdeal);
            ActionDecision receive = match.PressAction();
            Assert.That(receive.Kind, Is.EqualTo(ActionKind.Receive));
            Assert.That(receive.Grade, Is.EqualTo(TimingGrade.Perfect));
            Assert.That(match.Flight.Target, Is.EqualTo(new Vector2(VolleyballMatch.PlayerSetSpot.x, Mathf.Clamp(contact.y, -3f, 3f))));
            Assert.That(match.Rally.Touches, Is.EqualTo(1));

            float smashIdeal = match.Flight.TimeAtHeightDescending(ActionResolver.SmashContactHeight);
            match.Player.PlaceAt(match.Flight.GroundAt(smashIdeal));
            MatchDriver.AdvanceToFlightTime(match, smashIdeal);
            ActionDecision smash = MatchDriver.JumpSmash(match, Vector2.right);
            Assert.That(smash.Kind, Is.EqualTo(ActionKind.Smash));
            Assert.That(smash.Grade, Is.EqualTo(TimingGrade.Perfect));
            Assert.That(match.Flight.Target, Is.EqualTo(new Vector2(7.5f, 0f)));
            Assert.That(match.Rally.Possession, Is.EqualTo(CourtSide.Opponent));

            Assert.That(MatchDriver.AdvanceUntil(match, () => match.BallState == BallState.Dead, 3f), Is.True);
            Assert.That(match.PlayerPoints, Is.EqualTo(1));
            Assert.That(match.Winners, Is.EqualTo(1));
        }

        [Test]
        public void EarlyLateSmashDumpsTheBallOnTheOwnSide()
        {
            var match = FrozenOpponentMatch();
            match.ForceServerForTest(CourtSide.Opponent);
            Assert.That(MatchDriver.AdvanceUntil(match, () => match.BallState == BallState.InPlay, 3f), Is.True);
            float receiveIdeal = match.Flight.TimeAtHeightDescending(ActionResolver.ReceiveContactHeight);
            match.Player.PlaceAt(match.Flight.GroundAt(receiveIdeal));
            MatchDriver.AdvanceToFlightTime(match, receiveIdeal);
            match.PressAction();

            float smashIdeal = match.Flight.TimeAtHeightDescending(ActionResolver.SmashContactHeight);
            match.Player.PlaceAt(match.Flight.GroundAt(smashIdeal));
            MatchDriver.AdvanceToFlightTime(match, smashIdeal - .35f);
            ActionDecision smash = MatchDriver.JumpSmash(match, Vector2.zero);
            Assert.That(smash.Kind, Is.EqualTo(ActionKind.Smash));
            Assert.That(smash.Grade, Is.EqualTo(TimingGrade.Late));
            Assert.That(CourtSpace.SideOf(match.Flight.Target), Is.EqualTo(CourtSide.Player));

            Assert.That(MatchDriver.AdvanceUntil(match, () => match.BallState == BallState.Dead, 3f), Is.True);
            Assert.That(match.OpponentPoints, Is.EqualTo(1));
        }

        [Test]
        public void LateLatePressStillSmashesAsTheDocumentedSoftLob()
        {
            var match = FrozenOpponentMatch();
            match.ForceServerForTest(CourtSide.Opponent);
            Assert.That(MatchDriver.AdvanceUntil(match, () => match.BallState == BallState.InPlay, 3f), Is.True);
            float receiveIdeal = match.Flight.TimeAtHeightDescending(ActionResolver.ReceiveContactHeight);
            match.Player.PlaceAt(match.Flight.GroundAt(receiveIdeal));
            MatchDriver.AdvanceToFlightTime(match, receiveIdeal);
            match.PressAction();

            // Pressed +0.33 s past the ideal smash moment: inside the LATE window (<=0.40 s)
            // but, before the fix, HeightAt(pressTime) had already dropped below SmashMinHeight,
            // silently falling through instead of smashing.
            float smashIdeal = match.Flight.TimeAtHeightDescending(ActionResolver.SmashContactHeight);
            match.Player.PlaceAt(match.Flight.GroundAt(smashIdeal));
            MatchDriver.AdvanceToFlightTime(match, smashIdeal + .33f);
            ActionDecision smash = MatchDriver.JumpSmash(match, Vector2.right);

            Assert.That(smash.Kind, Is.EqualTo(ActionKind.Smash));
            Assert.That(smash.Grade, Is.EqualTo(TimingGrade.Late));
            Assert.That(match.Flight.Target, Is.EqualTo(new Vector2(7.5f, 0f)));
            Assert.That(CourtSpace.SideOf(match.Flight.Target), Is.EqualTo(CourtSide.Opponent));
        }

        // The set falls fast past the contact height: a press graded PERFECT or GOOD on the late
        // side must still be hit from the jump's reach, not from the ball's lower height, or it
        // would bury itself in the net under a PERFECT/GOOD call-out.
        [TestCase(.09f, 1f, TimingGrade.Perfect)]
        [TestCase(.09f, -1f, TimingGrade.Perfect)]
        [TestCase(.2f, 1f, TimingGrade.Good)]
        [TestCase(.2f, -1f, TimingGrade.Good)]
        public void LateSidePerfectOrGoodSmashClearsTheNetAndLandsOnTheAim(float offset, float stickX,
            TimingGrade expected)
        {
            var match = ReceivedAndUnderTheSet(out float smashIdeal);
            MatchDriver.AdvanceToFlightTime(match, smashIdeal + offset);
            var stick = new Vector2(stickX, 0f);
            ActionDecision smash = MatchDriver.JumpSmash(match, stick);

            Assert.That(smash.Kind, Is.EqualTo(ActionKind.Smash));
            Assert.That(smash.Grade, Is.EqualTo(expected));
            Assert.That(match.Flight.Target, Is.EqualTo(VolleyballMatch.SmashAim(stick)));
            Assert.That(match.Flight.ClearsNet, Is.True);

            Assert.That(MatchDriver.AdvanceUntil(match, () => match.BallState == BallState.Dead, 3f), Is.True);
            Assert.That(match.PlayerPoints, Is.EqualTo(1));
            Assert.That(match.OpponentPoints, Is.Zero);
        }

        [Test]
        public void OnTheGroundAtTheNetTheRingTimesTheReceiveAndMidAirTheSmash()
        {
            var match = ReceivedAndUnderTheSet(out float smashIdeal);
            float receiveIdeal = match.Flight.TimeAtHeightDescending(ActionResolver.ReceiveContactHeight);
            MatchDriver.AdvanceToFlightTime(match, smashIdeal - .3f);

            // Grounded, ĐÁNH plays the ball low, so the ring must close on the receive moment.
            Assert.That(match.TryGetPlayerContactCue(out float grounded, out Vector2 receivePoint), Is.True);
            Assert.That(grounded, Is.EqualTo(receiveIdeal - match.FlightTime).Within(1e-3f));
            Assert.That(Vector2.Distance(receivePoint, match.Flight.GroundAt(receiveIdeal)), Is.LessThan(1e-3f));

            Assert.That(match.PressJump(), Is.True);
            Assert.That(match.TryGetPlayerContactCue(out float airborne, out Vector2 smashPoint), Is.True);
            Assert.That(airborne, Is.EqualTo(smashIdeal - match.FlightTime).Within(1e-3f));
            Assert.That(Vector2.Distance(smashPoint, match.Flight.GroundAt(smashIdeal)), Is.LessThan(1e-3f));
        }

        [Test]
        public void ThirdTouchDiveSendsAWeakFreeBallOverTheNetInsteadOfAlwaysLosingThePoint()
        {
            var match = FrozenOpponentMatch();
            match.ForceServerForTest(CourtSide.Opponent);
            Assert.That(MatchDriver.AdvanceUntil(match, () => match.BallState == BallState.InPlay, 3f), Is.True);

            // Touch 1: an early (LATE-graded) receive pushes the set well away from the net so
            // touch 2 cannot smash and instead becomes another kept receive.
            float receiveIdeal = match.Flight.TimeAtHeightDescending(ActionResolver.ReceiveContactHeight);
            match.Player.PlaceAt(match.Flight.GroundAt(receiveIdeal));
            MatchDriver.AdvanceToFlightTime(match, receiveIdeal - .25f);
            ActionDecision touch1 = match.PressAction();
            Assert.That(touch1.Kind, Is.EqualTo(ActionKind.Receive));
            Assert.That(touch1.Grade, Is.EqualTo(TimingGrade.Late));
            Assert.That(Mathf.Abs(match.Flight.Target.x), Is.GreaterThan(ActionResolver.SmashNetDistance));
            Assert.That(match.Rally.Touches, Is.EqualTo(1));

            // Touch 2: still too far from the net to smash, so it's kept as another receive,
            // reaching the third and final touch.
            float touch2Ideal = match.Flight.TimeAtHeightDescending(ActionResolver.ReceiveContactHeight);
            match.Player.PlaceAt(match.Flight.GroundAt(touch2Ideal));
            MatchDriver.AdvanceToFlightTime(match, touch2Ideal);
            ActionDecision touch2 = match.PressAction();
            Assert.That(touch2.Kind, Is.EqualTo(ActionKind.Receive));
            Assert.That(match.Rally.Touches, Is.EqualTo(2));

            // Touch 3: out of clean reach with the ball dropping past 1 m - only a dive can save
            // it, and with no touches left it must legally cross the net.
            float diveIdeal = match.Flight.TimeAtHeightDescending(ActionResolver.ReceiveContactHeight);
            match.Player.PlaceAt(match.Flight.Target + new Vector2(0f, 1.6f));
            MatchDriver.AdvanceToFlightTime(match, diveIdeal + .05f);
            ActionDecision dive = match.PressAction();

            Assert.That(dive.Kind, Is.EqualTo(ActionKind.Dive));
            Assert.That(match.Rally.Touches, Is.EqualTo(0));
            Assert.That(match.Rally.Possession, Is.EqualTo(CourtSide.Opponent));
            Assert.That(CourtSpace.SideOf(match.Flight.Target), Is.EqualTo(CourtSide.Opponent));
        }

        [Test]
        public void OpponentReceivingASmashClearsTheWinnerCreditIfTheAiLosesThePointLater()
        {
            var match = FrozenOpponentMatch();
            match.ForceServerForTest(CourtSide.Opponent);
            Assert.That(MatchDriver.AdvanceUntil(match, () => match.BallState == BallState.InPlay, 3f), Is.True);

            float receiveIdeal = match.Flight.TimeAtHeightDescending(ActionResolver.ReceiveContactHeight);
            match.Player.PlaceAt(match.Flight.GroundAt(receiveIdeal));
            MatchDriver.AdvanceToFlightTime(match, receiveIdeal);
            match.PressAction();

            float smashIdeal = match.Flight.TimeAtHeightDescending(ActionResolver.SmashContactHeight);
            match.Player.PlaceAt(match.Flight.GroundAt(smashIdeal));
            MatchDriver.AdvanceToFlightTime(match, smashIdeal);
            ActionDecision smash = MatchDriver.JumpSmash(match, Vector2.zero);
            Assert.That(smash.Kind, Is.EqualTo(ActionKind.Smash));
            Assert.That(smash.Grade, Is.EqualTo(TimingGrade.Perfect));
            Assert.That(match.Rally.Possession, Is.EqualTo(CourtSide.Opponent));

            // The AI genuinely receives the smash (a real defensive touch) ...
            float aiReceiveIdeal = match.Flight.TimeAtHeightDescending(ActionResolver.ReceiveContactHeight);
            match.Opponent.PlaceAt(match.Flight.GroundAt(aiReceiveIdeal));
            MatchDriver.AdvanceToFlightTime(match, aiReceiveIdeal);
            Assert.That(match.Rally.Touches, Is.EqualTo(1));
            Assert.That(match.Rally.LastToucher, Is.EqualTo(CourtSide.Opponent));

            // ... but is frozen (speed 0) and never reaches its own follow-up set, so that set
            // simply lands untouched on the AI's own side - the player wins the point, but not
            // because of the original smash.
            Assert.That(MatchDriver.AdvanceUntil(match, () => match.BallState == BallState.Dead, 3f), Is.True);
            Assert.That(match.PlayerPoints, Is.EqualTo(1));
            Assert.That(match.Winners, Is.Zero);
        }

        [Test]
        public void FirstToFiveCompletesOnceWithTheSpecScore()
        {
            var match = FrozenOpponentMatch();
            int completions = 0;
            match.Completed += () => completions++;

            for (int point = 0; point < VolleyballMatch.PointsToWin; point++)
            {
                Assert.That(match.Server, Is.EqualTo(CourtSide.Player), $"point {point + 1}");
                Assert.That(MatchDriver.ServePerfectAce(match).Grade, Is.EqualTo(TimingGrade.Perfect));
                Assert.That(MatchDriver.AdvanceUntil(match, () => match.BallState == BallState.Dead, 3f), Is.True);
                if (!match.IsOver)
                    MatchDriver.Advance(match, VolleyballMatch.PointPause + .02f);
            }

            Assert.That(match.PlayerPoints, Is.EqualTo(5));
            Assert.That(match.IsOver, Is.True);
            Assert.That(completions, Is.EqualTo(1));

            MinigameResult result = match.BuildResult();
            Assert.That(result.Pass, Is.True);
            Assert.That(result.Score, Is.EqualTo(9f));
            Assert.That(result.Rank, Is.EqualTo(Rank.S));
        }

        [Test]
        public void TimeCapWithALeadPasses()
        {
            var match = TimedMatch();
            int completions = 0;
            match.Completed += () => completions++;
            match.SetScoreForTest(2, 1);
            // One long tick reaches the cap before the held serve can toss itself and change the score.
            match.Tick(VolleyballMatch.TimeLimit + .5f);

            Assert.That(match.IsOver, Is.True);
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(match.TimeRemaining, Is.Zero);
            Assert.That(match.BuildResult().Pass, Is.True);
        }

        [Test]
        public void TimeCapWithATieFails()
        {
            var match = TimedMatch();
            match.SetScoreForTest(2, 2);
            // One long tick reaches the cap before the held serve can toss itself and change the score.
            match.Tick(VolleyballMatch.TimeLimit + .5f);
            MinigameResult result = match.BuildResult();
            Assert.That(result.Pass, Is.False);
            Assert.That(result.Score, Is.Zero);
            Assert.That(result.Rank, Is.EqualTo(Rank.F));
        }

        [Test]
        public void FinishedMatchIgnoresInputAndTime()
        {
            var match = TimedMatch();
            int completions = 0;
            match.Completed += () => completions++;
            match.SetScoreForTest(1, 0);
            // One long tick reaches the cap before the held serve can toss itself and change the score.
            match.Tick(VolleyballMatch.TimeLimit + .5f);
            float elapsed = match.Elapsed;

            Assert.That(match.PressAction().Kind, Is.EqualTo(ActionKind.None));
            match.Tick(1f);
            Assert.That(match.Elapsed, Is.EqualTo(elapsed));
            Assert.That(completions, Is.EqualTo(1));
        }

        [Test]
        public void ZeroDeltaTimeChangesNothing()
        {
            var match = new VolleyballMatch();
            match.Tick(0f);
            Assert.That(match.Elapsed, Is.Zero);
        }

        [Test]
        public void ContactCueTracksTheTossApex()
        {
            var match = new VolleyballMatch();
            Assert.That(match.TryGetPlayerContactCue(out _), Is.False);
            match.PressAction();
            Assert.That(match.TryGetPlayerContactCue(out float seconds), Is.True);
            Assert.That(seconds, Is.EqualTo(match.Flight.ApexTime).Within(1e-4f));
        }

        [Test]
        public void ContactCueMarksWhereToStandNotWhereTheBallIsNow()
        {
            var match = FrozenOpponentMatch();
            match.ForceServerForTest(CourtSide.Opponent);
            Assert.That(MatchDriver.AdvanceUntil(match, () => match.BallState == BallState.InPlay, 3f), Is.True);
            MatchDriver.Advance(match, .1f);

            Assert.That(match.TryGetPlayerContactCue(out float seconds, out Vector2 contact), Is.True);
            float ideal = match.Flight.TimeAtHeightDescending(ActionResolver.ReceiveContactHeight);
            Assert.That(seconds, Is.EqualTo(ideal - match.FlightTime).Within(1e-4f));
            Assert.That(Vector2.Distance(contact, match.Flight.GroundAt(ideal)), Is.LessThan(1e-4f));
            Assert.That(Vector2.Distance(contact, match.BallGround), Is.GreaterThan(1f));

            match.Player.PlaceAt(contact + new Vector2(0f, 3f));
            Assert.That(match.PlayerInReachOf(contact), Is.False);
            match.Player.PlaceAt(contact + new Vector2(0f, 1.4f));
            Assert.That(match.PlayerInReachOf(contact), Is.True);
        }

        [Test]
        public void OnceTheSmashWindowPassesTheCueFallsBackToTheReceive()
        {
            var match = FrozenOpponentMatch();
            match.ForceServerForTest(CourtSide.Opponent);
            Assert.That(MatchDriver.AdvanceUntil(match, () => match.BallState == BallState.InPlay, 3f), Is.True);
            // A dive sets low (apex 3 m): the ball is still up once the smash window has passed.
            // A regular 4.5 m set lands before the window's late edge.
            float serveReceiveIdeal = match.Flight.TimeAtHeightDescending(ActionResolver.ReceiveContactHeight);
            match.Player.PlaceAt(match.Flight.Target + new Vector2(0f, 1.6f));
            MatchDriver.AdvanceToFlightTime(match, serveReceiveIdeal + .05f);
            Assert.That(match.PressAction().Kind, Is.EqualTo(ActionKind.Dive));

            float smashIdeal = match.Flight.TimeAtHeightDescending(ActionResolver.SmashContactHeight);
            float receiveIdeal = match.Flight.TimeAtHeightDescending(ActionResolver.ReceiveContactHeight);
            match.Player.PlaceAt(match.Flight.GroundAt(smashIdeal));
            Assert.That(Mathf.Abs(match.Player.Position.x), Is.LessThanOrEqualTo(ActionResolver.SmashNetDistance));
            MatchDriver.AdvanceToFlightTime(match, smashIdeal);
            // The ring only times the smash once the player is up for it.
            Assert.That(match.PressJump(), Is.True);
            Assert.That(match.TryGetPlayerContactCue(out float toSmash, out _), Is.True);
            Assert.That(toSmash, Is.EqualTo(0f).Within(1e-3f));

            MatchDriver.AdvanceToFlightTime(match, smashIdeal + TimingWindows.Late + .01f);
            Assert.That(match.BallState, Is.EqualTo(BallState.InPlay));
            Assert.That(match.TryGetPlayerContactCue(out float toReceive, out Vector2 point), Is.True);
            Assert.That(toReceive, Is.EqualTo(receiveIdeal - match.FlightTime).Within(1e-3f));
            Assert.That(Vector2.Distance(point, match.Flight.GroundAt(receiveIdeal)), Is.LessThan(1e-3f));
        }

        [Test]
        public void PlayerOutrunsTheOpponent()
        {
            var match = new VolleyballMatch();
            Assert.That(match.Player.Speed, Is.EqualTo(6.5f));
            Assert.That(match.Opponent.Speed, Is.EqualTo(5f * OpponentTuning.Default.SpeedFactor).Within(1e-4f));
        }

        [Test]
        public void PlayerActedReportsEveryResolvedPress()
        {
            var match = FrozenOpponentMatch();
            int acted = 0;
            match.PlayerActed += _ => acted++;
            match.PressAction();
            MatchDriver.Advance(match, .05f);
            match.PressAction();
            Assert.That(acted, Is.EqualTo(1));
        }

        [Test]
        public void AimMapsTheStickOntoTheOpponentHalf()
        {
            Assert.That(VolleyballMatch.AimAtOpponent(Vector2.zero), Is.EqualTo(new Vector2(7f, 0f)));
            Assert.That(VolleyballMatch.AimAtOpponent(new Vector2(-1f, 1f)), Is.EqualTo(new Vector2(3f, 3f)));
            Assert.That(VolleyballMatch.AimAtOpponent(new Vector2(1f, -1f)), Is.EqualTo(new Vector2(7f, -3f)));
        }

        [Test]
        public void SmashAimMapsTheStickContinuouslyInsideTheOpponentCourt()
        {
            Assert.That(VolleyballMatch.SmashAim(Vector2.zero), Is.EqualTo(new Vector2(5.25f, 0f)));
            Assert.That(VolleyballMatch.SmashAim(Vector2.right), Is.EqualTo(new Vector2(7.5f, 0f)));
            Assert.That(VolleyballMatch.SmashAim(Vector2.left), Is.EqualTo(new Vector2(3f, 0f)));
            Assert.That(VolleyballMatch.SmashAim(Vector2.up), Is.EqualTo(new Vector2(5.25f, 3.5f)));
            Assert.That(VolleyballMatch.SmashAim(Vector2.down), Is.EqualTo(new Vector2(5.25f, -3.5f)));
            Assert.That(VolleyballMatch.SmashAim(new Vector2(0f, .5f)).y, Is.EqualTo(2.5f).Within(1e-4f));
            Assert.That(VolleyballMatch.SmashAim(new Vector2(5f, 0f)), Is.EqualTo(new Vector2(7.5f, 0f)), "stick is clamped to 1");

            Vector2 diagonal = VolleyballMatch.SmashAim(new Vector2(1f, 1f));
            Assert.That(diagonal.x, Is.EqualTo(5.25f + 2.25f * .70710678f).Within(1e-4f));
            Assert.That(diagonal.y, Is.EqualTo(3.5f).Within(1e-4f), "a diagonal reaches the sideline");
            Assert.That(CourtSpace.IsIn(diagonal), Is.True);
        }

        [Test]
        public void PlayerAimFollowsTheStick()
        {
            var match = FrozenOpponentMatch();
            match.SetMove(Vector2.up);
            Assert.That(match.PlayerAim, Is.EqualTo(VolleyballMatch.SmashAim(Vector2.up)));
        }

        [Test]
        public void JumpOnlyWorksWhileTheBallIsInPlay()
        {
            var match = FrozenOpponentMatch();
            Assert.That(match.BallState, Is.EqualTo(BallState.Held));
            Assert.That(match.PressJump(), Is.False);
            match.PressAction();
            Assert.That(match.BallState, Is.EqualTo(BallState.Toss));
            Assert.That(match.PressJump(), Is.False);
            Assert.That(match.Player.IsAirborne, Is.False);

            VolleyballMatch rally = ReceivedAndUnderTheSet(out _);
            Assert.That(rally.PressJump(), Is.True);
            Assert.That(rally.Player.IsAirborne, Is.True);
            Assert.That(rally.Player.Action, Is.EqualTo(AthleteAction.JumpLoad));
            Assert.That(rally.PressJump(), Is.False, "no double jump");

            rally.SetScoreForTest(0, VolleyballMatch.PointsToWin - 1);
            rally.Player.PlaceAt(new Vector2(-8f, 0f));
            Assert.That(MatchDriver.AdvanceUntil(rally, () => rally.IsOver, 10f), Is.True);
            Assert.That(rally.PressJump(), Is.False);
        }

        [Test]
        public void JumpCueOpensEightTenthsBeforeTheSmashAndClosesOnceAirborne()
        {
            VolleyballMatch match = ReceivedAndUnderTheSet(out float smashIdeal);
            MatchDriver.AdvanceToFlightTime(match, smashIdeal - .9f);
            Assert.That(match.TryGetJumpCue(out _), Is.False, "too early");

            MatchDriver.AdvanceToFlightTime(match, smashIdeal - .7f);
            Assert.That(match.TryGetJumpCue(out float seconds), Is.True);
            Assert.That(seconds, Is.EqualTo(.7f).Within(1e-3f));

            match.PressJump();
            Assert.That(match.TryGetJumpCue(out _), Is.False);
        }

        // Jumping the moment the cue lights still leaves the player up for a PERFECT hit.
        [Test]
        public void AJumpAtTheFirstCueStillHangsThroughThePerfectWindow()
        {
            VolleyballMatch match = ReceivedAndUnderTheSet(out float smashIdeal);
            MatchDriver.AdvanceToFlightTime(match, smashIdeal - VolleyballMatch.JumpCueLead);
            Assert.That(match.TryGetJumpCue(out _), Is.True);
            Assert.That(match.PressJump(), Is.True);

            MatchDriver.AdvanceToFlightTime(match, smashIdeal + .14f);
            Assert.That(match.Player.IsAirborne, Is.True);
            ActionDecision smash = match.PressAction();
            Assert.That(smash.Kind, Is.EqualTo(ActionKind.Smash));
            Assert.That(smash.Grade, Is.EqualTo(TimingGrade.Perfect));
        }

        [Test]
        public void NoJumpCueWhileTheSmashContactIsOutOfReach()
        {
            VolleyballMatch match = ReceivedAndUnderTheSet(out float smashIdeal);
            Vector2 contact = match.Flight.GroundAt(smashIdeal);
            match.Player.PlaceAt(contact + new Vector2(0f, 2f));
            MatchDriver.AdvanceToFlightTime(match, smashIdeal - .3f);
            Assert.That(match.PlayerInReachOf(contact), Is.False);
            Assert.That(match.TryGetJumpCue(out _), Is.False, "a jump from here cannot connect");

            match.Player.PlaceAt(contact);
            Assert.That(match.TryGetJumpCue(out _), Is.True);
        }

        [Test]
        public void NoJumpCueAwayFromTheNet()
        {
            VolleyballMatch match = ReceivedAndUnderTheSet(out float smashIdeal);
            match.Player.PlaceAt(new Vector2(-5f, 0f));
            MatchDriver.AdvanceToFlightTime(match, smashIdeal - .3f);
            Assert.That(match.TryGetJumpCue(out _), Is.False);
        }

        [Test]
        public void JumpCueAndBlockPoseWhenTheOpponentTelegraphsASmashAtTheNet()
        {
            var match = new VolleyballMatch();
            MatchDriver.ServeGood(match);
            Assert.That(MatchDriver.AdvanceUntil(match, () => match.OpponentSmashTell, 6f), Is.True);

            match.Player.PlaceAt(new Vector2(-3f, 0f));
            Assert.That(match.TryGetJumpCue(out _), Is.False, "too far from the net to block");

            match.Player.PlaceAt(new Vector2(-.8f, 0f));
            Assert.That(match.TryGetJumpCue(out _), Is.True);

            ActionDecision acted = ActionDecision.None;
            match.PlayerActed += d => acted = d;
            Assert.That(match.PressJump(), Is.True);
            Assert.That(match.Player.Action, Is.EqualTo(AthleteAction.Block));
            Assert.That(acted.Kind, Is.EqualTo(ActionKind.Block));
            Assert.That(acted.IsTimed, Is.False, "a block jump is not graded");
        }
    }
}
