using System.Reflection;
using KMA.Gameplay;
using KMA.Gameplay.UI;
using KMA.UI.Kit;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

namespace KMA.Tests.Gameplay.Football
{
    public sealed class FootballControllerTests
    {
        Fixture fixture;

        [SetUp]
        public void SetUp()
        {
            FootballController.ResetSessionPreferenceForTests();
            fixture = new Fixture();
        }

        [TearDown]
        public void TearDown()
        {
            if (fixture != null) fixture.Dispose();
        }

        [Test]
        public void ReenablingControllerRestoresInputAndCancelsHeldShot()
        {
            fixture.controller.enabled = false;
            fixture.controller.enabled = true;
            Assert.That(fixture.controller.BeginMatch(FootballDifficulty.Normal), Is.True);
            AdvanceLifecycle(10f); TickPlay(.01f);
            fixture.aim.onValueChanged.Invoke(-.6f);
            Assert.That(fixture.controller.Rules.AimX, Is.EqualTo(-.6f));
            PointerDown(fixture.shoot.gameObject, 81);
            Assert.That(fixture.controller.Rules.State, Is.EqualTo(FootballState.Charging));
            fixture.controller.enabled = false;
            Assert.That(fixture.controller.Rules.State, Is.EqualTo(FootballState.Aiming));
            fixture.controller.enabled = true;
            PointerDown(fixture.shoot.gameObject, 82);
            Assert.That(fixture.controller.Rules.State, Is.EqualTo(FootballState.Charging));
            Assert.That(fixture.controller.Rules.Kicks, Is.Zero);
        }

        [Test]
        public void StartGateDoesNotStartSimulationUntilPlayerStartsAndCountdownCompletes()
        {
            Assert.That(fixture.score.text, Is.EqualTo("BÀN: 0"));
            Assert.That(fixture.remaining.text, Is.EqualTo("CÒN 5 LƯỢT"));
            Assert.That(fixture.controller.Rules.State, Is.EqualTo(FootballState.Start));
            Assert.That(fixture.controller.PresentationPhase, Is.EqualTo(MinigamePhase.Tutorial));
            AdvanceLifecycle(20f);
            Assert.That(fixture.controller.Rules.State, Is.EqualTo(FootballState.Start));
            fixture.aim.onValueChanged.Invoke(.5f);
            Assert.That(fixture.controller.Rules.State, Is.EqualTo(FootballState.Start));
            Assert.That(fixture.controller.BeginMatch(FootballDifficulty.Hard), Is.True);
            Assert.That(fixture.controller.BeginMatch(FootballDifficulty.Easy), Is.False);
            Assert.That(fixture.controller.Rules.State, Is.EqualTo(FootballState.Start));
            Assert.That(fixture.controller.PresentationPhase, Is.EqualTo(MinigamePhase.Countdown));
            AdvanceLifecycle(10f);
            TickPlay(.01f);
            Assert.That(fixture.controller.Rules.State, Is.EqualTo(FootballState.Aiming));
        }

        [Test]
        public void FiveResolvedShotsEmitOneCampaignResultAfterTheFeedbackPhase()
        {
            int completed = 0;
            fixture.controller.Completed += _ => completed++;
            fixture.controller.BeginMatch(FootballDifficulty.Normal);
            AdvanceLifecycle(10f);
            TickPlay(.01f);

            for (int i = 0; i < 5; i++)
            {
                Assert.That(fixture.controller.Rules.State, Is.EqualTo(FootballState.Aiming));
                fixture.controller.Rules.SetAim(0f);
                fixture.controller.Rules.BeginCharge();
                // .9 s charges to power ~.4: a centre shot at chest height that the crouched keeper saves.
                fixture.controller.Rules.Tick(.9f);
                fixture.controller.Rules.ReleaseShot();
                Assert.That(completed, Is.Zero);
                TickPlay(20f);
            }

            Assert.That(fixture.controller.Rules.State, Is.EqualTo(FootballState.MatchResult));
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(fixture.controller.LastResult, Is.Not.Null);
            Assert.That(fixture.controller.LastResult.Pass, Is.False);
            Assert.That(fixture.resultPanel.IsVisible, Is.False, "The campaign router owns when the result panel opens.");
            TickPlay(2f);
            Assert.That(completed, Is.EqualTo(1));
        }

