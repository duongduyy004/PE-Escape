using System.Collections.Generic;
using System.Linq;
using KMA.Gameplay;
using KMA.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMA.Gameplay.UI
{
    public static class MapPresentationBuilder
    {
        static Sprite heartSprite;
        static Sprite lockSprite;

        readonly struct Entry
        {
            public readonly SubjectId Subject;
            public readonly string Label;
            public readonly Color Color;
            public readonly bool Available;

            public Entry(SubjectId subject, string label, Color color, bool available)
            {
                Subject = subject; Label = label; Color = color; Available = available;
            }
        }

        static readonly Entry[] Entries =
        {
            new Entry(SubjectId.Sprint, "Chạy nước rút", UITheme.Shared.LessonJourney.sprint, true),
            new Entry(SubjectId.Volleyball, "Bóng chuyền", UITheme.Shared.LessonJourney.volleyball, true),
            new Entry(SubjectId.Football, "Bóng đá", UITheme.Shared.LessonJourney.football, true),
            new Entry(SubjectId.Chess, "Bài kiểm tra cuối", UITheme.Shared.LessonJourney.chess, true),
        };

        public static void Build(MapScreen screen, GameSession session, Sprite sharedBackground = null)
        {
            if (screen == null) return;
            var existing = screen.transform.Find("S5MapPresentation");
            if (existing != null)
            {
                var existingNodes = existing.GetComponentsInChildren<MapNodeView>(true);
                // Scenes baked with three stops get the final exam stop added at runtime.
                if (existingNodes.Length > 0 && existingNodes.All(n => n.SubjectId != SubjectId.Chess))
                {
                    Entry final = Entries.First(e => e.Subject == SubjectId.Chess);
                    MapNodeView chess = MapStopBuilder.Build(existingNodes[0].transform.parent, screen,
                        final.Subject, final.Label, final.Color, Entries.Length);
                    existingNodes = existingNodes.Append(chess).ToArray();
                    // The baked finish flag sat on the third stop; the final exam is the course's end now.
                    foreach (MapNodeView node in existingNodes)
                        if (node != chess) node.transform.Find("Badge/FinishFlag")?.gameObject.SetActive(false);
                }
                var existingHearts = existing.GetComponentInChildren<HeartBar>(true);
                ApplySharedBackground(existing, sharedBackground);
                Transform oldFutureRow = existing.Find("Content/FutureRow");
                if (oldFutureRow != null) oldFutureRow.gameObject.SetActive(false);
                var lessonList = existing.GetComponentInChildren<JourneyLessonList>(true);
                // Panels baked before the level-select layout carry lesson cards, not stage nodes.
                if (lessonList != null && lessonList.transform.Find("DetailCard") == null)
                {
                    GameObject stale = lessonList.gameObject;
                    stale.transform.SetParent(null, false);
                    if (Application.isPlaying) UnityEngine.Object.Destroy(stale);
                    else UnityEngine.Object.DestroyImmediate(stale);
                    lessonList = null;
                }
                if (lessonList == null) lessonList = JourneyLessonList.Create(existing.Find("Content"));
                EnsureCourseSummary(screen, existing.Find("Content"));
                // Scenes baked before the popup carry the panel docked under the map.
                JourneyLessonPresentation.EnsurePopup((RectTransform)lessonList.transform);
                screen.BindPresentation(existingNodes, existingHearts, session ?? new GameSession(), lessonList);
                lessonList.Close();
                screen.BindBudgetLabel(existing.Find("Content/Header/LivesPanel/LivesLabelContainer/LivesLabel")
                    ?.GetComponent<TMP_Text>());
                screen.RefreshJourney(session ?? new GameSession());
                ConfigureJourneyPath(existing.Find("Content/SelectionGrid"), existingNodes);
                ApplyHeaderLayout(existing.Find("Content/Header"));
                var back = existing.Find("Content/Header/BackButton")?.GetComponent<Button>();
                if (back != null)
                {
                    back.onClick.RemoveAllListeners();
                    back.onClick.AddListener(() => KMA.Gameplay.Core.SceneRouter.Instance?.RouteToMenu());
                }
                foreach (var node in existingNodes)
                {
                    if (node.IsComingSoon) continue;
                    var button = node.GetComponent<Button>();
                    if (button == null) continue;
                    button.onClick.RemoveAllListeners();
                    var subject = node.SubjectId;
                    button.onClick.AddListener(() => screen.SelectSubject(subject));
                }
                return;
            }
            foreach (Button button in screen.GetComponentsInChildren<Button>(true)) button.gameObject.SetActive(false);
            UITheme theme = screen.Theme;
            Color background = Color.Lerp(theme.Background,
                UITheme.Shared.MapBackgroundTint, .46f);

            RectTransform root = Rect(screen.transform, "S5MapPresentation");
            Stretch(root, Vector2.zero, Vector2.zero);
            CreateFullBleedBackdrop(root, sharedBackground, background);
            RectTransform content = Rect(root, "Content");
            Stretch(content, new Vector2(64, 40), new Vector2(-64, -40));

            HeartBar hearts = Header(content, session);
            PinToTop((RectTransform)hearts.transform.parent.parent, HeaderHeight);
            RectTransform grid = Rect(content, "SelectionGrid");
            Anchor(grid, new Vector2(0f, .37f), new Vector2(1f, .84f));
            GridLayoutGroup gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 3;
            gridLayout.spacing = new Vector2(16, 16); gridLayout.childAlignment = TextAnchor.UpperCenter;
            grid.gameObject.AddComponent<ResponsiveGridLayout>().Refresh();
            LayoutElement gridElement = grid.gameObject.AddComponent<LayoutElement>();
            gridElement.flexibleWidth = 1; gridElement.preferredHeight = 392;
            grid.GetComponent<ResponsiveGridLayout>().Refresh();
            var nodes = new List<MapNodeView>();
            for (int i = 0; i < Entries.Length; i++)
                nodes.Add(MapStopBuilder.Build(grid, screen, Entries[i].Subject, Entries[i].Label,
                    Entries[i].Color, i + 1));
            grid.GetComponent<ResponsiveGridLayout>().Refresh();
            JourneyLessonList lessons = JourneyLessonList.Create(content);
            EnsureCourseSummary(screen, content);
            // The summary is added after the panel; keep the popup on top of it.
            JourneyLessonPresentation.EnsurePopup((RectTransform)lessons.transform);
            screen.BindPresentation(nodes.ToArray(), hearts, session ?? new GameSession(), lessons);
            lessons.Close();
            screen.BindBudgetLabel(content.Find("Header/LivesPanel/LivesLabelContainer/LivesLabel")
                ?.GetComponent<TMP_Text>());
            screen.RefreshJourney(session ?? new GameSession());
            ConfigureJourneyPath(grid, nodes.ToArray());
        }

        static void ApplySharedBackground(Transform root, Sprite sharedBackground)
        {
            if (root == null || sharedBackground == null) return;
            Color fallback = Color.Lerp(UITheme.Shared.Background, UITheme.Shared.MapBackgroundTint, .46f);
            CreateFullBleedBackdrop(root, sharedBackground, fallback);
        }

        static void CreateFullBleedBackdrop(Transform contentRoot, Sprite sprite, Color fallback)
        {
            // MapScreen can live under SafeAreaRoot. Keep its controls there, but draw scenery
            // on the root Canvas so it extends behind system bars and gesture insets.
            Canvas canvas = contentRoot.GetComponentInParent<Canvas>();
            Transform canvasRoot = canvas != null ? canvas.rootCanvas.transform : contentRoot.root;
            const string backdropName = "S5MapBackdrop";
            Transform old = canvasRoot.Find(backdropName);
            GameObject backgroundObject = old != null ? old.gameObject
                : new GameObject(backdropName, typeof(RectTransform), typeof(Image));
            RectTransform backgroundRect = (RectTransform)backgroundObject.transform;
            if (backgroundRect.parent != canvasRoot) backgroundRect.SetParent(canvasRoot, false);
            Stretch(backgroundRect, Vector2.zero, Vector2.zero);
            Image image = backgroundObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : fallback;
            image.preserveAspect = false;
            image.raycastTarget = false;
            UiKit.GetOrAdd<StyleExempt>(backgroundObject);
            backgroundRect.SetAsFirstSibling();
            Transform oldShade = canvasRoot.Find(backdropName + "Shade");
            if (sprite == null) { if (oldShade != null) oldShade.gameObject.SetActive(false); }
            else
            {
                GameObject shadeObject = oldShade != null ? oldShade.gameObject
                    : new GameObject(backdropName + "Shade", typeof(RectTransform), typeof(Image));
                RectTransform shadeRect = (RectTransform)shadeObject.transform;
                shadeRect.SetParent(canvasRoot, false);
                Stretch(shadeRect, Vector2.zero, Vector2.zero);
                Image shade = shadeObject.GetComponent<Image>();
                shade.color = MinigameUiTheme.WithAlpha(ShellTheme.Ink, .42f);
                shade.raycastTarget = false;
                shadeRect.SetAsFirstSibling();
                backgroundRect.SetAsFirstSibling();
            }
            Image insetImage = contentRoot.GetComponent<Image>();
            if (insetImage != null) insetImage.enabled = false;
            Transform insetShade = contentRoot.Find("BackgroundShade");
            if (insetShade != null) insetShade.gameObject.SetActive(false);
        }

        static void ConfigureJourneyPath(Transform selectionGrid, MapNodeView[] nodes)
        {
            if (selectionGrid == null) return;
            Anchor((RectTransform)selectionGrid, UITheme.Shared.LessonJourney.mapAnchorMin,
                UITheme.Shared.LessonJourney.mapAnchorMax);
            GridLayoutGroup grid = selectionGrid.GetComponent<GridLayoutGroup>();
            if (grid != null) grid.enabled = false;
            ResponsiveGridLayout responsive = selectionGrid.GetComponent<ResponsiveGridLayout>();
            if (responsive != null) responsive.enabled = false;
            MapJourneyPathLayout path = selectionGrid.GetComponent<MapJourneyPathLayout>();
            if (path == null) path = selectionGrid.gameObject.AddComponent<MapJourneyPathLayout>();
            path.Configure(nodes);
        }

        static JourneyCourseSummary EnsureCourseSummary(MapScreen screen, Transform parent)
        {
            JourneyCourseSummary summary = screen.GetComponentInChildren<JourneyCourseSummary>(true);
            if (summary != null) return summary;
            var panel = new GameObject("JourneyCourseSummary", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = UITheme.Shared.LessonJourney.summaryAnchorMin;
            rect.anchorMax = UITheme.Shared.LessonJourney.summaryAnchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = Color.clear;
            panel.GetComponent<Image>().raycastTarget = false;
            ShellKit.StyleCard(rect);
            var labelObject = new GameObject("SummaryText", typeof(RectTransform));
            labelObject.transform.SetParent(panel.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(16f, 2f);
            labelRect.offsetMax = new Vector2(-16f, -2f);
            TMP_Text label = labelObject.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            UiKit.StyleLabel(label, UITheme.Shared.LessonJourney.captionSize, ShellTheme.Ink);
            label.fontSize = UITheme.Shared.LessonJourney.captionSize;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            JourneyCourseSummary view = panel.AddComponent<JourneyCourseSummary>();
            view.Configure(label);
            view.Hide();
            return view;
        }

        // Header blocks are Shell cards: a cream face in an ink border over a hard ink drop,
        // floating on the scenery instead of sitting on a strip.
        const float BlockHeight = 104f, HeaderHeight = 116f, TitleBoxHeight = 76f;
        const float HeartSize = 40f;

        static void ApplyHeaderLayout(Transform header)
        {
            if (header == null) return;
            PinToTop((RectTransform)header, HeaderHeight);
            LayoutElement headerElement = header.GetComponent<LayoutElement>();
            if (headerElement != null) headerElement.minHeight = headerElement.preferredHeight = HeaderHeight;

            RectTransform back = (RectTransform)header.Find("BackButton");
            LayoutElement backElement = back.GetComponent<LayoutElement>();
            backElement.preferredWidth = backElement.preferredHeight = BlockHeight;
            back.sizeDelta = Vector2.one * BlockHeight;
            TMP_Text arrow = back.GetComponentInChildren<TMP_Text>(true);
            if (arrow != null)
            {
                arrow.text = VietText.Fix("←");
                arrow.fontSize = MinigameUiTheme.Headline;
                UiKit.FitLabel(arrow, MinigameUiTheme.Headline);
            }

            LayoutElement heading = header.Find("Heading").GetComponent<LayoutElement>();
            heading.preferredWidth = 480f;
            heading.preferredHeight = BlockHeight;
            header.Find("Heading/TitleContainer").GetComponent<LayoutElement>().preferredHeight = TitleBoxHeight;
            foreach (TMP_Text text in header.Find("Heading/TitleContainer/Title").GetComponentsInChildren<TMP_Text>(true))
            {
                text.fontSize = MinigameUiTheme.Title;
                UiKit.FitLabel(text, MinigameUiTheme.Title);
            }
            header.Find("LivesPanel").GetComponent<LayoutElement>().preferredHeight = BlockHeight;
        }

        static HeartBar Header(Transform parent, GameSession session)
        {
            Color navy = ShellTheme.Ink;
            RectTransform header = Rect(parent, "Header");
            HorizontalLayoutGroup layout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            // Right and bottom padding leave room for the blocks' drop shadow.
            layout.padding = new RectOffset(0, (int)ShellTheme.CardShadow.x, 0, (int)-ShellTheme.CardShadow.y);
            layout.spacing = 20; layout.childAlignment = TextAnchor.MiddleLeft; layout.childControlWidth = true;
            layout.childControlHeight = true; layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            LayoutElement headerElement = header.gameObject.AddComponent<LayoutElement>();
            headerElement.minHeight = HeaderHeight;
            headerElement.preferredHeight = HeaderHeight;
            headerElement.flexibleHeight = 0;
            Button back = HeaderButton(header, "BackButton", "←");
            back.onClick.AddListener(() => KMA.Gameplay.Core.SceneRouter.Instance?.RouteToMenu());

            RectTransform heading = Rect(header, "Heading");
            ShellKit.StyleCard(heading, decorated: true);
            LayoutElement headingElement = heading.gameObject.AddComponent<LayoutElement>();
            headingElement.preferredWidth = 480;
            headingElement.flexibleWidth = 0;
            headingElement.preferredHeight = BlockHeight;
            VerticalLayoutGroup headingLayout = heading.gameObject.AddComponent<VerticalLayoutGroup>();
            headingLayout.padding = new RectOffset(52, 24, 6, 8);
            headingLayout.childAlignment = TextAnchor.MiddleLeft;
            headingLayout.childControlWidth = true;
            headingLayout.childControlHeight = true;
            headingLayout.childForceExpandHeight = false;
            RectTransform titleBox = Rect(heading, "TitleContainer");
            titleBox.gameObject.AddComponent<LayoutElement>().preferredHeight = TitleBoxHeight;
            ShellTitleHandle title = ShellKit.Title(titleBox, "Title", "CHỌN MÔN THI", MinigameUiTheme.Title);
            Stretch(title.Root, Vector2.zero, Vector2.zero);
            title.Text.alignment = title.Drop.alignment = TextAlignmentOptions.Left;

            RectTransform spacer = Rect(header, "Spacer");
            spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            RectTransform livesPanel = Rect(header, "LivesPanel");
            ShellKit.StyleCard(livesPanel);
            HorizontalLayoutGroup livesLayout = livesPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
            livesLayout.padding = new RectOffset(20, 20, 6, 6);
            livesLayout.spacing = 16;
            livesLayout.childAlignment = TextAnchor.MiddleCenter;
            livesLayout.childControlWidth = true;
            livesLayout.childControlHeight = true;
            livesLayout.childForceExpandWidth = false;
            livesLayout.childForceExpandHeight = false;
            LayoutElement livesElement = livesPanel.gameObject.AddComponent<LayoutElement>();
            livesElement.preferredWidth = 500;
            livesElement.flexibleWidth = 0;
            livesElement.preferredHeight = BlockHeight;
            RectTransform bar = Rect(livesPanel, "HeartBar");
            bar.gameObject.AddComponent<StyleExempt>();
            HorizontalLayoutGroup heartLayout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            heartLayout.spacing = 8; heartLayout.childAlignment = TextAnchor.MiddleCenter;
            heartLayout.childControlWidth = true; heartLayout.childControlHeight = true;
            heartLayout.childForceExpandWidth = false; heartLayout.childForceExpandHeight = false;
            bar.gameObject.AddComponent<LayoutElement>().preferredWidth = 5 * HeartSize + 4 * heartLayout.spacing;
            HeartBar hearts = bar.gameObject.AddComponent<HeartBar>();
            var slots = new Image[5];
            for (int i = 0; i < slots.Length; i++)
            {
                RectTransform slot = Rect(bar, "Heart" + (i + 1)); slots[i] = slot.gameObject.AddComponent<Image>();
                slots[i].sprite = HeartSprite();
                slots[i].preserveAspect = true;
                LayoutElement element = slot.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = HeartSize; element.preferredHeight = HeartSize - 2f;
            }
            hearts.SetSlots(slots);
            int currentLives = session == null ? GameSession.MaxLives : session.Lives;
            TMP_Text lives = LayoutLabel(livesPanel, "LivesLabel", $"Lượt thi: {currentLives}/{GameSession.MaxLives}",
                (int)MinigameUiTheme.Caption, navy, TextAnchor.MiddleLeft);
            lives.fontStyle |= FontStyles.UpperCase;
            lives.enableWordWrapping = false;
            Transform livesColumn = lives.transform.parent;
            VerticalLayoutGroup columnLayout = livesColumn.gameObject.AddComponent<VerticalLayoutGroup>();
            columnLayout.childAlignment = TextAnchor.MiddleLeft;
            columnLayout.childControlWidth = true;
            columnLayout.childControlHeight = true;
            columnLayout.childForceExpandWidth = true;
            columnLayout.childForceExpandHeight = false;
            LayoutElement livesLabelLayout = livesColumn.gameObject.AddComponent<LayoutElement>();
            livesLabelLayout.preferredWidth = 210;
            livesLabelLayout.preferredHeight = 64;
            lives.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
            // The life regen countdown sits under the count, so the hearts stay centred.
            TMP_Text timer = TextTmp(livesColumn, "LifeTimer", string.Empty, (int)MinigameUiTheme.Caption, navy,
                TextAnchor.MiddleLeft);
            timer.enableWordWrapping = false;
            timer.raycastTarget = false;
            timer.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            hearts.UseLightSurface(timer, navy);
            hearts.SetHearts(currentLives);
            return hearts;
        }

        static Button HeaderButton(Transform parent, string name, string label)
        {
            ShellButtonHandle handle = ShellKit.IconButton(parent, name, label);
            LayoutElement element = handle.Button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = BlockHeight;
            element.preferredHeight = BlockHeight;
            element.flexibleWidth = 0;
            return handle.Button;
        }


        // Material Symbols glyphs (Apache 2.0), imported from Resources/Icons; see Art/Icons/Source~.
        internal static Sprite SportIconSprite(SubjectId subject) =>
            Resources.Load<Sprite>("Icons/SportIcon_" + subject);

        internal static Sprite LockSprite()
        {
            if (lockSprite != null)
                return lockSprite;

            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RuntimeLockIcon",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            DrawRing(pixels, size, 48, 59, 23, 8);
            for (int y = 15; y <= 57; y++)
            for (int x = 19; x <= 77; x++)
                SetIconPixel(pixels, size, x, y);
            for (int y = 29; y <= 50; y++)
            for (int x = 31; x <= 65; x++)
                pixels[y * size + x] = new Color32(255, 255, 255, 0);
            DrawCircle(pixels, size, 48, 37, 6);
            DrawLine(pixels, size, 48, 35, 48, 24, 5);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            lockSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
            lockSprite.name = "RuntimeLockIcon";
            lockSprite.hideFlags = HideFlags.HideAndDontSave;
            return lockSprite;
        }

        internal static void DrawCircle(Color32[] pixels, int size, int centerX, int centerY, int radius)
        {
            int radiusSquared = radius * radius;
            for (int y = centerY - radius; y <= centerY + radius; y++)
            for (int x = centerX - radius; x <= centerX + radius; x++)
                if ((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY) <= radiusSquared)
                    SetIconPixel(pixels, size, x, y);
        }

        static void DrawRing(Color32[] pixels, int size, int centerX, int centerY, int radius, int thickness)
        {
            int outer = radius * radius;
            int innerRadius = radius - thickness;
            int inner = innerRadius * innerRadius;
            for (int y = centerY - radius; y <= centerY + radius; y++)
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                int distance = (x - centerX) * (x - centerX) + (y - centerY) * (y - centerY);
                if (distance <= outer && distance >= inner)
                    SetIconPixel(pixels, size, x, y);
            }
        }

        internal static void DrawLine(Color32[] pixels, int size, int startX, int startY, int endX, int endY, int thickness)
        {
            int steps = Mathf.Max(Mathf.Abs(endX - startX), Mathf.Abs(endY - startY));
            if (steps == 0)
            {
                DrawCircle(pixels, size, startX, startY, Mathf.Max(1, thickness / 2));
                return;
            }
            for (int index = 0; index <= steps; index++)
            {
                float t = (float)index / steps;
                DrawCircle(pixels, size,
                    Mathf.RoundToInt(Mathf.Lerp(startX, endX, t)),
                    Mathf.RoundToInt(Mathf.Lerp(startY, endY, t)),
                    Mathf.Max(1, thickness / 2));
            }
        }

        internal static void SetIconPixel(Color32[] pixels, int size, int x, int y)
        {
            if (x < 0 || x >= size || y < 0 || y >= size)
                return;
            pixels[y * size + x] = new Color32(255, 255, 255, 255);
        }

        static Sprite HeartSprite()
        {
            if (heartSprite != null)
                return heartSprite;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RuntimeHeartIcon",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                float px = ((x + .5f) / size * 2f - 1f) * 1.22f;
                float py = ((y + .5f) / size * 2f - 1f) * 1.22f;
                py += .12f;
                float square = px * px + py * py - 1f;
                bool inside = square * square * square - px * px * py * py * py <= 0f;
                pixels[y * size + x] = inside
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(255, 255, 255, 0);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            heartSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
            heartSprite.name = "RuntimeHeartIcon";
            heartSprite.hideFlags = HideFlags.HideAndDontSave;
            return heartSprite;
        }

        internal static RectTransform Rect(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform)); gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        internal static TMP_Text TextTmp(Transform parent, string name, string value, int size, Color color, TextAnchor alignment)
        {
            RectTransform rect = Rect(parent, name); Stretch(rect, Vector2.zero, Vector2.zero);
            TextAlignmentOptions tmpAlignment = alignment switch
            {
                TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
                TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
                TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
                TextAnchor.MiddleRight => TextAlignmentOptions.Right,
                TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
                TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
                TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
                _ => TextAlignmentOptions.Center
            };
            TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            UiKit.StyleLabel(text, size, color);
            text.text = VietText.Fix(value);
            VietTypography.Apply(text);
            text.alignment = tmpAlignment;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            UiKit.FitLabel(text, size);
            return text;
        }

        internal static TMP_Text LayoutLabel(Transform parent, string name, string value, int size, Color color, TextAnchor alignment)
        {
            return TextTmp(Rect(parent, name + "Container"), name, value, size, color, alignment);
        }

        internal static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = min; rect.offsetMax = max;
        }

        internal static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void PinToTop(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
        }
    }

}
