using KMA.Gameplay;
using KMA.Gameplay.Volleyball;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace KMA.Tests.Gameplay.Volleyball
{
    public sealed class PresentationTests
    {
        GameObject root;

        [SetUp]
        public void SetUp() => root = new GameObject("PresentationTests");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        static Sprite[] Frames(string name, int count)
        {
            var texture = new Texture2D(4, 4);
            var frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                frames[i] = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(.5f, 0f));
                frames[i].name = $"{name}_{i}";
            }

            return frames;
        }

        SpriteRenderer Renderer(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform);
            return child.AddComponent<SpriteRenderer>();
        }

        [Test]
        public void FlipbookLoopsAndClamps()
        {
            SpriteRenderer renderer = Renderer("Book");
            var book = renderer.gameObject.AddComponent<SpriteFlipbook>();
            Sprite[] frames = Frames("f", 3);
            book.Configure(renderer, frames, true, 10f);

            Assert.That(renderer.sprite, Is.SameAs(frames[0]));
            book.Advance(.15f);
            Assert.That(book.FrameIndex, Is.EqualTo(1));
            book.Advance(.2f);
            Assert.That(book.FrameIndex, Is.EqualTo(0));

            book.Play(frames, false);
            book.Advance(5f);
            Assert.That(book.FrameIndex, Is.EqualTo(2));
            Assert.That(renderer.sprite, Is.SameAs(frames[2]));
        }


        [Test]
        public void SameFlipbookActionCanBeRestartedOnARepeatedContact()
        {
            SpriteRenderer renderer = Renderer("RestartBook");
            var book = renderer.gameObject.AddComponent<SpriteFlipbook>();
            Sprite[] frames = Frames("repeat", 3);
            book.Configure(renderer, frames, false, 12f);
            book.Advance(.16f);
            Assert.That(book.FrameIndex, Is.EqualTo(1));

            book.Play(frames, false, forceRestart: true);

            Assert.That(book.FrameIndex, Is.Zero);
            Assert.That(renderer.sprite, Is.SameAs(frames[0]));
        }

        [Test]
        public void AthleteViewFollowsTheModel()
        {
            SpriteRenderer body = Renderer("Athlete");
            var book = body.gameObject.AddComponent<SpriteFlipbook>();
            var view = body.gameObject.AddComponent<VolleyAthleteView>();
            Sprite[] idle = Frames("idle", 2), run = Frames("run", 2), receive = Frames("rec", 2),
                smash = Frames("smash", 2), block = Frames("block", 2), dive = Frames("dive", 2);
            view.Configure(body, book, true, idle, run, receive, smash, block, dive);
            Sprite[] serve = Frames("serve", 2), toss = Frames("toss", 1), dig = Frames("dig", 1),
                set = Frames("set", 1), recover = Frames("recover", 1), load = Frames("load", 1),
                windup = Frames("windup", 1), contact = Frames("contact", 1), follow = Frames("follow", 1),
                land = Frames("land", 1);
            view.ConfigureSequences(serve, toss, dig, set, recover, load, windup, contact, follow, land);

            var athlete = new VolleyAthlete(CourtSide.Opponent, 5f);
            athlete.PlaceAt(new Vector2(5f, 2f));
            view.Render(athlete);
            Assert.That(view.transform.position, Is.EqualTo(CourtSpace.ToWorld(new Vector2(5f, 2f), 0f)));
            Assert.That(body.sortingOrder, Is.EqualTo(80));
            Assert.That(body.flipX, Is.True);
            Assert.That(book.Frames, Is.SameAs(idle));

            athlete.BeginAction(AthleteAction.Smash, .45f);
            view.Render(athlete);
            Assert.That(book.Frames, Is.SameAs(contact));
            Assert.That(book.Loop, Is.False);
            Assert.That(view.FramesFor(AthleteAction.Serve), Is.SameAs(serve));
            Assert.That(view.FramesFor(AthleteAction.Serve), Is.Not.SameAs(view.FramesFor(AthleteAction.Smash)));
            CollectionAssert.Contains(view.FramesFor(AthleteAction.Dive), dive[0]);
            Assert.That(view.FramesFor(AthleteAction.Run), Is.SameAs(run));
        }

        [Test]
        public void NearerAthletesDrawInFront()
        {
            Assert.That(VolleyAthleteView.SortingOrderFor(-3f), Is.GreaterThan(VolleyAthleteView.SortingOrderFor(3f)));
            Assert.That(VolleyAthleteView.SortingOrderFor(-5.5f), Is.LessThan(VolleyBallView.BallSortingOrder));
        }

        [Test]
        public void BallViewDrawsBallAboveItsShadow()
        {
            SpriteRenderer ball = Renderer("Ball"), shadow = Renderer("Shadow"),
                contact = Renderer("Contact"), aim = Renderer("Aim");
            var view = root.AddComponent<VolleyBallView>();
            view.Configure(ball, shadow, contact, aim);
            var match = new VolleyballMatch();

            view.Render(match);

            // The held ball sits beside the server's hand (towards the net), not on his face.
            Assert.That(ball.transform.position, Is.EqualTo(
                CourtSpace.ToWorld(VolleyballMatch.PlayerServeSpot, VolleyballMatch.TossStartHeight)
                + Vector3.right * VolleyBallView.HeldBallSideOffset));
            Assert.That(shadow.transform.position, Is.EqualTo(CourtSpace.ToWorld(VolleyballMatch.PlayerServeSpot, 0f)));
            Assert.That(shadow.transform.localScale.x,
                Is.EqualTo(VolleyBallView.ShadowScaleFor(VolleyballMatch.TossStartHeight)).Within(1e-4f));
            Assert.That(ball.sortingOrder, Is.EqualTo(VolleyBallView.BallSortingOrder));
            Assert.That(contact.enabled, Is.False);
            Assert.That(aim.enabled, Is.False);

            match.PressAction();
            view.Render(match);
            Assert.That(contact.enabled, Is.True);
        }

        [Test]
        public void ContactMarkerSitsOnTheContactPointAndTurnsGreenInReach()
        {
            SpriteRenderer ball = Renderer("Ball"), shadow = Renderer("Shadow"),
                contact = Renderer("Contact"), aim = Renderer("Aim");
            var view = root.AddComponent<VolleyBallView>();
            view.Configure(ball, shadow, contact, aim);
            var match = new VolleyballMatch(null, new OpponentTuning(0f, .25f));
            match.ForceServerForTest(CourtSide.Opponent);
            while (match.BallState != BallState.InPlay)
                match.Tick(1f / 60f);
            match.Tick(.1f);

            Assert.That(match.TryGetPlayerContactCue(out _, out Vector2 point), Is.True);
            match.Player.PlaceAt(point + new Vector2(0f, 3f));
            view.Render(match);
            Assert.That(contact.enabled, Is.True);
            Assert.That(Vector3.Distance(contact.transform.position, CourtSpace.ToWorld(point, 0f)), Is.LessThan(1e-4f));
            Assert.That(contact.color, Is.EqualTo(VolleyBallView.MarkerOutOfReachColor));

            match.Player.PlaceAt(point);
            view.Render(match);
            Assert.That(contact.color, Is.EqualTo(VolleyBallView.MarkerInReachColor));
        }

        [Test]
        public void HeldBallSitsBesideTheServerTowardsTheNet()
        {
            SpriteRenderer ball = Renderer("Ball"), shadow = Renderer("Shadow");
            var view = root.AddComponent<VolleyBallView>();
            view.Configure(ball, shadow, null, null);
            var match = new VolleyballMatch();
            match.ForceServerForTest(CourtSide.Opponent);

            view.Render(match);

            Vector3 column = CourtSpace.ToWorld(VolleyballMatch.OpponentServeSpot, VolleyballMatch.TossStartHeight);
            Assert.That(ball.transform.position.x, Is.EqualTo(column.x - VolleyBallView.HeldBallSideOffset).Within(1e-4f),
                "The opponent serves from the far side, so the net is to his left.");
            Assert.That(ball.transform.position.y, Is.EqualTo(column.y).Within(1e-4f));
            Assert.That(shadow.transform.position, Is.EqualTo(CourtSpace.ToWorld(VolleyballMatch.OpponentServeSpot, 0f)),
                "The ground shadow stays under the server.");
        }

        [Test]
        public void TossedBallLeavesTheHandAndRisesOverTheHead()
        {
            float start = VolleyballMatch.TossStartHeight, apex = VolleyballMatch.TossApexHeight;
            Assert.That(VolleyBallView.HandOffsetWeight(BallState.Held, start), Is.EqualTo(1f));
            Assert.That(VolleyBallView.HandOffsetWeight(BallState.Toss, start), Is.EqualTo(1f));
            Assert.That(VolleyBallView.HandOffsetWeight(BallState.Toss, (start + apex) * .5f), Is.EqualTo(.5f).Within(1e-4f));
            Assert.That(VolleyBallView.HandOffsetWeight(BallState.Toss, apex), Is.EqualTo(0f));
            Assert.That(VolleyBallView.HandOffsetWeight(BallState.Toss, apex + 1f), Is.EqualTo(0f));
            Assert.That(VolleyBallView.HandOffsetWeight(BallState.InPlay, start), Is.EqualTo(0f),
                "Once served the ball follows its real flight, so there is no jump.");
            Assert.That(VolleyBallView.HandOffsetWeight(BallState.Dead, start), Is.EqualTo(0f));
        }

        [Test]
        public void ShadowAndMarkerScalesShrinkAsDesigned()
        {
            Assert.That(VolleyBallView.ShadowScaleFor(0f), Is.EqualTo(1f));
            Assert.That(VolleyBallView.ShadowScaleFor(10f), Is.EqualTo(.6f));
            Assert.That(VolleyBallView.MarkerScaleFor(0f), Is.EqualTo(1f));
            Assert.That(VolleyBallView.MarkerScaleFor(.6f), Is.EqualTo(2.5f));
            Assert.That(VolleyBallView.MarkerScaleFor(-.2f), Is.EqualTo(1f));
        }

        [Test]
        public void HudTextsMatchTheSpec()
        {
            Assert.That(VolleyballHud.ScoreText(3, 2), Is.EqualTo("3  :  2"));
            Assert.That(VolleyballHud.FeedbackText(TimingGrade.Perfect, 0f), Is.EqualTo("HOÀN HẢO"));
            Assert.That(VolleyballHud.FeedbackText(TimingGrade.Good, .1f), Is.EqualTo("TỐT"));
            Assert.That(VolleyballHud.FeedbackText(TimingGrade.Late, -.2f), Is.EqualTo("SỚM"));
            Assert.That(VolleyballHud.FeedbackText(TimingGrade.Late, .2f), Is.EqualTo("MUỘN"));
        }

        [Test]
        public void HudShowsFeedbackOnlyForTimedPressesAndHidesItAgain()
        {
            var hud = root.AddComponent<VolleyballHud>();
            TMP_Text score = new GameObject("Score").AddComponent<TextMeshPro>();
            TMP_Text feedback = new GameObject("Feedback").AddComponent<TextMeshPro>();
            TMP_Text hint = new GameObject("Hint").AddComponent<TextMeshPro>();
            score.transform.SetParent(root.transform);
            feedback.transform.SetParent(root.transform);
            hint.transform.SetParent(root.transform);
            hud.Configure(score, feedback, hint);
            var match = new VolleyballMatch();

            hud.ShowFeedback(new ActionDecision(ActionKind.Block, TimingGrade.Miss, 0f));
            hud.Render(match, MinigamePhase.Play, 0f);
            Assert.That(feedback.enabled, Is.False);

            hud.ShowFeedback(new ActionDecision(ActionKind.Receive, TimingGrade.Perfect, 0f));
            hud.Render(match, MinigamePhase.Play, .1f);
            Assert.That(feedback.enabled, Is.True);
            Assert.That(feedback.text, Is.EqualTo("HOÀN HẢO"));
            Assert.That(score.text, Is.EqualTo("0  :  0"));
            Assert.That(hint.enabled, Is.True);

            hud.Render(match, MinigamePhase.Play, VolleyballHud.FeedbackSeconds);
            Assert.That(feedback.enabled, Is.False);

            hud.Render(match, MinigamePhase.Play, VolleyballHud.HintSeconds);
            hud.Render(match, MinigamePhase.Play, 0f);
            Assert.That(hint.enabled, Is.False);

            hud.Render(match, MinigamePhase.Tutorial, 0f);
            Assert.That(hint.enabled, Is.False);
            Assert.That(hint.text, Is.EqualTo(VolleyballHud.HintText));
        }

        [Test]
        public void AthleteViewLiftsAJumpingAthlete()
        {
            SpriteRenderer body = Renderer("Jumper");
            var book = body.gameObject.AddComponent<SpriteFlipbook>();
            var view = body.gameObject.AddComponent<VolleyAthleteView>();
            Sprite[] frames = Frames("f", 2);
            view.Configure(body, book, false, frames, frames, frames, frames, frames, frames);

            var athlete = new VolleyAthlete(CourtSide.Player, 5f);
            athlete.PlaceAt(new Vector2(-2f, 1f));
            athlete.TryJump(AthleteAction.Smash);
            athlete.Tick(VolleyAthlete.JumpSeconds / 2f);
            view.Render(athlete);
            Assert.That(view.transform.position,
                Is.EqualTo(CourtSpace.ToWorld(new Vector2(-2f, 1f), athlete.JumpHeight)));
            Assert.That(athlete.JumpHeight, Is.GreaterThan(0f));
        }

        [Test]
        public void PlayerAimRingShowsOnlyMidAirOnTheAimedSpot()
        {
            SpriteRenderer ball = Renderer("Ball"), shadow = Renderer("Shadow"),
                contact = Renderer("Contact"), aim = Renderer("Aim"), playerAim = Renderer("PlayerAim");
            var view = root.AddComponent<VolleyBallView>();
            view.Configure(ball, shadow, contact, aim, playerAim);
            Assert.That(view.PlayerAimMarker, Is.SameAs(playerAim));
            var match = new VolleyballMatch(null, new OpponentTuning(0f, .25f));
            match.ForceServerForTest(CourtSide.Opponent);
            while (match.BallState != BallState.InPlay)
                match.Tick(1f / 60f);

            view.Render(match);
            Assert.That(playerAim.enabled, Is.False);

            Assert.That(match.PressJump(), Is.True);
            match.SetMove(Vector2.up);
            view.Render(match);
            Assert.That(playerAim.enabled, Is.True);
            Assert.That(Vector3.Distance(playerAim.transform.position,
                CourtSpace.ToWorld(VolleyballMatch.SmashAim(Vector2.up), 0f)), Is.LessThan(1e-4f));
            Assert.That(playerAim.sortingOrder, Is.EqualTo(VolleyBallView.MarkerSortingOrder));
        }

        [Test]
        public void HudExplainsJumpingAndCallsOutABlock()
        {
            Assert.That(VolleyballHud.HintText,
                Is.EqualTo("Joystick: di chuyển  ·  NHẢY rồi kéo joystick để nhắm  ·  ĐÁNH để đập"));
            Assert.That(string.Format(VolleyballHud.PracticeHintFormat, 1, 3),
                Is.EqualTo("ĐẠT 3 ĐIỂM TRƯỚC ĐỐI THỦ · 1/3"));

            var hud = root.AddComponent<VolleyballHud>();
            TMP_Text score = new GameObject("Score").AddComponent<TextMeshPro>();
            TMP_Text feedback = new GameObject("Feedback").AddComponent<TextMeshPro>();
            TMP_Text hint = new GameObject("Hint").AddComponent<TextMeshPro>();
            score.transform.SetParent(root.transform);
            feedback.transform.SetParent(root.transform);
            hint.transform.SetParent(root.transform);
            hud.Configure(score, feedback, hint);
            hud.ShowPoint(CourtSide.Player);
            hud.ShowBlock();
            hud.Render(new VolleyballMatch(), MinigamePhase.Play, .1f);
            Assert.That(feedback.enabled, Is.True);
            Assert.That(feedback.text, Is.EqualTo(VolleyballHud.BlockText));
            Assert.That(VolleyballHud.BlockText, Is.EqualTo("CHẮN!"));
        }
    }
}
