using KMA.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Gameplay.UI
{
    // Geometry and decoration only; JourneyLessonList owns progress and navigation.
    // The popup reads like a level select: three stage nodes on a dotted road, and a
    // detail card under them holding the selected stage's objective and the play button.
    internal static class JourneyLessonPresentation
    {
        public const int StageCount = 3;
        public const int RoadDots = 7;
        static UITheme Theme => UITheme.Shared;
        static UITheme.LessonJourneyStyle Style => Theme.LessonJourney;
        static readonly string[] StageIcons = { "StageIcon_Learn", "StageIcon_Practice", "StageIcon_Exam" };

        public static RectTransform Create(Transform parent)
        {
            Image surface = Shape(parent, "JourneyLessons", Theme.Surface);
            RectTransform panel = surface.rectTransform;
            Anchor(panel, Style.panelAnchorMin, Style.panelAnchorMax);
            AddBorder(surface, Theme.MapLockedBorder);

            Image accent = Shape(panel, "ChapterAccent", Theme.LessonJourney.sprint);
            Anchor(accent.rectTransform, new Vector2(.025f, .982f), new Vector2(.975f, .989f));

            Image emblem = Shape(panel, "CourseIcon", Theme.LessonJourney.sprint, true);
            Place(emblem.rectTransform, new Vector2(.025f, .90f), new Vector2(78f, 78f), new Vector2(0f, .5f));
            SubjectId[] subjects = { SubjectId.Sprint, SubjectId.Volleyball, SubjectId.Football, SubjectId.Chess };
            foreach (SubjectId subject in subjects)
            {
                Image sport = Shape(emblem.transform, subject + "Glyph", Theme.Surface);
                sport.sprite = MapPresentationBuilder.SportIconSprite(subject);
                sport.type = Image.Type.Simple;
                sport.preserveAspect = true;
                Anchor(sport.rectTransform, Vector2.one * .2f, Vector2.one * .8f);
                sport.gameObject.SetActive(subject == SubjectId.Sprint);
            }

            TMP_Text heading = Label(panel, "CourseTitle", Style.headingSize, Theme.TextPrimary, FontStyles.Bold,
                VietFontRole.Hud);
            Anchor(heading.rectTransform, new Vector2(.025f, .83f), new Vector2(.40f, .97f));
            heading.rectTransform.offsetMin = new Vector2(100f, 0f);

            Image star = Shape(panel, "ProgressStar", Theme.Accent);
            star.sprite = MapStopBuilder.StarSprite();
            star.type = Image.Type.Simple;
            star.preserveAspect = true;
            Place(star.rectTransform, new Vector2(.42f, .90f), new Vector2(51f, 51f), new Vector2(0f, .5f));
            TMP_Text progress = Label(panel, "CourseProgress", Style.bodySize, Theme.MapHint);
            Anchor(progress.rectTransform, new Vector2(.42f, .83f), new Vector2(.88f, .97f));
            progress.rectTransform.offsetMin = new Vector2(66f, 0f);

            // Roads first so the stage nodes draw over their ends.
            for (int index = 0; index < StageCount - 1; index++) CreateRoad(panel, index);
            for (int index = 0; index < StageCount; index++) CreateNode(panel, index);
            CreateDetailCard(panel);
            EnsurePopup(panel);
            return panel;
        }

        // The panel opens as a popup over the map: a dimming scrim behind it that closes it on tap,
        // and a round close button hanging off its top-right corner. Safe to run on baked scenes.
        public static void EnsurePopup(RectTransform panel)
        {
            Anchor(panel, Style.panelAnchorMin, Style.panelAnchorMax);
            var parent = (RectTransform)panel.parent;
            RectTransform scrim = (RectTransform)parent.Find("LessonScrim");
            if (scrim == null)
            {
                scrim = Rect(parent, "LessonScrim");
                Image shade = scrim.gameObject.AddComponent<Image>();
                shade.color = MinigameUiTheme.WithAlpha(HomeMenuStyle.Navy, .62f);
                Button dismiss = scrim.gameObject.AddComponent<Button>();
                dismiss.targetGraphic = shade;
                dismiss.transition = Selectable.Transition.None;
            }
            // Cover the whole screen, not just the padded content area the panel sits in.
            scrim.anchorMin = Vector2.zero;
            scrim.anchorMax = Vector2.one;
            scrim.offsetMin = -parent.offsetMin;
            scrim.offsetMax = -parent.offsetMax;
            scrim.SetAsLastSibling();
            panel.SetAsLastSibling();

            if (panel.Find("CloseButton") != null) return;
            Image close = Shape(panel, "CloseButton", HomeMenuStyle.Navy, true);
            close.raycastTarget = true;
            Place(close.rectTransform, Vector2.one, new Vector2(96f, 96f));
            close.rectTransform.anchoredPosition = new Vector2(-32f, -32f);
            Outline ring = close.gameObject.AddComponent<Outline>();
            ring.effectColor = HomeMenuStyle.Gold;
            ring.effectDistance = new Vector2(3f, -3f);
            Button button = close.gameObject.AddComponent<Button>();
            button.targetGraphic = close;
            button.transition = Selectable.Transition.None;
            close.gameObject.AddComponent<KitPressFeedback>().Configure(close, close.rectTransform);
            foreach (float angle in new[] { 45f, -45f })
            {
                Image stroke = Shape(close.transform, "Stroke", HomeMenuStyle.White);
                stroke.sprite = null;
                stroke.type = Image.Type.Simple;
                Place(stroke.rectTransform, Vector2.one * .5f, new Vector2(45f, 9f));
                stroke.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        /// Centres of the visible stage nodes along the road, as panel x fractions.
        public static float NodeCenter(int index, int count) => count switch
        {
            1 => .5f,
            2 => index == 0 ? .32f : .68f,
            _ => .18f + index * .32f
        };

        /// Spreads the visible nodes and the roads between them; a one-lesson chapter is a lone node.
        public static void LayoutStages(RectTransform panel, int count)
        {
            for (int index = 0; index < StageCount; index++)
            {
                var node = (RectTransform)panel.Find($"Lesson{index + 1}");
                if (node == null || index >= count) continue;
                float center = NodeCenter(index, count);
                node.anchorMin = new Vector2(center - Style.nodeHalfWidth, Style.nodeRowBottom);
                node.anchorMax = new Vector2(center + Style.nodeHalfWidth, Style.nodeRowTop);
                node.offsetMin = node.offsetMax = Vector2.zero;
            }
            for (int index = 0; index < StageCount - 1; index++)
            {
                var road = (RectTransform)panel.Find($"LessonRoad{index + 1}");
                if (road == null) continue;
                bool used = index + 1 < count;
                road.gameObject.SetActive(used);
                if (!used) continue;
                road.anchorMin = new Vector2(NodeCenter(index, count), Style.nodeLineY);
                road.anchorMax = new Vector2(NodeCenter(index + 1, count), Style.nodeLineY);
                road.offsetMin = new Vector2(Style.nodeSize * .5f, -Style.lessonDotSize);
                road.offsetMax = new Vector2(-Style.nodeSize * .5f, Style.lessonDotSize);
            }
        }

        static void CreateNode(RectTransform panel, int index)
        {
            // The whole column (disc and name) is the tap target; the transparent face only catches taps.
            Image face = Shape(panel, $"Lesson{index + 1}", Color.clear);
            face.sprite = null;
            face.raycastTarget = true;
            RectTransform rect = face.rectTransform;
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = Style.nodeSize;
            rect.gameObject.AddComponent<CanvasGroup>();

            // The disc sits on the road line; its local anchor maps the panel line into the node column.
            float discY = (Style.nodeLineY - Style.nodeRowBottom) / (Style.nodeRowTop - Style.nodeRowBottom);
            Image disc = Shape(rect, "StageIcon", Style.sprint, true);
            Place(disc.rectTransform, new Vector2(.5f, discY), Vector2.one * Style.nodeSize);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = disc;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = colors.highlightedColor = colors.selectedColor = colors.disabledColor = Color.white;
            colors.pressedColor = Color.Lerp(Color.white, Color.black, Theme.Motion.pressLighten);
            colors.fadeDuration = Theme.Motion.buttonFade;
            button.colors = colors;

            Image glow = Shape(disc.transform, "Glow", Theme.Accent, true);
            glow.sprite = UiKitAssets.Load().Ring;
            Anchor(glow.rectTransform, Vector2.one * -.12f, Vector2.one * 1.12f);
            Image selected = Shape(disc.transform, "SelectedRing", Theme.Accent, true);
            selected.sprite = UiKitAssets.Load().Ring;
            Anchor(selected.rectTransform, Vector2.one * -.07f, Vector2.one * 1.07f);
            selected.gameObject.SetActive(false);

            Image glyph = Shape(disc.transform, "Glyph", Theme.Surface);
            glyph.sprite = Resources.Load<Sprite>("Icons/" + StageIcons[index]);
            glyph.type = Image.Type.Simple;
            glyph.preserveAspect = true;
            Anchor(glyph.rectTransform, Vector2.one * .22f, Vector2.one * .78f);

            // A small corner badge carries the done tick or the lock.
            Image badge = Shape(disc.transform, "StateBadge", Theme.Success, true);
            Place(badge.rectTransform, new Vector2(.86f, .86f), Vector2.one * Style.nodeBadgeSize);
            RectTransform completeMark = Rect(badge.transform, "CompletedMark");
            Anchor(completeMark, new Vector2(.18f, .18f), new Vector2(.82f, .82f));
            Image shortStroke = Shape(completeMark, "ShortStroke", Theme.Surface);
            shortStroke.sprite = null;
            Place(shortStroke.rectTransform, new Vector2(.32f, .42f), new Vector2(18f, 7.5f));
            shortStroke.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            Image longStroke = Shape(completeMark, "LongStroke", Theme.Surface);
            longStroke.sprite = null;
            Place(longStroke.rectTransform, new Vector2(.62f, .54f), new Vector2(33f, 7.5f));
            longStroke.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image lockIcon = Shape(badge.transform, "LockIcon", Theme.Surface);
            lockIcon.sprite = MapPresentationBuilder.LockSprite();
            lockIcon.type = Image.Type.Simple;
            lockIcon.preserveAspect = true;
            Anchor(lockIcon.rectTransform, Vector2.one * .2f, Vector2.one * .8f);
            badge.gameObject.SetActive(false);

            TMP_Text title = Label(rect, "StageTitle", Style.bodySize, Theme.TextPrimary, FontStyles.Bold,
                VietFontRole.Hud);
            title.alignment = TextAlignmentOptions.Center;
            Inset(title.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero,
                new Vector2(0f, Style.nodeLabelHeight));
        }

        static void CreateRoad(RectTransform panel, int index)
        {
            RectTransform road = Rect(panel, $"LessonRoad{index + 1}");
            for (int dot = 0; dot < RoadDots; dot++)
            {
                Image piece = Shape(road, $"Dot{dot + 1}", Theme.MapLockedBorder, true);
                float x = (dot + .5f) / RoadDots;
                Place(piece.rectTransform, new Vector2(x, .5f), Vector2.one * Style.lessonDotSize);
            }
        }

        static void CreateDetailCard(RectTransform panel)
        {
            Image card = Shape(panel, "DetailCard", Theme.Card);
            Anchor(card.rectTransform, Style.detailAnchorMin, Style.detailAnchorMax);
            AddBorder(card, Theme.MapLockedBorder);
            const float pad = 24f;

            TMP_Text objective = Label(card.transform, "Objective", Style.objectiveSize, Theme.Surface,
                FontStyles.Bold, VietFontRole.Body);
            objective.enableWordWrapping = true;
            objective.lineSpacing = 0f;
            objective.enableAutoSizing = true;
            objective.fontSizeMax = Style.objectiveSize;
            objective.fontSizeMin = Mathf.Max(32f, Style.objectiveSize - 7f);
            Inset(objective.rectTransform, new Vector2(0f, .40f), new Vector2(.68f, 1f),
                new Vector2(pad, 0f), new Vector2(0f, -10f));

            TMP_Text status = Label(card.transform, "Status", Style.captionSize, Theme.MutedForeground,
                FontStyles.Normal, VietFontRole.Body);
            status.enableAutoSizing = true;
            status.fontSizeMax = Style.captionSize;
            status.fontSizeMin = Style.captionSize - 3f;
            Inset(status.rectTransform, Vector2.zero, new Vector2(.68f, .40f),
                new Vector2(pad, 8f), Vector2.zero);

            Button play = ActionButton(card.transform, "PlayButton", "CHƠI  ›");
            Anchor((RectTransform)play.transform, new Vector2(.70f, .18f), new Vector2(.97f, .82f));
        }

        static Button ActionButton(Transform parent, string name, string text)
        {
            Image image = Shape(parent, name, Theme.Accent);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            AddBorder(image, Theme.Menu.goldDark);
            TMP_Text label = Label(image.transform, "Label", Style.bodySize, Theme.Surface, FontStyles.Bold,
                VietFontRole.Hud);
            label.text = VietText.Fix(text);
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMax = Style.bodySize;
            label.fontSizeMin = Style.captionSize - 2f;
            Anchor(label.rectTransform, new Vector2(.04f, 0f), new Vector2(.96f, 1f));
            var feedback = image.gameObject.AddComponent<KitPressFeedback>();
            feedback.Configure(image, image.rectTransform);
            // Bound so a disabled button paints DisabledText on DisabledSurface, not navy on grey.
            feedback.BindLabel(label);
            return button;
        }

        static void AddBorder(Image image, Color color)
        {
            Outline border = image.gameObject.AddComponent<Outline>();
            border.effectColor = color;
            border.effectDistance = new Vector2(Style.borderWidth, -Style.borderWidth);
            Shadow shadow = image.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Theme.ShadowColor;
            shadow.effectDistance = Theme.ShadowOffset;
        }

        static Image Shape(Transform parent, string name, Color color, bool circle = false)
        {
            RectTransform rect = Rect(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = circle ? UiKitAssets.Load().Circle : UiKitAssets.Load().RoundRect20;
            image.type = circle ? Image.Type.Simple : Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static TMP_Text Label(Transform parent, string name, float size, Color color,
            FontStyles style = FontStyles.Normal, VietFontRole? role = null)
        {
            RectTransform rect = Rect(parent, name);
            TMP_Text label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.fontSize = size;
            label.color = color;
            label.fontStyle = style;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            if (role.HasValue) VietTypography.Apply(label, role.Value);
            else VietTypography.Apply(label);
            return label;
        }

        static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static void Inset(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 size, Vector2? pivot = null)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot ?? Vector2.one * .5f;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }
    }
}
