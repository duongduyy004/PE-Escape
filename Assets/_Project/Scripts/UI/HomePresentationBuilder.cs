using System;
using System.Linq;
using KMA.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Gameplay.UI
{
    public static class HomePresentationBuilder
    {
        // The panel, title and button faces are painted into the menu key art (Art/UI/MenuKeyArtPanel.png),
        // so the real buttons are invisible hit areas laid over the painted ones. The button captions are
        // kept as clear labels so the buttons still read as "CHƠI MỚI", "TIẾP TỤC", ... to tests and tools.
        const float ArtWidth = 1672f;
        const float ArtHeight = 941f;
        const float ButtonLeft = 102f;
        const float ButtonRight = 592f;

        public static void Build(MainMenuScreen screen)
        {
            if (screen == null || screen.transform.Find("HomeMenuLayout") != null) return;
            var layout = Rect(screen.transform, "HomeMenuLayout");
            layout.anchorMin = Vector2.zero;
            layout.anchorMax = Vector2.one;
            layout.offsetMin = layout.offsetMax = Vector2.zero;
            // The hit areas follow the fitted panel frame; older scenes without one fall back to the full art.
            var art = (screen.transform.root.Find("HomePanelFrame") ?? screen.transform.root.Find("HomeIllustration")) as RectTransform;
            layout.gameObject.AddComponent<HomeKeyArtLayout>().Configure(art != null ? art : (RectTransform)screen.transform);
            var oldLogo = screen.transform.Find("HomeLogo");
            if (oldLogo != null) oldLogo.gameObject.SetActive(false);
            var oldTitle = screen.transform.Find("HomeTitle");
            if (oldTitle != null) oldTitle.gameObject.SetActive(false);
            HitArea(screen, layout, "NEW GAMEButton", "CHƠI MỚI", 426f, 530f);
            HitArea(screen, layout, "CONTINUEButton", "TIẾP TỤC", 532f, 634f);
            HitArea(screen, layout, "SETTINGSButton", "CÀI ĐẶT", 636f, 738f);
            HitArea(screen, layout, "QUITButton", "THOÁT", 740f, 842f);
        }

        /// Drops an earlier menu layout (keeping the scene's buttons) so authoring can rebuild it.
        public static void Rebuild(MainMenuScreen screen)
        {
            foreach (var name in new[] { "HomeMenuLayout", "HomeMenuVeil", "HomeRunners" })
            {
                var old = screen.transform.Find(name);
                if (old == null) continue;
                foreach (var button in old.GetComponentsInChildren<Button>(true))
                    button.transform.SetParent(screen.transform, false);
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            Build(screen);
        }

        // top/bottom are pixel rows of the painted button in the 1672 x 941 key art, measured from the top.
        static void HitArea(Component screen, RectTransform parent, string name, string caption, float top, float bottom)
        {
            var button = screen.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(candidate => string.Equals(candidate.name, name, StringComparison.Ordinal));
            if (button == null) return;
            var rect = (RectTransform)button.transform;
            rect.SetParent(parent, false);
            foreach (var stale in new[] { "Fill", "Icon", "HoverArrow", "DisabledShade" })
            {
                var old = rect.Find(stale);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            rect.anchorMin = new Vector2(ButtonLeft / ArtWidth, 1f - bottom / ArtHeight);
            rect.anchorMax = new Vector2(ButtonRight / ArtWidth, 1f - top / ArtHeight);
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            var hit = button.GetComponent<Image>() ?? button.gameObject.AddComponent<Image>();
            hit.sprite = null;
            hit.type = Image.Type.Simple;
            hit.color = Color.white;
            hit.raycastTarget = true;
            hit.canvasRenderer.cullTransparentMesh = false;
            var oldOutline = button.GetComponent<Outline>();
            if (oldOutline != null) oldOutline.enabled = false;
            var shade = Rect(rect, "DisabledShade");
            Stretch(shade, Vector2.one * 3f, -Vector2.one * 3f);
            var shadeImage = shade.gameObject.AddComponent<Image>();
            shadeImage.color = new Color(.84f, .83f, .8f, .6f);
            shadeImage.raycastTarget = false;
            var label = UiKit.EnsureTmpLabel(rect, 22, Color.clear);
            label.text = VietText.Fix(caption);
            label.color = Color.clear;
            label.raycastTarget = false;
            Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
            var visual = button.GetComponent<HomeKeyArtButton>() ?? button.gameObject.AddComponent<HomeKeyArtButton>();
            visual.Configure(button, shadeImage);
        }

        static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }
    }
}
