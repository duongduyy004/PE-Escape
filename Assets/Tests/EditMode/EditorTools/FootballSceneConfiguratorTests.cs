#if UNITY_EDITOR
using System;
using System.Linq;
using KMA.EditorTools;
using KMA.Gameplay;
using KMA.Gameplay.UI;
using KMA.UI.Kit;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KMA.Tests.EditorTools
{
    public sealed class FootballSceneConfiguratorTests
    {
        [TearDown]
        public void ReleaseBuiltSceneAfterTest() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test]
        public void RepeatedBuildAndSharedAssemblerKeepOneSourcedFootballScene()
        {
            FootballSceneConfigurator.BuildScene();
            FootballSceneConfigurator.BuildScene();
            MinigameUIAssembler.AssembleScenePath(FootballSceneConfigurator.ScenePath);
            var scene = EditorSceneManager.OpenScene(FootballSceneConfigurator.ScenePath, OpenSceneMode.Single);

            int controllers = 0, resultPanels = 0, huds = 0, placeholders = 0, pauses = 0;
            FootballPresentation presentation = null;
            EventSystem eventSystem = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                controllers += root.GetComponentsInChildren<FootballController>(true).Length;
                huds += root.GetComponentsInChildren<FootballHud>(true).Length;
                pauses += root.GetComponentsInChildren<PausePanel>(true).Length;
                placeholders += root.GetComponentsInChildren<PlaceholderMinigameController>(true).Length;
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour is IResultPreviewPanel) resultPanels++;
                presentation ??= root.GetComponentInChildren<FootballPresentation>(true);
                eventSystem ??= root.GetComponentInChildren<EventSystem>(true);
            }

            Assert.That(controllers, Is.EqualTo(1));
            Assert.That(resultPanels, Is.EqualTo(1), "only the shared ResultPanel");
            Assert.That(huds, Is.EqualTo(1));
            Assert.That(pauses, Is.EqualTo(1));
            Assert.That(placeholders, Is.Zero);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.ValidateReferences(), Is.True);
            Assert.That(eventSystem, Is.Not.Null);
            Assert.That(eventSystem.GetComponent<InputSystemUIInputModule>(), Is.Not.Null);

            GameObject hud = GameObject.Find("S2_HUD_Minigame");
            Assert.That(hud, Is.Not.Null, "Football uses the shared HUD canvas");
            Assert.That(hud.GetComponent<Canvas>().sortingOrder, Is.EqualTo(500));
            Assert.That(GameObject.Find("FootballHUD"), Is.Null);
            // The shared PhaseOverlay's tutorial card has its own BackButton; Football's old back button must be gone.
            Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Button>(true))
                    .Where(button => button.name == "BackButton" && !button.GetComponentInParent<PhaseOverlay>(true)),
                Is.Empty, "the pause menu replaces the back button");
            Assert.That(GameObject.Find("CountdownPanel"), Is.Null, "the shared 3-2-1 replaces SẴN SÀNG!");

            var controller = UnityEngine.Object.FindFirstObjectByType<FootballController>();
            Assert.That(controller.ValidateReferences(), Is.True);
            var overlay = UnityEngine.Object.FindFirstObjectByType<PhaseOverlay>(FindObjectsInactive.Include);
            Assert.That(new SerializedObject(overlay).FindProperty("minigameSource").objectReferenceValue, Is.SameAs(controller));
            var lecturer = UnityEngine.Object.FindFirstObjectByType<StartLecturer>();
            Assert.That(lecturer, Is.Not.Null, "StartLecturer must be present in Football scene");
            var adapter = UnityEngine.Object.FindFirstObjectByType<FootballDialogueAdapter>();
            Assert.That(adapter, Is.Not.Null, "FootballDialogueAdapter must be present in Football scene");

            var pause = UnityEngine.Object.FindFirstObjectByType<PausePanel>(FindObjectsInactive.Include);
            Assert.That(pause.transform.parent.name, Is.EqualTo("SafeAreaRoot"));
            Assert.That(pause.GetComponent<Image>().sprite, Is.SameAs(UiKitAssets.Load().RoundRect20));

            var slider = GameObject.Find("DirectionSlider").GetComponent<Slider>();
            Assert.That(slider.minValue, Is.EqualTo(-1f));
            Assert.That(slider.maxValue, Is.EqualTo(1f));
            Assert.That(slider.handleRect, Is.Not.Null);
            Assert.That(GameObject.Find("PowerBar").GetComponent<KitBar>(), Is.Not.Null);
            Assert.That(GameObject.Find("SHOOT").GetComponent<KitPressFeedback>(), Is.Not.Null);
            Assert.That(GameObject.Find("AIM"), Is.Null);
            Assert.That(GameObject.Find("TrajectoryDot0").GetComponent<SpriteRenderer>().enabled, Is.False);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<Camera>().backgroundColor,
                Is.EqualTo((Color)new Color32(120, 207, 235, 255)), "the sky survives the assembler's camera setup");

            var subject = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/_Project/ScriptableObjects/Subjects/Football.asset");
            Assert.That(subject, Is.Not.Null);
            var serializedSubject = new SerializedObject(subject);
            Assert.That(serializedSubject.FindProperty("timeLimit").floatValue, Is.Zero);
            Assert.That(serializedSubject.FindProperty("goalText").stringValue, Is.EqualTo("Ghi ít nhất 3 bàn sau 5 lượt sút."));
            Assert.That(EditorBuildSettings.scenes, Has.Some.Matches<EditorBuildSettingsScene>(s => s.path == FootballSceneConfigurator.ScenePath && s.enabled));
        }

        [Test]
        public void StartCardIsOpaqueAndDimsTheWorldBehindIt()
        {
            FootballSceneConfigurator.BuildScene();
            EditorSceneManager.OpenScene(FootballSceneConfigurator.ScenePath, OpenSceneMode.Single);
            var start = GameObject.Find("StartPanel").GetComponent<UnityEngine.UI.Image>();
            Assert.That(start.color.a, Is.EqualTo(1f).Within(1e-3f), "a translucent card shows the kicker through it");
            Transform scrim = start.transform.parent.Find("StartScrim");
            Assert.That(scrim, Is.Not.Null);
            Assert.That(scrim.GetSiblingIndex(), Is.EqualTo(start.transform.GetSiblingIndex() - 1));
            Assert.That(scrim.GetComponent<UnityEngine.UI.Image>().color, Is.EqualTo(KMA.UI.Kit.MinigameUiTheme.Scrim));
        }

        [Test]
        public void StartCardSitsBetweenTheScoreChipAndTheKicker()
        {
            FootballSceneConfigurator.BuildScene();
            EditorSceneManager.OpenScene(FootballSceneConfigurator.ScenePath, OpenSceneMode.Single);
            var card = (RectTransform)GameObject.Find("StartPanel").transform;
            Assert.That(card.sizeDelta.y, Is.LessThanOrEqualTo(420f), "the start card is compact");

            // The HUD canvas scales by height against 1080 px; the card is centre-anchored in the safe area.
            float cardBottom01 = .5f + (card.anchoredPosition.y - card.sizeDelta.y * card.pivot.y) / 1080f;
            float cardTop01 = .5f + (card.anchoredPosition.y + card.sizeDelta.y * (1f - card.pivot.y)) / 1080f;
            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            float viewBottom = camera.transform.position.y - camera.orthographicSize;
            var kicker = GameObject.Find("FootballWorld").transform.Find("Player").GetComponent<SpriteRenderer>();
            float kickerTop01 = (kicker.bounds.max.y - viewBottom) / (camera.orthographicSize * 2f);
            Assert.That(cardBottom01, Is.GreaterThanOrEqualTo(kickerTop01), "the start card must not cover the kicker");

            var scoreChip = (RectTransform)GameObject.Find("ScoreChip").transform;
            float chipBottom01 = 1f - (-scoreChip.anchoredPosition.y + scoreChip.sizeDelta.y) / 1080f;
            Assert.That(cardTop01, Is.LessThanOrEqualTo(chipBottom01), "the start card must not cover the score chip");
        }
    }
}
#endif