        [Test]
        public void JourneyExamForcesAuthoredDifficultyAndEmitsOneTypedResultAfterFiveKicks()
        {
            ChallengeDefinition definition = ChallengeCatalog.LoadDefault().Get("soccer_exam");
            var context = new ChallengeAttemptContext("attempt", definition.Id, ChallengeAttemptMode.Journey,
                definition.Difficulty);
            fixture.controller.ConfigureChallenge(definition, context);
            int completed = 0;
            ChallengeAttemptResult typed = null;
            fixture.controller.ChallengeCompleted += result => { completed++; typed = result; };
            Assert.That(fixture.controller.BeginMatch(FootballDifficulty.Easy), Is.True);
            AdvanceLifecycle(10f);
            TickPlay(.01f);

            for (int i = 0; i < 5; i++)
            {
                fixture.controller.Rules.SetAim(0f);
                fixture.controller.Rules.BeginCharge();
                fixture.controller.Rules.Tick(.7f);
                fixture.controller.Rules.ReleaseShot();
                TickPlay(20f);
            }

            Assert.That(fixture.controller.Rules.Kicks, Is.EqualTo(5));
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(typed.Context.ChallengeId, Is.EqualTo("soccer_exam"));
            Assert.That(typed.ExamResult, Is.Not.Null);
            Assert.That(fixture.controller.LastResult.Pass, Is.EqualTo(typed.Pass));
        }

        [TestCase(FootballDifficulty.Easy)]
        [TestCase(FootballDifficulty.Hard)]
        public void FreePlayIgnoresLegacyDifficultyAndUsesNormalChargeTiming(FootballDifficulty legacyDifficulty)
        {
            ChallengeDefinition definition = ChallengeCatalog.LoadDefault().Get("soccer_exam");
            fixture.controller.ConfigureChallenge(definition, new ChallengeAttemptContext(
                "normal-replay", definition.Id, ChallengeAttemptMode.FreePlay, ChallengeDifficulty.Normal));
            Assert.That(fixture.controller.BeginMatch(legacyDifficulty), Is.True);
            AdvanceLifecycle(10f);
            TickPlay(.01f);
            fixture.controller.Rules.BeginCharge();
            fixture.controller.Rules.Tick(2.042035f);
            Assert.That(fixture.controller.Rules.Power, Is.EqualTo(1f).Within(.00001f));
        }

        [Test]
        public void PausingCancelsChargeButFreezesAFlightWithoutConsumingAnotherShot()
        {
            fixture.controller.BeginMatch(FootballDifficulty.Normal);
            AdvanceLifecycle(10f);
            TickPlay(.01f);
            fixture.aim.onValueChanged.Invoke(.5f);
            Assert.That(fixture.controller.Rules.State, Is.EqualTo(FootballState.Aiming));
            PointerDown(fixture.shoot.gameObject, 5);
            fixture.controller.Rules.Tick(.3f);
            Assert.That(fixture.controller.Rules.State, Is.EqualTo(FootballState.Charging));
            SendPause(true);
            Assert.That(fixture.controller.Rules.State, Is.EqualTo(FootballState.Aiming));
            Assert.That(fixture.controller.Rules.Kicks, Is.Zero);
            SendPause(false);

            PointerDown(fixture.shoot.gameObject, 6);
            fixture.controller.Rules.Tick(.4f);
            PointerUp(fixture.shoot.gameObject, 6);
            TickPlay(.2f);
            Assert.That(fixture.controller.Rules.State, Is.EqualTo(FootballState.Flying));
            float elapsed = fixture.controller.Rules.FlightElapsed;
            SendPause(true);
            TickPlay(1f);
            Assert.That(fixture.controller.Rules.FlightElapsed, Is.EqualTo(elapsed));
            Assert.That(fixture.controller.Rules.Kicks, Is.Zero);
            SendPause(false);
            TickPlay(.1f);
            Assert.That(fixture.controller.Rules.FlightElapsed, Is.GreaterThan(elapsed));
        }

        [Test]
        public void MissingReferencesLogOnceAndDisableController()
        {
            var root = new GameObject("MissingFootballReferences");
            root.SetActive(false);
            var controller = root.AddComponent<FootballController>();
            LogAssert.Expect(LogType.Error, new Regex("FootballController disabled: required scene references are missing"));
            root.SetActive(true);
            Assert.That(controller.enabled, Is.False);
            Object.DestroyImmediate(root);
        }

        void AdvanceLifecycle(float dt)
        {
            var field = typeof(MinigameBase).GetField("lifecycle", BindingFlags.Instance | BindingFlags.NonPublic);
            ((MinigameLifecycle)field.GetValue(fixture.controller)).Tick(dt);
        }

