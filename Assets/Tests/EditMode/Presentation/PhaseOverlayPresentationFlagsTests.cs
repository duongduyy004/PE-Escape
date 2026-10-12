using System.Reflection;
using KMA.Gameplay;
using KMA.Gameplay.UI;
using NUnit.Framework;
using UnityEngine;

namespace KMA.Tests.Presentation
{
    public sealed class PhaseOverlayPresentationFlagsTests
    {
        GameObject root;
        PhaseOverlay overlay;
        GameObject tutorialRoot, countdownRoot, playRoot, resolveRoot;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("PhaseOverlay");
            root.SetActive(false);
            overlay = root.AddComponent<PhaseOverlay>();
            tutorialRoot = Child("TutorialRoot");
            countdownRoot = Child("CountdownRoot");
            playRoot = Child("PlayRoot");
            resolveRoot = Child("ResolveRoot");
            Set("tutorialRoot", tutorialRoot);
            Set("countdownRoot", countdownRoot);
            Set("playRoot", playRoot);
            Set("resolveRoot", resolveRoot);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void SharedMinigamesShowTheTutorialThenTheCountdown()
        {
            FlagMinigame minigame = Minigame(sharedTutorial: true, sharedCountdown: true, ownsGate: false);
            overlay.Bind(minigame);
            Assert.That(tutorialRoot.activeSelf, Is.True);
            minigame.Advance(2f);
            Assert.That(countdownRoot.activeSelf, Is.True);
            Assert.That(tutorialRoot.activeSelf, Is.False);
        }

        [Test]
        public void CustomTutorialWithSharedCountdownWaitsForItsGuide()
        {
            FlagMinigame minigame = Minigame(sharedTutorial: false, sharedCountdown: true, ownsGate: false);
            minigame.SetTutorialGate(true);
            overlay.Bind(minigame);
            Assert.That(minigame.PresentationPhase,Is.EqualTo(MinigamePhase.Tutorial),"overlay must not release the guide hold");
            Assert.That(countdownRoot.activeSelf,Is.False);
            minigame.SetTutorialGate(false);
            Assert.That(minigame.PresentationPhase, Is.EqualTo(MinigamePhase.Countdown), "closing the guide opens the requested countdown");
            Assert.That(countdownRoot.activeSelf, Is.True);
            Assert.That(tutorialRoot.activeSelf, Is.False);
        }

        [Test]
        public void OwnedGateStaysClosedUntilTheMinigameOpensIt()
        {
            FlagMinigame minigame = Minigame(sharedTutorial: false, sharedCountdown: true, ownsGate: true);
            minigame.SetTutorialGate(true);
            overlay.Bind(minigame);
            minigame.Advance(10f);
            Assert.That(minigame.PresentationPhase, Is.EqualTo(MinigamePhase.Tutorial), "the difficulty picker is still up");
            Assert.That(tutorialRoot.activeSelf, Is.False);
            Assert.That(countdownRoot.activeSelf, Is.False);

            minigame.SetTutorialGate(false);
            Assert.That(minigame.PresentationPhase, Is.EqualTo(MinigamePhase.Countdown));
            Assert.That(countdownRoot.activeSelf, Is.True, "the shared 3-2-1 follows BẮT ĐẦU");
        }

        [Test]
        public void MinigamesWithTheirOwnCountdownHideTheSharedOne()
        {
            FlagMinigame minigame = Minigame(sharedTutorial: false, sharedCountdown: false, ownsGate: true);
            overlay.Bind(minigame);
            minigame.SetTutorialGate(false);
            Assert.That(minigame.PresentationPhase, Is.EqualTo(MinigamePhase.Countdown));
            Assert.That(countdownRoot.activeSelf, Is.False);
        }

        [Test]
        public void BaseDefaultsKeepTheSharedPresentation()
        {
            FlagMinigame minigame = Minigame(sharedTutorial: true, sharedCountdown: true, ownsGate: false);
            Assert.That(typeof(MinigameBase).GetProperty(nameof(MinigameBase.UsesSharedTutorial)).GetGetMethod().IsVirtual, Is.True);
            Assert.That(minigame.BaseUsesSharedTutorial, Is.True);
            Assert.That(minigame.BaseUsesSharedCountdown, Is.True);
            Assert.That(minigame.BaseOwnsStartGate, Is.False);
        }

        [Test]
        public void ResolveLeavesTheHeadlineToTheResultPanel()
        {
            var phaseLabel = new GameObject("PhaseLabel").AddComponent<TMPro.TextMeshPro>();
            phaseLabel.transform.SetParent(root.transform, false);
            Set("phaseLabel", phaseLabel);
            FlagMinigame minigame = Minigame(sharedTutorial: true, sharedCountdown: true, ownsGate: false);
            overlay.Bind(minigame);
            for (int i = 0; i < 20 && minigame.PresentationPhase != MinigamePhase.Play; i++)
                minigame.Advance(1f);
            Assert.That(minigame.PresentationPhase, Is.EqualTo(MinigamePhase.Play));
            minigame.Lose();
            Assert.That(minigame.PresentationPhase, Is.EqualTo(MinigamePhase.Resolve));
            Assert.That(resolveRoot.activeSelf, Is.False, "no RESOLVE chip on top of the result panel");
            Assert.That(phaseLabel.text, Is.Empty, "no second headline above the result title");
        }

        GameObject Child(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            return child;
        }

        void Set(string field, object value) =>
            typeof(PhaseOverlay).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(overlay, value);

        FlagMinigame Minigame(bool sharedTutorial, bool sharedCountdown, bool ownsGate)
        {
            var minigame = new GameObject("Minigame").AddComponent<FlagMinigame>();
            minigame.transform.SetParent(root.transform, false);
            minigame.Initialize(sharedTutorial, sharedCountdown, ownsGate);
            return minigame;
        }

        sealed class FlagMinigame : MinigameBase
        {
            bool sharedTutorial = true, sharedCountdown = true, ownsGate;

            public override bool UsesSharedTutorial => sharedTutorial;
            public override bool UsesSharedCountdown => sharedCountdown;
            public override bool OwnsStartGate => ownsGate;
            public bool BaseUsesSharedTutorial => base.UsesSharedTutorial;
            public bool BaseUsesSharedCountdown => base.UsesSharedCountdown;
            public bool BaseOwnsStartGate => base.OwnsStartGate;

            public void Initialize(bool tutorial, bool countdown, bool gate)
            {
                sharedTutorial = tutorial;
                sharedCountdown = countdown;
                ownsGate = gate;
                if (Lifecycle == null)
                    Awake();
            }

            public void Lose() => Finish(new MinigameResult(false, 0f, Rank.F));
            public void Advance(float seconds) => Lifecycle.Tick(seconds);
            protected override void TickPlay(float dt) { }
        }
    }
}
