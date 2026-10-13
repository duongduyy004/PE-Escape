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
            var follow = camera.GetComponent<SprintCameraFollow>();
            Assert.That(follow, Is.Not.Null);
            yield return null;
            float startX = follow.StartX;
            Assert.That(camera.transform.position.x - camera.orthographicSize * camera.aspect,
                Is.EqualTo(SprintTrackLayout.TrackStartX - SprintCameraFollow.StartMargin).Within(.001f),
                "the start line sits just inside the left edge, so the grid is fully on screen");

            // One unit short of the hold point at this aspect: the player is still left of centre.
            controller.AdvanceToDistance(startX - SprintTrackLayout.TrackStartX - 1f,
                new[] { new RivalPaceProfile("Far", 9f, 9f) });
            yield return null;
            yield return null;
            Assert.That(camera.transform.position.x, Is.EqualTo(startX).Within(.001f),
                "the player is still left of centre, so the camera waits");

            controller.AdvanceToDistance(60f, new[] { new RivalPaceProfile("Far", 9f, 9f) });
            yield return null;
            yield return null;
            Assert.That(camera.transform.position.x, Is.EqualTo(SprintTrackLayout.WorldX(60f)).Within(.001f),
                "the camera keeps the player centred, whatever the rivals do");

            // Mid-run at speed the camera sits exactly on the player: no lag pushing them off centre.
            for (int i = 0; i < 6; i++) { controller.OnLeftTap(); controller.OnRightTap(); }
            var player = GameObject.Find("Player").transform;
            float previous = player.position.x;
            for (int frame = 0; frame < 10; frame++)
            {
                controller.Simulate(.05f);
                // The coroutine resumes after Update but before LateUpdate: give the camera its frame.
                yield return null;
                yield return null;
                Assert.That(camera.transform.position.x, Is.EqualTo(player.position.x).Within(1e-3f),
                    $"frame {frame}: the player must stay centred while running");
            }
            Assert.That(player.position.x, Is.GreaterThan(previous + 1f), "the player really was running");
        }

        [UnityTest]
        public IEnumerator TrackTilesCoverTheWholeViewAtEveryAspect()
        {
            float? authoredRootOffset = null;
            foreach (float aspect in new[] { 4f / 3f, 16f / 9f, 20f / 9f, 21f / 9f })
            {
                yield return SceneManager.LoadSceneAsync("MG_Sprint");
                var controller = Object.FindFirstObjectByType<SprintController>();
                controller.ConfigureForTest();
                controller.enabled = false;
                var camera = Camera.main;
                camera.aspect = aspect;
                var follow = camera.GetComponent<SprintCameraFollow>();
                var parallax = Object.FindFirstObjectByType<SprintParallax>();
                yield return null;
                yield return null;

                float halfWidth = camera.orthographicSize * aspect;
                Assert.That(camera.transform.position.x,
                    Is.EqualTo(SprintCameraFollow.StartXFor(halfWidth)).Within(.001f), $"start at {aspect:0.00}");
                AssertBackdropCovers(parallax, camera.transform.position.x, halfWidth, $"{aspect:0.00} at the start");
                // The backdrop root rides with the camera at its authored offset, whatever the aspect:
                // travel is measured from the authored camera X, not from the aspect-dependent start.
                float rootOffset = parallax.transform.position.x - camera.transform.position.x;
                if (authoredRootOffset == null) authoredRootOffset = rootOffset;
                Assert.That(rootOffset, Is.EqualTo(authoredRootOffset.Value).Within(.001f),
                    $"backdrop root off-centre at {aspect:0.00}");

                // 200 units down the straight, as the parallax sees it after the camera has moved.
                camera.transform.position += Vector3.right * 200f;
                parallax.RefreshForTest(follow.Travel);
                AssertBackdropCovers(parallax, camera.transform.position.x, halfWidth, $"{aspect:0.00} after 200 units");
                Assert.That(parallax.transform.position.x - camera.transform.position.x,
                    Is.EqualTo(authoredRootOffset.Value).Within(.001f), $"backdrop root drifted at {aspect:0.00}");
                camera.ResetAspect();
            }
        }

        /// Every backdrop layer (the factor-1 track above all) must span the whole view width.
        static void AssertBackdropCovers(SprintParallax parallax, float cameraX, float halfWidth, string label)
        {
            var layers = parallax.GetComponentsInChildren<SpriteRenderer>()
                .Where(renderer => renderer.sprite != null)
                .GroupBy(renderer => renderer.sprite.name).ToArray();
            Assert.That(layers.Select(layer => layer.Key), Does.Contain("SprintTrack"), label);
            float left = cameraX - halfWidth, right = cameraX + halfWidth;
            foreach (var layer in layers)
            {
                var tiles = layer.Select(renderer => renderer.bounds).OrderBy(bounds => bounds.min.x).ToArray();
                string name = $"{label}, {layer.Key}";
                Assert.That(tiles, Has.Length.EqualTo(2), name);
                Assert.That(tiles[0].min.x, Is.LessThanOrEqualTo(left + 1e-3f), $"{name}: gap at the left edge");
                Assert.That(tiles[0].max.x, Is.GreaterThanOrEqualTo(tiles[1].min.x - 1e-3f), $"{name}: gap between tiles");
                Assert.That(tiles[1].max.x, Is.GreaterThanOrEqualTo(right - 1e-3f), $"{name}: gap at the right edge");
            }
        }

        [UnityTest]
        public IEnumerator PlayerFinishingLastRunsOutTheStrideBeforeCelebrating()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint");
            var controller = Object.FindFirstObjectByType<SprintController>();
            controller.ConfigureForTest();
            controller.enabled = false;
            controller.AdvanceToDistance(SprintRules.RaceDistance - 1f, new[] { new RivalPaceProfile("Fast", 500f, 500f) });
            for (int i = 0; i < 4; i++) { controller.OnLeftTap(); controller.OnRightTap(); }
            for (int i = 0; i < 300 && controller.Phase != MinigamePhase.Resolve; i++) controller.Simulate(.01f);
            Assert.That(controller.Phase, Is.EqualTo(MinigamePhase.Resolve));
            Assert.That(controller.LastResult.Pass, Is.True);
            Assert.That(controller.PlayerMetersPerSecond, Is.GreaterThan(.05f), "still running out past the line");

            var animator = GameObject.Find("Player").GetComponentInChildren<Animator>();
            yield return null;
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Run"), Is.True,
                "a body still sliding past the line keeps its stride");

            for (int i = 0; i < 500; i++) controller.Simulate(.01f);
            Assert.That(controller.PlayerMetersPerSecond, Is.Zero);
            yield return null;
            yield return null;
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Celebrate"), Is.True,
                "the cheer comes once the runner has stopped");
        }

        [UnityTest]
        public IEnumerator RivalStillMovingInResolveKeepsRunning()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint");
            var rival = Object.FindObjectsByType<RivalRunnerAI>(FindObjectsSortMode.None)[0];
            rival.enabled = false;
            var pass = new MinigameResult(true, 1f, Rank.C);

            rival.RefreshForTest(140f, 6f, false, SprintRules.RaceDistance, MinigamePhase.Resolve, pass);
            Assert.That(rival.State, Is.EqualTo(RivalRunnerState.Run), "the field races in after the result");
            rival.RefreshForTest(153f, 0f, true, SprintRules.RaceDistance, MinigamePhase.Resolve, pass);
            Assert.That(rival.State, Is.EqualTo(RivalRunnerState.Celebrate));
            rival.RefreshForTest(153f, 0f, true, SprintRules.RaceDistance, MinigamePhase.Resolve,
                new MinigameResult(false, 0f, Rank.F));
            Assert.That(rival.State, Is.EqualTo(RivalRunnerState.Fail));
            yield return null;
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
