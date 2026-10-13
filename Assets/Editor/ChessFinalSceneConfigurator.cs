#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using KMA.Gameplay;
using KMA.Gameplay.Chess;
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
using Object = UnityEngine.Object;

namespace KMA.EditorTools
{
    public static class ChessFinalSceneConfigurator
    {
        public const string ScenePath = "Assets/_Project/Scenes/MG_ChessFinal.unity";
        const string HudRootName = "S2_HUD_Minigame";
        const float BoardSize = 740f;
        // Side column centres and avatar box, tuned on 1920x1080 captures: the teacher column clears the
        // pause button, and the 3:4 avatar box matches the 192x256 poses so caps and ponytails stay whole.
        const float StudentX = .1f, TeacherX = .9f;
        static readonly Vector2 AvatarSize = new Vector2(360f, 480f);
        static readonly string[] StudentPoses = { "idle", "hurt", "cheer0", "cheer1", "fear", "think", "happy", "disappointed" };
        static readonly string[] TeacherPoses =
            { "idleBoss", "strictLook", "chessThink", "chessMove", "whistle0", "whistle1", "taunt", "cheer0", "command", "angry", "scoreWrite", "handsOnHips", "scoreHold" };
        // Pose keys the controller asks for → the supplement art drawing them; other keys draw the file of the same name.
        static readonly Dictionary<string, string> StudentArt = new Dictionary<string, string>
        {
            { "idle", "think" }, { "hurt", "disappointed" }, { "cheer0", "happy" }, { "cheer1", "happy" }
        };
        static readonly Dictionary<string, string> TeacherArt = new Dictionary<string, string>
        {
            { "idleBoss", "handsOnHips" }, { "taunt", "taunt" }, { "chessThink", "think" }, { "cheer0", "scoreWrite" }
        };

        [MenuItem("KMA/Chess Final/Build Scene")]
        public static void BuildScene()
        {
            ChessArtImporter.ImportAll();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var controllerObject = new GameObject("ChessFinalController");
            controllerObject.AddComponent<ChessFinalController>();
            var serialized = new SerializedObject(controllerObject.GetComponent<ChessFinalController>());
            serialized.FindProperty("tutorialSeconds").floatValue = 0f;
            serialized.FindProperty("countdownSeconds").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);

            MinigameUIAssembler.AssembleScenePath(ScenePath);
            BuildLayout();
            EnsureInBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[KMA] MG_ChessFinal built.");
        }

        static void BuildLayout()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject hudRoot = scene.GetRootGameObjects().Single(go => go.name == HudRootName);
            var parent = (RectTransform)(hudRoot.transform.Find("SafeAreaRoot") ?? hudRoot.transform);
            foreach (Transform child in hudRoot.GetComponentsInChildren<Transform>(true)
                         .Where(t => new[] { "Phase", "Stamina", "Score", "HeartBar", "Progress", "Status", "Timer" }
                             .Contains(t.name)).ToArray())
                child.gameObject.SetActive(false);
            Vector2 top = new Vector2(.5f, 1f), centre = new Vector2(.5f, .5f), bottom = new Vector2(.5f, 0f);

            // Backdrop: the shared campus on the canvas root (covers the safe-area insets), then a wash
            // in the theme background so the board stays the focus.
            var canvasRoot = (RectTransform)hudRoot.transform;
            CampusBackdropAuthoring.AddUi(canvasRoot, .18f, .30f, true);
            RectTransform wash = Rect(canvasRoot, "Wash", Vector2.zero, Vector2.one);
            var washImage = wash.gameObject.AddComponent<Image>();
            washImage.color = MinigameUiTheme.WithAlpha(UITheme.Shared.Background, .62f);
            washImage.raycastTarget = false;
            washImage.gameObject.AddComponent<StyleExempt>();
            wash.SetSiblingIndex(1);

            TMP_Text title = UiKit.Label(parent, "Title", "Bài kiểm tra cuối", MinigameUiTheme.Title,
                MinigameUiTheme.TextPrimary);
            UiKit.Place(title.rectTransform, top, top, new Vector2(0f, -18f), new Vector2(900f, 70f));
            ChipHandle objective = UiKit.Chip(parent, "Objective", "Chiếu hết");
            UiKit.Place(objective.Background.rectTransform, top, top, new Vector2(-150f, -96f), new Vector2(480f, 64f));
            ChipHandle clock = UiKit.Chip(parent, "Clock", "01:30");
            UiKit.Place(clock.Background.rectTransform, top, top, new Vector2(230f, -96f), new Vector2(200f, 64f));