        void TickPlay(float dt) => typeof(FootballController)
            .GetMethod("TickPlay", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fixture.controller, new object[] { dt });

        void SendPause(bool paused) => typeof(FootballController)
            .GetMethod("OnApplicationPause", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fixture.controller, new object[] { paused });

        static void PointerDown(GameObject target, int id) => ExecuteEvents.Execute(target,
            new PointerEventData(EventSystem.current) { pointerId = id, button = PointerEventData.InputButton.Left },
            ExecuteEvents.pointerDownHandler);
        static void PointerUp(GameObject target, int id) => ExecuteEvents.Execute(target,
            new PointerEventData(EventSystem.current) { pointerId = id, button = PointerEventData.InputButton.Left },
            ExecuteEvents.pointerUpHandler);

        internal sealed class Fixture
        {
            public readonly GameObject root;
            public readonly FootballController controller;
            public readonly Slider aim;
            public readonly FootballHoldButton shoot;
            public readonly ResultPanel resultPanel;
            public readonly TMP_Text score;
            public readonly TMP_Text remaining;
            readonly FootballDifficultyConfig config;
            readonly EventSystem eventSystem;

            public Fixture()
            {
                eventSystem = new GameObject("FootballTestEventSystem").AddComponent<EventSystem>();
                root = new GameObject("FootballControllerFixture");
                root.SetActive(false);
                config = ScriptableObject.CreateInstance<FootballDifficultyConfig>();
                var aimObject = new GameObject("AIM", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
                aimObject.transform.SetParent(root.transform);
                aim = aimObject.GetComponent<Slider>();
                var shootObject = new GameObject("SHOOT", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                    typeof(Button), typeof(FootballHoldButton));
                shootObject.transform.SetParent(root.transform);
                shoot = shootObject.GetComponent<FootballHoldButton>();
                var hud = root.AddComponent<FootballHud>();
                KitBar power = UiKit.Bar(root.transform, "Power", label: true);
                TMP_Text Text(string name)
                {
                    var text = new GameObject(name).AddComponent<TextMeshPro>();
                    text.transform.SetParent(root.transform);
                    return text;
                }
                var markers = new Image[5];
                for (int i = 0; i < markers.Length; i++)
                {
                    markers[i] = new GameObject("Kick" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                    markers[i].transform.SetParent(root.transform);
                }
                var warning = new GameObject("Warning", typeof(RectTransform)); warning.transform.SetParent(root.transform);
                var start = new GameObject("Start", typeof(RectTransform)); start.transform.SetParent(root.transform);
                score = Text("Score");
                remaining = Text("Remaining");
                hud.Configure(aim, shoot, power, warning, score, remaining, markers, start);
                var input = root.AddComponent<FootballInputBridge>();
                var presentation = root.AddComponent<FootballPresentation>();
                SpriteRenderer Make(string name)
                {
                    var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
                    renderer.transform.SetParent(root.transform);
                    return renderer;
                }
                var left = new GameObject("LeftPost").transform; left.SetParent(root.transform); left.localPosition = new Vector3(-4f, 0f);
                var right = new GameObject("RightPost").transform; right.SetParent(root.transform); right.localPosition = new Vector3(4f, 0f);
                presentation.Configure(Make("Field"), Make("Goal"), Make("Ball"), Make("Shadow"),
                    Make("Player"), Make("Keeper"), Make("Crosshair"), left, right);
                resultPanel = root.AddComponent<ResultPanel>();
                var resultContent = new GameObject("ResultContent", typeof(RectTransform));
                resultContent.transform.SetParent(root.transform);
                resultContent.SetActive(false);
                var continueButton = new GameObject("Continue", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)).GetComponent<Button>();
                var retryButton = new GameObject("Retry", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)).GetComponent<Button>();
                continueButton.transform.SetParent(root.transform); retryButton.transform.SetParent(root.transform);
                resultPanel.Configure(resultContent, Text("Result"), Text("Goals"), Text("ResultScore"), Text("Rank"), Text("Lives"), continueButton, retryButton);
                controller = root.AddComponent<FootballController>();
                controller.Configure(config, input, presentation, hud, resultPanel);
                root.SetActive(true);
            }

            public void Dispose()
            {
                if (root) Object.DestroyImmediate(root);
                if (config) Object.DestroyImmediate(config);
                if (eventSystem) Object.DestroyImmediate(eventSystem.gameObject);
            }
        }
    }
}
