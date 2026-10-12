using System.Collections;
using System.Linq;
using System.Reflection;
using KMA.Gameplay.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KMA.Tests.Presentation
{
    public sealed class UIComponentPlayModeTests
    {
        [Test]
        public void HudPrefabSatisfiesSharedPresentationContract()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/HUD_Minigame.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));

            Assert.That(prefab.GetComponentsInChildren<Canvas>(true), Has.Length.EqualTo(1));
            var scaler = prefab.GetComponentInChildren<CanvasScaler>(true);
            Assert.That(scaler, Is.Not.Null);
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(1f).Within(.001f));

            var fitter = prefab.GetComponentInChildren<SafeAreaFitter>(true);
            var hud = prefab.GetComponentInChildren<MinigameHUD>(true);
            var heartBar = prefab.GetComponentInChildren<HeartBar>(true);
            Assert.That(fitter, Is.Not.Null);
            Assert.That(hud, Is.Not.Null);
            Assert.That(heartBar, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true), Has.None.Null);
            Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true)
                .Any(component => component.GetType().GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.NonPublic) != null), Is.False);
            AssertRequiredHudReferences(hud);
        }

        [UnityTest]
        public IEnumerator HudPrefabInstantiatesWithVisibleScaleAndActiveText()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/HUD_Minigame.prefab");
            Assert.That(prefab, Is.Not.Null);

            var instance = Object.Instantiate(prefab);
            try
            {
                Assert.That(instance.transform.localScale.x, Is.GreaterThan(0f));
                Assert.That(instance.transform.localScale.y, Is.GreaterThan(0f));
                Assert.That(instance.transform.localScale.z, Is.GreaterThan(0f));
                var textComponents = instance.GetComponentsInChildren<TMP_Text>(true);
                Assert.That(textComponents, Has.Length.GreaterThan(0));
                foreach (var text in textComponents)
                {
                    Assert.That(text.isActiveAndEnabled, Is.True, text.name);
                    Assert.That(text.font, Is.Not.Null, text.name);
                }

                var fitter = instance.GetComponentInChildren<SafeAreaFitter>(true);
                Assert.That(fitter, Is.Not.Null);
                Canvas.ForceUpdateCanvases();
                fitter.Apply(new Rect(100f, 0f, 1720f, 1080f), new Vector2Int(1920, 1080));
                var rect = (RectTransform)fitter.transform;
                Assert.That(rect.anchorMin.x, Is.EqualTo(100f / 1920f).Within(.001f));
                Assert.That(rect.anchorMax.x, Is.EqualTo(1820f / 1920f).Within(.001f));
                Assert.That(rect.offsetMin.x, Is.Zero);
                Assert.That(rect.offsetMax.x, Is.Zero);
            }
            finally
            {
                Object.Destroy(instance);
            }

            yield return null;
        }

        private static void AssertRequiredHudReferences(MinigameHUD hud)
        {
            var serializedHud = new SerializedObject(hud);
            foreach (var propertyName in new[]
                     {
                         "timeLabel", "phaseLabel", "scoreLabel", "statusLabel", "progressFill", "staminaFill"
                     })
            {
                var property = serializedHud.FindProperty(propertyName);
                Assert.That(property, Is.Not.Null, propertyName);
                Assert.That(property.objectReferenceValue, Is.Not.Null, propertyName);
            }
        }
    }
}
