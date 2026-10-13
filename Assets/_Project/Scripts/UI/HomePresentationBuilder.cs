using System;
using System.Linq;
using System.Collections.Generic;
using KMA.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Gameplay.UI
{
    public static class HomePresentationBuilder
    {
        static Sprite disc;
        static readonly Dictionary<(float, float, int, int, bool), Sprite> SlantSprites = new Dictionary<(float, float, int, int, bool), Sprite>();

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
            foreach (var old in button.GetComponents<HomeMenuButton>()) UnityEngine.Object.DestroyImmediate(old);
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

        // Splash and menu share the same badge sprites, rim and shine component.
        public static RectTransform CreateBadge(RectTransform parent, string name)
        {
            var root = Rect(parent, name);
            root.sizeDelta = Vector2.one * HomeMenuStyle.BadgeSize;
            Disc(root, "BadgeShadow", new Vector2(3f, -4f), 132f, new Color(0f, .04f, .1f, .35f));
            Disc(root, "WhiteRim", Vector2.zero, 132f, HomeMenuStyle.White);
            Disc(root, "NavyFace", Vector2.zero, 124f, HomeMenuStyle.Navy);
            Disc(root, "Track", new Vector2(0f, -19f), 78f, HomeMenuStyle.Red);
            var star = Rect(root, "Star");
            Place(star, new Vector2(0f, 31f), new Vector2(42f, 42f));
            var starImage = star.gameObject.AddComponent<Image>();
            starImage.sprite = HomeMenuIcons.Get(HomeMenuIcons.Shape.Star);
            starImage.color = HomeMenuStyle.Gold;
            starImage.raycastTarget = false;
            for (int i = 0; i < 3; i++)
            {
                var lane = Rect(root, "Lane" + i);
                Place(lane, new Vector2(-16f + i * 16f, -22f), new Vector2(3f, 44f));
                lane.localRotation = Quaternion.Euler(0f, 0f, -32f);
                var laneImage = lane.gameObject.AddComponent<Image>();
                laneImage.color = HomeMenuStyle.White;
                laneImage.raycastTarget = false;
            }
            AddSchoolMark(root);
            root.gameObject.AddComponent<HomeBadgeShine>();
            return root;
        }

        // Refreshes badges already serialized into a Bootstrap scene, where a code change alone
        // cannot replace the old laurel-only children.
        public static void RestyleBadge(RectTransform root)
        {
            if (root == null) return;
            foreach (Transform child in root)
                if (child.name.StartsWith("Laurel", StringComparison.Ordinal)) child.gameObject.SetActive(false);
            if (root.Find("SchoolBuilding") == null) AddSchoolMark(root);
        }

        static void AddSchoolMark(RectTransform root)
        {
            var school = Rect(root, "SchoolBuilding");
            Place(school, new Vector2(0f, 1f), new Vector2(38f, 20f));
            var schoolImage = school.gameObject.AddComponent<Image>();
            schoolImage.color = HomeMenuStyle.White;
            schoolImage.raycastTarget = false;
            var roof = Rect(root, "SchoolRoof");
            Place(roof, new Vector2(0f, 13f), new Vector2(48f, 5f));
            var roofImage = roof.gameObject.AddComponent<Image>();
            roofImage.color = HomeMenuStyle.Gold;
            roofImage.raycastTarget = false;
            var door = Rect(root, "SchoolDoor");
            Place(door, new Vector2(0f, -4f), new Vector2(7f, 11f));
            var doorImage = door.gameObject.AddComponent<Image>();
            doorImage.color = HomeMenuStyle.Navy;
            doorImage.raycastTarget = false;
            for (int i = 0; i < 3; i++)
            {
                var window = Rect(root, "SchoolWindow" + i);
                Place(window, new Vector2((i - 1) * 11f, 3f), new Vector2(4f, 4f));
                var windowImage = window.gameObject.AddComponent<Image>();
                windowImage.color = HomeMenuStyle.Red;
                windowImage.raycastTarget = false;
            }
        }

        // Restyles a scene-authored Button as a slanted menu button; shared by Home and Game Over.
        public static HomeMenuButton StyleButton(Button button, RectTransform parent, string caption,
            HomeMenuButton.Kind kind, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)button.transform;
            rect.SetParent(parent, false);
            Place(rect, position, size);
            rect.anchorMin = rect.anchorMax = anchor;
            var border = button.GetComponent<Image>() ?? button.gameObject.AddComponent<Image>();
            border.sprite = SlantSprite(true);
            border.type = Image.Type.Simple;
            border.color = HomeMenuStyle.White;
            var oldOutline = button.GetComponent<Outline>();
            if (oldOutline != null) oldOutline.enabled = false;
            var fill = Rect(rect, "Fill").gameObject.AddComponent<Image>();
            float stroke = UITheme.Shared.BorderWidth * .5f;
            Stretch(fill.rectTransform, Vector2.one * stroke, -Vector2.one * stroke);
            fill.sprite = SlantSprite(false);
            fill.raycastTarget = false;
            var label = UiKit.EnsureTmpLabel(rect, 22, HomeMenuStyle.White);
            label.text = VietText.Fix(caption);
            label.fontStyle = FontStyles.Normal;
            label.alignment = TextAlignmentOptions.Left;
            label.enableAutoSizing = true;
            label.fontSizeMin = 18;
            label.fontSizeMax = label.fontSize;
            Stretch(label.rectTransform, new Vector2(55f, 0f), new Vector2(-30f, 0f));
            label.transform.SetAsLastSibling();
            var iconRect = Rect(rect, "Icon");
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, .5f);
            iconRect.pivot = new Vector2(.5f, .5f);
            iconRect.sizeDelta = new Vector2(23f, 23f);
            iconRect.anchoredPosition = new Vector2(35f, 0f);
            var iconImage = iconRect.gameObject.AddComponent<Image>();
            iconImage.sprite = HomeMenuIcons.Get(kind == HomeMenuButton.Kind.NewGame ? HomeMenuIcons.Shape.Runner :
                kind == HomeMenuButton.Kind.Continue ? HomeMenuIcons.Shape.Volleyball :
                kind == HomeMenuButton.Kind.Exit ? HomeMenuIcons.Shape.Power : HomeMenuIcons.Shape.Gear);
            iconImage.color = HomeMenuStyle.White;
            iconImage.raycastTarget = false;
            var arrowRect = Rect(rect, "HoverArrow");
            arrowRect.anchorMin = arrowRect.anchorMax = new Vector2(1f, .5f);
            arrowRect.sizeDelta = new Vector2(15f, 23f);
            arrowRect.anchoredPosition = new Vector2(-22f, 0f);
            var arrow = arrowRect.gameObject.AddComponent<Image>();
            arrow.sprite = HomeMenuIcons.Get(HomeMenuIcons.Shape.Chevron);
            arrow.color = HomeMenuStyle.Gold;
            arrow.raycastTarget = false;
            arrow.gameObject.SetActive(false);
            button.transition = Selectable.Transition.None;
            var visual = button.gameObject.AddComponent<HomeMenuButton>();
            visual.Initialize(kind, border, fill, arrow);
            return visual;
        }

        static void Disc(Transform parent, string name, Vector2 position, float size, Color color)
        {
            var rect = Rect(parent, name);
            Place(rect, position, new Vector2(size, size));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = disc ??= DiscSprite();
            image.color = color;
            image.raycastTarget = false;
        }

        static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }

        static Sprite DiscSprite()
        {
            var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f,
                        Mathf.Clamp01((63.5f - Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f))) * 2f)));
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 128, 128), new Vector2(.5f, .5f));
        }

        // Rasterize at the actual target dimensions so short loading tracks keep the
        // same slant angle and a uniform stroke instead of stretching a button texture.
        public static Sprite SlantSprite(bool outline, int width = 320, int height = 56, float? borderWidth = null)
        {
            float angle = UITheme.Shared.Menu.buttonSlantAngle;
            float stroke = Mathf.Clamp(borderWidth ?? UITheme.Shared.BorderWidth * .5f, 0f, height * .5f - 1f);
            var key = (angle, stroke, width, height, outline);
            if (SlantSprites.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            float lean = Mathf.Tan(angle * Mathf.Deg2Rad) * height;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float left = lean - y * lean / height;
                    float right = width - y * lean / height;
                    float outer = Mathf.Clamp01(x - left + 1f) * Mathf.Clamp01(right - x);
                    float inner = y >= stroke && y <= height - 1f - stroke && x >= left + stroke && x <= right - stroke ? 1f : 0f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, outer * (outline ? 1f - inner : 1f)));
                }
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f));
            SlantSprites[key] = sprite;
            return sprite;
        }
    }
}
