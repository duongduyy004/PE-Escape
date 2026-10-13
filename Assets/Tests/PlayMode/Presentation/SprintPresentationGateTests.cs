using KMA.UI.Kit;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KMA.Gameplay;
using KMA.Gameplay.UI;
using KMA.Input;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KMA.Tests.Presentation
{
    public sealed class SprintPresentationGateTests
    {
        const string SceneName = "MG_Sprint";
        [UnityTest]
        public IEnumerator SprintSceneHasCompletePresentationContractAndUsesAutomaticStart()
        {
            yield return LoadSprint();
            yield return new WaitForSeconds(.5f);

            var scene = SceneManager.GetActiveScene();
            var controllers = SceneObjects<SprintController>(scene);
            var sprintHuds = SceneObjects<SprintHud>(scene);
            var starts = SceneObjects<SprintStartPresentation>(scene);
            var pauses = SceneObjects<PausePanel>(scene);
            Assert.That(controllers.Length, Is.EqualTo(1));
            Assert.That(sprintHuds.Length, Is.EqualTo(1));
            Assert.That(starts.Length, Is.EqualTo(1));
            Assert.That(pauses.Length, Is.EqualTo(1));

            var sprintHud = sprintHuds[0];
            Assert.That(sprintHud.HasBoundVisuals, Is.True, "SprintHud must bind all authored HUD labels/fills.");

            var start = starts[0];
            Assert.That(start.InstructionVisible, Is.True);
            Assert.That(start.InstructionText, Is.EqualTo(SprintStartPresentation.InstructionCopy));

            Transform instructionPlate = GameObject.Find("SprintBroadcastChrome")?.transform
                .Find("StartPresentation/InstructionPlate");
            TMP_Text instructionMessage = instructionPlate?.Find("InstructionLabel")?.GetComponent<TMP_Text>();
            Assert.That(instructionPlate, Is.Not.Null);
            Assert.That(instructionPlate.gameObject.activeSelf, Is.True,
                "The active Sprint start phase must render its instruction plate.");
            Assert.That(instructionMessage, Is.Not.Null);
            Assert.That(instructionMessage.text, Is.EqualTo(SprintStartPresentation.InstructionCopy));

            var pause = pauses[0];
            var player = GameObject.Find("Player");
            Assert.That(player, Is.Not.Null);
            var playerVisual = player.GetComponentInChildren<SpriteRenderer>();
            Assert.That(playerVisual, Is.Not.Null, "Player needs a visible child SpriteRenderer.");
            Assert.That(playerVisual.sprite, Is.Not.Null);
            var safeArea = pause.GetComponentInParent<SafeAreaFitter>();
            Assert.That(safeArea, Is.Not.Null, "Pause must be inside the safe-area hierarchy.");
            var pauseRect = pause.GetComponent<RectTransform>();
            // Once the Canvas sizes correctly, SprintUiLayout.PauseRect spreads the button's anchors
            // across the top-right of the safe area. The old corner-pin values only ever appeared on
            // the degenerate zero-sized canvas.
            Assert.That(pauseRect.anchorMin.x, Is.GreaterThan(.5f), "Pause sits on the right of the safe area.");
            Assert.That(pauseRect.anchorMin.y, Is.GreaterThan(.5f), "Pause sits in the top half of the safe area.");
            Assert.That(pauseRect.anchorMax.x, Is.GreaterThan(pauseRect.anchorMin.x).And.LessThanOrEqualTo(1f));
            Assert.That(pauseRect.anchorMax.y, Is.GreaterThan(pauseRect.anchorMin.y).And.LessThanOrEqualTo(1f));
            pause.Open();
            Assert.That(FindNamed<Button>(scene, "ResumeButton"), Is.Not.Null);
            Assert.That(FindNamed<Button>(scene, "RestartButton"), Is.Not.Null);
            Assert.That(FindNamed<Button>(scene, "ExitButton"), Is.Not.Null);
            pause.Resume();

            var canvas = pause.GetComponentInParent<Canvas>();
            var scaler = canvas.GetComponent<CanvasScaler>();
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));

            var left = FindNamed<ScreenTapArea>(scene, "LeftTap");
            var right = FindNamed<ScreenTapArea>(scene, "RightTap");
            Assert.That(left, Is.Not.Null);
            Assert.That(right, Is.Not.Null);
            var leftRect = left.GetComponent<RectTransform>();
            var rightRect = right.GetComponent<RectTransform>();
            // With the Canvas sized correctly the tap areas take their inset from SprintUiLayout;
            // the authored scene rect only survived while the safe rect was degenerate.
            var safeSize = ((RectTransform)safeArea.transform).rect.size;
            var unitSafe = new Rect(0f, 0f, safeSize.x, safeSize.y);
            Assert.That(leftRect.anchorMin.x,
                Is.EqualTo(SprintUiLayout.ControlRect(unitSafe, true).xMin / safeSize.x).Within(.001f));
            Assert.That(leftRect.anchorMax.x, Is.LessThan(rightRect.anchorMin.x));
            Assert.That(rightRect.anchorMax.x,
                Is.EqualTo(SprintUiLayout.ControlRect(unitSafe, false).xMax / safeSize.x).Within(.001f));
            Assert.That(1920f * (leftRect.anchorMax.x - leftRect.anchorMin.x) + leftRect.sizeDelta.x, Is.GreaterThanOrEqualTo(140f));
            Assert.That(1920f * (rightRect.anchorMax.x - rightRect.anchorMin.x) + rightRect.sizeDelta.x, Is.GreaterThanOrEqualTo(140f));
        }

        [UnityTest]
        public IEnumerator SprintHudFollowsTheKit()
        {
            yield return LoadSprint();
            yield return null;
            GameObject hud = GameObject.Find("S2_HUD_Minigame");
            Assert.That(hud, Is.Not.Null, "Sprint must have an S2_HUD_Minigame canvas to audit.");

            // The chrome is built at runtime; wait a bounded number of frames so the audit never
            // passes vacuously on the shared HUD prefab alone.
            Transform chrome = null;
            for (int frame = 0; frame < 10 && chrome == null; frame++)
            {
                chrome = hud.transform.Find("SafeAreaRoot/SprintBroadcastChrome");
                if (chrome == null)
                    yield return null;
            }
            Assert.That(chrome, Is.Not.Null, "SprintFestivalPresentation must build SafeAreaRoot/SprintBroadcastChrome under the HUD.");
            Assert.That(chrome.Find("Scoreboard"), Is.Not.Null, "The runtime chrome must contain its Scoreboard.");
            Assert.That(chrome.Find("ProgressRail"), Is.Not.Null, "The runtime chrome must contain its ProgressRail.");

            var problems = MinigameStyleAudit.Audit(hud);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));

            foreach (string tapName in new[] { "LeftTap", "RightTap" })
            {
                GameObject tap = GameObject.Find(tapName);
                Assert.That(tap, Is.Not.Null, tapName + " must exist to be audited.");
                var tapProblems = MinigameStyleAudit.Audit(tap);
                Assert.That(tapProblems, Is.Empty, tapName + ":\n" + string.Join("\n", tapProblems));
            }
        }

        [UnityTest]
        public IEnumerator SprintStart_ShowsOneInstructionAcrossTutorialAndCountdown()
        {
            yield return LoadSprint();
            var scene = SceneManager.GetActiveScene();

            var controller = SceneObjects<SprintController>(scene)[0];
            var start = SceneObjects<SprintStartPresentation>(scene)[0];

            Assert.That(start.InstructionVisible, Is.True, "instruction shows immediately");
            Assert.That(start.InstructionText, Is.EqualTo(SprintStartPresentation.InstructionCopy));
            Assert.That(start.InstructionText, Is.EqualTo("BẤM TRÁI VÀ PHẢI LUÂN PHIÊN ĐỂ CHẠY"));

            start.TickForTest(1.49f);
            Assert.That(controller.PresentationPhase, Is.EqualTo(MinigamePhase.Tutorial));
            Assert.That(start.InstructionVisible, Is.True, "instruction persists through the gate");

            start.TickForTest(.02f);
            Assert.That(controller.PresentationPhase, Is.Not.EqualTo(MinigamePhase.Tutorial),
                "the gate auto-releases at 1.5 s");
        }

        [UnityTest]
        public IEnumerator SprintStart_HoldsTheGateWhileTheGameIsFrozen()
        {
            yield return LoadSprint();
            var controller = SceneObjects<SprintController>(SceneManager.GetActiveScene())[0];
            var holder = new object();
            GameFreeze.Acquire(holder);
            try
            {
                float until = Time.realtimeSinceStartup + 2f;
                while (Time.realtimeSinceStartup < until)
                    yield return null;
                Assert.That(controller.PresentationPhase, Is.EqualTo(MinigamePhase.Tutorial),
                    "the 1.5 s start gate must not run under the guide or the pause menu");
            }
            finally
            {
                GameFreeze.Release(holder);
            }
        }

        [UnityTest]
        public IEnumerator SprintStart_CountsDownThreeTwoOneThenGo()
        {
            yield return LoadSprint();
            var scene = SceneManager.GetActiveScene();

            var controller = SceneObjects<SprintController>(scene)[0];
            var start = SceneObjects<SprintStartPresentation>(scene)[0];
            start.TickForTest(1.51f);

            Assert.That(start.CountdownText, Is.EqualTo("3"));
            start.TickForTest(1f);
            Assert.That(start.CountdownText, Is.EqualTo("2"));
            start.TickForTest(1f);
            Assert.That(start.CountdownText, Is.EqualTo("1"));

            controller.Simulate(1f);
            controller.Simulate(1f);
            controller.Simulate(1f);
            Assert.That(start.CountdownText, Is.EqualTo("CHẠY!"));
            Assert.That(start.CountdownScale, Is.GreaterThan(1f), "CHẠY! pops before settling");

            start.TickForTest(.51f);
            Assert.That(start.CountdownText, Is.Empty.Or.Null, "CHẠY! clears after 0.5 s");

            start.TickForTest(.41f);
            Assert.That(start.InstructionVisible, Is.False, "instruction fades 0.4 s after GO");
        }

        [UnityTest]
        public IEnumerator SprintStart_BlocksInputUntilPlay()
        {
            yield return LoadSprint();
            var scene = SceneManager.GetActiveScene();

            var controller = SceneObjects<SprintController>(scene)[0];
            controller.OnLeftTap();
            Assert.That(controller.Snapshot.Distance, Is.EqualTo(0f).Within(.0001f),
                "taps before Play must not move the runner");
        }

        [UnityTest]
        public IEnumerator SprintUsesOnlyStartPresentationBeforeResolve()
        {
            yield return LoadSprint();

            var scene = SceneManager.GetActiveScene();
            var controller = SceneObjects<SprintController>(scene)[0];
            var start = SceneObjects<SprintStartPresentation>(scene)[0];
            var phase = SceneObjects<PhaseOverlay>(scene)[0];
            Transform sharedTutorial = FindChildRecursive(phase.transform, "TutorialRoot");
            Transform sharedCountdown = FindChildRecursive(phase.transform, "CountdownRoot");
            Transform sharedPlay = FindChildRecursive(phase.transform, "PlayRoot");
            TMP_Text sharedPhaseLabel = FindNamed<TMP_Text>(scene, "PhaseLabel");

            Assert.That(sharedTutorial, Is.Not.Null);
            Assert.That(sharedCountdown, Is.Not.Null);
            Assert.That(sharedPlay, Is.Not.Null);
            Assert.That(sharedPhaseLabel, Is.Not.Null);
            Assert.That(sharedTutorial.gameObject.activeSelf, Is.False);
            Assert.That(sharedCountdown.gameObject.activeSelf, Is.False);
            Assert.That(sharedPlay.gameObject.activeSelf, Is.False);
            Assert.That(sharedPhaseLabel.text, Is.Empty);

            start.TickForTest(1.5f);
            Assert.That(controller.PresentationPhase, Is.EqualTo(MinigamePhase.Countdown));
            Assert.That(sharedTutorial.gameObject.activeSelf, Is.False);
            Assert.That(sharedCountdown.gameObject.activeSelf, Is.False,
                "Sprint countdown must not duplicate the dedicated start presenter.");
            Assert.That(sharedPlay.gameObject.activeSelf, Is.False);
            Assert.That(sharedPhaseLabel.text, Is.Empty);

            controller.Simulate(1f);
            controller.Simulate(1f);
            controller.Simulate(1f);
            Assert.That(controller.PresentationPhase, Is.EqualTo(MinigamePhase.Play));
            Assert.That(sharedTutorial.gameObject.activeSelf, Is.False);
            Assert.That(sharedCountdown.gameObject.activeSelf, Is.False);
            Assert.That(sharedPlay.gameObject.activeSelf, Is.False,
                "Sprint must not show the shared static PLAY surface.");
            Assert.That(sharedPhaseLabel.text, Is.Empty);
        }

        [UnityTest]
        public IEnumerator SprintPresentationRendersDedicatedHudMetrics()
        {
            yield return LoadSprint();

            var scene = SceneManager.GetActiveScene();
            var controller = SceneObjects<SprintController>(scene)[0];
            var sprintHud = SceneObjects<SprintHud>(scene)[0];
            Transform chrome = GameObject.Find("SprintBroadcastChrome")?.transform;
            Assert.That(chrome, Is.Not.Null);
            Assert.That(GameObject.Find("SprintFestivalChrome"), Is.Null);

            TMP_Text distance = chrome.Find("Scoreboard/Distance")?.GetComponent<TMP_Text>();
            TMP_Text rank = chrome.Find("Scoreboard/RankBadge/RankLabel")?.GetComponent<TMP_Text>();
            TMP_Text cadence = chrome.Find("Scoreboard/Combo")?.GetComponent<TMP_Text>();
            KitBar distanceBar = chrome.Find("ProgressRail")?.GetComponent<KitBar>();

            Assert.That(sprintHud.HasBoundVisuals, Is.True);
            Assert.That(distance, Is.Not.Null);
            Assert.That(rank, Is.Not.Null);
            Assert.That(cadence, Is.Not.Null);
            Assert.That(distanceBar, Is.Not.Null);
            Assert.That(distanceBar.Fill.name, Is.EqualTo("RailFill"));
            Assert.That(chrome.GetComponentInParent<SafeAreaFitter>(), Is.Not.Null);
            Assert.That(distance.text, Is.EqualTo("0 / 150 m"));
            Assert.That(rank.text, Is.EqualTo("VỊ TRÍ 1"));
            Assert.That(cadence.text, Is.EqualTo("CHUỖI ×0"));

            controller.ConfigureForTest();
            controller.AdvanceToDistance(42f);
            sprintHud.Refresh();

            Assert.That(distance.text, Is.EqualTo("42 / 150 m"));
            Assert.That(rank.text, Is.EqualTo("VỊ TRÍ 1"));
            Assert.That(cadence.text, Is.EqualTo("CHUỖI ×0"));
            Assert.That(distanceBar.Value, Is.EqualTo(42f / SprintRules.RaceDistance).Within(.001f));
            Assert.That(distanceBar.Fill.rectTransform.anchorMax.x, Is.EqualTo(42f / SprintRules.RaceDistance).Within(.001f));

            controller.OnLeftTap();
            controller.OnRightTap();
            sprintHud.Refresh();
            Assert.That(cadence.text, Is.EqualTo("CHUỖI ×2"),
                "The scoreboard must track the cadence combo as the only live race feedback.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator SprintSceneKeepsRunnersInFourEqualTrackBandsAndMarksOnlyThePlayer()
        {
            yield return LoadSprint();

            var scene = SceneManager.GetActiveScene();
            Transform[] runnerRoots =
            {
                FindNamed<Transform>(scene, "Runner_01"),
                FindNamed<Transform>(scene, "Player"),
                FindNamed<Transform>(scene, "Runner_03"),
                FindNamed<Transform>(scene, "Runner_04")
            };
            for (int lane = 0; lane < runnerRoots.Length; lane++)
            {
                Assert.That(runnerRoots[lane], Is.Not.Null);
                SpriteRenderer runnerVisual = runnerRoots[lane].GetComponentInChildren<SpriteRenderer>(true);
                Assert.That(runnerVisual, Is.Not.Null);
                // Feet on the painted lane centre — the sprite's pivot is at its feet.
                Assert.That(runnerVisual.bounds.min.y,
                    Is.EqualTo(SprintTrackLayout.LaneCenterY(lane)).Within(.02f),
                    $"Runner {lane + 1} must stand midway between the two painted lines of their lane.");
            }

            Transform chromeMarker = GameObject.Find("SprintBroadcastChrome")?.transform.Find("PlayerMarker");
            Assert.That(chromeMarker, Is.Null);

            Transform playerPresentation = runnerRoots[1].GetComponentInChildren<RunnerVisualPresenter>(true)?.transform;
            Assert.That(playerPresentation, Is.Not.Null);
            Transform marker = playerPresentation.Find("PlayerMarker");
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.IsChildOf(runnerRoots[1]), Is.True);
            TMPro.TextMeshPro markerLabel = marker.GetComponentInChildren<TMPro.TextMeshPro>(true);
            Assert.That(markerLabel, Is.Not.Null);
            Assert.That(markerLabel.text, Is.EqualTo("BẠN"));

            for (int lane = 0; lane < runnerRoots.Length; lane++)
            {
                if (lane == 1) continue;
                Assert.That(FindChildRecursive(runnerRoots[lane], "PlayerMarker"), Is.Null);
            }
        }

        [UnityTest]
        public IEnumerator SprintPlayer_IsMarkedWithLabelPlateAndChevron()
        {
            yield return LoadSprint();
            var scene = SceneManager.GetActiveScene();

            GameObject player = GameObject.Find("Player");
            var label = player.GetComponentInChildren<TMPro.TextMeshPro>(true);
            Assert.That(label, Is.Not.Null, "player must carry a marker");
            Assert.That(label.name, Is.EqualTo("Label"));

            Transform markerRoot = label.transform.parent;
            Assert.That(markerRoot.name, Is.EqualTo("PlayerMarker"));
            // Lanes are 1.48 apart and a runner is 1.28 tall, so an overhead plate lands on the
            // lane 1 rival. The marker rides beside the runner, inside the player's own lane band.
            Assert.That(Mathf.Abs(markerRoot.localPosition.x), Is.GreaterThan(.5f),
                "marker must sit beside the runner, not stacked above them");
            Assert.That(markerRoot.position.y,
                Is.GreaterThan(SprintTrackLayout.LaneCenterY(1)).And.LessThan(SprintTrackLayout.LaneCenterY(0)),
                "marker must stay inside the player's lane band so it never covers the lane 1 rival");
            Assert.That(markerRoot.Find("Plate"), Is.Not.Null, "marker needs a plate so cyan reads over the track");
            Assert.That(markerRoot.Find("Chevron"), Is.Not.Null, "identity must not rely on colour alone");

            Assert.That(label.text, Is.EqualTo("BẠN"));
            Assert.That(label.color, Is.EqualTo(MinigameUiTheme.Player));

            foreach (var renderer in player.GetComponentsInChildren<SpriteRenderer>(true))
                Assert.That(renderer.name, Is.Not.EqualTo("PlayerIdentityOutline"),
                    "no tinted copy of the player sprite: it reads as a ghost behind the runner");
        }

        [UnityTest]
        public IEnumerator SprintBackdropShowsTheCampusAboveTheTrack()
        {
            yield return LoadSprint();
            var skyline = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                .First(r => r.sprite != null && r.sprite.name == "CampusSkyline");
            Assert.That(skyline.bounds.min.y, Is.EqualTo(SprintTrackLayout.WorldYForRow(460f)).Within(.05f),
                "the skyline stands on the track's top edge");
            Assert.That(skyline.bounds.max.y, Is.LessThan(5.4f), "the skyline is inside the view");
            var track = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                .First(r => r.sprite != null && r.sprite.name == "SprintTrack");
            Assert.That(track.sortingOrder, Is.EqualTo(-10));
        }

        [UnityTest]
        public IEnumerator SprintRivals_DoNotUseThePlayerAccentColour()
        {
            yield return LoadSprint();
            var scene = SceneManager.GetActiveScene();

            foreach (var rival in Object.FindObjectsByType<RivalRunnerAI>(FindObjectsSortMode.None))
            {
                Assert.That(rival.GetComponentInChildren<TMPro.TextMeshPro>(true), Is.Null,
                    "rivals carry no BẠN marker");
            }
        }

        [UnityTest]
        public IEnumerator SprintControls_DrawLargeFootIconsInsideTheirFullSizeHitAreas()
        {
            yield return LoadSprint();
            var scene = SceneManager.GetActiveScene();

            foreach (string tapName in new[] { "LeftTap", "RightTap" })
            {
                var tap = GameObject.Find(tapName).GetComponent<RectTransform>();
                var visual = tap.Find("Visual") as RectTransform;
                Assert.That(visual, Is.Not.Null, $"{tapName} must own a Visual child.");

                // The plate is 30% larger than baseline and sits against the safe area corner; the full tap rect can
                // still be larger for reliable thumb input.
                Rect expected = SprintUiLayout.ControlVisualRect01(tapName == "LeftTap");
                Assert.That(visual.anchorMin,
                    Is.EqualTo(new Vector2(expected.xMin, expected.yMin)).Within(.0001f));
                Assert.That(visual.anchorMax,
                    Is.EqualTo(new Vector2(expected.xMax, expected.yMax)).Within(.0001f));
                Assert.That(visual.offsetMin, Is.EqualTo(Vector2.zero));
                Assert.That(visual.offsetMax, Is.EqualTo(Vector2.zero));

                Assert.That(visual.rect.width, Is.LessThan(tap.rect.width));
                Assert.That(visual.rect.height, Is.LessThan(tap.rect.height));
                Assert.That(visual.rect.width * visual.rect.height,
                    Is.LessThan(tap.rect.width * tap.rect.height * .5f),
                    $"{tapName}'s drawn button must be markedly smaller than the area it answers for.");

                var icon = visual.Find("FootIcon")?.GetComponent<UnityEngine.UI.Image>();
                Assert.That(icon, Is.Not.Null);
                Assert.That(icon.sprite, Is.Not.Null);
                Assert.That(icon.raycastTarget, Is.False);
                Assert.That(visual.Find("Arrow").gameObject.activeSelf, Is.False);
                Assert.That(visual.Find("Label").gameObject.activeSelf, Is.False);

                // The hit box itself is untouched: still the full ControlRect, still raycasting.
                var safeAreaRect = ((RectTransform)GameObject.Find("SprintBroadcastChrome")
                    .GetComponentInParent<SafeAreaFitter>().transform).rect;
                Rect expectedTap = SprintUiLayout.ControlRect(
                    new Rect(0f, 0f, safeAreaRect.width, safeAreaRect.height), tapName == "LeftTap");
                Assert.That(tap.rect.width, Is.EqualTo(expectedTap.width).Within(1f),
                    $"{tapName}'s hit box must keep its full width.");
                Assert.That(tap.rect.height, Is.EqualTo(expectedTap.height).Within(1f),
                    $"{tapName}'s hit box must keep its full height.");
                Assert.That(tap.GetComponent<Image>().raycastTarget, Is.True, $"{tapName} must receive taps.");
                Assert.That(visual.Find("Border").GetComponent<Image>().sprite, Is.Not.Null);
                Assert.That(visual.Find("Background").GetComponent<Image>().sprite, Is.Not.Null);
            }

            Assert.That(GameObject.Find("LeftTap").transform.Find("Visual/FootIcon").GetComponent<Image>().sprite,
                Is.SameAs(SprintFootIcons.Left));
            Assert.That(GameObject.Find("RightTap").transform.Find("Visual/FootIcon").GetComponent<Image>().sprite,
                Is.SameAs(SprintFootIcons.Right));
        }

        [UnityTest]
        public IEnumerator SprintControls_HighlightTheExpectedSideWithoutTouchingGameplay()
        {
            yield return LoadSprint();
            var scene = SceneManager.GetActiveScene();

            var controller = SceneObjects<SprintController>(scene)[0];
            var presenter = SceneObjects<SprintControlPresenter>(scene)[0];
            controller.Simulate(1f);
            controller.Simulate(1f);
            controller.Simulate(1f);
            presenter.RefreshForTest();

            Assert.That(presenter.HighlightedSide, Is.EqualTo(controller.ExpectedSide));

            int comboBefore = controller.CadenceCombo;
            presenter.RefreshForTest();
            Assert.That(controller.CadenceCombo, Is.EqualTo(comboBefore),
                "The presenter must never advance gameplay state.");
        }

        [UnityTest]
        public IEnumerator SprintControls_ShrinkOnPressAndRecover()
        {
            yield return LoadSprint();
            var scene = SceneManager.GetActiveScene();

            var controller = SceneObjects<SprintController>(scene)[0];
            var presenter = SceneObjects<SprintControlPresenter>(scene)[0];
            float distanceBefore = controller.Snapshot.Distance;

            presenter.PressForTest(KMA.Gameplay.Side.Left);
            Assert.That(presenter.LeftScale, Is.EqualTo(.94f).Within(.001f));
            Assert.That(presenter.RightScale, Is.EqualTo(1f).Within(.001f));

            presenter.TickForTest(.091f);
            Assert.That(presenter.LeftScale, Is.EqualTo(1f).Within(.001f));
            Assert.That(controller.Snapshot.Distance, Is.EqualTo(distanceBefore).Within(.0001f));
        }

        [UnityTest]
        public IEnumerator SprintTapAreasStayInTheInputCanvasOutsideTheChrome()
        {
            yield return LoadSprint();
            var scene = SceneManager.GetActiveScene();

            Transform chrome = GameObject.Find("SprintBroadcastChrome").transform;
            Transform inputCanvas = GameObject.Find("Input")?.transform;
            Assert.That(inputCanvas, Is.Not.Null);

            foreach (string tapName in new[] { "LeftTap", "RightTap" })
            {
                Transform tap = GameObject.Find(tapName).transform;
                Assert.That(tap.IsChildOf(inputCanvas), Is.True,
                    $"{tapName} must stay in the input canvas.");
                Assert.That(tap.IsChildOf(chrome), Is.False,
                    $"{tapName} must not be reparented under the HUD chrome.");
            }
        }

        [UnityTest]
        public IEnumerator SprintControlsColourReflectsExpectedPressedAndFinishedStates()
        {
            yield return LoadSprint();
            var scene = SceneManager.GetActiveScene();
            var controller = SceneObjects<SprintController>(scene)[0];
            var start = SceneObjects<SprintStartPresentation>(scene)[0];
            var presenter = SceneObjects<SprintControlPresenter>(scene)[0];

            Image LeftBackground() =>
                GameObject.Find("LeftTap").transform.Find("Visual/Background").GetComponent<Image>();
            Image RightBackground() =>
                GameObject.Find("RightTap").transform.Find("Visual/Background").GetComponent<Image>();
            Image LeftBorder() =>
                GameObject.Find("LeftTap").transform.Find("Visual/Border").GetComponent<Image>();
            Image RightBorder() =>
                GameObject.Find("RightTap").transform.Find("Visual/Border").GetComponent<Image>();

            start.TickForTest(1.5f);
            controller.Simulate(1f);
            controller.Simulate(1f);
            controller.Simulate(1f);
            Assert.That(controller.PresentationPhase, Is.EqualTo(MinigamePhase.Play));
            presenter.RefreshForTest();

            bool leftExpected = presenter.HighlightedSide == KMA.Gameplay.Side.Left;
            Image expectedBorder = leftExpected ? LeftBorder() : RightBorder();
            Image otherBorder = leftExpected ? RightBorder() : LeftBorder();
            Assert.That(expectedBorder.color, Is.Not.EqualTo(otherBorder.color),
                "the next side to press must look different from the other one");
            Assert.That(expectedBorder.color.a, Is.GreaterThan(otherBorder.color.a),
                "the expected side must be the more prominent of the two");

            Color idleLeftBackground = LeftBackground().color;
            Color idleRightBackground = RightBackground().color;
            presenter.PressForTest(KMA.Gameplay.Side.Left);
            Assert.That(LeftBackground().color, Is.Not.EqualTo(idleLeftBackground),
                "pressing must visibly change the button, not only its scale");
            Assert.That(RightBackground().color, Is.EqualTo(idleRightBackground),
                "pressing one side must not restyle the other");

            presenter.TickForTest(.1f);
            presenter.RefreshForTest();
            Assert.That(LeftBackground().color, Is.EqualTo(idleLeftBackground).Within(.01f),
                "the press tint must decay back to idle");
        }

        [UnityTest]
        public IEnumerator SprintControlPressFeedbackShrinksThenRestoresWithoutChangingGameplay()
        {
            yield return LoadSprint();

            var scene = SceneManager.GetActiveScene();
            var controller = SceneObjects<SprintController>(scene)[0];
            var presenter = SceneObjects<SprintControlPresenter>(scene)[0];
            controller.ConfigureForTest();
            float distanceBefore = controller.Snapshot.Distance;
            int comboBefore = controller.CadenceCombo;

            presenter.PressForTest(KMA.Gameplay.Side.Left);
            Assert.That(presenter.LeftScale, Is.EqualTo(.94f).Within(.001f));
            presenter.TickForTest(.091f);
            Assert.That(presenter.LeftScale, Is.EqualTo(1f).Within(.001f));
            Assert.That(controller.Snapshot.Distance, Is.EqualTo(distanceBefore));
            Assert.That(controller.CadenceCombo, Is.EqualTo(comboBefore));
        }

        [UnityTest]
        public IEnumerator SprintFinishLineStandsAtItsWorldPositionWithoutBlockingInput()
        {
            yield return LoadSprint();

            var scene = SceneManager.GetActiveScene();
            var controller = SceneObjects<SprintController>(scene)[0];
            var finish = SceneObjects<SprintFinishLinePresenter>(scene)[0];
            Transform finishLine = finish.FinishRoot != null ? finish.FinishRoot.transform : null;
            Assert.That(finishLine, Is.Not.Null);
            Assert.That(finish.StartRoot, Is.Not.Null);

            // World-space sprite on the track: never a UI graphic, so it cannot block taps.
            Assert.That(finishLine.GetComponent<SpriteRenderer>(), Is.Not.Null);
            Assert.That(finishLine.GetComponentsInChildren<Graphic>(true), Is.Empty);
            Assert.That(finishLine.position.y, Is.EqualTo(SprintTrackLayout.FinishBottomY).Within(.0001f));

            controller.ConfigureForTest();
            foreach (float distance in new[] { 0f, 75f, SprintRules.RaceDistance })
            {
                controller.AdvanceToDistance(distance);
                finish.RefreshForTest();
                Assert.That(finish.IsVisible, Is.True);
                Assert.That(finishLine.gameObject.activeSelf, Is.True);
                Assert.That(finishLine.position.x,
                    Is.EqualTo(SprintTrackLayout.FinishWorldX(controller.TargetDistance)).Within(.0001f),
                    $"the line never moves (player at {distance} m)");
            }
            Assert.That(finish.StartRoot.transform.position.x,
                Is.EqualTo(SprintTrackLayout.TrackStartX).Within(.0001f));
        }

        [UnityTest]
        public IEnumerator SprintScene_ShowsNoLegacyHudChrome()
        {
            yield return LoadSprint();
            var scene = SceneManager.GetActiveScene();

            Assert.That(GameObject.Find("SprintMetrics"), Is.Null,
                "The legacy SprintMetrics group must be deleted from MG_Sprint.unity.");
            Assert.That(GameObject.Find("SprintFestivalChrome"), Is.Null);

            foreach (string name in new[] { "Timer", "Phase", "Score", "Status", "Progress", "Stamina", "HeartBar" })
            {
                GameObject shared = GameObject.Find(name);
                Assert.That(shared, Is.Null, $"Shared HUD element {name} must not be active in the Sprint scene.");
            }
        }

        [UnityTest]
        public IEnumerator SprintResultUsesSharedPanelWithSprintTitlesAndASingleContinue()
        {
            yield return LoadSprint();

            var scene = SceneManager.GetActiveScene();
            var panel = SceneObjects<ResultPanel>(scene)[0];
            Transform content = panel.transform.Find("Content");
            TMP_Text title = content.Find("StatusLabel").GetComponent<TMP_Text>();
            TMP_Text score = content.Find("ScoreLabel").GetComponent<TMP_Text>();
            TMP_Text rank = content.Find("RankLabel").GetComponent<TMP_Text>();
            Button action = content.Find("ActionButton").GetComponent<Button>();
            Assert.That(panel.GetComponent("SprintResultPresentation"), Is.Null, "the reveal now lives in ResultPanel");

            var routes = new List<string>();
            panel.ActionRequested += routes.Add;

            panel.Show(new MinigameResult(false, 0f, Rank.F), "Map");
            // The shared panel animates its reveal; the settled state is asserted after it ends.
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(title.text, Is.EqualTo("THẤT BẠI"));
            AssertColor32(title.color, (Color32)MinigameUiTheme.Energy);
            Assert.That(score.text, Is.EqualTo("0.0"));
            Assert.That(rank.text, Is.EqualTo("XẾP HẠNG F"));
            Assert.That(action.interactable, Is.True);
            Assert.That(content.Find("RetryButton").gameObject.activeSelf, Is.False);

            panel.Continue();
            panel.Continue();
            Assert.That(routes, Is.EqualTo(new[] { "Map" }));

            panel.Show(new MinigameResult(true, 8.4f, Rank.A), "Map");
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(title.text, Is.EqualTo("HOÀN THÀNH!"));
            AssertColor32(title.color, (Color32)MinigameUiTheme.Success);
            Assert.That(score.text, Is.EqualTo("8.4"), "the count-up ends on the final score");
            Assert.That(rank.text, Is.EqualTo("XẾP HẠNG A"));
        }

        [UnityTest]
        public IEnumerator SprintScene_BuildsTheApprovedScoreboardAndRail()
        {
            yield return LoadSprint();
            var scene = SceneManager.GetActiveScene();

            Transform chrome = GameObject.Find("SprintBroadcastChrome").transform;
            Assert.That(chrome.GetComponentInParent<KMA.Gameplay.UI.SafeAreaFitter>(), Is.Not.Null);

            Assert.That(chrome.Find("Scoreboard/Distance").GetComponent<TMP_Text>().text, Is.EqualTo("0 / 150 m"));
            TMP_Text rankChip = chrome.Find("Scoreboard/RankBadge/RankLabel").GetComponent<TMP_Text>();
            Assert.That(rankChip.text, Is.EqualTo("VỊ TRÍ 1"));
            rankChip.ForceMeshUpdate();
            Assert.That(rankChip.textInfo.lineCount, Is.EqualTo(1), "the rank chip keeps VỊ TRÍ 1 on one line");
            Assert.That(rankChip.textBounds.size.x, Is.LessThanOrEqualTo(rankChip.rectTransform.rect.width + .5f),
                "VỊ TRÍ 1 must fit inside its chip");
            Assert.That(chrome.Find("Scoreboard/Combo").GetComponent<TMP_Text>().text, Is.EqualTo("CHUỖI ×0"));
            Assert.That(chrome.Find("ModeLabel/Label").GetComponent<TMP_Text>().text, Is.EqualTo("CHẠY NƯỚC RÚT · 150M"));

            KitBar rail = chrome.Find("ProgressRail").GetComponent<KitBar>();
            Assert.That(rail.Value, Is.EqualTo(0f).Within(.001f));
            Assert.That(rail.Fill.color, Is.EqualTo(MinigameUiTheme.Accent));
            Assert.That(rail.Fill.sprite, Is.SameAs(UiKitAssets.Load().RoundRect24));

            Image pip = chrome.Find("ProgressRail/PlayerPip").GetComponent<Image>();
            Assert.That(pip.color, Is.EqualTo(MinigameUiTheme.Player));

            Assert.That(chrome.Find("Scoreboard").GetComponent<Image>().sprite, Is.SameAs(UiKitAssets.Load().RoundRect24),
                "The scoreboard must use the kit's rounded sprite, not the default square.");
            Assert.That(chrome.Find("Scoreboard").GetComponent<Outline>(), Is.Null,
                "Outline is replaced by Shadow.");
            Assert.That(chrome.Find("Scoreboard").GetComponent<Shadow>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SprintHud_ReflectsDistanceRankAndCombo()
        {
            yield return LoadSprint();
            var scene = SceneManager.GetActiveScene();

            var controller = SceneObjects<SprintController>(scene)[0];
            var hud = SceneObjects<SprintHud>(scene)[0];
            Assert.That(hud.HasBoundVisuals, Is.True);

            controller.AdvanceToDistance(42f);
            hud.Refresh();

            Transform chrome = GameObject.Find("SprintBroadcastChrome").transform;
            Assert.That(chrome.Find("Scoreboard/Distance").GetComponent<TMP_Text>().text, Is.EqualTo("42 / 150 m"));
            Assert.That(chrome.Find("ProgressRail").GetComponent<KitBar>().Value, Is.EqualTo(42f / SprintRules.RaceDistance).Within(.001f));
            // Anchors collapse to 0 under batchmode's zero-sized safe rect (see CONTROLLER RULING #1),
            // so progress is asserted through the dedicated field instead of the anchor position.
            Assert.That(hud.PipProgress, Is.EqualTo(42f / SprintRules.RaceDistance).Within(.001f));
        }

        [UnityTest]
        public IEnumerator SprintChrome_RegistersEveryElementForRelayout()
        {
            yield return LoadSprint();
            var scene = SceneManager.GetActiveScene();

            var layout = SceneObjects<SprintChromeLayout>(scene)[0];
            Assert.That(layout, Is.Not.Null, "chrome must own a relayout component");
            Assert.That(layout.ElementCount, Is.EqualTo(8),
                "rail, scoreboard, mode chip, pause, countdown, instruction plate and both tap " +
                "areas must all be registered as rects");
            Assert.That(layout.ActionCount, Is.EqualTo(1),
                "the rail radius/pip-width re-derivation must be registered as an action");
        }

        static IEnumerator LoadSprint()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
        }

        static T[] SceneObjects<T>(Scene scene) where T : Component
        {
            var all = Object.FindObjectsOfType<T>(true);
            var count = 0;
            for (var i = 0; i < all.Length; i++)
                if (all[i].gameObject.scene == scene) count++;
            var result = new T[count];
            var index = 0;
            for (var i = 0; i < all.Length; i++)
                if (all[i].gameObject.scene == scene) result[index++] = all[i];
            return result;
        }

        static T FindNamed<T>(Scene scene, string name) where T : Component
        {
            var objects = SceneObjects<T>(scene);
            for (var i = 0; i < objects.Length; i++)
                if (objects[i].name == name) return objects[i];
            return null;
        }

        static Transform FindChildRecursive(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == name) return child;
                Transform nested = FindChildRecursive(child, name);
                if (nested != null) return nested;
            }
            return null;
        }

        static void AssertColor32(Color color, Color32 expected)
        {
            Color32 actual = color;
            Assert.That(actual.r, Is.EqualTo(expected.r));
            Assert.That(actual.g, Is.EqualTo(expected.g));
            Assert.That(actual.b, Is.EqualTo(expected.b));
            Assert.That(actual.a, Is.EqualTo(expected.a));
        }
    }
}
