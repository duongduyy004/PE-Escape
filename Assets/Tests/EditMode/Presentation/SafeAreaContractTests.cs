using System.Reflection;
using KMA.Gameplay;
using KMA.Gameplay.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KMA.Tests.Presentation
{
    /// <summary>
    /// Pins where the safe-area inset is allowed to be applied. A <see cref="SafeAreaFitter"/>
    /// writes offsetMin/offsetMax; on a rect whose anchors are coincident those offsets *are*
    /// sizeDelta, so applying there resizes the rect to nothing instead of insetting it. The
    /// minigame Canvas root is exactly such a rect, which is why the HUD collapsed to the origin.
    /// </summary>
    public sealed class SafeAreaContractTests
    {
        const string HudPrefabPath = "Assets/_Project/Prefabs/UI/HUD_Minigame.prefab";

        [Test]
        public void SafeAreaWaitsForTheCanvasTransformBeforeChangingAnchors()
        {
            var root = new GameObject("uninitialized-canvas", typeof(RectTransform), typeof(Canvas));
            root.SetActive(false);
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var canvasRect = (RectTransform)root.transform;
                canvasRect.sizeDelta = new Vector2(1920f, 1080f);
                canvasRect.localScale = Vector3.zero;
                var child = new GameObject("safe-area", typeof(RectTransform));
                child.transform.SetParent(root.transform, false);
                var rect = (RectTransform)child.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var fitter = child.AddComponent<SafeAreaFitter>();
                fitter.Apply(new Rect(0, 0, 1920, 1080), new Vector2Int(1920, 1080));
                Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
                canvasRect.localScale = Vector3.one;
                fitter.Apply(new Rect(100, 0, 1720, 1080), new Vector2Int(1920, 1080));
                Assert.That(rect.anchorMin.x, Is.EqualTo(100f / 1920f).Within(.001f));
                Assert.That(rect.anchorMax.x, Is.EqualTo(1820f / 1920f).Within(.001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SafeAreaFitterLeavesACoincidentAnchorRectUnresized()
        {
            var host = new GameObject("coincident-anchors", typeof(RectTransform));
            try
            {
                var rect = host.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.sizeDelta = new Vector2(1920f, 1080f);

                host.AddComponent<SafeAreaFitter>()
                    .Apply(new Rect(0f, 0f, 1920f, 1080f), new Vector2Int(1920, 1080));

                Assert.That(rect.sizeDelta.x, Is.EqualTo(1920f).Within(.01f),
                    "Coincident anchors turn an offset write into a resize; the fitter must not touch that axis.");
                Assert.That(rect.sizeDelta.y, Is.EqualTo(1080f).Within(.01f),
                    "Coincident anchors turn an offset write into a resize; the fitter must not touch that axis.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SafeAreaFitterStillInsetsAStretchedRect()
        {
            var host = new GameObject("stretched-anchors", typeof(RectTransform));
            try
            {
                var rect = host.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;

                host.AddComponent<SafeAreaFitter>()
                    .Apply(new Rect(100f, 0f, 1720f, 1080f), new Vector2Int(1920, 1080));

                Assert.That(rect.offsetMin.x, Is.EqualTo(100f).Within(.01f));
                Assert.That(rect.offsetMax.x, Is.EqualTo(-100f).Within(.01f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [TestCase(1920, 1080, 72, 36, 1776, 1008, 72, 72, 36, 36)]
        [TestCase(2160, 1080, 80, 0, 1970, 1070, 80, 110, 10, 0)]
        [TestCase(2340, 1080, 90, 70, 2160, 986, 90, 90, 24, 70)]
        [TestCase(2400, 1080, 120, 0, 2200, 1060, 120, 80, 20, 0)]
        public void SafeAreaFitterCalculatesInsetsForCommonLandscapeRatiosAndAsymmetricCutouts(
            int width, int height, int left, int bottom, int safeWidth, int safeHeight,
            float expectedLeft, float expectedRight, float expectedTop, float expectedBottom)
        {
            var host = new GameObject("safe-area-offsets", typeof(RectTransform));
            try
            {
                var fitter = host.AddComponent<SafeAreaFitter>();
                SafeAreaFitter.Offsets offsets = fitter.CalculateOffsets(
                    new Rect(left, bottom, safeWidth, safeHeight), new Vector2(width, height));
                Assert.That(offsets.left, Is.EqualTo(expectedLeft).Within(.01f));
                Assert.That(offsets.right, Is.EqualTo(expectedRight).Within(.01f));
                Assert.That(offsets.top, Is.EqualTo(expectedTop).Within(.01f));
                Assert.That(offsets.bottom, Is.EqualTo(expectedBottom).Within(.01f));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void SafeAreaFitterReappliesInsetsWhenTheScreenSizeChanges()
        {
            var host = new GameObject("safe-area-resize", typeof(RectTransform));
            try
            {
                var rect = host.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                var fitter = host.AddComponent<SafeAreaFitter>();
                fitter.Apply(new Rect(72f, 36f, 1776f, 1008f), new Vector2Int(1920, 1080));
                Assert.That(rect.offsetMin, Is.EqualTo(new Vector2(72f, 36f)));
                Assert.That(rect.offsetMax, Is.EqualTo(new Vector2(-72f, -36f)));

                fitter.Apply(new Rect(120f, 0f, 2200f, 1060f), new Vector2Int(2400, 1080));
                Assert.That(rect.offsetMin, Is.EqualTo(new Vector2(120f, 0f)));
                Assert.That(rect.offsetMax, Is.EqualTo(new Vector2(-80f, -20f)));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void HudPrefabKeepsTheSafeAreaFitterOffTheCanvasRoot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"{HudPrefabPath} must exist.");

            var rootRect = (RectTransform)prefab.transform;
            Assert.That(rootRect.anchorMin, Is.EqualTo(rootRect.anchorMax),
                "Guard for the assertion below: the Canvas root is anchored coincidentally.");
            Assert.That(prefab.GetComponent<SafeAreaFitter>(), Is.Null,
                "A fitter on the Canvas root resizes it to 0x0, collapsing every anchored child to the origin.");
        }

        [Test]
        public void HudPrefabAppliesTheSafeAreaOnTheStretchedSafeAreaRoot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"{HudPrefabPath} must exist.");

            var safeAreaRoot = prefab.transform.Find("SafeAreaRoot") as RectTransform;
            Assert.That(safeAreaRoot, Is.Not.Null, "HUD_Minigame must keep a SafeAreaRoot child.");
            Assert.That(safeAreaRoot.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(safeAreaRoot.anchorMax, Is.EqualTo(Vector2.one));

            var fitter = safeAreaRoot.GetComponent<SafeAreaFitter>();
            Assert.That(fitter, Is.Not.Null, "SafeAreaRoot is the only rect that can carry the inset.");
            Assert.That(fitter.enabled, Is.True, "SafeAreaRoot's fitter must be the one that runs.");
        }

        [Test]
        public void AssemblerMovesTheFitterOffTheCanvasRootOntoSafeAreaRoot()
        {
            var canvasRoot = new GameObject("S2_HUD_Minigame", typeof(RectTransform));
            try
            {
                var rootRect = canvasRoot.GetComponent<RectTransform>();
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.zero;
                canvasRoot.AddComponent<SafeAreaFitter>();

                var safeAreaRoot = new GameObject("SafeAreaRoot", typeof(RectTransform));
                safeAreaRoot.transform.SetParent(canvasRoot.transform, false);

                typeof(MinigameUIAssembler)
                    .GetMethod("EnsureSafeAreaFitter", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { canvasRoot });

                Assert.That(canvasRoot.GetComponent<SafeAreaFitter>(), Is.Null,
                    "Re-assembling a scene must not put a fitter back on the Canvas root.");
                var fitter = safeAreaRoot.GetComponent<SafeAreaFitter>();
                Assert.That(fitter, Is.Not.Null, "The assembler must place the fitter on SafeAreaRoot.");
                Assert.That(fitter.enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(canvasRoot);
            }
        }

        [Test]
        public void SprintPrepareSafeAreaLeavesTheNestedFitterEnabled()
        {
            var safeArea = new GameObject("SafeAreaRoot", typeof(RectTransform));
            try
            {
                var rect = safeArea.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                var fitter = safeArea.AddComponent<SafeAreaFitter>();

                typeof(SprintFestivalPresentation)
                    .GetMethod("PrepareSafeArea", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { safeArea.transform });

                Assert.That(fitter.enabled, Is.True,
                    "Sprint must keep SafeAreaRoot's fitter as the rect that insets the HUD, not switch it off.");
            }
            finally
            {
                Object.DestroyImmediate(safeArea);
            }
        }
    }
}
