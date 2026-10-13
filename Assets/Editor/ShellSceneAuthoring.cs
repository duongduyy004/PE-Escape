#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using KMA.Gameplay.Shell;
using KMA.Gameplay.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KMA.EditorTools
{
    public static class ShellSceneAuthoring
    {
        const string Scenes = "Assets/_Project/Scenes/";
        const string Sprites = "Assets/_Project/Art/UI/GeneratedSceneSprites";

        [MenuItem("KMA/Presentation/Author Shell Scenes")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Author("Bootstrap", scene =>
            {
                var presenter = Require<SplashScreenPresenter>(scene);
                var view = SplashPresentationView.Build(presenter.transform);
                if (view == null) throw new InvalidOperationException("Bootstrap has no splash Canvas.");
                KeepOnly(presenter.transform, "Illustration", "SplashNavyGradient", "SplashSafeArea");
                var properties = new SerializedObject(presenter);
                properties.FindProperty("loadingBar").objectReferenceValue = view.LoadingBar;
                properties.FindProperty("statusText").objectReferenceValue = view.Status;
                properties.FindProperty("progressText").objectReferenceValue = view.Percent;
                properties.ApplyModifiedPropertiesWithoutUndo();
                view.GetComponent<CanvasGroup>().alpha = 1f;
            });
            AuthorMenu();
            ApplyMap();
            Author("GameOver", scene =>
            {
                var screen = Require<GameOverScreen>(scene);
                GameOverPresentationBuilder.Rebuild(screen, null);
                KeepOnly(screen.transform, "GameOverVeil", "GameOverLayout", "DialogueCard");
            });
            AssetDatabase.SaveAssets();
            Debug.Log("[KMA] Authored Bootstrap, Menu, Map, and GameOver scene UI.");
        }

        const string MenuBackdropPath = "Assets/_Project/Art/UI/MenuKeyArtBackdrop.png";
        const string MenuPanelPath = "Assets/_Project/Art/UI/MenuKeyArtPanel.png";
        // Pixel box of the panel overlay on the 1672 x 941 canvas (menu asset pack layout.json).
        const float ArtWidth = 1672f, ArtHeight = 941f;
        const float PanelLeft = 48f, PanelTop = 43f, PanelRight = 650f, PanelBottom = 890f;

        /// The menu is a full-bleed scenery backdrop plus the painted panel (title and buttons) as its own
        /// sprite. The panel is fitted inside the screen, so it stays compact on wide phones while the
        /// scenery still covers every pixel; the scene lays invisible hit areas over the panel's buttons.
        [MenuItem("KMA/Presentation/Author Menu Scene")]
        public static void AuthorMenu()
        {
            Author("Menu", scene =>
            {
                var screen = Require<MainMenuScreen>(scene);
                var root = screen.transform.root;
                var art = root.Find("HomeIllustration");
                RemoveChild(root, "HomeTint"); // the key art is meant to be seen at full brightness
                if (art == null) throw new InvalidOperationException("Menu has no HomeIllustration.");
                art.GetComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MenuBackdropPath);
                var fitter = art.GetComponent<AspectRatioFitter>() ?? art.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = ArtWidth / ArtHeight;

                RemoveChild(root, "HomePanelFrame");
                var frame = new GameObject("HomePanelFrame", typeof(RectTransform), typeof(AspectRatioFitter))
                    .GetComponent<RectTransform>();
                frame.SetParent(art.parent, false);
                frame.SetSiblingIndex(art.GetSiblingIndex() + 1);
                frame.anchorMin = Vector2.zero;
                frame.anchorMax = Vector2.one;
                frame.offsetMin = frame.offsetMax = Vector2.zero;
                var frameFit = frame.GetComponent<AspectRatioFitter>();
                frameFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                frameFit.aspectRatio = ArtWidth / ArtHeight;

                var panel = new GameObject("HomePanelArt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var panelRect = panel.GetComponent<RectTransform>();
                panelRect.SetParent(frame, false);
                panelRect.anchorMin = new Vector2(PanelLeft / ArtWidth, 1f - PanelBottom / ArtHeight);
                panelRect.anchorMax = new Vector2(PanelRight / ArtWidth, 1f - PanelTop / ArtHeight);
                panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
                var panelImage = panel.GetComponent<Image>();
                panelImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MenuPanelPath);
                panelImage.raycastTarget = false;

                HomePresentationBuilder.Rebuild(screen);
                RemoveChild(screen.transform, "HomeLogo");
                RemoveChild(screen.transform, "HomeTitle");
            });
        }

        [MenuItem("KMA/Presentation/Author Map Lesson Journey")]
        public static void ApplyMap()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Author("Map", scene =>
            {
                var screen = Require<MapScreen>(scene);
                var controller = Require<S5ShellSceneController>(scene);
                RemoveChild(screen.transform, "S5MapPresentation");
                MapPresentationBuilder.Build(screen, null, controller.MainMenuBackground);
                KeepOnly(screen.transform, "S5MapPresentation");
            });
            EditorUtility.SetDirty(UITheme.Shared);
            AssetDatabase.SaveAssets();
        }

        public static void Validate()
        {
            ValidateScene("Bootstrap", scene =>
            {
                var presenter = Require<SplashScreenPresenter>(scene);
                Check(presenter.transform.Find("Backdrop") == null, "Legacy Bootstrap backdrop remains");
                var view = SplashPresentationView.Build(presenter.transform);
                Check(view != null && view.LoadingBar != null && view.Status != null && view.Percent != null,
                    "Splash view is missing its runtime controls");
            });
            ValidateScene("Menu", scene =>
            {
                var screen = Require<MainMenuScreen>(scene);
                Check(screen.transform.Find("HomeLogo") == null, "Legacy Home logo remains");
                HomePresentationBuilder.Build(screen);
                var buttons = screen.transform.Find("HomeMenuLayout")?.GetComponentsInChildren<Button>(true);
                Check(buttons != null && buttons.Length == 4, "Menu needs four authored buttons");
            });
            ValidateScene("Map", scene =>
            {
                var screen = Require<MapScreen>(scene);
                Check(screen.transform.Find("SprintButton") == null, "Legacy Map buttons remain");
                var grid = screen.transform.Find("S5MapPresentation/Content/SelectionGrid");
                Check(grid != null && grid.GetComponent<GridLayoutGroup>() != null
                    && grid.GetComponent<ResponsiveGridLayout>() != null,
                    "Authored Map grid is missing layout components");
                MapPresentationBuilder.Build(screen, null);
                Check(screen.Nodes.Length == 4, "Map needs the three authored subject nodes and the final exam stop");
                Check(screen.Nodes.All(node => node.transform.Find("Badge") != null &&
                    node.transform.Find("LabelGroup/MetaPill/Stars") != null),
                    "Map stops must be the circular journey stops");
                Check(screen.Nodes[0].IsCurrent && !screen.Nodes[1].IsCurrent && !screen.Nodes[2].IsCurrent,
                    "A fresh game must mark only Sprint as the current stop");
                Check(grid.Find("PathTrack1") != null && grid.Find("PathTrack2") != null,
                    "Map road segments are missing");
                Check(screen.Nodes[0].SubjectId == KMA.Gameplay.SubjectId.Sprint &&
                    screen.Nodes[0].IsInteractable && !screen.Nodes[1].IsInteractable &&
                    !screen.Nodes[2].IsInteractable,
                    "Map must unlock only Sprint before its exam is passed");
                Check(screen.LessonList != null && screen.LessonList.CurrentChallengeId == "sprint_learn" &&
                    screen.LessonList.LessonIds.Count == 3,
                    "Map must show the three Sprint challenges at the current checkpoint");
                int selected = 0;
                screen.SubjectRequested += _ => selected++;
                foreach (var node in screen.Nodes)
                    node.GetComponent<Button>()?.onClick.Invoke();
                Check(selected == 4, "Authored Map buttons were not rebound");
                string challenge = null;
                KMA.Gameplay.ChallengeAttemptMode? mode = null;
                screen.ChallengeRequested += (id, attemptMode) => { challenge = id; mode = attemptMode; };
                screen.LessonList.transform.Find("DetailCard/PlayButton")?.GetComponent<Button>()?.onClick.Invoke();
                Check(challenge == "sprint_learn" && mode == KMA.Gameplay.ChallengeAttemptMode.Journey,
                    "Map play button must request the current challenge in Journey mode");
            });
            ValidateScene("GameOver", scene =>
            {
                var screen = Require<GameOverScreen>(scene);
                GameOverPresentationBuilder.Build(screen, null);
                var buttons = screen.transform.Find("GameOverLayout")?.GetComponentsInChildren<Button>(true);
                Check(buttons != null && buttons.Length == 3, "GameOver needs three authored buttons");
                int actions = 0;
                screen.RetryRequested += () => actions++;
                screen.NewGameRequested += () => actions++;
                screen.MenuRequested += () => actions++;
                var controller = Require<S5ShellSceneController>(scene);
                typeof(S5ShellSceneController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(controller, null);
                foreach (var button in buttons) button.onClick.Invoke();
                Check(actions == 3, "Authored GameOver buttons were not rebound");
            });
            Debug.Log("[KMA] Shell scene assets validated after reopen.");
        }

        static void ValidateScene(string name, Action<Scene> check)
        {
            var scene = EditorSceneManager.OpenScene(Scenes + name + ".unity", OpenSceneMode.Single);
            int before = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Length);
            check(scene);
            int after = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Length);
            Check(before == after, name + " builder duplicated UI after reopen");
            foreach (var root in scene.GetRootGameObjects())
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                Check(image.sprite == null || AssetDatabase.Contains(image.sprite),
                    name + "/" + image.name + " has a transient sprite");
            Debug.Log($"[KMA] Validated {name}: {after} objects, no transient sprites.");
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static void Author(string name, Action<Scene> build)
        {
            var scene = EditorSceneManager.OpenScene(Scenes + name + ".unity", OpenSceneMode.Single);
            build(scene);
            int baked = BakeSprites(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save " + scene.path);
            Debug.Log($"[KMA] Saved {name}; baked {baked} generated sprite references.");
        }

        static T Require<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var result = root.GetComponentInChildren<T>(true);
                if (result != null) return result;
            }
            throw new InvalidOperationException($"{scene.path} is missing {typeof(T).Name}.");
        }

        static void RemoveChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        static void KeepOnly(Transform parent, params string[] names)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (!names.Contains(child.name)) UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            foreach (var name in names)
                if (parent.Find(name) == null)
                    throw new InvalidOperationException($"Missing authored UI {name} under {parent.name}.");
        }

        static int BakeSprites(Scene scene)
        {
            Directory.CreateDirectory(Sprites);
            int count = 0;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                var sprite = image.sprite;
                if (sprite == null || AssetDatabase.Contains(sprite)) continue;
                var rect = sprite.rect;
                var source = sprite.texture;
                var texture = new Texture2D(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height),
                    TextureFormat.RGBA32, false);
                texture.SetPixels(source.GetPixels(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y),
                    texture.width, texture.height));
                texture.Apply();
                byte[] png = texture.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(texture);
                string key;
                using (var sha = SHA256.Create())
                {
                    var geometry = System.Text.Encoding.UTF8.GetBytes(
                        $"{sprite.pixelsPerUnit}:{sprite.pivot}:{sprite.border}");
                    var payload = new byte[png.Length + geometry.Length];
                    Buffer.BlockCopy(png, 0, payload, 0, png.Length);
                    Buffer.BlockCopy(geometry, 0, payload, png.Length, geometry.Length);
                    key = BitConverter.ToString(sha.ComputeHash(payload)).Replace("-", "").Substring(0, 20);
                }
                var path = Sprites + "/" + key + ".png";
                if (!File.Exists(path)) File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = sprite.pixelsPerUnit;
                importer.spritePivot = new Vector2(sprite.pivot.x / rect.width, sprite.pivot.y / rect.height);
                importer.spriteBorder = sprite.border;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
                image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path)
                    ?? throw new InvalidOperationException("Could not import generated sprite " + path);
                count++;
            }
            return count;
        }
    }
}
#endif
