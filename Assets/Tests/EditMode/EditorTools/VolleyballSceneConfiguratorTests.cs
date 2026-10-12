#if UNITY_EDITOR
using System;
using KMA.EditorTools;
using KMA.Gameplay.UI;
using KMA.Gameplay.Volleyball;
using KMA.UI.Kit;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KMA.Tests.EditorTools
{
    public sealed class VolleyballSceneConfiguratorTests
    {
        [TearDown]
        public void ReleaseBuiltSceneAfterTest() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test]
        public void CourtSitsInTheSchoolyardWithTheCampusBehindIt()
        {
            VolleyballSceneConfigurator.BuildScene();
            EditorSceneManager.OpenScene(VolleyballSceneConfigurator.ScenePath, OpenSceneMode.Single);
            Assert.That(GameObject.Find("Sand"), Is.Null, "no beach");
            var backdrop = Object.FindFirstObjectByType<KMA.Gameplay.CampusBackdropWorld>();
            Assert.That(backdrop, Is.Not.Null);
            // The backdrop's own child is named "Sky"; the old flat-colour quad must be gone.
            Assert.That(GameObject.Find("Sky").transform.IsChildOf(backdrop.transform), Is.True, "the shared backdrop owns the sky");
            Assert.That(backdrop.Ground.color, Is.EqualTo((Color)new Color32(0xcf, 0xc8, 0xb8, 0xff)));
            Assert.That(GameObject.Find("Court").GetComponent<SpriteRenderer>().color,
                Is.EqualTo((Color)new Color32(0x3f, 0x8f, 0xcf, 0xff)));
            var net = GameObject.Find("Net").GetComponent<SpriteRenderer>();
            Assert.That(AssetDatabase.GetAssetPath(net.sprite), Does.EndWith("/Campus/VolleyNet.png"));
            Assert.That(net.sortingOrder, Is.EqualTo(VolleyAthleteView.NetSortingOrder));
            Assert.That(backdrop.transform.parent, Is.Null);
            Assert.That(GameObject.Find("GameCamera"), Is.Not.Null, "the assembler's camera survives; the backdrop is bound to it");
        }

        [Test]
        public void FramingLeavesRoomForTheSkylineAboveTheFarSideline()
        {
            var cameraObject = new GameObject("Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            VolleyballCameraFraming.Apply(camera);
            Assert.That(camera.orthographicSize, Is.EqualTo(VolleyballCameraFraming.SizeFor(camera.aspect)).Within(1e-3f));
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(0f, VolleyballCameraFraming.Y, -10f)));
            float top = VolleyballCameraFraming.Y + VolleyballCameraFraming.Size;
            float bottom = VolleyballCameraFraming.Y - VolleyballCameraFraming.Size;
            Assert.That(top - 4.97f, Is.GreaterThanOrEqualTo(2.1f), "skyline band fits between horizon and view top");
            Assert.That(CourtSpace.ToWorld(new Vector2(0f, -CourtSpace.HalfWidth), 0f).y, Is.GreaterThan(bottom + .5f),
                "the near sideline stays on screen");
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void VolleyballNoLongerUsesBeachAudio()
        {
            AudioAssetConfigurator.Configure();
            var library = AssetDatabase.LoadAssetAtPath<KMA.Gameplay.Core.GameAudioLibrary>("Assets/_Project/Resources/GameAudioLibrary.asset");
            Assert.That(library.volleyball.name, Is.EqualTo("Monkeys Spinning Monkeys"));
        }

        [Test]
        public void HeroFacesAnUntintedPackOpponent()
        {
            VolleyballSceneConfigurator.BuildScene();
            VolleyballSceneConfigurator.BuildScene();
            EditorSceneManager.OpenScene(VolleyballSceneConfigurator.ScenePath, OpenSceneMode.Single);

            Assert.That(Object.FindObjectsByType<VolleyAthleteView>(FindObjectsSortMode.None), Has.Length.EqualTo(2));
            var controller = Object.FindFirstObjectByType<VolleyballController>();
            AssertDrawnFrom(controller.PlayerView, CharacterArt.Hero);
            AssertDrawnFrom(controller.OpponentView, VolleyballSceneConfigurator.OpponentCharacter);
            Assert.That(Mirrored(controller.PlayerView), Is.False, "The hero is never mirrored.");
            Assert.That(Mirrored(controller.OpponentView), Is.True, "The opponent faces the net.");
            foreach (var view in new[] { controller.PlayerView, controller.OpponentView })
            {
                Assert.That(view.GetComponent<SpriteRenderer>().color, Is.EqualTo(Color.white), view.name);
                Assert.That(view.transform.localScale, Is.EqualTo(Vector3.one * VolleyballSceneConfigurator.AthleteScale), view.name);
            }
        }

        [Test]
        public void EverySpriteIsSmoothFlatArtSoTheHeroDoesNotClashWithTheCourt()
        {
            VolleyballSceneConfigurator.BuildScene();
            EditorSceneManager.OpenScene(VolleyballSceneConfigurator.ScenePath, OpenSceneMode.Single);
            var allowedEnvironment = new[] { "Ball.png", "Shadow.png", "Pixel.png" };
            int checkedSprites = 0;
            foreach (var renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                Assert.That(renderer.sprite, Is.Not.Null, renderer.name);
                string path = AssetDatabase.GetAssetPath(renderer.sprite);
                Assert.That(renderer.sprite.texture.filterMode, Is.EqualTo(FilterMode.Bilinear),
                    renderer.name + " uses " + path + ": pixel-filtered art next to the smooth character athletes");
                if (path.Contains("/Environments/Volleyball/"))
                    Assert.That(allowedEnvironment, Does.Contain(System.IO.Path.GetFileName(path)),
                        renderer.name + " still draws the pixel-art BVA2 sheet " + path);
                checkedSprites++;
            }
            Assert.That(checkedSprites, Is.GreaterThan(10));
            Assert.That(System.IO.Directory.GetFiles("Assets/_Project/Art/Environments/Volleyball", "*.png"),
                Has.Length.EqualTo(allowedEnvironment.Length), "Unused BVA2 sheets must be deleted from the project.");
        }

        [Test]
        public void MarkersSitAboveTheTallestPoseOfEachAthlete()
        {
            VolleyballSceneConfigurator.BuildScene();
            EditorSceneManager.OpenScene(VolleyballSceneConfigurator.ScenePath, OpenSceneMode.Single);
            var controller = Object.FindFirstObjectByType<VolleyballController>();
            AssertMarkerClearsHead(controller.PlayerView, "PlayerMarkerEdge");
            AssertMarkerClearsHead(controller.OpponentView, "EnemyMarkerEdge");
        }

        static void AssertMarkerClearsHead(VolleyAthleteView view, string markerName)
        {
            float markerBottom = view.transform.Find(markerName).GetComponent<SpriteRenderer>().bounds.min.y
                                 - view.transform.position.y;
            float headTop = 0f;
            foreach (AthleteAction action in Enum.GetValues(typeof(AthleteAction)))
                foreach (Sprite sprite in view.FramesFor(action))
                    headTop = Mathf.Max(headTop, OpaqueTop(sprite) * VolleyballSceneConfigurator.AthleteScale);
            Assert.That(markerBottom, Is.GreaterThanOrEqualTo(headTop),
                $"{markerName} must not cover the head of {view.name} (tallest pose reaches {headTop:0.00}).");
        }


        [Test]
        public void JumpButtonAndPlayerAimMarkerAreWired()
        {
            VolleyballSceneConfigurator.BuildScene();
            EditorSceneManager.OpenScene(VolleyballSceneConfigurator.ScenePath, OpenSceneMode.Single);

            var jump = Object.FindFirstObjectByType<JumpButton>();
            Assert.That(jump, Is.Not.Null);
            Assert.That(jump.GetComponentInChildren<TMPro.TMP_Text>(), Is.Null);
            Assert.That(jump.transform.Find("Icon").GetComponent<Image>().sprite.name, Is.EqualTo("VolleyballJump"));
            Assert.That(jump.GetComponent<KitPressFeedback>(), Is.Not.Null);

            var controller = Object.FindFirstObjectByType<VolleyballController>();
            Assert.That(controller.Input.JumpButton, Is.SameAs(jump));
            var aim = GameObject.Find("PlayerAimMarker").GetComponent<SpriteRenderer>();
            Assert.That(controller.BallView.PlayerAimMarker, Is.SameAs(aim));
            Assert.That(aim.color, Is.EqualTo(VolleyBallView.PlayerAimColor));

            Rect WorldRect(RectTransform rect)
            {
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            }
            Rect jumpArea = WorldRect((RectTransform)jump.transform);
            Rect hitArea = WorldRect((RectTransform)Object.FindFirstObjectByType<ActionButton>().transform);
            Assert.That(jumpArea.Overlaps(hitArea), Is.False, "two thumbs must not share a hit area");
            Rect jumpDisc = WorldRect((RectTransform)jump.transform.Find("Icon"));
            Assert.That(jumpArea.Contains(jumpDisc.min) && jumpArea.Contains(jumpDisc.max), Is.True,
                "every visible pixel of NHẢY must be tappable");
            Assert.That(jumpDisc.Overlaps(hitArea), Is.False, "tapping NHẢY's edge must not hit ĐÁNH");
            Assert.That(jumpArea.center.x, Is.LessThan(hitArea.center.x), "NHẢY sits inside ĐÁNH");
        }

        [Test]
        public void ControlsScoreboardAndPauseAreDrawnWithTheUiKit()
        {
            VolleyballSceneConfigurator.BuildScene();
            EditorSceneManager.OpenScene(VolleyballSceneConfigurator.ScenePath, OpenSceneMode.Single);
            UiKitAssets assets = UiKitAssets.Load();

            foreach (Image image in Object.FindObjectsByType<Image>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (image.sprite != null)
                    Assert.That(image.sprite.name, Is.Not.EqualTo("Knob"), image.name + " still stretches the built-in knob");

            var scoreboard = GameObject.Find("VolleyballScoreboard").GetComponent<Image>();
            Assert.That(scoreboard.sprite, Is.SameAs(assets.RoundRect24));
            Assert.That(scoreboard.type, Is.EqualTo(Image.Type.Sliced), "a rounded panel, not a stretched ellipse");

            var action = Object.FindFirstObjectByType<ActionButton>();
            var feedback = action.GetComponent<KitPressFeedback>();
            Assert.That(feedback, Is.Not.Null);
            Assert.That(feedback.RestColor, Is.EqualTo(Color.white));

            Assert.That(GameObject.Find("JoystickBase").GetComponent<Image>().sprite, Is.SameAs(assets.Circle));
            Assert.That(GameObject.Find("PlayerTitle").GetComponent<TMPro.TMP_Text>().color, Is.EqualTo(MinigameUiTheme.Player));
            Assert.That(GameObject.Find("EnemyTitle").GetComponent<TMPro.TMP_Text>().color, Is.EqualTo(MinigameUiTheme.Energy));
            Assert.That(GameObject.Find("PlayerTitle").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("BẠN"));
            Assert.That(GameObject.Find("EnemyTitle").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("ĐỐI THỦ"));

            var pause = Object.FindFirstObjectByType<PausePanel>();
            Assert.That(pause.GetComponent<Image>().sprite, Is.SameAs(assets.RoundRect20));
            Assert.That(Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
        }
        // World height, above the feet pivot, of the highest opaque pixel of an imported character pose.
        static float OpaqueTop(Sprite sprite)
        {
            var texture = new Texture2D(2, 2);
            texture.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
            for (int y = texture.height - 1; y >= 0; y--)
                for (int x = 0; x < texture.width; x++)
                    if (texture.GetPixel(x, y).a > .5f)
                    {
                        float top = (y + 1) / sprite.pixelsPerUnit;
                        Object.DestroyImmediate(texture);
                        return top;
                    }
            Object.DestroyImmediate(texture);
            return 0f;
        }

        static bool Mirrored(VolleyAthleteView view) => new SerializedObject(view).FindProperty("mirror").boolValue;

        static void AssertDrawnFrom(VolleyAthleteView view, string character)
        {
            foreach (AthleteAction action in Enum.GetValues(typeof(AthleteAction)))
                foreach (Sprite sprite in view.FramesFor(action))
                    Assert.That(CharacterArt.IsPoseOf(sprite, character), Is.True,
                        $"{view.name} {action} uses {AssetDatabase.GetAssetPath(sprite)}");
        }
    }
}
#endif
