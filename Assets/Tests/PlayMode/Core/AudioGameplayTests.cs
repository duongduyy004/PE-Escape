using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KMA.Gameplay;
using KMA.Gameplay.Core;
using KMA.Gameplay.Volleyball;
using KMA.Gameplay.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace KMA.Tests.Gameplay.Core
{
    public sealed class AudioGameplayTests
    {
        readonly List<GameSound> sounds = new List<GameSound>();
#if UNITY_EDITOR
        bool originalEditorMute;
#endif

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            originalEditorMute = UnityEditor.EditorUtility.audioMasterMute;
            UnityEditor.EditorUtility.audioMasterMute = false;
#endif
            sounds.Clear();
            GameAudio.Requested += Record;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameAudio.Requested -= Record;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.audioMasterMute = originalEditorMute;
#endif
            Time.timeScale = 1f;
            Scene previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("AudioTestCleanup"));
            if (previous.IsValid() && previous.isLoaded) yield return SceneManager.UnloadSceneAsync(previous);
            foreach (var manager in Object.FindObjectsByType<AudioManager>(FindObjectsSortMode.None))
                Object.Destroy(manager.gameObject);
            yield return null;
        }

        static IEnumerator WaitForOpening()
        {
            var guide=MinigameGuideHost.Current?.Panel;while(guide!=null && guide.IsOpen)guide.PressPrimary();
            yield return null;var opening=Object.FindFirstObjectByType<GameplayOpeningFlow>();float deadline=Time.realtimeSinceStartup+12;
            while(opening!=null && !opening.IsComplete){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
        }

        void Record(GameSound sound) => sounds.Add(sound);

        [UnityTest]
        public IEnumerator AllPlayableScenesHaveOneListenerAndProduceAudioSamples()
        {
            var samples = new float[1024];
            foreach (string scene in new[] { "MG_Sprint", "MG_Volleyball", "MG_Football" })
            {
                yield return SceneManager.LoadSceneAsync(scene);
                Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)
                    .Count(listener => listener.isActiveAndEnabled), Is.EqualTo(1), scene + " needs an active audio listener.");
                AudioManager.Instance.SetMusicVolume(1f);
                yield return null;
                // Start can bind saved settings on the first frame. Set the test's
                // explicit audible volume after that lifecycle work has completed.
                AudioManager.Instance.SetMusicVolume(1f);
                float peak = 0f;
                // Cipher has about .82 s of leading silence; sample beyond the intro.
                float until = Time.realtimeSinceStartup + 2f;
                while (Time.realtimeSinceStartup < until)
                {
                    AudioListener.GetOutputData(samples, 0);
                    foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
                    yield return null;
                }
                Debug.Log($"[AudioQA] {scene} output peak={peak}");
                Assert.That(peak, Is.GreaterThan(.00001f), scene + " should produce a non-silent audio mix.");
            }
        }

        [UnityTest]
        public IEnumerator SprintMovementProducesStepsAndPauseStopsThem()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint");
            var runner = Object.FindFirstObjectByType<SprintController>();
            runner.ConfigureForTest();
            sounds.Clear();
            runner.Simulate(.1f);
            Assert.That(sounds, Has.No.Member(GameSound.RunStep), "An idle athlete must not have footsteps.");
            for (int i = 0; i < 20; i++)
                if (i % 2 == 0) runner.OnLeftTap(); else runner.OnRightTap();
            runner.Simulate(.25f);
            Assert.That(sounds, Has.Member(GameSound.RunStep));
            Time.timeScale = 0f;
            sounds.Clear();
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(sounds, Has.No.Member(GameSound.RunStep));
        }

        [UnityTest]
        public IEnumerator VolleyballTossIsSilentAndContactPlaysOnce()
        {
            yield return SceneManager.LoadSceneAsync("MG_Volleyball");
            var controller = Object.FindFirstObjectByType<VolleyballController>();
            yield return WaitForOpening();
            controller.SkipToPlayForTest();
            sounds.Clear();
            controller.Input.FeedActionForTest();
            yield return null;
            Assert.That(controller.Match.BallState, Is.EqualTo(BallState.Toss));
            Assert.That(sounds, Has.No.Member(GameSound.VolleyHit));
            controller.Match.Tick(controller.Match.Flight.ApexTime - controller.Match.FlightTime);
            controller.Input.FeedActionForTest();
            yield return null;
            Assert.That(sounds.Count(x => x == GameSound.VolleyHit), Is.EqualTo(1));
            yield return null;
            Assert.That(sounds.Count(x => x == GameSound.VolleyHit), Is.EqualTo(1), "A flight must not replay its impact every frame.");
        }

        [UnityTest]
        public IEnumerator FootballKickAndOutcomePlayOnceAcrossManyFrames()
        {
            yield return SceneManager.LoadSceneAsync("MG_Football");
            var controller = Object.FindFirstObjectByType<FootballController>();
            yield return WaitForOpening();
            Assert.That(controller.BeginMatch(FootballDifficulty.Normal), Is.True);
            var lifecycle = (MinigameLifecycle)typeof(MinigameBase).GetField("lifecycle",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            lifecycle.Tick(4f);
            yield return null;
            sounds.Clear();
            controller.Rules.BeginCharge();
            controller.Rules.Tick(.7f);
            controller.Rules.ReleaseShot();
            float deadline = Time.realtimeSinceStartup + 12f;
            while (controller.Rules.Kicks == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.Rules.Kicks, Is.EqualTo(1));
            yield return new WaitForSeconds(.15f);
            Assert.That(sounds.Count(x => x == GameSound.Kick), Is.EqualTo(1));
            Assert.That(sounds.Count(x => x == GameSound.Save || x == GameSound.Post ||
                x == GameSound.Cheer || x == GameSound.Miss), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LateCreatedButtonGetsOneClickFeedbackAndDisabledButtonIsSilent()
        {
            yield return SceneManager.LoadSceneAsync("MG_Football");
            var canvas = Object.FindFirstObjectByType<Canvas>();
            var root = new GameObject("LateAudioButton", typeof(RectTransform), typeof(Button));
            root.transform.SetParent(canvas.transform, false);
            var button = root.GetComponent<Button>();
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            sounds.Clear();
            Click(button);
            Assert.That(sounds.Count(x => x == GameSound.Click), Is.EqualTo(1));
            button.interactable = false;
            Click(button);
            Assert.That(sounds.Count(x => x == GameSound.Click), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AButtonThatHidesItsPanelStillMakesOneClick()
        {
            yield return SceneManager.LoadSceneAsync("MG_Football");
            var canvas = Object.FindFirstObjectByType<Canvas>();
            var root = new GameObject("ClosingAudioButton", typeof(RectTransform), typeof(Button));
            root.transform.SetParent(canvas.transform, false);
            var button = root.GetComponent<Button>();
            button.onClick.AddListener(() => root.SetActive(false));
            yield return null;
            Canvas.ForceUpdateCanvases();
            sounds.Clear();
            Click(button);
            Assert.That(root.activeSelf, Is.False);
            Assert.That(sounds.Count(x => x == GameSound.Click), Is.EqualTo(1));
        }

        static void Click(Button button) => ExecuteEvents.Execute(button.gameObject,
            new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left },
            ExecuteEvents.pointerClickHandler);
    }
}
