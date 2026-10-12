using System.Collections;
using KMA.Gameplay;
using KMA.Gameplay.Volleyball;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KMA.Tests.Gameplay.Volleyball
{
    public sealed class VolleyballChallengeRuntimeTests
    {
        GameObject root;
        GameObject viewRoot;
        Texture2D poseTexture;
        Sprite[] poseSprites;

        [UnityTest]
        public IEnumerator ExamDeadlineProducesOneTypedScoredResult()
        {
            root = new GameObject("JourneyVolleyballController");
            root.SetActive(false);
            var input = root.AddComponent<VolleyballInputBridge>();
            var controller = root.AddComponent<VolleyballController>();
            viewRoot = new GameObject("VolleyballViews");
            VolleyAthleteView player = View("Player"), opponent = View("Opponent");
            var ball = new GameObject("Ball").AddComponent<VolleyBallView>();
            ball.transform.SetParent(viewRoot.transform);
            var hud = root.AddComponent<VolleyballHud>();
            controller.Configure(player, opponent, ball, input, hud);
            root.SetActive(true);

            ChallengeDefinition definition = ChallengeCatalog.LoadDefault().Get("volleyball_exam");
            var session = new GameSession();
            Complete(session, "sprint_learn");
            Complete(session, "sprint_practice");
            Complete(session, "sprint_exam");
            Complete(session, "volleyball_learn");
            Complete(session, "volleyball_practice");
            Assert.That(session.TryStartChallenge(definition.Id, ChallengeAttemptMode.Journey,
                definition.Difficulty, out ChallengeAttemptContext context), Is.True);
            controller.ConfigureChallenge(definition, context);
            int typed = 0;
            ChallengeAttemptResult result = null;
            controller.ChallengeCompleted += value => { typed++; result = value; };
            yield return null;
            controller.SkipToPlayForTest();

            // An idle player loses every rally; the exam ends on the opponent's tenth point.
            for (int i = 0; i < 600 * 60 && !controller.Match.IsOver; i++) controller.Match.Tick(1f / 60f);
            yield return null;
            Assert.That(controller.Match.OpponentPoints, Is.EqualTo(10));
            Assert.That(typed, Is.EqualTo(1));
            Assert.That(result.Pass, Is.False);
            Assert.That(result.ExamResult, Is.Not.Null);
            Assert.That(session.SubmitChallengeResult(result).Accepted, Is.True);
            Assert.That(session.Journey.CheckpointChallengeId, Is.EqualTo("volleyball_exam"));
        }

        // On device the PhaseOverlay can open the start gate before the router binds the scene;
        // a throw there aborted every later sceneLoaded handler, pause exit included.
        [UnityTest]
        public IEnumerator ChallengeCanBeConfiguredOnceTheCountdownHasStarted()
        {
            root = new GameObject("JourneyVolleyballController");
            root.SetActive(false);
            var input = root.AddComponent<VolleyballInputBridge>();
            var controller = root.AddComponent<VolleyballController>();
            viewRoot = new GameObject("VolleyballViews");
            VolleyAthleteView player = View("Player"), opponent = View("Opponent");
            var ball = new GameObject("Ball").AddComponent<VolleyBallView>();
            ball.transform.SetParent(viewRoot.transform);
            var hud = root.AddComponent<VolleyballHud>();
            controller.Configure(player, opponent, ball, input, hud);
            root.SetActive(true);
            controller.SetTutorialGate(false);
            Assert.That(controller.PresentationPhase, Is.EqualTo(MinigamePhase.Countdown));

            var session = new GameSession();
            Complete(session, "sprint_learn");
            Complete(session, "sprint_practice");
            Complete(session, "sprint_exam");
            ChallengeDefinition definition = ChallengeCatalog.LoadDefault().Get("volleyball_learn");
            Assert.That(session.TryStartChallenge(definition.Id, ChallengeAttemptMode.Journey,
                definition.Difficulty, out ChallengeAttemptContext context), Is.True);
            Assert.DoesNotThrow(() => controller.ConfigureChallenge(definition, context));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DigSpikeContactAndLandingUseTheirAuthoredPoseSprites()
        {
            viewRoot = new GameObject("PoseTimelineViews");
            poseTexture = new Texture2D(32, 4, TextureFormat.RGBA32, false);
            poseSprites = new Sprite[8];
            for (int i = 0; i < poseSprites.Length; i++)
                poseSprites[i] = Sprite.Create(poseTexture, new Rect(i * 4, 0, 4, 4), Vector2.zero);

            var go = new GameObject("PoseTimelinePlayer");
            go.transform.SetParent(viewRoot.transform);
            var renderer = go.AddComponent<SpriteRenderer>();
            var book = go.AddComponent<SpriteFlipbook>();
            var view = go.AddComponent<VolleyAthleteView>();
            Sprite[] idle = { poseSprites[0] };
            view.Configure(renderer, book, false, idle, idle, idle, idle, idle, idle);
            Sprite[] dig = { poseSprites[1] }, recover = { poseSprites[2] }, jumpLoad = { poseSprites[3] };
            Sprite[] windup = { poseSprites[4] }, contact = { poseSprites[5] }, follow = { poseSprites[6] }, land = { poseSprites[7] };
            view.ConfigureSequences(null, null, dig, idle, recover, jumpLoad, windup, contact, follow, land);
            var athlete = new VolleyAthlete(CourtSide.Player, VolleyballMatch.PlayerSpeed);
            athlete.PlaceAt(Vector2.zero);

            athlete.BeginAction(AthleteAction.Dig, VolleyballMatch.ReceiveSeconds);
            view.Render(athlete);
            Assert.That(book.Frames, Is.SameAs(dig), "a low legal receive starts with the Dig art");
            athlete.Tick(.2f);
            view.Render(athlete);
            Assert.That(book.Frames, Is.SameAs(recover), "the dig transitions into recovery");

            athlete.PlaceAt(Vector2.zero);
            Assert.That(athlete.TryJump(AthleteAction.JumpLoad), Is.True);
            view.Render(athlete);
            Assert.That(book.Frames, Is.SameAs(jumpLoad));
            athlete.Tick(.2f);
            view.Render(athlete);
            Assert.That(book.Frames, Is.SameAs(windup), "the airborne approach uses spike windup");

            athlete.BeginAction(AthleteAction.Smash, VolleyballMatch.SmashSeconds);
            view.Render(athlete);
            Assert.That(book.Frames, Is.SameAs(contact), "the smash contact starts on the spike-contact sprite");
            athlete.Tick(.15f);
            view.Render(athlete);
            Assert.That(book.Frames, Is.SameAs(follow));
            athlete.Tick(.5f);
            view.Render(athlete);
            Assert.That(book.Frames, Is.SameAs(land), "the airborne landing phase uses its dedicated Land sprite");
            yield return null;
        }

        [UnityTest]
        public IEnumerator LegacyOpponentSmashKeepsItsFullAuthoredActionStrip()
        {
            viewRoot = new GameObject("LegacyPoseTimelineViews");
            poseTexture = new Texture2D(8, 4, TextureFormat.RGBA32, false);
            poseSprites = new Sprite[2];
            for (int i = 0; i < poseSprites.Length; i++)
                poseSprites[i] = Sprite.Create(poseTexture, new Rect(i * 4, 0, 4, 4), Vector2.zero);

            var go = new GameObject("LegacyOpponent");
            go.transform.SetParent(viewRoot.transform);
            var renderer = go.AddComponent<SpriteRenderer>();
            var book = go.AddComponent<SpriteFlipbook>();
            var view = go.AddComponent<VolleyAthleteView>();
            Sprite[] idle = { poseSprites[0] };
            Sprite[] smash = { poseSprites[0], poseSprites[1] };
            view.Configure(renderer, book, true, idle, idle, idle, smash, idle, idle);
            // SharedVolleyPoses has no per-phase arrays; it must keep using Smash as one flipbook.
            view.ConfigureSequences(null, null, null, null, null, null, null, null, null, null);
            var athlete = new VolleyAthlete(CourtSide.Opponent, VolleyballMatch.OpponentBaseSpeed);
            athlete.PlaceAt(Vector2.zero);
            athlete.BeginAction(AthleteAction.Smash, 1f);

            view.Render(athlete);
            Assert.That(book.Frames, Is.SameAs(smash));
            book.Advance(.1f);
            Assert.That(renderer.sprite, Is.SameAs(poseSprites[1]),
                "the opponent's second authored attack pose must remain visible during Smash");
            yield return null;
        }

        VolleyAthleteView View(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(viewRoot.transform);
            var renderer = go.AddComponent<SpriteRenderer>();
            var book = go.AddComponent<SpriteFlipbook>();
            var view = go.AddComponent<VolleyAthleteView>();
            var texture = new Texture2D(4, 4);
            var frames = new[] { Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero) };
            view.Configure(renderer, book, false, frames, frames, frames, frames, frames, frames);
            return view;
        }

        static void Complete(GameSession session, string id)
        {
            ChallengeDefinition item = session.Journey.Catalog.Get(id);
            Assert.That(session.TryStartChallenge(id, ChallengeAttemptMode.Journey, item.Difficulty,
                out ChallengeAttemptContext context), Is.True);
            Assert.That(session.SubmitChallengeResult(new ChallengeAttemptResult(context, true,
                new ChallengeMetrics(completedTargets: item.TargetCount))).Accepted, Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root) Object.Destroy(root);
            if (viewRoot) Object.Destroy(viewRoot);
            if (poseSprites != null) foreach (Sprite sprite in poseSprites) if (sprite) Object.Destroy(sprite);
            if (poseTexture) Object.Destroy(poseTexture);
            yield return null;
        }
    }
}
