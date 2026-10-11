#if UNITY_EDITOR
using System;
using System.Linq;
using KMA.Gameplay.Celebration;
using KMA.Gameplay.UI;
using KMA.UI.Kit;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KMA.EditorTools
{
    public static class CelebrationSceneConfigurator
    {
        public const string ScenePath = "Assets/_Project/Scenes/Celebration.unity";
        const string Classmate = "FemalePerson";
        static readonly Vector2 AvatarSize = new Vector2(300f, 400f);
        // Slot order: arrive, cheer A, cheer B. Slots 1 and 2 alternate, so they stay the designed cheer pair.
        static readonly string[] HeroPoses = { "idle", "happy", "happy" };
        static readonly string[] ClassmatePoses = { "idle", "cheer0", "cheer1" };
        // Slot order: waiting, first reaction, final cheer.
        static readonly string[] TeacherPoses = { "strictLook", "scoreHold", "scoreWrite" };

        [MenuItem("KMA/Celebration/Build Scene")]
        public static void BuildScene()
        {
            ChessArtImporter.ImportAll();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var cameraObject = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = CampusBackdropAuthoring.EnsureArt().SkyColor;

            var canvasObject = new GameObject("CelebrationCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            CampusBackdropAuthoring.AddUi((RectTransform)canvasObject.transform, .14f, .34f, true);
            RectTransform parent = Rect(canvasObject.transform, "SafeAreaRoot", Vector2.zero, Vector2.one);
            parent.gameObject.AddComponent<SafeAreaFitter>();
            Vector2 centre = new Vector2(.5f, .5f), bottom = new Vector2(.5f, 0f), top = new Vector2(.5f, 1f);

            Image classmate = Avatar(parent, "Classmate", .33f, CharacterArt.Load(Classmate, "idle"));
            classmate.rectTransform.localScale = Vector3.one * .9f;
            Image student = Avatar(parent, "Student", .5f, CharacterArt.Load(CharacterArt.Hero, HeroPoses[0]));
            Image teacher = Avatar(parent, "Teacher", .78f, CharacterArt.Load(CharacterArt.Boss, "strictLook"));
            // The art looks right; she stands right of the students, so mirror her to look at them.
            teacher.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

            // Confetti falls in front of the cast and behind the speech bubble and the summary.
            var confetti = Rect(parent, "Confetti", Vector2.zero, Vector2.one).gameObject.AddComponent<UiConfetti>();

            Image bubble = UiKit.Panel(parent, "SpeechBubble");
            UiKit.Place(bubble.rectTransform, new Vector2(.78f, .45f), centre, Vector2.zero, new Vector2(320f, 150f));
            TMP_Text bubbleText = UiKit.Label(bubble.transform, "Text", string.Empty, MinigameUiTheme.Caption,
                MinigameUiTheme.TextPrimary);
            UiKit.Stretch(bubbleText.rectTransform, new Vector2(18f, 14f), new Vector2(-18f, -14f));
            bubbleText.textWrappingMode = TextWrappingModes.Normal;
            bubble.gameObject.SetActive(false);

            ButtonHandle skip = UiKit.Button(parent, "SkipButton", "Bỏ qua", ButtonVariant.Secondary);
            UiKit.Place((RectTransform)skip.Button.transform, Vector2.one, Vector2.one, new Vector2(-24f, -24f),
                new Vector2(220f, MinigameUiTheme.ButtonHeight));

            Image summary = UiKit.Panel(parent, "Summary");
            UiKit.Place(summary.rectTransform, centre, centre, Vector2.zero, new Vector2(1100f, 620f));
            var summaryGroup = summary.gameObject.AddComponent<CanvasGroup>();
            TMP_Text title = UiKit.Label(summary.transform, "Title", "Đã qua thể chất!",
                MinigameUiTheme.Display * .5f, MinigameUiTheme.Accent, outline: true);
            UiKit.Place(title.rectTransform, top, top, new Vector2(0f, -24f), new Vector2(1000f, 100f));
            TMP_Text[] rows = Enumerable.Range(0, 4).Select(i =>
            {
                TMP_Text row = UiKit.Label(summary.transform, "Row" + i, string.Empty, MinigameUiTheme.Body,
                    MinigameUiTheme.TextPrimary, TextAlignmentOptions.Left);
                UiKit.Place(row.rectTransform, centre, centre, new Vector2(0f, 160f - 76f * i), new Vector2(980f, 60f));
                return row;
            }).ToArray();
            TMP_Text footnote = UiKit.Label(summary.transform, "Footnote", string.Empty, MinigameUiTheme.Caption,
                MinigameUiTheme.TextPrimary);
            UiKit.Place(footnote.rectTransform, centre, centre, new Vector2(0f, -140f), new Vector2(980f, 44f));
            ButtonHandle menu = UiKit.Button(summary.transform, "MenuButton", "Về menu", ButtonVariant.Primary);
            UiKit.Place((RectTransform)menu.Button.transform, bottom, bottom, new Vector2(170f, 32f),
                new Vector2(300f, MinigameUiTheme.ButtonHeight));
            ButtonHandle replay = UiKit.Button(summary.transform, "ReplayButton", "Chơi lại", ButtonVariant.Secondary);
            UiKit.Place((RectTransform)replay.Button.transform, bottom, bottom, new Vector2(-170f, 32f),
                new Vector2(300f, MinigameUiTheme.ButtonHeight));

            // Black cover that fades out at the start; it never takes raycasts, so Skip always works.
            Image fade = Rect(parent, "FadeIn", Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            fade.color = Color.black;
            fade.raycastTarget = false;

            var controller = canvasObject.AddComponent<CelebrationSceneController>();
            controller.Configure(student, classmate, teacher, Poses(CharacterArt.Hero, HeroPoses),
                Poses(Classmate, ClassmatePoses), Poses(CharacterArt.Boss, TeacherPoses), bubble.gameObject, bubbleText,
                confetti, summaryGroup, title, rows, footnote, skip.Button, menu.Button, replay.Button);
            controller.SetFade(fade);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureInBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[KMA] Celebration built.");
        }

        static Sprite[] Poses(string character, string[] names) =>
            names.Select(name => CharacterArt.Load(character, name)).ToArray();

        static Image Avatar(RectTransform parent, string name, float x, Sprite sprite)
        {
            RectTransform rect = Rect(parent, name, new Vector2(x, .04f), new Vector2(x, .04f));
            rect.pivot = new Vector2(.5f, 0f);
            rect.sizeDelta = AvatarSize;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        static void EnsureInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
