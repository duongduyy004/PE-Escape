using System.Collections;
using System.Linq;
using KMA.Gameplay;
using UnityEngine.InputSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KMA.Tests.Gameplay.Running
{
    public sealed class SprintControllerTests
    {
        [UnityTest]
        public IEnumerator InputSystemAsset_BindsSprintActionsAndControllerResolvesThem()
        {
            var controller = CreateSprintController();
            var asset = new InputActionAsset();
            var map = asset.AddActionMap("Sprint");
            var left = map.AddAction("SprintLeft", InputActionType.Button);
            var right = map.AddAction("SprintRight", InputActionType.Button);
            left.AddBinding("<Keyboard>/leftArrow");
            right.AddBinding("<Keyboard>/rightArrow");

            controller.ConfigureInputForTest(asset);

            Assert.That(controller.InputActionsReady, Is.True);
            Assert.That(controller.LeftInputAction, Is.EqualTo("SprintLeft"));
            Assert.That(controller.RightInputAction, Is.EqualTo("SprintRight"));
            Assert.That(left.bindings[0].path, Is.EqualTo("<Keyboard>/leftArrow"));
            Assert.That(right.bindings[0].path, Is.EqualTo("<Keyboard>/rightArrow"));
            DestroyController(controller);
            yield return null;
        }

        [Test]
        public void RunnersKeepPullingUpAfterTheResultOpens()
        {
            var controller = CreateSprintController();
            try
            {
                controller.AdvanceToDistance(SprintRules.RaceDistance - 1f, new[] { new RivalPaceProfile("Fast", 500f, 500f) });
                for (int i = 0; i < 4; i++) { controller.OnLeftTap(); controller.OnRightTap(); }
                for (int i = 0; i < 300 && controller.Phase != MinigamePhase.Resolve; i++) controller.Simulate(.01f);
                Assert.That(controller.Phase, Is.EqualTo(MinigamePhase.Resolve));

                float player = controller.PlayerPosition;
                float rival = controller.GetRivalPosition(0);
                Assert.That(player, Is.LessThan(SprintRules.RaceDistance + 4.05f));
                for (int i = 0; i < 300; i++) controller.Simulate(.01f);

                Assert.That(controller.PlayerPosition, Is.GreaterThan(player), "the player pulls up, not freezes");
                Assert.That(controller.PlayerPosition - SprintRules.RaceDistance, Is.InRange(3.8f, 4.05f));
                Assert.That(controller.GetRivalPosition(0), Is.GreaterThanOrEqualTo(rival));
                Assert.That(controller.PlayerMetersPerSecond, Is.Zero);
                Assert.That(controller.IsPlayerFinished, Is.True);
            }
            finally
            {
                DestroyController(controller);
            }
        }

        [UnityTest]
        public IEnumerator ReachingTheFinish_EmitsOnePassResultAndIgnoresPostResolveTaps()
        {
            var controller = CreateSprintController();
            MinigameResult result = null;
            int completions = 0;
            controller.Completed += value => { result = value; completions++; };
            controller.AdvanceToDistance(SprintRules.RaceDistance);
            controller.Simulate(0f);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Pass, Is.True);
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(controller.Phase, Is.EqualTo(MinigamePhase.Resolve));
            controller.OnLeftTap();
            controller.OnRightTap();
            controller.Simulate(1f);
            Assert.That(completions, Is.EqualTo(1));
            DestroyController(controller);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TimeLimit_EmitsOneFailureResult()
        {
            var controller = CreateSprintController();
            MinigameResult result = null;
            int completions = 0;
            controller.Completed += value => { result = value; completions++; };
            controller.Simulate(SprintRules.DefaultTimeLimit);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Pass, Is.False);
            Assert.That(completions, Is.EqualTo(1));
            DestroyController(controller);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SprintScene_AuthorsThreeCosmeticRivalsAndThreeLayerParallax()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint", LoadSceneMode.Single);
            yield return null;

            var scene = SceneManager.GetActiveScene();
            var rivals = SceneObjects<RivalRunnerAI>(scene)
                .OrderBy(rival => rival.Lane)
                .ThenBy(rival => rival.name)
                .ToArray();
            Assert.That(rivals.Length, Is.EqualTo(3));
            AssertAuthoredRivalMappings();

            for (var i = 0; i < rivals.Length; i++)
            {
                Assert.That(rivals[i].Lane, Is.Not.EqualTo(2));
                Assert.That(rivals[i].ProfileAsset, Is.Not.Null);
                var profile = rivals[i].ProfileAsset.ToRuntime();
                Assert.That(profile, Is.Not.Null);
                Assert.That(profile.OpeningSpeed, Is.GreaterThan(0f));
                Assert.That(profile.SustainedSpeed, Is.GreaterThan(0f));
                var renderer = rivals[i].GetComponentInChildren<SpriteRenderer>();
                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.sprite, Is.Not.Null);
                var animator = rivals[i].GetComponentInChildren<Animator>();
                Assert.That(animator, Is.Not.Null);
                Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
                AssertAnimatorStatesHaveAuthoredVisualMotions(animator);
            }

            var player = GameObject.Find("Player");
            Assert.That(player, Is.Not.Null);
            Assert.That(player.transform.position.x, Is.EqualTo(-9.6f).Within(.001f));
            Assert.That(player.transform.position.y,
                Is.EqualTo(SprintTrackLayout.LaneCenterYForAuthoredLane(2)).Within(.001f));

            var parallax = SceneObjects<SprintParallax>(scene);
            Assert.That(parallax.Length, Is.EqualTo(1));
            Assert.That(parallax[0].LayerCount, Is.EqualTo(3));
            Assert.That(parallax[0].BoundLayerCount, Is.EqualTo(3));
            Assert.That(parallax[0].CoveragePixels, Is.EqualTo(new Vector2(2560f, 1080f)));
            for (var i = 0; i < parallax[0].LayerCount; i++)
            {
                Assert.That(parallax[0].GetLayerTileRendererCount(i), Is.EqualTo(2));
                Assert.That(parallax[0].TryGetLayerTilePositions(i, out var first, out var second), Is.True);
                Assert.That(Mathf.Abs(first.x - second.x), Is.EqualTo(25.6f).Within(.001f));
            }

        }

        [UnityTest]
        public IEnumerator SprintParallax_SmallScrollPreservesTilesAndLargeScrollRecyclesOnlyOffscreenTile()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint", LoadSceneMode.Single);
            yield return null;

            var parallax = SceneObjects<SprintParallax>(SceneManager.GetActiveScene()).Single();
            // The follow camera has already framed the start for this screen's aspect; measure from
            // the camera's authored position instead.
            parallax.RefreshForTest(0f);
            Assert.That(parallax.TryGetLayerTilePositions(0, out var initialFirst, out var initialSecond), Is.True);
            Assert.That(initialFirst.x, Is.EqualTo(0f).Within(.001f));
            Assert.That(initialSecond.x, Is.EqualTo(25.6f).Within(.001f));

            parallax.RefreshForTest(10f);
            Assert.That(parallax.TryGetLayerTilePositions(0, out var smallFirst, out var smallSecond), Is.True);
            Assert.That(smallFirst.x, Is.EqualTo(-1.5f).Within(.001f), "small scroll must not recycle the left tile");
            Assert.That(smallSecond.x, Is.EqualTo(24.1f).Within(.001f), "small scroll must move the right tile by the same delta");

            parallax.RefreshForTest(200f);
            Assert.That(parallax.TryGetLayerTilePositions(0, out var largeFirst, out var largeSecond), Is.True);
            Assert.That(largeFirst.x, Is.EqualTo(21.2f).Within(.001f), "only the offscreen first tile may recycle after the threshold");
            Assert.That(largeSecond.x, Is.EqualTo(-4.4f).Within(.001f), "the still-visible second tile must not recycle");
            Assert.That(largeFirst.x - largeSecond.x, Is.EqualTo(25.6f).Within(.001f));
        }

        [UnityTest]
        public IEnumerator SprintParallax_ArbitrarilyLargeScrollNormalizesAllOffscreenTilesAndKeepsCoverage()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint", LoadSceneMode.Single);
            yield return null;

            var parallax = SceneObjects<SprintParallax>(SceneManager.GetActiveScene()).Single();
            parallax.RefreshForTest(2000f);

            for (var layer = 0; layer < parallax.LayerCount; layer++)
            {
                Assert.That(parallax.TryGetLayerTilePositions(layer, out var first, out var second), Is.True);
                Assert.That(Mathf.Abs(first.x - second.x), Is.EqualTo(25.6f).Within(.001f));
                Assert.That(Mathf.Min(first.x, second.x), Is.GreaterThan(-25.6f),
                    $"layer {layer} must repeatedly recycle tiles past the offscreen boundary");
                Assert.That(Mathf.Min(first.x, second.x), Is.LessThanOrEqualTo(0f),
                    $"layer {layer} must cover the viewport origin from the left");
                Assert.That(Mathf.Max(first.x, second.x), Is.GreaterThanOrEqualTo(0f),
                    $"layer {layer} must cover the viewport origin from the right");
            }
        }

        [UnityTest]
        public IEnumerator SprintRivalAnimator_EvaluationPreservesEverySceneAssignedLaneRoot()
        {
            yield return SceneManager.LoadSceneAsync("MG_Sprint", LoadSceneMode.Single);
            yield return null;

            var expectedLaneY = new System.Collections.Generic.Dictionary<int, float>
            {
                { 1, SprintTrackLayout.LaneCenterYForAuthoredLane(1) },
                { 3, SprintTrackLayout.LaneCenterYForAuthoredLane(3) },
                { 4, SprintTrackLayout.LaneCenterYForAuthoredLane(4) }
            };

            var rivals = SceneObjects<RivalRunnerAI>(SceneManager.GetActiveScene());
            foreach (var rival in rivals)
            {
                var expectedRootY = expectedLaneY[rival.Lane];
                var expectedRootPosition = new Vector3(-9.6f, expectedRootY, 0f);
                Assert.That(rival.transform.localPosition, Is.EqualTo(expectedRootPosition).Within(.001f));

                var visual = rival.Sprite.transform;
                Assert.That(visual, Is.Not.SameAs(rival.transform),
                    "animation must target a child visual transform rather than the lane root");
                Assert.That(visual.parent, Is.SameAs(rival.transform));

                var animator = rival.Animator;
                var visualPosition = visual.localPosition;
                animator.Play("Burst", 0, 0f);
                animator.Update(0f);
                var initialSprite = rival.Sprite.sprite;
                var initialScale = visual.localScale;
                animator.Play("Burst", 0, .5f);
                animator.Update(0f);

                Assert.That(rival.transform.localPosition, Is.EqualTo(expectedRootPosition).Within(.001f),
                    $"Animator evaluation must not overwrite lane {rival.Lane} placement");
                Assert.That(visual.localPosition, Is.EqualTo(visualPosition).Within(.001f),
                    "sprite animation must preserve lane foot placement and race-owned progress");
                Assert.That(rival.Sprite.sprite, Is.Not.EqualTo(initialSprite),
                    "the burst clip must change the child visual pose");
                Assert.That(visual.localScale.y, Is.GreaterThan(initialScale.y),
                    "the burst clip retains subtle visual motion without moving the root");
            }
        }

        [Test]
        public void RivalVisualBurstNearTheFinish_IsCosmeticAndDoesNotChangeRulesDistances()
        {
            var profile = ScriptableObject.CreateInstance<RivalPaceProfileAsset>();
            profile.profileName = "Test";
            profile.openingSpeed = 8f;
            profile.sustainedSpeed = 7f;
            var runner = new GameObject("Rival").AddComponent<RivalRunnerAI>();
            runner.Configure(profile, 1, 0, null);

            var rules = new SprintRules(rivalProfiles: new[] { profile.ToRuntime() });
            rules.Tick(1f);
            var before = rules.RivalDistances;
            var rankBefore = rules.Rank;
            var resultBefore = rules.BuildResult();
            runner.RefreshForTest(before[0], 7f, false, SprintRules.RaceDistance, MinigamePhase.Play, null);
            Assert.That(runner.State, Is.EqualTo(RivalRunnerState.Run));
            runner.RefreshForTest(SprintRules.RaceDistance * (RivalRunnerAI.RivalBurstFromProgress + .02f), 7f, false,
                SprintRules.RaceDistance, MinigamePhase.Play, null);

            Assert.That(runner.State, Is.EqualTo(RivalRunnerState.Burst));
            Assert.That(rules.RivalDistances, Is.EqualTo(before));
            Assert.That(rules.Rank, Is.EqualTo(rankBefore));
            var resultAfter = rules.BuildResult();
            Assert.That(resultAfter.Pass, Is.EqualTo(resultBefore.Pass));
            Assert.That(resultAfter.Score, Is.EqualTo(resultBefore.Score));
            Object.DestroyImmediate(runner.gameObject);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void Rival_StandsAtItsOwnWorldPosition()
        {
            var runner = new GameObject("Rival").AddComponent<RivalRunnerAI>();
            runner.RefreshForTest(70f, 9f, false, SprintRules.RaceDistance, MinigamePhase.Play, null);
            Assert.That(runner.transform.position.x, Is.EqualTo(SprintTrackLayout.WorldX(70f)).Within(.001f));
            Assert.That(runner.State, Is.EqualTo(RivalRunnerState.Run));
            Object.DestroyImmediate(runner.gameObject);
        }

        [Test]
        public void Rival_WaitsForTheGunThenPullsUpAndStandsPastTheLine()
        {
            var runner = new GameObject("Rival").AddComponent<RivalRunnerAI>();
            runner.RefreshForTest(0f, 0f, false, SprintRules.RaceDistance, MinigamePhase.Play, null);
            Assert.That(runner.State, Is.EqualTo(RivalRunnerState.Idle), "still reacting to the gun");
            runner.RefreshForTest(SprintRules.RaceDistance + 1f, 6f, true, SprintRules.RaceDistance, MinigamePhase.Play, null);
            Assert.That(runner.State, Is.EqualTo(RivalRunnerState.Run), "pulling up past the line");
            runner.RefreshForTest(SprintRules.RaceDistance + 4f, 0f, true, SprintRules.RaceDistance, MinigamePhase.Play, null);
            Assert.That(runner.State, Is.EqualTo(RivalRunnerState.Idle));
            Object.DestroyImmediate(runner.gameObject);
        }

        [Test]
        public void Rival_NeverBurstsInLearn()
        {
            var runner = new GameObject("Rival").AddComponent<RivalRunnerAI>();
            runner.RefreshForTest(400f, 9f, false, float.PositiveInfinity, MinigamePhase.Play, null);
            Assert.That(runner.State, Is.EqualTo(RivalRunnerState.Run));
            Object.DestroyImmediate(runner.gameObject);
        }

        [Test]
        public void CadenceCombo_TracksConsecutiveValidTapsAndResetsOnWrongSide()
        {
            var controller = CreateSprintController();
            try
            {
                controller.OnLeftTap();
                controller.OnRightTap();
                Assert.That(controller.CadenceCombo, Is.EqualTo(2));

                controller.OnRightTap();
                Assert.That(controller.CadenceCombo, Is.Zero);
            }
            finally
            {
                DestroyController(controller);
            }
        }

        static SprintController CreateSprintController()
        {
            var value = new GameObject("SprintController").AddComponent<SprintController>();
            value.ConfigureForTest();
            return value;
        }

        static void DestroyController(SprintController controller) => Object.Destroy(controller.gameObject);

        static void AssertAnimatorStatesHaveAuthoredVisualMotions(Animator animator)
        {
            // Characters other than the base one reach the shared state machine through an
            // override controller, which swaps in their own clip set.
            var runtime = animator.runtimeAnimatorController;
            var overrideController = runtime as AnimatorOverrideController;
            var controller = (overrideController == null
                ? runtime
                : overrideController.runtimeAnimatorController) as AnimatorController;
            Assert.That(controller, Is.Not.Null, "RivalRunner must resolve to an authored AnimatorController asset.");

            var states = controller.layers[0].stateMachine.states;
            var expectedStates = new[] { "Idle", "Run", "Burst", "Stumble", "Celebrate", "Fail" };
            Assert.That(states.Select(value => value.state.name), Is.EquivalentTo(expectedStates));

            foreach (var expectedState in expectedStates)
            {
                var state = states.Single(value => value.state.name == expectedState).state;
                Assert.That(state.motion, Is.TypeOf<AnimationClip>(), $"{expectedState} must use an AnimationClip.");
                var baseClip = (AnimationClip)state.motion;
                var clip = overrideController == null ? baseClip : overrideController[baseClip];
                Assert.That(clip, Is.Not.Null,
                    $"{expectedState} must be overridden with this character's own clip.");
                Assert.That(clip.length, Is.GreaterThan(0f), $"{expectedState} clip must have a duration.");
                Assert.That(AnimationUtility.GetCurveBindings(clip).Any(binding =>
                        binding.type == typeof(Transform) && binding.path == "Visual" &&
                        binding.propertyName.StartsWith("m_Local")), Is.True,
                    $"{expectedState} clip must animate the child visual transform, never the lane root.");
            }
        }

        static void AssertAuthoredRivalMappings()
        {
            var authoredScene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/MG_Sprint.unity");
            try
            {
                var rivals = authoredScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<RivalRunnerAI>(true))
                    .OrderBy(rival => rival.Lane)
                    .ThenBy(rival => rival.name)
                    .ToArray();
                var rivalDiagnostics = string.Join("\n", rivals.Select(RivalMappingDiagnostic));
                Debug.Log($"[KMA] Sprint authored rival mappings:\n{rivalDiagnostics}");
                Assert.That(rivals.Length, Is.EqualTo(3), rivalDiagnostics);

                var controller = authoredScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SprintController>(true))
                    .Single();
                var expectedMappings = SprintRivalMappings.Required;
                var mappingFailures = new System.Collections.Generic.List<string>();
                for (var i = 0; i < rivals.Length; i++)
                {
                    var rival = rivals[i];
                    var expected = expectedMappings[i];
                    var expectedMapping = $"{expected.Name} / lane {expected.Lane}";
                    if (rival.name != expected.Name)
                        mappingFailures.Add($"{expectedMapping}: name was {rival.name}");
                    if (rival.Lane != expected.Lane)
                        mappingFailures.Add($"{expectedMapping}: lane was {rival.Lane}");
                    if (AssetDatabase.GetAssetPath(rival.ProfileAsset) != expected.ProfilePath)
                        mappingFailures.Add($"{expectedMapping}: profile was {AssetDatabase.GetAssetPath(rival.ProfileAsset)}");
                    if (PrefabUtility.GetCorrespondingObjectFromSource(rival.gameObject) == null)
                        mappingFailures.Add($"{expectedMapping}: corresponding prefab source was null");
                    if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(rival.gameObject) !=
                        "Assets/_Project/Prefabs/Gameplay/RivalRunner.prefab")
                        mappingFailures.Add($"{expectedMapping}: nearest prefab path was " +
                            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(rival.gameObject));

                    var serializedRival = new SerializedObject(rival);
                    if (serializedRival.FindProperty("controller").objectReferenceValue != controller)
                        mappingFailures.Add($"{expectedMapping}: controller reference was " +
                            serializedRival.FindProperty("controller").objectReferenceValue);
                }
                Assert.That(mappingFailures, Is.Empty,
                    $"Invalid rival mappings:\n{string.Join("\n", mappingFailures)}\nAll rivals:\n{rivalDiagnostics}");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(authoredScene);
            }
        }

        static string RivalMappingDiagnostic(RivalRunnerAI rival)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(rival.gameObject);
            var sourceDescription = source == null
                ? "<null>"
                : $"{source.name} ({AssetDatabase.GetAssetPath(source)})";
            var nearestPrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(rival.gameObject);
            return $"name={rival.name}, lane={rival.Lane}, correspondingSource={sourceDescription}, " +
                $"nearestPrefabPath={nearestPrefabPath}";
        }

        static T[] SceneObjects<T>(Scene scene) where T : Component
        {
            var all = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var matches = new System.Collections.Generic.List<T>();
            for (var i = 0; i < all.Length; i++)
                if (all[i].gameObject.scene == scene) matches.Add(all[i]);
            return matches.ToArray();
        }
    }
}
