#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using KMA.Gameplay.FrogJump;
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
    public static class FrogJumpSceneConfigurator
    {
        public const string ScenePath = "Assets/_Project/Scenes/MG_FrogJump.unity";
        const string BalanceDir = "Assets/_Project/ScriptableObjects/FrogJump";
        const string BalancePath = BalanceDir + "/FrogJumpBalance.asset";
        const string HudRootName = "S2_HUD_Minigame";
        const int HudSortingOrder = 500;
        public const float StartX = -7f, FinishX = 7f, GroundY = -2f;
        public const float HeroScale = 2.07f;
        public const float TrackHeight = 1.38f;
        const int Segments = 20;

        [MenuItem("KMA/Frog Jump/Build Scene")]
        public static void BuildScene()
        {
            CharacterArt.ImportAll();
            FrogJumpBalanceConfig balance = EnsureBalance();
            BuildWorld(balance);
            MinigameUIAssembler.AssembleScenePath(ScenePath);
            AddControls(balance);
            EnsureInBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[KMA] MG_FrogJump built.");
        }

        static FrogJumpBalanceConfig EnsureBalance()
        {
            if (!AssetDatabase.IsValidFolder(BalanceDir))
            {
                Directory.CreateDirectory(BalanceDir);
                AssetDatabase.Refresh();
            }

            var balance = AssetDatabase.LoadAssetAtPath<FrogJumpBalanceConfig>(BalancePath);
            if (balance == null)
            {
                balance = ScriptableObject.CreateInstance<FrogJumpBalanceConfig>();
                AssetDatabase.CreateAsset(balance, BalancePath);
            }

            return balance;
        }

        static void BuildWorld(FrogJumpBalanceConfig balance)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Sprite pixel = CampusBackdropAuthoring.Load("CampusPixel");
            Quad("Track", pixel, new Color32(0xe0, 0x60, 0x4a, 0xff), new Vector3(0f, GroundY - TrackHeight * .5f, 0f),
                new Vector2(FinishX - StartX + 2f, TrackHeight), -29);
            Quad("StartLine", pixel, new Color32(0xff, 0xfb, 0xea, 0xff), new Vector3(StartX, GroundY - TrackHeight * .5f, 0f), new Vector2(.12f, TrackHeight), -28);
            Quad("FinishLine", pixel, new Color32(0xff, 0xfb, 0xea, 0xff), new Vector3(FinishX, GroundY - TrackHeight * .5f, 0f), new Vector2(.2f, TrackHeight), -28);
            Quad("FinishFlag", pixel, MinigameUiTheme.Accent, new Vector3(FinishX + .3f, GroundY + .9f, 0f), new Vector2(.6f, .4f), -27);

            Sprite squat = CharacterArt.Load(CharacterArt.Hero, "frogReadyRight");
            Sprite jump = CharacterArt.Load(CharacterArt.Hero, "frogAirRight");
            Sprite fall = CharacterArt.Load(CharacterArt.Hero, "fallSitRight");
            var heroObject = new GameObject("Hero");
            heroObject.transform.position = new Vector3(StartX, GroundY, 0f);
            heroObject.transform.localScale = Vector3.one * HeroScale;
            var hero = heroObject.AddComponent<SpriteRenderer>();
            hero.sprite = squat;
            hero.sortingOrder = 10;

            var viewObject = new GameObject("FrogJumpView");
            var view = viewObject.AddComponent<FrogJumpView>();
            view.Configure(hero, squat, jump, fall, StartX, FinishX, GroundY);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            var controllerObject = new GameObject("FrogJumpController");
            var dialogueController=controllerObject.AddComponent<FrogJumpController>();
            var dialogueHost=DialoguePresentationAuthoring.Configure(controllerObject.transform,DialogueHudSlot.Frog);
            var dialogueAdapter=controllerObject.AddComponent<FrogJumpDialogueAdapter>();
            DialoguePresentationAuthoring.WireOpening(dialogueController,dialogueHost,dialogueAdapter);
            StartLecturerAuthoring.AddToFrogJump(scene);

            EditorSceneManager.SaveScene(scene, ScenePath);
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
            // CampusPixel is 8x8 px at 100 ppu (0.08 units), so scale by the sprite size to get `size` in world units.
            renderer.transform.localScale = new Vector3(size.x / pixel.bounds.size.x, size.y / pixel.bounds.size.y, 1f);
        }

        static void AddControls(FrogJumpBalanceConfig balance)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            CampusBackdropAuthoring.AddWorld(scene, Camera.main, GroundY, 3.2f, true);
            GameObject hudRoot = scene.GetRootGameObjects().Single(go => go.name == HudRootName);
            hudRoot.GetComponent<Canvas>().sortingOrder = HudSortingOrder;
            var parent = (RectTransform)(hudRoot.transform.Find("SafeAreaRoot") ?? hudRoot.transform);
            Vector2 centre = new Vector2(.5f, .5f);
            ConfigureSharedHud(hudRoot.transform);

            // Full-screen tap catcher behind every other HUD element.
            RectTransform tapRect = UiRect("TapArea", parent, Vector2.zero, Vector2.one);
            var tapImage = tapRect.gameObject.AddComponent<Image>();
            tapImage.color = Color.clear;
            tapImage.raycastTarget = true;
            var tap = tapRect.gameObject.AddComponent<FrogJumpTapArea>();
            tapRect.SetAsFirstSibling();

            Image barPanel = UiKit.Panel(parent, "PowerBar");
            barPanel.raycastTarget = false;
            UiKit.Place(barPanel.rectTransform, new Vector2(.5f, .1f), centre, Vector2.zero, new Vector2(900f, 72f));
            RectTransform track = UiRect("Track", barPanel.transform, Vector2.zero, Vector2.one);
            track.offsetMin = new Vector2(12f, 12f);
            track.offsetMax = new Vector2(-12f, -12f);
            RectTransform segments = UiRect("Segments", track, Vector2.zero, Vector2.one);
            segments.gameObject.AddComponent<StyleExempt>();
            for (int i = 0; i < Segments; i++)
            {
                RectTransform segment = UiRect("Segment" + i, segments, new Vector2(i / (float)Segments, 0f),
                    new Vector2((i + 1) / (float)Segments, 1f));
                var image = segment.gameObject.AddComponent<Image>();
                image.color = FrogJumpPowerBar.SegmentColor((i + .5f) / Segments, balance.Tuning);
                image.raycastTarget = false;
            }

            RectTransform needle = UiRect("Needle", track, new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            needle.sizeDelta = new Vector2(10f, 92f);
            var needleImage = needle.gameObject.AddComponent<Image>();
            needleImage.color = Color.white;
            needleImage.raycastTarget = false;
            needle.gameObject.AddComponent<StyleExempt>();
            var needleOutline = needle.gameObject.AddComponent<Outline>();
            needleOutline.effectColor = MinigameUiTheme.TextPrimary;
            needleOutline.effectDistance = new Vector2(2f, -2f);
            var bar = barPanel.gameObject.AddComponent<FrogJumpPowerBar>();
            bar.Configure(track, needle);

            TMP_Text feedback = UiKit.Label(parent, "Feedback", string.Empty, MinigameUiTheme.Headline,
                MinigameUiTheme.Accent, TextAlignmentOptions.Center, outline: true);
            feedback.raycastTarget = false;
            UiKit.Place(feedback.rectTransform, new Vector2(.5f, .62f), centre, Vector2.zero, new Vector2(680f, 100f));

            // Sits above the power bar; same hint chip the volleyball court uses.
            ChipHandle hint = UiKit.Chip(parent, "HintBanner",
                VietText.Fix("Chạm đúng lúc kim ở giữa vùng xanh. Chạm vùng đỏ là ngã!"));
            hint.Label.name = "Hint";
            hint.Label.raycastTarget = false;
            hint.Background.raycastTarget = false;
            UiKit.Place(hint.Background.rectTransform, new Vector2(.5f, .21f), centre, Vector2.zero, new Vector2(900f, 66f));

            var pause = Object.FindFirstObjectByType<PausePanel>();
            if (pause)
            {
                pause.transform.SetParent(parent, false);
                UiKit.PlacePause((RectTransform)pause.transform);
            }

            var controller = Object.FindFirstObjectByType<FrogJumpController>();
            var view = Object.FindFirstObjectByType<FrogJumpView>();
            controller.Configure(balance, view, bar, tap, feedback);

            DialoguePresentationAuthoring.BindResult(controller,Object.FindFirstObjectByType<GameplayDialogueHost>());

            foreach (Object dirty in new Object[] { tap, bar, controller, view, hudRoot })
                EditorUtility.SetDirty(dirty);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// The frog jump has no phase caption, stamina or score, and its hearts are never bound to the
        /// session (they would always show 5 full), so hide them too. Keep the timer and the distance
        /// progress bar, and put the "Còn x m" status under the progress bar.
        static void ConfigureSharedHud(Transform hud)
        {
            // Children of a prefab instance cannot be reparented, so detach the HUD from its prefab first.
            if (PrefabUtility.IsPartOfPrefabInstance(hud))
                PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(hud),
                    PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Transform Child(string name) => hud.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            foreach (string name in new[] { "Phase", "Stamina", "Score", "HeartBar" })
                Child(name)?.gameObject.SetActive(false);

            var safe = (RectTransform)(hud.Find("SafeAreaRoot") ?? hud);
            Transform existing = safe.Find("ProgressCard");
            Image card = existing != null ? existing.GetComponent<Image>()
                : UiKit.Panel(safe, "ProgressCard", MinigameUiTheme.RadiusPanel, MinigameUiTheme.SurfaceSoft);
            UiKit.Place(card.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                new Vector2(0f, -MinigameUiTheme.SpaceMd), new Vector2(560f, 150f));

            void Into(string name, Vector2 min, Vector2 max)
            {
                var rect = Child(name) as RectTransform;
                if (rect == null) return;
                rect.SetParent(card.transform, false);
                rect.anchorMin = min;
                rect.anchorMax = max;
                rect.pivot = new Vector2(.5f, .5f);
                rect.offsetMin = new Vector2(MinigameUiTheme.SpaceSm, 0f);
                rect.offsetMax = new Vector2(-MinigameUiTheme.SpaceSm, 0f);
            }
            Into("Timer", new Vector2(0f, .58f), new Vector2(1f, .98f));
            Into("Progress", new Vector2(0f, .38f), new Vector2(1f, .52f));
            Into("Status", new Vector2(0f, .04f), new Vector2(1f, .34f));
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
