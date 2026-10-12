#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using KMA.Gameplay.UI;
using KMA.Gameplay.Volleyball;
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
    public static class VolleyballSceneConfigurator
    {
        public const string ScenePath = "Assets/_Project/Scenes/MG_Volleyball.unity";
        public const string OpponentCharacter = "FemaleAdventurer";

        /// <summary>Pose names per action, in VolleyAthleteView.Configure order.</summary>
        sealed class AthletePoses
        {
            public string[] Idle, Run, Receive, Smash, Block, Dive;
            public string[] Serve, ServeToss, Dig, Set, Recover, JumpLoad, SpikeWindup, SpikeContact, SpikeFollow, Land;
        }

        /// The hero has dedicated volleyball art; the opponent keeps the shared poses.
        static readonly AthletePoses HeroVolleyPoses = new AthletePoses
        {
            Idle = new[] { "volleyReady" },
            Run = new[] { "volleyShuffleRight", "volleyShuffleLeft" },
            Receive = new[] { "volleyReceive" },
            Smash = new[] { "volleySpikeWindup", "volleySpikeContact" },
            Block = new[] { "volleyJumpLoad", "volleySet" },
            Serve = new[] { "volleySpikeContact", "volleySpikeFollow" },
            ServeToss = new[] { "volleySet" },
            Dig = new[] { "volleyDig" },
            Set = new[] { "volleySet" },
            Recover = new[] { "volleyRecover" },
            JumpLoad = new[] { "volleyJumpLoad" },
            SpikeWindup = new[] { "volleySpikeWindup" },
            SpikeContact = new[] { "volleySpikeContact" },
            SpikeFollow = new[] { "volleySpikeFollow" },
            Land = new[] { "volleyLand" },
            Dive = new[] { "fallForwardRight", "fallSitRight" }
        };

        static readonly AthletePoses SharedVolleyPoses = new AthletePoses
        {
            Idle = new[] { "idle" },
            Run = new[] { "run0", "run1", "run2", "run1" },
            Receive = new[] { "duck", "hold" },
            Smash = new[] { "jump", "attack1" },
            Block = new[] { "jump", "cheer1" },
            Dive = new[] { "fall", "slide" }
        };
        // Character poses are 1.28 units tall; scaled up so the athletes read clearly on a phone.
        public const float AthleteScale = 2.3f;
        // Marker centre, in world units above the feet; its lowest point clears the tallest (spike contact) pose.
        const float MarkerWorldHeight = 3.25f;
        const string EnvironmentDir = "Assets/_Project/Art/Environments/Volleyball";
        const string PixelPath = EnvironmentDir + "/Pixel.png";
        const string HudRootName = "S2_HUD_Minigame";

        // Tuned by eye in the visual QA task; see the plan's Task 13.
        const float HorizonWorldY = 4.97f;
        const int HudSortingOrder = 500;
        const float NetWidth = .5f;
        public const float JoystickScale = 1.35f;
        static readonly Color GroundColor = new Color32(0xcf, 0xc8, 0xb8, 0xff); // schoolyard concrete
        static readonly Color CourtColor = new Color32(0x3f, 0x8f, 0xcf, 0xff);  // painted court
        static readonly Color LineColor = new Color32(0xff, 0xfb, 0xea, 0xff);
        const float SkylineHeight = 2.1f;
        static readonly Color ShadowTint = new Color(1f, 1f, 1f, .8f);
        static readonly Color AimTint = new Color(1f, .25f, .2f, .85f);
        static readonly Color BallInk = new Color32(10, 40, 61, 255);

        readonly struct TextureSpec
        {
            public readonly string Path;
            public readonly float PixelsPerUnit;

            public TextureSpec(string path, float pixelsPerUnit)
            {
                Path = path;
                PixelsPerUnit = pixelsPerUnit;
            }
        }

        // World size 0.75 x 0.75 (ball) and about 0.96 x 0.42 (shadow), grown with the athletes.
        const string BallPath = EnvironmentDir + "/Ball.png";
        const string ShadowPath = EnvironmentDir + "/Shadow.png";
        const int BallPixels = 128;
        const float BallPixelsPerUnit = BallPixels / .75f;
        const int ShadowWidth = 128, ShadowHeight = 56;
        const float ShadowPixelsPerUnit = ShadowWidth / .96f;
        const float AttackLineMetres = 3f;
        static readonly Vector2 Centre = new Vector2(.5f, .5f);

        static readonly TextureSpec[] Textures =
        {
            new TextureSpec(BallPath, BallPixelsPerUnit),
            new TextureSpec(ShadowPath, ShadowPixelsPerUnit),
            new TextureSpec(PixelPath, 4f)
        };

        [MenuItem("KMA/Volleyball/Build Scene")]
        public static void BuildScene()
        {
            ImportArt();
            BuildWorld();
            MinigameUIAssembler.AssembleScenePath(ScenePath);
            AddControlsAndHud();
            EnsureInBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[KMA] MG_Volleyball built.");
        }

        public static void ImportArt()
        {
            Directory.CreateDirectory(EnvironmentDir);
            EnsureGeneratedTexture(PixelPath, 4, 4, (u, v) => Color.white);
            EnsureGeneratedTexture(BallPath, BallPixels, BallPixels, BallColor);
            EnsureGeneratedTexture(ShadowPath, ShadowWidth, ShadowHeight, ShadowColor);
            foreach (TextureSpec spec in Textures)
                ConfigureTexture(spec);
            CharacterArt.ImportAll();
        }

        static readonly Color BallCream = new Color32(252, 246, 230, 255);
        static readonly Color BallSeam = new Color32(226, 150, 40, 255);

        // A flat volleyball: cream panels, three curved amber seams and an ink outline. u, v in [-1, 1].
        static Color BallColor(float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            if (r > 1f)
                return Color.clear;
            if (r > .88f)
                return BallInk;
            for (int k = 0; k < 3; k++)
            {
                float angle = (90f + k * 120f) * Mathf.Deg2Rad;
                float centreU = Mathf.Cos(angle) * 1.25f, centreV = Mathf.Sin(angle) * 1.25f;
                float distance = Mathf.Sqrt((u - centreU) * (u - centreU) + (v - centreV) * (v - centreV));
                if (Mathf.Abs(distance - 1f) < .06f)
                    return BallSeam;
            }

            return BallCream;
        }

        // A flat mid-grey ellipse; the scene tints it for the shadow and the two markers.
        static Color ShadowColor(float u, float v) =>
            u * u + v * v <= 1f ? new Color32(150, 150, 150, 255) : Color.clear;

        /// <summary>Writes a supersampled flat-colour PNG once; existing files are left alone.</summary>
        static void EnsureGeneratedTexture(string path, int width, int height, Func<float, float, Color> shade)
        {
            if (File.Exists(path))
                return;

            const int samples = 3;
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Color sum = Color.clear;
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    float u = ((x + (sx + .5f) / samples) / width) * 2f - 1f;
                    float v = ((y + (sy + .5f) / samples) / height) * 2f - 1f;
                    Color c = shade(u, v);
                    sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                }

                sum /= samples * samples;
                pixels[y * width + x] = sum.a > 0f ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a) : Color.clear;
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }

        static void ConfigureTexture(TextureSpec spec)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(spec.Path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = spec.PixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = Centre;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }


        static Sprite Single(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path) ??
            throw new InvalidOperationException($"[KMA] {path} did not import as a sprite.");

        static void BuildWorld()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Sprite pixel = Single(PixelPath);
            Sprite shadowSprite = Single(ShadowPath);

            // CourtSpace.ToWorld scales the y (court-width) axis by PixelsPerMetreY /
            // BackgroundPixelsPerUnit (not 1:1), so the court's on-screen height isn't simply
            // HalfWidth * 2 world units - measure it via ToWorld so the band reaches both sidelines.
            float netWorldHeight = CourtSpace.ToWorld(new Vector2(0f, CourtSpace.HalfWidth), 0f).y -
                                    CourtSpace.ToWorld(new Vector2(0f, -CourtSpace.HalfWidth), 0f).y;
            Sprite netSprite = CampusBackdropAuthoring.Load("VolleyNet");
            SpriteRenderer net = Renderer("Net", netSprite, CourtSpace.ToWorld(Vector2.zero, 0f), VolleyAthleteView.NetSortingOrder);
            net.transform.localScale = new Vector3(NetWidth / netSprite.bounds.size.x, netWorldHeight / netSprite.bounds.size.y, 1f);
            float courtX = CourtSpace.ToWorld(new Vector2(CourtSpace.HalfLength, 0f), 0f).x;
            float courtY = CourtSpace.ToWorld(new Vector2(0f, CourtSpace.HalfWidth), 0f).y;
            Quad("Court", pixel, CourtColor, Vector3.zero, new Vector2(courtX * 2f, courtY * 2f), -20);
            float attackX = CourtSpace.ToWorld(new Vector2(AttackLineMetres, 0f), 0f).x;
            for (int side = -1; side <= 1; side += 2)
            {
                Quad(side < 0 ? "NearAttackLine" : "FarAttackLine", pixel, LineColor,
                    new Vector3(side * attackX, 0f, 0f), new Vector2(.09f, courtY * 2f), -9);
                Quad(side < 0 ? "NearBaseline" : "FarBaseline", pixel, LineColor,
                    new Vector3(side * courtX, 0f, 0f), new Vector2(.09f, courtY * 2f), -9);
                Quad(side < 0 ? "NearSideline" : "FarSideline", pixel, LineColor,
                    new Vector3(0f, side * courtY, 0f), new Vector2(courtX * 2f, .09f), -9);
            }

            VolleyAthleteView player = Athlete("Player", false, CharacterArt.Hero, HeroVolleyPoses);
            VolleyAthleteView opponent = Athlete("Opponent", true, OpponentCharacter, SharedVolleyPoses);
            AddAthleteMarker(player.transform, pixel, "Player", MinigameUiTheme.Player);
            AddAthleteMarker(opponent.transform, pixel, "Enemy", MinigameUiTheme.Energy);

            SpriteRenderer ball = Renderer("Ball", Single(BallPath), Vector3.zero, VolleyBallView.BallSortingOrder);
            SpriteRenderer shadow = Renderer("BallShadow", shadowSprite, Vector3.zero, VolleyBallView.ShadowSortingOrder);
            shadow.color = ShadowTint;
            SpriteRenderer contact = Renderer("ContactMarker", shadowSprite, Vector3.zero, VolleyBallView.MarkerSortingOrder);
            contact.color = VolleyBallView.MarkerOutOfReachColor;
            SpriteRenderer aim = Renderer("AimMarker", shadowSprite, Vector3.zero, VolleyBallView.MarkerSortingOrder);
            aim.color = AimTint;
            SpriteRenderer playerAim = Renderer("PlayerAimMarker", shadowSprite, Vector3.zero, VolleyBallView.MarkerSortingOrder);
            playerAim.color = VolleyBallView.PlayerAimColor;
            var ballView = new GameObject("BallView").AddComponent<VolleyBallView>();
            ballView.Configure(ball, shadow, contact, aim, playerAim);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            var controllerObject = new GameObject("VolleyballController");
            var input = controllerObject.AddComponent<VolleyballInputBridge>();
            var controller = controllerObject.AddComponent<VolleyballController>();
            controller.Configure(player, opponent, ballView, input, null);
            StartLecturerAuthoring.AddToVolleyball(scene);
            var dialogue=DialoguePresentationAuthoring.Configure(controller.transform,DialogueHudSlot.Volleyball);
            var adapter=controllerObject.AddComponent<VolleyballDialogueAdapter>();
            DialoguePresentationAuthoring.WireOpening(controller,dialogue,adapter);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static VolleyAthleteView Athlete(string name, bool mirror, string character, AthletePoses poses)
        {
            Sprite[] Poses(string[] names) => names == null || names.Length == 0 ? null : CharacterArt.Frames(character, names);
            Sprite[] idle = Poses(poses.Idle);
            SpriteRenderer body = Renderer(name, idle[0], Vector3.zero, 0);
            body.transform.localScale = Vector3.one * AthleteScale;
            var book = body.gameObject.AddComponent<SpriteFlipbook>();
            book.Configure(body, idle, true, 12f);
            var view = body.gameObject.AddComponent<VolleyAthleteView>();
            view.Configure(body, book, mirror, idle, Poses(poses.Run), Poses(poses.Receive), Poses(poses.Smash),
                Poses(poses.Block), Poses(poses.Dive));
            view.ConfigureSequences(Poses(poses.Serve), Poses(poses.ServeToss), Poses(poses.Dig), Poses(poses.Set),
                Poses(poses.Recover), Poses(poses.JumpLoad), Poses(poses.SpikeWindup), Poses(poses.SpikeContact),
                Poses(poses.SpikeFollow), Poses(poses.Land));
            return view;
        }

        static void AddAthleteMarker(Transform athlete, Sprite pixel, string name, Color accent)
        {
            SpriteRenderer edge = Renderer(name + "MarkerEdge", pixel, Vector3.zero, 290);
            edge.transform.SetParent(athlete, false);
            edge.transform.localPosition = new Vector3(0f, MarkerWorldHeight / AthleteScale, 0f);
            edge.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            edge.transform.localScale = new Vector3(.52f, .52f, 1f) / AthleteScale;
            edge.color = MinigameUiTheme.Surface;
            SpriteRenderer centre = Renderer(name + "Marker", pixel, Vector3.zero, 291);
            centre.transform.SetParent(athlete, false);
            centre.transform.localPosition = edge.transform.localPosition;
            centre.transform.localRotation = edge.transform.localRotation;
            centre.transform.localScale = new Vector3(.38f, .38f, 1f) / AthleteScale;
            centre.color = accent;
        }

        static SpriteRenderer Renderer(string name, Sprite sprite, Vector3 position, int order)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        static void Quad(string name, Sprite pixel, Color color, Vector3 position, Vector2 size, int order = -30)
        {
            SpriteRenderer renderer = Renderer(name, pixel, position, order);
            renderer.color = color;
            renderer.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        static void AddControlsAndHud()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject hudRoot = scene.GetRootGameObjects().Single(go => go.name == HudRootName);
            hudRoot.GetComponent<Canvas>().sortingOrder = HudSortingOrder;
            var parent = (RectTransform)(hudRoot.transform.Find("SafeAreaRoot") ?? hudRoot.transform);
            // The generic HUD belongs to other sports. Its timer, progress and status fields do
            // not represent volleyball, so hide only its prefab children in this scene.
            foreach (Transform child in parent)
                child.gameObject.SetActive(false);
            Camera camera = Camera.main; // the assembler's root GameCamera
            VolleyballCameraFraming.Apply(camera);
            // The ground override paints the schoolyard concrete instead of the default campus grass.
            CampusBackdropAuthoring.AddWorld(scene, camera, HorizonWorldY, SkylineHeight, true, GroundColor);

            RectTransform controls = UiRect("VolleyballControls", parent, Vector2.zero, Vector2.one);
            controls.SetAsFirstSibling();

            RectTransform area = UiRect("JoystickArea", controls, Vector2.zero, new Vector2(.38f, .52f));
            area.gameObject.AddComponent<Image>().color = Color.clear;
            JoystickHandle stick = UiKit.Joystick(area);
            // The volleyball stick is the main control on a phone: larger than the kit default.
            stick.Base.rectTransform.sizeDelta = Vector2.one * MinigameUiTheme.JoystickBase * JoystickScale;
            stick.Knob.rectTransform.sizeDelta = Vector2.one * MinigameUiTheme.JoystickKnob * JoystickScale;
            var joystick = area.gameObject.AddComponent<VirtualJoystick>();
            joystick.Configure(area, stick.Base.rectTransform, stick.Knob.rectTransform, 88f * JoystickScale,
                new Vector2(-120f, -100f));

            RectTransform buttonRect = UiRect("ActionButton", controls, Vector2.one, Vector2.one);
            buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(1f, 0f);
            buttonRect.sizeDelta = new Vector2(400f, 400f);
            buttonRect.anchoredPosition = new Vector2(-18f, 18f);
            Image actionHitImage = buttonRect.gameObject.AddComponent<Image>();
            actionHitImage.color = Color.clear;
            actionHitImage.raycastTarget = true;
            actionHitImage.canvasRenderer.cullTransparentMesh = false;
            var button = buttonRect.gameObject.AddComponent<ActionButton>();
            BuildTransparentIconButton(buttonRect, false, MinigameUiTheme.RoundButton * JoystickScale);

            // NHẢY sits just inside ĐÁNH and a little higher, its hit area clear of ĐÁNH's.
            RectTransform jumpRect = UiRect("JumpButton", controls, Vector2.one, Vector2.one);
            jumpRect.anchorMin = jumpRect.anchorMax = jumpRect.pivot = new Vector2(1f, 0f);
            // At least as large as the disc, so every visible pixel of NHẢY is tappable.
            jumpRect.sizeDelta = new Vector2(300f, 300f);
            jumpRect.anchoredPosition = new Vector2(-428f, 120f);
            Image jumpHitImage = jumpRect.gameObject.AddComponent<Image>();
            jumpHitImage.color = Color.clear;
            jumpHitImage.raycastTarget = true;
            jumpHitImage.canvasRenderer.cullTransparentMesh = false;
            var jump = jumpRect.gameObject.AddComponent<JumpButton>();
            BuildTransparentIconButton(jumpRect, true, MinigameUiTheme.RoundButton * JoystickScale);

            Vector2 centre = new Vector2(.5f, .5f);
            Image scoreboard = UiKit.Panel(controls, "VolleyballScoreboard");
            UiKit.Place(scoreboard.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -12f),
                new Vector2(650f, 84f));
            TMP_Text playerTitle = UiKit.Label(scoreboard.transform, "PlayerTitle", "BẠN", MinigameUiTheme.Body,
                MinigameUiTheme.Player);
            UiKit.Place(playerTitle.rectTransform, new Vector2(0f, .5f), centre, new Vector2(136f, 0f), new Vector2(225f, 65f));
            TMP_Text score = UiKit.Label(scoreboard.transform, "Score", VolleyballHud.ScoreText(0, 0),
                MinigameUiTheme.Title, MinigameUiTheme.TextPrimary);
            UiKit.Place(score.rectTransform, centre, centre, Vector2.zero, new Vector2(215f, 74f));
            TMP_Text enemyTitle = UiKit.Label(scoreboard.transform, "EnemyTitle", "ĐỐI THỦ", MinigameUiTheme.Body,
                MinigameUiTheme.Energy);
            UiKit.Place(enemyTitle.rectTransform, new Vector2(1f, .5f), centre, new Vector2(-136f, 0f), new Vector2(225f, 65f));

            // Kept for timed variants; the HUD hides it when the match has no clock (the journey matches end on points).
            // It sits on a chip in the top-left corner, clear of the scoreboard and the net.
            ChipHandle timerChip = UiKit.Chip(controls, "TimerChip", "2:00");
            TMP_Text timer = timerChip.Label;
            timer.name = "Timer";
            timer.color = MinigameUiTheme.Accent;
            timer.alignment = TextAlignmentOptions.Center;
            timer.raycastTarget = false;
            UiKit.Place(timerChip.Background.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -12f),
                new Vector2(170f, 64f));
            timer.enableAutoSizing = false;
            timer.fontSize = 36f;

            TMP_Text feedback = UiKit.Label(controls, "Feedback", string.Empty, MinigameUiTheme.Headline,
                MinigameUiTheme.Accent, TextAlignmentOptions.Center, outline: true);
            UiKit.Place(feedback.rectTransform, new Vector2(.5f, .75f), centre, Vector2.zero, new Vector2(680f, 100f));
            ChipHandle hint = UiKit.Chip(controls, "HintBanner", VolleyballHud.HintText);
            hint.Label.name = "Hint";
            UiKit.Place(hint.Background.rectTransform, new Vector2(.5f, .045f), centre, Vector2.zero, new Vector2(1000f, 66f));
            var hud = controls.gameObject.AddComponent<VolleyballHud>();
            hud.Configure(score, feedback, hint.Label);
            hud.ConfigureHintBackdrop(hint.Background.gameObject);
            hud.ConfigureTimer(timer, timerChip.Background.gameObject);

            var pause = Object.FindFirstObjectByType<PausePanel>();
            if (pause)
            {
                pause.transform.SetParent(parent, false);
                UiKit.PlacePause((RectTransform)pause.transform);
            }

            var controller = Object.FindFirstObjectByType<VolleyballController>();
            controller.Input.Configure(joystick, button, jump);
            controller.Configure(controller.PlayerView, controller.OpponentView, controller.BallView, controller.Input, hud);

            DialoguePresentationAuthoring.BindResult(controller,Object.FindFirstObjectByType<GameplayDialogueHost>());

            foreach (Object dirty in new Object[] { joystick, button, jump, hud, controller, controller.Input, hudRoot })
                EditorUtility.SetDirty(dirty);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void BuildTransparentIconButton(RectTransform hitArea, bool jump, float size)
        {
            UiKitAssets assets = UiKitAssets.Load();
            RectTransform visual = UiKit.Rect(hitArea, "RoundButton");
            UiKit.Place(visual, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero,
                Vector2.one * size);
            Image shadow = UiKit.Disc(visual, "IconShadow", true, MinigameUiTheme.TextOutline);
            UiKit.Place(shadow.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, Vector2.one * (size + 24f));
            Image ring = UiKit.Disc(visual, "IconRim", true, MinigameUiTheme.Accent);
            UiKit.Place(ring.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, Vector2.one * (size + 10f));
            Image plate = UiKit.Disc(visual, "IconPlate", false, new Color(.025f, .075f, .14f, .94f));
            UiKit.Place(plate.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, Vector2.one * (size - 4f));

            if (jump)
                BuildJumpGlyph(visual, assets.RoundRect24, size);
            else
                BuildHitGlyph(visual, assets.RoundRect24, size);

            visual.gameObject.AddComponent<CanvasGroup>();
            var feedback = hitArea.gameObject.AddComponent<KitPressFeedback>();
            feedback.Configure(plate, visual);
        }

        static void BuildHitGlyph(Transform parent, Sprite rounded, float size)
        {
            float u = size / 320f;
            Image ballEdge = UiKit.Disc(parent, "HitBallOutline", false, MinigameUiTheme.TextOutline);
            UiKit.Place(ballEdge.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(58f * u, 54f * u), Vector2.one * (76f * u));
            Image ball = UiKit.Disc(parent, "HitBall", false, MinigameUiTheme.TextPrimary);
            UiKit.Place(ball.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(58f * u, 54f * u), Vector2.one * (60f * u));
            IconPart(parent, "HitPalm", rounded, new Vector2(-20f * u, -18f * u), new Vector2(84f * u, 56f * u), -24f);
            IconPart(parent, "HitArm", rounded, new Vector2(26f * u, 13f * u), new Vector2(19f * u, 91f * u), -39f);
            IconPart(parent, "HitFinger", rounded, new Vector2(16f * u, 38f * u), new Vector2(16f * u, 58f * u), -18f);
        }

        static void BuildJumpGlyph(Transform parent, Sprite rounded, float size)
        {
            float u = size / 320f;
            Image head = UiKit.Disc(parent, "JumpHead", false, MinigameUiTheme.TextPrimary);
            UiKit.Place(head.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0f, 56f * u), Vector2.one * (43f * u));
            IconPart(parent, "JumpBody", rounded, new Vector2(0f, 4f * u), new Vector2(25f * u, 68f * u), 5f);
            IconPart(parent, "JumpLeftArm", rounded, new Vector2(-28f * u, 37f * u), new Vector2(15f * u, 58f * u), 34f);
            IconPart(parent, "JumpRightArm", rounded, new Vector2(29f * u, 42f * u), new Vector2(15f * u, 58f * u), -43f);
            IconPart(parent, "JumpLeftLeg", rounded, new Vector2(-18f * u, -46f * u), new Vector2(18f * u, 62f * u), -28f);
            IconPart(parent, "JumpRightLeg", rounded, new Vector2(20f * u, -46f * u), new Vector2(18f * u, 62f * u), 30f);
        }

        static void IconPart(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, float angle)
        {
            RectTransform rect = UiKit.Rect(parent, name);
            UiKit.Place(rect, new Vector2(.5f, .5f), new Vector2(.5f, .5f), position, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = MinigameUiTheme.TextPrimary;
            image.raycastTarget = false;
        }

        static RectTransform UiRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        static void EnsureInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath))
                return;

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
