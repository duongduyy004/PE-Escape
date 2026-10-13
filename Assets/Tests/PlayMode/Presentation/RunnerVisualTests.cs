using System.Collections;
using System.Linq;
using KMA.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KMA.Tests.Presentation
{
    public sealed class RunnerVisualTests
    {
        [UnityTest]
        public IEnumerator LargerVisualScaleLivesOutsideAnimatedSpriteTransforms()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint");
            var player = GameObject.Find("Player").GetComponentInChildren<RunnerVisualPresenter>(true);
            Assert.That(player.transform.localScale.x, Is.EqualTo(1.25f).Within(.001f));
            var playerAnimator = player.GetComponent<Animator>();
            Assert.That(playerAnimator, Is.Not.Null);
            playerAnimator.Play("Run", 0, 0f);
            playerAnimator.Update(0f);
            playerAnimator.Play("Idle", 0, 0f);
            playerAnimator.Update(0f);
            Assert.That(player.transform.localScale.x, Is.EqualTo(1.25f).Within(.001f),
                "State clips must not reset the enlarged player parent.");

            foreach (var rival in Object.FindObjectsByType<RivalRunnerAI>(FindObjectsSortMode.None))
            {
                Assert.That(rival.transform.localScale.x, Is.EqualTo(1.625f).Within(.001f));
                rival.Animator.Play("Run", 0, 0f);
                rival.Animator.Update(0f);
                rival.Animator.Play("Idle", 0, 0f);
                rival.Animator.Update(0f);
                Assert.That(rival.transform.localScale.x, Is.EqualTo(1.625f).Within(.001f),
                    "State clips must not reset the enlarged rival parent.");
            }
        }

        [UnityTest]
        public IEnumerator PlayIsUninterrupted_KeepsPlayerAndRivalsRunning()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint");
            var controller = Object.FindFirstObjectByType<SprintController>();
            controller.ConfigureForTest();
            controller.enabled = false;
            controller.AdvanceToDistance(30f);
            controller.Simulate(0f);
            controller.Simulate(2.01f);
            Assert.That(controller.Phase, Is.EqualTo(MinigamePhase.Play));
            // A runner who is not moving now rests and breathes, so get the player up to speed first.
            for (int i = 0; i < 4; i++)
            {
                controller.OnLeftTap();
                controller.OnRightTap();
            }
            yield return null;
            yield return null;
            var player = GameObject.Find("Player").GetComponentInChildren<Animator>();
            Assert.That(player.GetCurrentAnimatorStateInfo(0).IsName("Run"), Is.True,
                "Nothing interrupts a mid-race runner now that the wind challenge is gone.");
            foreach (var rival in Object.FindObjectsByType<RivalRunnerAI>(FindObjectsSortMode.None)
                .Where(r => r.RivalIndex < controller.RivalCount))
                Assert.That(rival.Animator.GetCurrentAnimatorStateInfo(0).IsName("Run"), Is.True);
        }

        [UnityTest]
        public IEnumerator PlayerFramesAnimateWithoutChangingRulesAndFreezeWhenPaused()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint");
            var controller = Object.FindFirstObjectByType<SprintController>();
            controller.ConfigureForTest();
            controller.enabled = false;
            var player = GameObject.Find("Player");
            var animator = player.GetComponentInChildren<Animator>();
            Assert.That(animator, Is.Not.Null, "The player requires a real sprite animator.");
            var renderer = animator.GetComponentInChildren<SpriteRenderer>();
            var snapshot = controller.Snapshot;
            yield return null;
            animator.Play("Run", 0, 0f);
            animator.Update(0f);
            var firstFrame = renderer.sprite;
            animator.Update(0.13f);
            Assert.That(renderer.sprite, Is.Not.EqualTo(firstFrame), "Run must change poses, not just bounce one image.");
            Assert.That(controller.Snapshot, Is.EqualTo(snapshot));
            float originalTime = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                yield return null;
                var frozen = renderer.sprite;
                yield return new WaitForSecondsRealtime(0.2f);
                Assert.That(renderer.sprite, Is.EqualTo(frozen));
            }
            finally { Time.timeScale = originalTime; }
            yield return SceneManager.LoadSceneAsync("MG_Sprint");
            var resetAnimator = GameObject.Find("Player").GetComponentInChildren<Animator>();
            Assert.That(resetAnimator, Is.Not.Null);
            yield return null;
            Assert.That(resetAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
        }

        [UnityTest]
        public IEnumerator SceneUsesArtworkOnEveryParallaxTileAndRunner()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint");
            var parallax = Object.FindFirstObjectByType<SprintParallax>();
            var backgrounds = parallax.GetComponentsInChildren<SpriteRenderer>();
            Assert.That(backgrounds, Has.Length.EqualTo(6),
                "The backdrop is artwork only — it reaches the viewport floor without a filler strip.");
            Assert.That(backgrounds.First(renderer => renderer.sprite.name == "SprintTrack").bounds.min.y,
                Is.LessThanOrEqualTo(-5.4f),
                "The track must cover the viewport floor so no sky shows beneath it.");

            foreach (var background in backgrounds)
            {
                Assert.That(background.sprite, Is.Not.Null);
                Assert.That(background.sharedMaterial, Is.Not.Null,
                    "Authored artwork must have a renderable material in the Android build.");
                Assert.That(background.sprite.texture.width, Is.GreaterThan(1000));
                Assert.That(background.bounds.size.x, Is.EqualTo(25.6f).Within(0.01f));
            }
            var rivals = Object.FindObjectsByType<RivalRunnerAI>(FindObjectsSortMode.None);
            Assert.That(rivals, Has.Length.EqualTo(3));
            foreach (var rival in rivals)
            {
                Assert.That(rival.Sprite.sprite.texture.name,
                    Does.Match("^(MalePerson|FemalePerson|FemaleAdventurer)_"),
                    "Rivals draw from the shared character library, never from the hero.");
                Assert.That(rival.Animator.runtimeAnimatorController.animationClips
                    .Where(c => c.name.Contains("Run")), Is.Not.Empty);
            }
        }

        [UnityTest]
        public IEnumerator EveryRunnerUsesItsOwnPackCharacter()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint");
            var rivals = Object.FindObjectsByType<RivalRunnerAI>(FindObjectsSortMode.None);
            Assert.That(rivals, Has.Length.EqualTo(3));
            var playerVisual = GameObject.Find("Player").GetComponentInChildren<SpriteRenderer>();
            Assert.That(playerVisual, Is.Not.Null);

            var sprites = rivals.Select(rival => rival.Sprite.sprite).Append(playerVisual.sprite).ToArray();
            Assert.That(sprites, Has.No.Null);
            Assert.That(sprites.Distinct().Count(), Is.EqualTo(4),
                "The player and all three rivals must be visually distinct characters, not four copies.");

            var playerAnimator = GameObject.Find("Player").GetComponentInChildren<Animator>();
            var controllers = rivals.Select(rival => rival.Animator.runtimeAnimatorController)
                .Append(playerAnimator.runtimeAnimatorController).ToArray();
            Assert.That(controllers, Has.No.Null);
            Assert.That(controllers.Distinct().Count(), Is.EqualTo(4),
                "Each character needs its own clip set, so each runner needs its own controller.");
        }

        [UnityTest]
        public IEnumerator CelebrateAndFailUseTheirOwnPosesRatherThanIdleAndHit()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint");
            var rival = Object.FindObjectsByType<RivalRunnerAI>(FindObjectsSortMode.None)[0];
            var animator = rival.Animator;
            var renderer = rival.Sprite;

            Sprite PoseOf(string state)
            {
                animator.Play(state, 0, 0f);
                animator.Update(0f);
                return renderer.sprite;
            }

            var idle = PoseOf("Idle");
            var hit = PoseOf("Stumble");
            var celebrate = PoseOf("Celebrate");
            var fail = PoseOf("Fail");

            Assert.That(celebrate, Is.Not.EqualTo(idle),
                "Winning must cheer rather than reuse the standing idle pose.");
            Assert.That(fail, Is.Not.EqualTo(hit),
                "Losing must use the authored fall pose rather than reuse the hit pose.");
            Assert.That(fail, Is.Not.EqualTo(idle));
        }

        [UnityTest]
        public IEnumerator PlayerMarkerStaysInViewAtBothEndsOfTheRace()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint");
            var controller = Object.FindFirstObjectByType<SprintController>();
            controller.ConfigureForTest();
            controller.enabled = false;
            yield return null;

            var marker = GameObject.Find("Player").GetComponentInChildren<TMPro.TextMeshPro>(true).transform.parent;
            Assert.That(marker.name, Is.EqualTo("PlayerMarker"));
            const float halfView = 9.6f;

            foreach (float distance in new[] { 0f, SprintRules.RaceDistance })
            {
                controller.AdvanceToDistance(distance);
                yield return null;
                yield return null;
                float fromCentre = marker.position.x - Camera.main.transform.position.x;
                Assert.That(fromCentre, Is.InRange(-halfView, halfView), $"marker off screen at {distance} m");
            }
        }

        [UnityTest]
        public IEnumerator CameraHoldsAtTheStartThenFollowsOnlyThePlayer()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint");
            var controller = Object.FindFirstObjectByType<SprintController>();
            controller.ConfigureForTest();
            controller.enabled = false;
            var camera = Camera.main;
            Assert.That(camera.GetComponent<SprintCameraFollow>(), Is.Not.Null);
            float startX = camera.GetComponent<SprintCameraFollow>().StartX;

            controller.AdvanceToDistance(5f, new[] { new RivalPaceProfile("Far", 9f, 9f) });
            yield return null;
            yield return null;
            Assert.That(camera.transform.position.x, Is.EqualTo(startX).Within(.001f),
                "the player is still left of centre, so the camera waits");

            controller.AdvanceToDistance(60f, new[] { new RivalPaceProfile("Far", 9f, 9f) });
            yield return null;
            yield return null;
            Assert.That(camera.transform.position.x, Is.EqualTo(SprintTrackLayout.WorldX(60f)).Within(.01f),
                "the camera keeps the player centred, whatever the rivals do");
        }

        [UnityTest]
        public IEnumerator PlayerStandsAtItsWorldPositionOnTheStraight()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint");
            var controller = Object.FindFirstObjectByType<SprintController>();
            controller.ConfigureForTest();
            controller.enabled = false;
            var player = GameObject.Find("Player");

            controller.AdvanceToDistance(SprintRules.RaceDistance / 2f);
            yield return null;

            Assert.That(player.transform.position.x,
                Is.EqualTo(SprintTrackLayout.WorldX(SprintRules.RaceDistance / 2f)).Within(.001f),
                "One race metre is one world unit from the start.");
            Assert.That(player.transform.position.y,
                Is.EqualTo(SprintTrackLayout.LaneCenterYForAuthoredLane(2)).Within(.001f),
                "Race progress must not move the player out of lane 2.");
        }

        [UnityTest]
        public IEnumerator SprintUsesSafeAreaBroadcastChromeAndKeepsCustomMetrics()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint");

            Transform safeArea = GameObject.Find("SafeAreaRoot")?.transform;
            Assert.That(safeArea, Is.Not.Null,
                "Sprint must use the shared safe-area root for its mobile presentation.");
            Assert.That(safeArea.Find("SprintBroadcastChrome"), Is.Not.Null,
                "The Sprint-specific broadcast chrome must replace generic HUD content.");
            Assert.That(safeArea.Find("SprintBroadcastChrome/Scoreboard"), Is.Not.Null,
                "Sprint-specific race metrics must remain visible.");
        }

        [UnityTest]
        public IEnumerator TrackLayerIsNailedToTheWorldWhileTheSkyDrifts()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint");
            var parallax = Object.FindFirstObjectByType<SprintParallax>();
            Assert.That(parallax.TryGetLayerTilePositions(2, out var trackBefore, out _), Is.True);
            Assert.That(parallax.TryGetLayerTilePositions(0, out var skyBefore, out _), Is.True);
            float rootBefore = parallax.transform.position.x;

            parallax.RefreshForTest(25f);

            Assert.That(parallax.transform.position.x, Is.EqualTo(rootBefore + 25f).Within(.001f));
            Assert.That(parallax.TryGetLayerTilePositions(2, out var trackAfter, out _), Is.True);
            Assert.That(rootBefore + 25f + trackAfter.x, Is.EqualTo(rootBefore + trackBefore.x).Within(.001f),
                "the painted track must not move in the world, so runners and lines stay on it");
            Assert.That(parallax.TryGetLayerTilePositions(0, out var skyAfter, out _), Is.True);
            Assert.That(skyAfter.x - skyBefore.x, Is.EqualTo(-25f * .15f).Within(.001f),
                "the sky drifts past at its own slower rate");
        }
    }
}