            Image frame = UiKit.Panel(parent, "BoardFrame");
            UiKit.Place(frame.rectTransform, centre, centre, new Vector2(0f, -30f), Vector2.one * (BoardSize + 64f));
            RectTransform boardRect = Rect(frame.transform, "Board", centre, centre);
            boardRect.sizeDelta = Vector2.one * BoardSize;
            var board = boardRect.gameObject.AddComponent<ChessBoardView>();
            board.Configure(ChessArtImporter.LoadPieces(), UiKitAssets.Load().Circle, UiKitAssets.Load().Ring);
            for (int i = 0; i < 8; i++)
            {
                TMP_Text file = UiKit.Label(frame.transform, "File" + (char)('a' + i), ((char)('a' + i)).ToString(),
                    MinigameUiTheme.Caption, MinigameUiTheme.TextPrimary);
                UiKit.Place(file.rectTransform, new Vector2(.5f, 0f), centre,
                    new Vector2((i - 3.5f) * BoardSize / 8f, 16f), new Vector2(40f, 30f));
                TMP_Text rank = UiKit.Label(frame.transform, "Rank" + (i + 1), (i + 1).ToString(),
                    MinigameUiTheme.Caption, MinigameUiTheme.TextPrimary);
                UiKit.Place(rank.rectTransform, new Vector2(0f, .5f), centre,
                    new Vector2(16f, (i - 3.5f) * BoardSize / 8f), new Vector2(30f, 40f));
            }

            TMP_Text turn = UiKit.Label(parent, "Turn", "Đọc đề rồi bấm Bắt đầu", MinigameUiTheme.Body,
                MinigameUiTheme.TextPrimary);
            UiKit.Place(turn.rectTransform, bottom, bottom, new Vector2(-170f, 28f), new Vector2(520f, 60f));
            ButtonHandle hint = UiKit.Button(parent, "HintButton", "Gợi ý", ButtonVariant.Secondary);
            UiKit.Place((RectTransform)hint.Button.transform, bottom, bottom, new Vector2(220f, 14f),
                new Vector2(220f, MinigameUiTheme.ButtonHeight));
            TMP_Text toast = UiKit.Label(parent, "Toast", string.Empty, MinigameUiTheme.Body,
                MinigameUiTheme.Accent, outline: true);
            UiKit.Place(toast.rectTransform, centre, centre, new Vector2(0f, 420f), new Vector2(760f, 56f));
            toast.gameObject.SetActive(false);

            // Side columns: avatars bottom-anchored below the name and counters.
            Image student = Avatar(parent, "Student", StudentX, Art(CharacterArt.Hero, StudentArt, "idle"));
            Image teacher = Avatar(parent, "Teacher", TeacherX, Art(CharacterArt.Boss, TeacherArt, "idleBoss"));
            // The art looks right; she stands right of the board, so mirror her to look at it.
            teacher.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            NameTag(parent, "StudentName", "SV năm 1", StudentX);
            NameTag(parent, "TeacherName", "Giảng viên thể chất", TeacherX);
            ChipHandle moves = UiKit.Chip(parent, "Moves", "Nước: 0/3");
            UiKit.Place(moves.Background.rectTransform, new Vector2(StudentX, .78f), centre, Vector2.zero, new Vector2(240f, 60f));
            Image bubble = UiKit.Panel(parent, "SpeechBubble");
            UiKit.Place(bubble.rectTransform, new Vector2(TeacherX, .72f), centre, Vector2.zero, new Vector2(320f, 190f));
            TMP_Text bubbleText = UiKit.Label(bubble.transform, "Text", string.Empty, MinigameUiTheme.Caption,
                MinigameUiTheme.TextPrimary);
            UiKit.Stretch(bubbleText.rectTransform, new Vector2(18f, 14f), new Vector2(-18f, -14f));
            bubbleText.textWrappingMode = TextWrappingModes.Normal;
            bubble.gameObject.SetActive(false);

            Image intro = UiKit.Panel(parent, "IntroCard", alpha: 1f);
            UiKit.Place(intro.rectTransform, centre, centre, new Vector2(0f, -30f), new Vector2(640f, 320f));
            TMP_Text introText = UiKit.Label(intro.transform, "Text",
                "Đang tạo thế cờ…",
                MinigameUiTheme.BodyLarge, MinigameUiTheme.TextPrimary);
            UiKit.Place(introText.rectTransform, top, top, new Vector2(0f, -36f), new Vector2(580f, 140f));
            ButtonHandle start = UiKit.Button(intro.transform, "StartButton", "Bắt đầu", ButtonVariant.Primary);
            UiKit.Place((RectTransform)start.Button.transform, bottom, bottom, new Vector2(0f, 36f),
                new Vector2(320f, MinigameUiTheme.ButtonHeight));

