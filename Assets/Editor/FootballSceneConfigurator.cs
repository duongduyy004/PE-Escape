#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using KMA.Gameplay;
using KMA.Gameplay.Core;
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
    public static class FootballSceneConfigurator
    {
        public const string ScenePath = "Assets/_Project/Scenes/MG_Football.unity";
        const string ArtRoot = "Assets/_Project/Art/Football/";
        const string ConfigPath = "Assets/_Project/ScriptableObjects/Football/FootballDifficulty.asset";
        const string SubjectPath = "Assets/_Project/ScriptableObjects/Subjects/Football.asset";
        const string HudRootName = "S2_HUD_Minigame";
        const int HudSortingOrder = 500;
        static readonly Color Sky = new Color32(120, 207, 235, 255);
        public const string KeeperCharacter = CharacterArt.Keeper;
        public const string KeeperReadyPose = "ready";
        // The kicker stands nearest the camera: 186x248 preview px, feet just below the penalty spot row.
        const float KickerDisplayWidth = 186f;
        public const float KickerDisplayHeight = 248f;
        static readonly Vector2 KickerFeet = new Vector2(510f, 600f);
        static readonly Vector2 StartCardSize = new Vector2(850f, 400f);
        const float StartCardY = 200f;

        [MenuItem("KMA/Football/Build Scene")]
        public static void BuildScene()
        {
            if (!File.Exists(ScenePath))
                throw new FileNotFoundException("MG_Football scene is missing; refusing to create a new scene identity.", ScenePath);
            ImportGoalViewArt();
            CharacterArt.ImportAll();
            EnsureConfiguration();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
            BuildWorld(scene);
            BuildController(scene);
            EnsureEventSystem(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            // The shared HUD, phase overlay, result panel and pause menu come from the assembler; it keeps the
            // sky because FootballController owns its camera background.
            MinigameUIAssembler.AssembleScenePath(ScenePath);
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            BuildUi(scene);
            EnsureEventSystem(scene);

            StartLecturerAuthoring.AddToFootball(scene);
            var lecturer = UnityEngine.Object.FindFirstObjectByType<StartLecturer>();
            var dialogueController = UnityEngine.Object.FindFirstObjectByType<FootballController>();
            var dialogueHost = DialoguePresentationAuthoring.Configure(dialogueController.transform, DialogueHudSlot.Football);
            var dialogueAdapter = dialogueController.gameObject.AddComponent<FootballDialogueAdapter>();
            var openingFlow = dialogueController.GetComponent<GameplayOpeningFlow>();
            DialoguePresentationAuthoring.WireOpening(dialogueController, dialogueHost, dialogueAdapter);
            dialogueAdapter.Configure(dialogueController, dialogueHost, openingFlow, lecturer);
            ConfigureSubjectAsset();
            EnsureInBuildSettings();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[KMA] MG_Football penalty scene built and saved.");
        }

        static void BuildController(Scene scene)
        {
            var input = new GameObject("FootballInputBridge").AddComponent<FootballInputBridge>();
            SceneManager.MoveGameObjectToScene(input.gameObject, scene);
            input.gameObject.AddComponent<FootballController>();
        }

        static void EnsureConfiguration()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            if (AssetDatabase.LoadAssetAtPath<FootballDifficultyConfig>(ConfigPath) != null) return;
            var config = ScriptableObject.CreateInstance<FootballDifficultyConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }

        static void BuildWorld(Scene scene)
        {
            var cameraRoot = new GameObject("GameCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraRoot, scene);
            cameraRoot.tag = "MainCamera";
            cameraRoot.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraRoot.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Sky;
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 100f;
            AddUrpCameraData(cameraRoot);

            var cameraFit = cameraRoot.AddComponent<FootballCameraFit>();
            var world = new GameObject("FootballWorld");
            SceneManager.MoveGameObjectToScene(world, scene);
            var presentation = world.AddComponent<FootballPresentation>();
            SpriteRenderer Layer(string name, string image, float x, float y, float width, float height, int order) =>
                Renderer(world.transform, name, LoadSprite("GoalView/" + image + ".png"),
                    FootballPresentation.ScreenToWorld(x, y), order, new Vector2(width, height) * FootballPresentation.PixelToWorld);
            var field = Layer("Field", "field", 600, 337.5f, 1200, 675, 0);
            cameraFit.Configure(field);
            var goal = Layer("Goal", "goal", 600, 230, 620, 170, 10);
            var net = Layer("GoalNet", "net", 600, 230, 620, 170, 11);
            var poses = LoadPoses();
            var keeper = Renderer(world.transform, "Goalkeeper", poses.keeperReady, FootballPresentation.KeeperWorldPosition(0f, 0f), 20,
                new Vector2(FootballPresentation.KeeperDisplayWidth, FootballPresentation.KeeperDisplayHeight) * FootballPresentation.PixelToWorld);
            var player = Renderer(world.transform, "Player", poses.kickerReady, FootballPresentation.ScreenToWorld(KickerFeet.x, KickerFeet.y), 21,
                new Vector2(KickerDisplayWidth, KickerDisplayHeight) * FootballPresentation.PixelToWorld);
            var ball = Layer("Ball", "ball", 600, 486, 36, 36, 25);
            var shadow = Layer("BallShadow", "shadow", 600, 502, 48, 16, 19);
            var crosshair = Layer("AimCrosshair", "crosshair", 600, 213, 44, 44, 30);
            crosshair.color = MinigameUiTheme.Accent;
            crosshair.enabled = false;
            var dots = new SpriteRenderer[140];
            for (int i = 0; i < dots.Length; i++)
            {
                dots[i] = Layer("TrajectoryDot" + i, "knob", 600, 486, 5, 5, 29);
                dots[i].color = MinigameUiTheme.WithAlpha(MinigameUiTheme.Accent, .75f);
                dots[i].enabled = false;
            }
            var left = new GameObject("GoalLeftInsidePost").transform;
            left.SetParent(world.transform, false);
            left.position = FootballPresentation.ScreenToWorld(300, 302);
            var right = new GameObject("GoalRightInsidePost").transform;
            right.SetParent(world.transform, false);
            right.position = FootballPresentation.ScreenToWorld(900, 302);
            presentation.Configure(field, goal, ball, shadow, player, keeper, crosshair, left, right, dots, net);
            presentation.ConfigurePoses(poses);
        }

        /// <summary>The hero kicks, seen from behind; the student keeper faces him.</summary>
        // Keeper poses never have a ball drawn in: the live ball stays visible on a save.
        static FootballPoseSprites LoadPoses() => new FootballPoseSprites
        {
            kickerReady = CharacterArt.Load(CharacterArt.Hero, "footballBackIdle"),
            kickerRunUp = CharacterArt.Load(CharacterArt.Hero, "footballBackApproach"),
            kickerStrike = CharacterArt.Load(CharacterArt.Hero, "footballBackKick"),
            kickerCelebrate = CharacterArt.Load(CharacterArt.Hero, "footballBackCelebrate"),
            keeperReady = CharacterArt.Load(KeeperCharacter, KeeperReadyPose),
            keeperReachLeft = CharacterArt.Load(KeeperCharacter, "reachLeftEmpty"),
            keeperReachRight = CharacterArt.Load(KeeperCharacter, "reachRightEmpty"),
            keeperDiveLeft = CharacterArt.Load(KeeperCharacter, "diveLeftEmpty"),
            keeperDiveRight = CharacterArt.Load(KeeperCharacter, "diveRightEmpty"),
            keeperCheer = CharacterArt.Load(KeeperCharacter, "cheer")
        };

        static void ImportGoalViewArt()
        {
            foreach (string file in Directory.GetFiles(ArtRoot + "GoalView", "*.png"))
            {
                AssetDatabase.ImportAsset(file, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(file);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 4096;
                importer.spritePixelsPerUnit = 200f;
                importer.filterMode = FilterMode.Bilinear;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(.5f, .5f);
                settings.spriteBorder = Vector4.zero;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        static void BuildUi(Scene scene)
        {
            GameObject hudRoot = scene.GetRootGameObjects().Single(go => go.name == HudRootName);
            hudRoot.GetComponent<Canvas>().sortingOrder = HudSortingOrder;
            var safe = (RectTransform)hudRoot.transform.Find("SafeAreaRoot");
            // The generic HUD widgets belong to other sports; hide only the prefab's children.
            foreach (Transform child in safe)
                child.gameObject.SetActive(false);

            RectTransform root = UiKit.Rect(safe, "FootballControls");
            UiKit.Stretch(root);
            root.SetAsFirstSibling();
            var hud = root.gameObject.AddComponent<FootballHud>();
            Vector2 centre = new Vector2(.5f, .5f);
            float edge = MinigameUiTheme.SpaceMd;

            // Top: score, shots left and one dot per kick.
            ChipHandle score = UiKit.Chip(root, "ScoreChip", "BÀN: 0");
            score.Label.name = "ScoreLabel";
            UiKit.StyleLabel(score.Label, MinigameUiTheme.Title, MinigameUiTheme.TextPrimary);
            UiKit.Place(score.Background.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -edge),
                new Vector2(340f, 84f));

            ChipHandle remaining = UiKit.Chip(root, "RemainingChip", "CÒN 5 LƯỢT");
            remaining.Label.name = "RemainingLabel";
            UiKit.Anchor(remaining.Label.rectTransform, new Vector2(0f, .45f), new Vector2(1f, 1f));
            UiKit.Place(remaining.Background.rectTransform, Vector2.one, Vector2.one,
                new Vector2(-(MinigameUiTheme.ButtonHeight + edge * 2f), -edge), new Vector2(360f, 120f));
            var markers = new Image[5];
            for (int i = 0; i < markers.Length; i++)
            {
                markers[i] = UiKit.Disc(remaining.Background.transform, "KickMarker" + (i + 1), false, MinigameUiTheme.Track);
                UiKit.Place(markers[i].rectTransform, new Vector2(.14f + i * .18f, .25f), centre, Vector2.zero, Vector2.one * 28f);
            }

            // Bottom left: aim.
            Image aim = UiKit.Panel(root, "DirectionPanel");
            UiKit.Anchor(aim.rectTransform, new Vector2(.025f, .025f), new Vector2(.395f, .235f));
            TMP_Text aimTitle = UiKit.Label(aim.transform, "DirectionTitle", "HƯỚNG BÓNG", MinigameUiTheme.Caption,
                MinigameUiTheme.TextPrimary, TextAlignmentOptions.Left);
            UiKit.Anchor(aimTitle.rectTransform, new Vector2(.05f, .70f), new Vector2(.58f, .95f));
            TMP_Text directionValue = UiKit.Label(aim.transform, "DirectionValue", "PHẢI 55%", MinigameUiTheme.Body,
                MinigameUiTheme.Accent, TextAlignmentOptions.Right);
            UiKit.Anchor(directionValue.rectTransform, new Vector2(.58f, .70f), new Vector2(.95f, .95f));
            SliderHandle direction = UiKit.Slider(aim.transform, "DirectionSlider");
            UiKit.Anchor((RectTransform)direction.Slider.transform, new Vector2(.06f, .34f), new Vector2(.94f, .70f));
            direction.Slider.minValue = -1f;
            direction.Slider.maxValue = 1f;
            direction.Slider.value = .55f;
            TMP_Text hint = UiKit.Label(aim.transform, "DirectionHint", "Kéo chọn hướng · Giữ SÚT để xem đường bay",
                MinigameUiTheme.Caption, MinigameUiTheme.TextPrimary);
            UiKit.Anchor(hint.rectTransform, new Vector2(.04f, .04f), new Vector2(.96f, .34f));
            UiKit.FitLabel(hint, MinigameUiTheme.Caption);

            // Bottom right: power and shoot.
            KitBar power = UiKit.Bar(root, "PowerBar", label: true);
            UiKit.Anchor((RectTransform)power.transform, new Vector2(.80f, .16f), new Vector2(.975f, .20f));
            power.Label.name = "PowerPercent";
            TMP_Text warning = UiKit.Label(root, "OverPowerWarning", "DỄ VƯỢT XÀ", MinigameUiTheme.Caption,
                MinigameUiTheme.Energy, TextAlignmentOptions.Center, outline: true);
            UiKit.Anchor(warning.rectTransform, new Vector2(.79f, .205f), new Vector2(.985f, .25f));
            warning.gameObject.SetActive(false);
            ButtonHandle shoot = UiKit.Button(root, "SHOOT", "GIỮ ĐỂ SÚT", ButtonVariant.Primary);
            UiKit.Anchor((RectTransform)shoot.Button.transform, new Vector2(.80f, .035f), new Vector2(.975f, .145f));
            var hold = shoot.Button.gameObject.AddComponent<FootballHoldButton>();
            TMP_Text feedback = UiKit.Label(root, "ShotFeedback", string.Empty, MinigameUiTheme.Headline,
                MinigameUiTheme.TextPrimary, TextAlignmentOptions.Center, outline: true);
            UiKit.Anchor(feedback.rectTransform, new Vector2(.20f, .69f), new Vector2(.80f, .79f));

            // Start card: compact and raised into the band between the score chip and the kicker,
            // so the kicker (top at ~48% of the view height) stays visible below it.
            Image scrim = UiKit.Shape(root, "StartScrim", 0f, MinigameUiTheme.Scrim);
            scrim.raycastTarget = true;
            UiKit.Stretch(scrim.rectTransform);
            Image start = UiKit.Panel(root, "StartPanel", alpha: 1f);
            start.raycastTarget = true;
            UiKit.Place(start.rectTransform, centre, centre, new Vector2(0f, StartCardY), StartCardSize);
            TMP_Text startTitle = UiKit.Label(start.transform, "StartTitle", "LOẠT SÚT LUÂN LƯU", MinigameUiTheme.Title,
                MinigameUiTheme.TextPrimary);
            UiKit.Anchor(startTitle.rectTransform, new Vector2(.06f, .72f), new Vector2(.94f, .94f));
            TMP_Text instructions = UiKit.Label(start.transform, "StartInstructions",
                "Kéo thanh chọn hướng. Giữ SÚT để xem đường bay, thả để đá.\nGhi ít nhất 3 bàn sau 5 lượt.",
                MinigameUiTheme.Body, MinigameUiTheme.TextPrimary);
            UiKit.Anchor(instructions.rectTransform, new Vector2(.08f, .36f), new Vector2(.92f, .72f));
            UiKit.FitLabel(instructions, MinigameUiTheme.Body);
            instructions.textWrappingMode = TextWrappingModes.Normal;
            Button startButton = StartButton(start.transform, "StartButton", "BẮT ĐẦU", ButtonVariant.Primary, .29f, .71f, .07f, .30f);

            // The shared pause button sits in the safe area's top-right corner, as in Volleyball.
            var pause = UnityEngine.Object.FindFirstObjectByType<PausePanel>(FindObjectsInactive.Include);
            pause.transform.SetParent(safe, false);
            UiKit.PlacePause((RectTransform)pause.transform);

            hud.Configure(direction.Slider, hold, power, warning.gameObject, score.Label, remaining.Label, markers,
                start.gameObject, startButton, null, null, null, directionValue, feedback, scrim.gameObject);
            hud.ShowStart(FootballDifficulty.Normal);

            var result = UnityEngine.Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
            var controller = UnityEngine.Object.FindFirstObjectByType<FootballController>();
            var presentation = GameObject.Find("FootballWorld").GetComponent<FootballPresentation>();
            var config = AssetDatabase.LoadAssetAtPath<FootballDifficultyConfig>(ConfigPath);
            controller.Configure(config, controller.GetComponent<FootballInputBridge>(), presentation, hud, result);

            foreach (UnityEngine.Object dirty in new UnityEngine.Object[] { hud, controller, hudRoot, pause })
                EditorUtility.SetDirty(dirty);
        }

        static Button StartButton(Transform parent, string name, string label, ButtonVariant variant,
            float minX, float maxX, float minY, float maxY)
        {
            ButtonHandle handle = UiKit.Button(parent, name, label, variant);
            UiKit.Anchor((RectTransform)handle.Button.transform, new Vector2(minX, minY), new Vector2(maxX, maxY));
            return handle.Button;
        }

        static void ConfigureSubjectAsset()
        {
            var subject = AssetDatabase.LoadAssetAtPath<ScriptableObject>(SubjectPath);
            if (!subject) return;
            var serialized = new SerializedObject(subject);
            serialized.FindProperty("goalText").stringValue = "Ghi ít nhất 3 bàn sau 5 lượt sút.";
            serialized.FindProperty("timeLimit").floatValue = 0f;
            serialized.FindProperty("unlocked").boolValue = true;
            serialized.FindProperty("comingSoon").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(subject);
        }

        static void EnsureInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == ScenePath)
                {
                    if (!scenes[i].enabled) { scenes[i].enabled = true; EditorBuildSettings.scenes = scenes; }
                    return;
                }
            }
            Array.Resize(ref scenes, scenes.Length + 1);
            scenes[scenes.Length - 1] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = scenes;
        }

        static void EnsureEventSystem(Scene scene)
        {
            var eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (!eventSystem || eventSystem.gameObject.scene != scene)
            {
                var root = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                SceneManager.MoveGameObjectToScene(root, scene);
                return;
            }
            foreach (var module in eventSystem.GetComponents<BaseInputModule>())
                if (!(module is InputSystemUIInputModule)) UnityEngine.Object.DestroyImmediate(module);
            var inputModule = UiKit.GetOrAdd<InputSystemUIInputModule>(eventSystem.gameObject);
            inputModule.AssignDefaultActions();
        }

        static void AddUrpCameraData(GameObject cameraRoot)
        {
            const string typeName = "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime";
            var type = Type.GetType(typeName);
            if (type != null && cameraRoot.GetComponent(type) == null)
                cameraRoot.AddComponent(type);
        }

        static SpriteRenderer Renderer(Transform parent, string name, Sprite sprite, Vector3 position,
            int sortingOrder, Vector2 worldSize)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.position = position;
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            renderer.drawMode = SpriteDrawMode.Simple;
            if (sprite)
            {
                var size = sprite.bounds.size;
                child.transform.localScale = new Vector3(worldSize.x / size.x, worldSize.y / size.y, 1f);
            }
            return renderer;
        }

        static Sprite LoadSprite(string path)
        {
            string full = ArtRoot + path;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(full);
            if (!sprite) throw new InvalidOperationException("Football sprite is missing or not imported as Sprite: " + full);
            return sprite;
        }
    }
}
#endif
