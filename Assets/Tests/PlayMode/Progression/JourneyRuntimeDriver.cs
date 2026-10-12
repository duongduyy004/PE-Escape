using System.Collections;
using System.Reflection;
using KMA.Gameplay;
using KMA.Gameplay.Core;
using KMA.Gameplay.UI;
using KMA.Gameplay.Volleyball;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KMA.Tests.Gameplay.Progression
{
    /// <summary>Exercises scene controllers with deterministic inputs and court positions.
    /// Never creates results, changes scores, or emits gameplay completion events.</summary>
    public static class JourneyRuntimeDriver
    {
        public static IEnumerator WaitForScene(SceneRouter router, string name)
        {
            float deadline = Time.realtimeSinceStartup + 12f;
            while (SceneManager.GetActiveScene().name != name || router.IsTransitioning)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Loading " + name);
                yield return null;
            }
            yield return null;
        }

        public static IEnumerator WaitForOpening()
        {
            var guide=MinigameGuideHost.Current?.Panel;
            while(guide!=null && guide.IsOpen)guide.PressPrimary();
            yield return null;
            var flow=Object.FindFirstObjectByType<GameplayOpeningFlow>();
            float deadline=Time.realtimeSinceStartup+12f;
            while(flow!=null && !flow.IsComplete){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline),"Opening dialogue must release its hold");yield return null;}
        }

        public static IEnumerator SkipDialogues()
        {
            var presenter = Object.FindFirstObjectByType<JourneyDialoguePresenter>();
            int remaining = 20;
            while (presenter != null && presenter.IsShowing && remaining-- > 0)
            {
                foreach (Button button in presenter.GetComponentsInChildren<Button>())
                    if (button.name == "BỎ QUA") { button.onClick.Invoke(); break; }
                yield return null;
            }
            Assert.That(presenter == null || !presenter.IsShowing, Is.True);
        }

        public static IEnumerator CompleteCurrent(ChallengeDefinition definition, bool pass)
        {
            MinigameBase controller = Object.FindFirstObjectByType<MinigameBase>();
            Assert.That(controller, Is.Not.Null, definition.Id);
            yield return WaitForOpening();
            bool completed = false;
            ((IChallengeController)controller).ChallengeCompleted += _ => completed = true;
            if (controller is FootballController football)
                Assert.That(football.BeginMatch(FootballDifficulty.Normal), Is.True);
            else controller.SetTutorialGate(false);
            var lifecycle = (MinigameLifecycle)typeof(MinigameBase).GetField("lifecycle",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            lifecycle.Tick(10f);
            MethodInfo tick = controller.GetType().GetMethod("TickPlay", BindingFlags.Instance | BindingFlags.NonPublic);
            void Step(float dt) => tick.Invoke(controller, new object[] { dt });
            Assert.That(controller.PresentationPhase, Is.EqualTo(MinigamePhase.Play));

            if (controller is SprintController sprint)
            {
                var input = Object.FindFirstObjectByType<KMA.Input.GameplayInputRouter>();
                for (int frame = 0; frame < 31 * 240 && !completed; frame++)
                {
                    if (pass && frame % 40 == 0)
                        input.FeedSprintTapForTest((frame / 40) % 2 == 0 ? KMA.Input.Side.Left : KMA.Input.Side.Right,
                            frame / 240d);
                    Step(1f / 240f);
                    if (frame % 240 == 0) yield return null;
                }
            }
            else if (controller is FootballController soccer)
            {
                Step(.001f);
                var rules = soccer.Rules;
                for (int kick = 0; kick < 10 && !completed; kick++)
                {
                    Assert.That(rules.State, Is.EqualTo(FootballState.Aiming));
                    rules.SetAim(.55f);
                    rules.BeginCharge();
                    Step(pass ? 1.0210175f : .01f);
                    rules.ReleaseShot();
                    for (int frame = 0; frame < 2400 && !completed && rules.State != FootballState.Aiming; frame++)
                        Step(.01f);
                    yield return null;
                }
            }
            else if (controller is VolleyballController volley)
            {
                var match = volley.Match;
                // Court positions are controlled fixtures. Touches, ball flight, point
                // awards, deadlines, challenge metrics and results use production rules.
                for (int frame = 0; frame < 130 * 200 && !completed; frame++)
                {
                    volley.Input.FeedMoveForTest(Vector2.zero);
                    if (pass && match.Server == CourtSide.Player && match.BallState == BallState.Held)
                        volley.Input.FeedActionForTest();
                    else if (pass && match.Server == CourtSide.Player && match.BallState == BallState.Toss)
                    {
                        if (Mathf.Abs(match.FlightTime - match.Flight.ApexTime) <= .005f)
                            volley.Input.FeedActionForTest();
                    }
                    else if (pass && match.BallState == BallState.InPlay && match.Rally.Possession == CourtSide.Player)
                    {
                        bool smash = match.Rally.Touches == 2;
                        float ideal = match.Flight.TimeAtHeightDescending(smash
                            ? ActionResolver.SmashContactHeight : ActionResolver.ReceiveContactHeight);
                        // At the second receive, wait beyond the smash window. The
                        // context button prioritizes a smash if both windows overlap.
                        float contactTime = ideal + (!smash && match.Rally.Touches == 1 ? .1f : 0f);
                        if (Mathf.Abs(match.FlightTime - contactTime) <= .005f)
                        {
                            match.Player.PlaceAt(match.Flight.GroundAt(ideal));
                            if (smash)
                            {
                                match.Opponent.PlaceAt(new Vector2(8.5f, -3.5f));
                                volley.Input.FeedMoveForTest(new Vector2(1f, 1f));
                                volley.Input.FeedJumpForTest();
                            }
                            volley.Input.FeedActionForTest();
                        }
                    }
                    Step(.005f);
                    if (frame % 400 == 0) yield return null;
                }
            }
            Assert.That(completed, Is.True, definition.Id + " did not emit a controller result. " +
                (controller is VolleyballController v ? $"points={v.Match.PlayerPoints}:{v.Match.OpponentPoints}, elapsed={v.Match.Elapsed}, state={v.Match.BallState}" : ""));
        }
    }
}