            Image errorCard = UiKit.Panel(parent, "AnalysisErrorCard", alpha: 1f);
            UiKit.Place(errorCard.rectTransform, centre, centre, new Vector2(0f, -30f), new Vector2(640f, 320f));
            TMP_Text errorText = UiKit.Label(errorCard.transform, "Text",
                "Chưa phân tích được nước đi này.\nNước của bạn vẫn được giữ. Đồng hồ đã dừng.",
                MinigameUiTheme.BodyLarge, MinigameUiTheme.TextPrimary);
            UiKit.Place(errorText.rectTransform, top, top, new Vector2(0f, -36f), new Vector2(580f, 140f));
            ButtonHandle retry = UiKit.Button(errorCard.transform, "RetryAnalysisButton", "Thử lại", ButtonVariant.Primary);
            UiKit.Place((RectTransform)retry.Button.transform, bottom, bottom, new Vector2(-170f, 36f),
                new Vector2(300f, MinigameUiTheme.ButtonHeight));
            ButtonHandle restartPuzzle = UiKit.Button(errorCard.transform, "RestartPuzzleButton", "Chơi lại đề", ButtonVariant.Secondary);
            UiKit.Place((RectTransform)restartPuzzle.Button.transform, bottom, bottom, new Vector2(170f, 36f),
                new Vector2(300f, MinigameUiTheme.ButtonHeight));
            errorCard.gameObject.SetActive(false);

            Image picker = UiKit.Panel(parent, "PromotionPicker");
            UiKit.Place(picker.rectTransform, centre, centre, new Vector2(0f, -30f), new Vector2(640f, 180f));
            Button[] options = new[] { "Hậu", "Xe", "Tượng", "Mã" }.Select((label, i) =>
            {
                ButtonHandle option = UiKit.Button(picker.transform, "Promote" + i, label, ButtonVariant.Primary);
                UiKit.Place((RectTransform)option.Button.transform, new Vector2((i + .5f) / 4f, .5f), centre,
                    Vector2.zero, new Vector2(136f, MinigameUiTheme.ButtonHeight));
                return option.Button;
            }).ToArray();
            var promotion = picker.gameObject.AddComponent<PromotionPicker>();
            promotion.Configure(options[0], options[1], options[2], options[3]);

            var hud = parent.gameObject.AddComponent<ChessFinalHud>();
            hud.Configure(objective.Label, clock.Label, moves.Label, turn, toast, intro.gameObject, start.Button,
                hint.Button, introText, errorCard.gameObject, retry.Button, restartPuzzle.Button);
            var cast = parent.gameObject.AddComponent<ChessCastView>();
            cast.Configure(student, teacher, Poses(CharacterArt.Hero, StudentPoses, StudentArt),
                Poses(CharacterArt.Boss, TeacherPoses, TeacherArt), bubble.gameObject, bubbleText);

            var pause = Object.FindFirstObjectByType<PausePanel>(FindObjectsInactive.Include);
            if (pause != null)
            {
                pause.transform.SetParent(parent, false);
                UiKit.PlacePause((RectTransform)pause.transform);
            }
            // Popups stay on top of the board.
            intro.transform.SetAsLastSibling();
            errorCard.transform.SetAsLastSibling();
            picker.transform.SetAsLastSibling();
            picker.gameObject.SetActive(false);

            var controller = Object.FindFirstObjectByType<ChessFinalController>();
            controller.Configure(board, hud, cast, promotion);
            var dialogue=controller.gameObject.AddComponent<GameplayDialogueHost>();dialogue.Configure(GameplayDialogueLibrary.LoadDefault(),cast);cast.BindDialogue(dialogue);
            var adapter=controller.gameObject.AddComponent<ChessDialogueAdapter>();
            DialoguePresentationAuthoring.WireOpening(controller,dialogue,adapter);
            var serializedAdapter=new SerializedObject(adapter);serializedAdapter.FindProperty("view").objectReferenceValue=cast;serializedAdapter.ApplyModifiedPropertiesWithoutUndo();
            foreach (Object dirty in new Object[] { controller, board, hud, cast, promotion, hudRoot })
                EditorUtility.SetDirty(dirty);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static ChessCastView.Pose[] Poses(string character, string[] names, IReadOnlyDictionary<string, string> art) => names
            .Select(name => new ChessCastView.Pose { name = name, sprite = Art(character, art, name) })
            .ToArray();

        static Sprite Art(string character, IReadOnlyDictionary<string, string> art, string key) =>
            CharacterArt.Load(character, art.TryGetValue(key, out string file) ? file : key);

        static Image Avatar(RectTransform parent, string name, float x, Sprite sprite)
        {
            RectTransform rect = Rect(parent, name, new Vector2(x, .06f), new Vector2(x, .06f));
            rect.pivot = new Vector2(.5f, 0f);
            rect.sizeDelta = AvatarSize;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.gameObject.AddComponent<StyleExempt>();
            image.raycastTarget = false;
            return image;
        }

        static void NameTag(RectTransform parent, string name, string text, float x)
        {
            TMP_Text label = UiKit.Label(parent, name, text, MinigameUiTheme.BodyLarge, MinigameUiTheme.TextPrimary,
                outline: true);
            UiKit.Place(label.rectTransform, new Vector2(x, .86f), new Vector2(.5f, .5f), Vector2.zero,
                new Vector2(320f, 60f));
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
