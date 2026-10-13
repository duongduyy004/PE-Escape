using System;
using System.Collections.Generic;
using KMA.UI.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Gameplay.UI
{
    /// Decorative art shared by the splash, the menu and the scene transition: the sport badge
    /// and the slanted loading-bar sprite. Content art, so the badge is exempt from the style audits.
    public static class ShellArt
    {
        static readonly Dictionary<(float, float, int, int, bool), Sprite> SlantSprites =
            new Dictionary<(float, float, int, int, bool), Sprite>();

        public static RectTransform CreateBadge(RectTransform parent, string name)
        {
            var root = Rect(parent, name);
            root.sizeDelta = Vector2.one * ShellTheme.BadgeSize;
            root.gameObject.AddComponent<StyleExempt>();
            Disc(root, "BadgeShadow", new Vector2(3f, -4f), 132f, MinigameUiTheme.WithAlpha(ShellTheme.Ink, .35f));
            Disc(root, "WhiteRim", Vector2.zero, 132f, ShellTheme.White);
            Disc(root, "NavyFace", Vector2.zero, 124f, ShellTheme.Ink);
            Disc(root, "Track", new Vector2(0f, -19f), 78f, ShellTheme.AccentRed);
            var star = Rect(root, "Star");
            Place(star, new Vector2(0f, 31f), new Vector2(42f, 42f));
            var starImage = star.gameObject.AddComponent<Image>();
            starImage.sprite = HomeMenuIcons.Get(HomeMenuIcons.Shape.Star);
            starImage.color = ShellTheme.Gold;
            starImage.raycastTarget = false;
            for (int i = 0; i < 3; i++)
            {
                var lane = Rect(root, "Lane" + i);
                Place(lane, new Vector2(-16f + i * 16f, -22f), new Vector2(3f, 44f));
                lane.localRotation = Quaternion.Euler(0f, 0f, -32f);
                var laneImage = lane.gameObject.AddComponent<Image>();
                laneImage.color = ShellTheme.White;
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
            UiKit.GetOrAdd<StyleExempt>(root.gameObject);
            foreach (Transform child in root)
                if (child.name.StartsWith("Laurel", StringComparison.Ordinal)) child.gameObject.SetActive(false);
            if (root.Find("SchoolBuilding") == null) AddSchoolMark(root);
        }

        // Rasterize at the actual target dimensions so short loading tracks keep the
        // same slant angle and a uniform stroke instead of stretching a button texture.
        public static Sprite SlantSprite(bool outline, int width = 320, int height = 56, float? borderWidth = null)
        {
            float angle = ShellTheme.SlantAngle;
            float stroke = Mathf.Clamp(borderWidth ?? ShellTheme.ControlBorder * .5f, 0f, height * .5f - 1f);
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

        static void AddSchoolMark(RectTransform root)
        {
            var school = Rect(root, "SchoolBuilding");
            Place(school, new Vector2(0f, 1f), new Vector2(38f, 20f));
            Solid(school, ShellTheme.White);
            var roof = Rect(root, "SchoolRoof");
            Place(roof, new Vector2(0f, 13f), new Vector2(48f, 5f));
            Solid(roof, ShellTheme.Gold);
            var door = Rect(root, "SchoolDoor");
            Place(door, new Vector2(0f, -4f), new Vector2(7f, 11f));
            Solid(door, ShellTheme.Ink);
            for (int i = 0; i < 3; i++)
            {
                var window = Rect(root, "SchoolWindow" + i);
                Place(window, new Vector2((i - 1) * 11f, 3f), new Vector2(4f, 4f));
                Solid(window, ShellTheme.AccentRed);
            }
        }

        static void Solid(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        static void Disc(Transform parent, string name, Vector2 position, float size, Color color)
        {
            var rect = Rect(parent, name);
            Place(rect, position, new Vector2(size, size));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiKitAssets.Load().Circle;
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
    }
}
