#if UNITY_EDITOR
using KMA.EditorTools;
using KMA.Gameplay.UI;
using KMA.UI.Kit;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Tests.EditorTools
{
    public sealed class MinigamePrefabStylerTests
    {
        [OneTimeSetUp]
        public void Restyle()
        {
            MinigamePrefabStyler.RestyleAll();
            MinigamePrefabStyler.RestyleAll();
        }

        [TearDown]
        public void ReleaseScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test]
        public void ResultPanelGainsRetryDetailLivesAndErrorAndKeepsItsNodeNames()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(MinigamePrefabStyler.ResultPrefab);
            try
            {
                var panel = root.GetComponent<ResultPanel>();
                Assert.That(panel.ValidateReferences(), Is.True);
                Assert.That(panel.SupportsRetry, Is.True);
                var serialized = new SerializedObject(panel);
                foreach (string field in new[] { "detailLabel", "livesLabel", "errorLabel", "retryButton" })
                    Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
                foreach (string path in new[] { "Backdrop", "Content", "Content/StatusLabel", "Content/ScoreLabel",
                             "Content/RankLabel", "Content/ActionButton", "Content/RetryButton" })
                    Assert.That(root.transform.Find(path), Is.Not.Null, path);
                int retries = 0;
                foreach (Transform child in root.transform.Find("Content"))
                    if (child.name == "RetryButton") retries++;
                Assert.That(retries, Is.EqualTo(1), "restyling twice must not add a second retry button");
                Assert.That(root.transform.Find("Content/ActionButton/Shadow"), Is.Null);
                Assert.That(root.transform.Find("Content/ScoreCaption").GetComponent<TMP_Text>().text,
                    Is.EqualTo("ĐIỂM"), "the score caption is Vietnamese");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [TestCase(MinigamePrefabStyler.HudPrefab)]
        [TestCase(MinigamePrefabStyler.PhasePrefab)]
        [TestCase(MinigamePrefabStyler.ResultPrefab)]
        public void SharedPrefabsUseOnlyKitSpritesFontsAndTokens(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                UiKitAssets assets = UiKitAssets.Load();
                foreach (Image image in root.GetComponentsInChildren<Image>(true))
                {
                    if (image.color.a < .01f || image.name == "Icon")
                        continue;
                    if (image.sprite == null)
                        Assert.That(MinigameUiTheme.RgbEquals(image.color, MinigameUiTheme.Scrim), Is.True, image.name);
                    else
                        Assert.That(System.Array.IndexOf(assets.AllSprites(), image.sprite), Is.GreaterThanOrEqualTo(0),
                            $"{image.name} uses {image.sprite.name}");
                }
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    var fonts = VietTypography.Library;
                    Assert.That(new[] { fonts.title, fonts.buttonHud, fonts.regular, fonts.bold }, Does.Contain(text.font), text.name);
                    Assert.That(text.extraPadding, Is.True, text.name);
                    float smallest = text.enableAutoSizing ? text.fontSizeMin : text.fontSize;
                    Assert.That(smallest, Is.GreaterThanOrEqualTo(MinigameUiTheme.MinimumFontSize), text.name);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

    }
}
#endif
