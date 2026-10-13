using KMA.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Gameplay.UI
{
    // Builds one stop of the journey map. Cross-stop layout and the road belong to
    // MapJourneyPathLayout; selection/current state is pushed in by MapScreen.
    internal static class MapStopBuilder
    {
        static Sprite starSprite;
        static Sprite checkSprite;
        static Sprite flagSprite;
        static UITheme.LessonJourneyStyle Style => UITheme.Shared.LessonJourney;

        const float RingPad = 28f;

        // The badge drops far enough that the current stop's enlarged glow ring clears the tag chip
        // and its hard drop shadow.
        internal static float BadgeCenterFromTop(UITheme.LessonJourneyStyle style) =>
            style.stopTagHeight + (style.stopBadgeSize + RingPad) * style.stopCurrentScale * .5f
            - ShellTheme.ControlShadow.y;

        public static MapNodeView Build(Transform parent, MapScreen screen, SubjectId subject,
            string label, Color accent, int order)
        {
            RectTransform root = MapPresentationBuilder.Rect(parent, subject + "Node");
            root.sizeDelta = Style.stopSize;
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;

            float badgeSize = Style.stopBadgeSize;
            float badgeCenterFromTop = BadgeCenterFromTop(Style);

            // Tag above the badge ("ĐANG Ở ĐÂY"), shown only on the current stop.
            ShellChipHandle tag = ShellKit.Chip(root, "CurrentTag", "ĐANG Ở ĐÂY", ShellTheme.Gold);
            RectTransform tagRect = tag.Card.Root;
            Place(tagRect, new Vector2(.5f, 1f), new Vector2(240f, 48f), new Vector2(0f, -4f));
            tagRect.pivot = new Vector2(.5f, 1f);
            tag.Label.fontSize = MinigameUiTheme.Caption;
            UiKit.FitLabel(tag.Label, MinigameUiTheme.Caption);

            // Gold ring behind the badge (current or selected).
            RectTransform ring = MapPresentationBuilder.Rect(root, "SelectionRing");
            Place(ring, new Vector2(.5f, 1f), Vector2.one * (badgeSize + RingPad), new Vector2(0f, -badgeCenterFromTop));
            Image ringImage = ring.gameObject.AddComponent<Image>();
            ringImage.sprite = UiKitAssets.Load().Circle;
            ringImage.color = ShellTheme.Gold;
            ringImage.raycastTarget = false;

            // Badge.
            RectTransform badge = MapPresentationBuilder.Rect(root, "Badge");
            Place(badge, new Vector2(.5f, 1f), Vector2.one * badgeSize, new Vector2(0f, -badgeCenterFromTop));
            ShellCardHandle badgeLayers = ShellKit.Badge(badge, accent);
            Image badgeImage = badgeLayers.Face;
            // The stop recolours its own badge per state; the feedback only slides it.
            var press = root.gameObject.AddComponent<ShellPressFeedback>();
            press.Configure(badge, null, null, Color.white, Color.white);

            RectTransform glyph = MapPresentationBuilder.Rect(badge, "IconGlyph");
            MapPresentationBuilder.Stretch(glyph, new Vector2(34f, 34f), new Vector2(-34f, -34f));
            Image glyphImage = glyph.gameObject.AddComponent<Image>();
            glyphImage.sprite = MapPresentationBuilder.SportIconSprite(subject);
            glyphImage.color = ShellTheme.Ink;
            glyphImage.gameObject.AddComponent<StyleExempt>();
            glyphImage.preserveAspect = true;
            glyphImage.raycastTarget = false;

            RectTransform orderBadge = MapPresentationBuilder.Rect(badge, "OrderBadge");
            Place(orderBadge, new Vector2(.14f, .86f), Vector2.one * 44f, Vector2.zero);
            Image orderImage = orderBadge.gameObject.AddComponent<Image>();
            orderImage.sprite = UiKitAssets.Load().Circle;
            orderImage.color = ShellTheme.Ink;
            orderImage.raycastTarget = false;
            MapPresentationBuilder.LayoutLabel(orderBadge, "Order", order.ToString(), (int)MinigameUiTheme.Caption, ShellTheme.Gold,
                TextAnchor.MiddleCenter);
            StretchChild(orderBadge);

            RectTransform done = MapPresentationBuilder.Rect(badge, "DoneMark");
            Place(done, new Vector2(.86f, .86f), Vector2.one * 48f, Vector2.zero);
            Image doneImage = done.gameObject.AddComponent<Image>();
            doneImage.sprite = UiKitAssets.Load().Circle;
            doneImage.color = ShellTheme.Gold;
            doneImage.raycastTarget = false;
            RectTransform check = MapPresentationBuilder.Rect(done, "CheckGlyph");
            MapPresentationBuilder.Stretch(check, new Vector2(10f, 10f), new Vector2(-10f, -10f));
            Image checkImage = check.gameObject.AddComponent<Image>();
            checkImage.sprite = CheckSprite();
            checkImage.color = ShellTheme.Ink;
            checkImage.gameObject.AddComponent<StyleExempt>();
            checkImage.preserveAspect = true;
            checkImage.raycastTarget = false;

            RectTransform lockRect = MapPresentationBuilder.Rect(badge, "LockIcon");
            Place(lockRect, new Vector2(.84f, .14f), Vector2.one * 52f, Vector2.zero);
            Image lockPlate = lockRect.gameObject.AddComponent<Image>();
            lockPlate.sprite = UiKitAssets.Load().Circle;
            lockPlate.color = ShellTheme.Ink;
            lockPlate.raycastTarget = false;
            RectTransform lockGlyph = MapPresentationBuilder.Rect(lockRect, "Glyph");
            MapPresentationBuilder.Stretch(lockGlyph, new Vector2(11f, 9f), new Vector2(-11f, -13f));
            Image lockImage = lockGlyph.gameObject.AddComponent<Image>();
            lockImage.sprite = MapPresentationBuilder.LockSprite();
            lockImage.color = Color.white;
            lockImage.gameObject.AddComponent<StyleExempt>();
            lockImage.preserveAspect = true;
            lockImage.raycastTarget = false;

            // The finish flag marks the last stop of the course.
            if (subject == SubjectId.Chess)
            {
                RectTransform flag = MapPresentationBuilder.Rect(badge, "FinishFlag");
                Place(flag, new Vector2(1f, .72f), Vector2.one * 56f, new Vector2(26f, 0f));
                Image flagImage = flag.gameObject.AddComponent<Image>();
                flagImage.sprite = FlagSprite();
                flagImage.preserveAspect = true;
                flagImage.raycastTarget = false;
                flagImage.gameObject.AddComponent<StyleExempt>();
                }

            // Name + meta pill below the badge.
            RectTransform labelGroup = MapPresentationBuilder.Rect(root, "LabelGroup");
            float labelTop = badgeCenterFromTop + badgeSize * .5f + 14f;
            Place(labelGroup, new Vector2(.5f, 1f), new Vector2(Style.stopSize.x, 120f), new Vector2(0f, -labelTop));
            labelGroup.pivot = new Vector2(.5f, 1f);

            TMP_Text title = MapPresentationBuilder.LayoutLabel(labelGroup, "Title", label, 36,
                Color.white, TextAnchor.MiddleCenter);
            RectTransform titleBox = (RectTransform)title.transform.parent;
            titleBox.anchorMin = new Vector2(0f, 1f);
            titleBox.anchorMax = Vector2.one;
            titleBox.pivot = new Vector2(.5f, 1f);
            titleBox.offsetMin = new Vector2(0f, -50f);
            titleBox.offsetMax = Vector2.zero;
            title.fontStyle = FontStyles.Bold;
            title.enableWordWrapping = false;
            title.outlineWidth = .2f;
            title.outlineColor = ShellTheme.Ink;

            RectTransform pill = MapPresentationBuilder.Rect(labelGroup, "MetaPill");
            pill.anchorMin = pill.anchorMax = new Vector2(.5f, 1f);
            pill.pivot = new Vector2(.5f, 1f);
            pill.sizeDelta = new Vector2(Style.stopSize.x - 4f, 52f);
            pill.anchoredPosition = new Vector2(0f, -52f);
            Image pillImage = pill.gameObject.AddComponent<Image>();
            pillImage.sprite = UiKitAssets.Load().RoundRect20;
            pillImage.type = Image.Type.Sliced;
            pillImage.color = MinigameUiTheme.WithAlpha(ShellTheme.Ink, .8f);
            pillImage.raycastTarget = false;

            RectTransform starsRoot = MapPresentationBuilder.Rect(pill, "Stars");
            starsRoot.anchorMin = new Vector2(0f, 0f);
            starsRoot.anchorMax = new Vector2(.32f, 1f);
            starsRoot.offsetMin = new Vector2(12f, 4f);
            starsRoot.offsetMax = new Vector2(0f, -4f);
            var row = starsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 2f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            var stars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                RectTransform star = MapPresentationBuilder.Rect(starsRoot, "Star" + (i + 1));
                stars[i] = star.gameObject.AddComponent<Image>();
                stars[i].sprite = StarSprite();
                stars[i].preserveAspect = true;
                stars[i].raycastTarget = false;
                stars[i].gameObject.AddComponent<StyleExempt>();
                var element = star.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = element.preferredHeight = 30f;
            }

            TMP_Text status = MapPresentationBuilder.LayoutLabel(pill, "Status", string.Empty, (int)MinigameUiTheme.Caption,
                ShellTheme.GoldLight, TextAnchor.MiddleRight);
            RectTransform statusBox = (RectTransform)status.transform.parent;
            statusBox.anchorMin = new Vector2(.32f, 0f);
            statusBox.anchorMax = Vector2.one;
            statusBox.offsetMin = new Vector2(0f, 2f);
            statusBox.offsetMax = new Vector2(-14f, -2f);
            status.enableWordWrapping = false;
            status.fontStyle = FontStyles.Bold;

            MapNodeView node = root.gameObject.AddComponent<MapNodeView>();
            node.Bind(button, title, null);
            node.BindPresentation(status, null, null, null, null, accent);
            node.BindJourneyStop(badgeImage, stars, lockRect.gameObject, done.gameObject,
                tagRect.gameObject, ringImage, badge, labelGroup, glyphImage);
            node.Configure(subject, label, false, null, 5);
            button.onClick.AddListener(() => screen.SelectSubject(subject));
            return node;
        }

        // LayoutLabel wraps each label in a "<Name>Container"; stretch it to fill its parent.
        static void StretchChild(RectTransform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = (RectTransform)parent.GetChild(i);
                if (child.name.EndsWith("Container"))
                    MapPresentationBuilder.Stretch(child, Vector2.zero, Vector2.zero);
            }
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        internal static Sprite StarSprite()
        {
            if (starSprite != null) return starSprite;
            const int size = 64;
            var polygon = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float radius = i % 2 == 0 ? 30f : 12.5f;
                float angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                polygon[i] = new Vector2(32f + Mathf.Cos(angle) * radius, 34f + Mathf.Sin(angle) * radius);
            }
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                if (Inside(polygon, x + .5f, y + .5f))
                    pixels[y * size + x] = new Color32(255, 255, 255, 255);
            starSprite = MakeSprite("RuntimeStarIcon", size, pixels);
            return starSprite;
        }

        internal static Sprite FlagSprite()
        {
            if (flagSprite != null) return flagSprite;
            const int size = 64;
            var pixels = new Color32[size * size];
            // Pole, then a 4x3 chequered cloth.
            for (int y = 4; y < 60; y++)
            for (int x = 8; x < 13; x++)
                pixels[y * size + x] = new Color32(255, 255, 255, 255);
            for (int cy = 0; cy < 3; cy++)
            for (int cx = 0; cx < 4; cx++)
            {
                bool dark = (cx + cy) % 2 == 0;
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 11; x++)
                    pixels[(34 + cy * 8 + y) * size + 13 + cx * 11 + x] = dark
                        ? new Color32(11, 42, 74, 255) : new Color32(255, 255, 255, 255);
            }
            flagSprite = MakeSprite("RuntimeFlagIcon", size, pixels);
            return flagSprite;
        }

        internal static Sprite CheckSprite()
        {
            if (checkSprite != null) return checkSprite;
            const int size = 64;
            var pixels = new Color32[size * size];
            MapPresentationBuilder.DrawLine(pixels, size, 10, 34, 25, 18, 10);
            MapPresentationBuilder.DrawLine(pixels, size, 25, 18, 54, 48, 10);
            checkSprite = MakeSprite("RuntimeCheckIcon", size, pixels);
            return checkSprite;
        }

        static Sprite MakeSprite(string name, int size, Color32[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static bool Inside(Vector2[] polygon, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                bool crosses = (polygon[i].y > y) != (polygon[j].y > y) &&
                    x < (polygon[j].x - polygon[i].x) * (y - polygon[i].y) /
                    (polygon[j].y - polygon[i].y) + polygon[i].x;
                if (crosses) inside = !inside;
            }
            return inside;
        }
    }
}
